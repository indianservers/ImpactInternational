using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class GovernanceDocuments
{
    public static void MapGovernanceDocuments(this RouteGroupBuilder api)
    {
        api.MapGet(
            "/governance/documents",
            async (
                GovernanceService s,
                Guid? orgId = null,
                Guid? committeeId = null,
                int page = 1
            ) =>
            {
                IQueryable<GovernanceDocument> q = s.Db.GovernanceDocuments.Where(d=>d.ObjectKey=="");
                var u = await s.A.User();
                if (committeeId != null)
                {
                    var c = await s.CommitteeAccess(committeeId.Value);
                    q = q.Where(x => x.TenantId == c.TenantId && x.CommitteeId == committeeId);
                    var since = await s
                        .Db.CommitteeMemberships.Where(x =>
                            x.CommitteeId == committeeId
                            && x.Status == "Active"
                            && x.StartsAt <= DateTimeOffset.UtcNow
                            && x.EndsAt > DateTimeOffset.UtcNow
                            && s.Db.Persons.Any(p => p.Id == x.PersonId && p.UserId == u.Id)
                        )
                        .MinAsync(x => (DateTimeOffset?)x.StartsAt);
                    if (since != null)
                        q = q.Where(x =>
                            x.ShareWithSuccessors || x.UploaderId == u.Id || x.At >= since
                        );
                }
                else
                {
                    var w = await s.A.Workspace("governance.documents");
                    q = q.Where(x =>
                        x.TenantId == w.TenantId
                        && x.CommitteeId == null
                        && s.A.VisibleIds(w).Contains(x.OrgId)
                    );
                    if (w.AppointmentId != null)
                    {
                        var start = await s
                            .Db.OfficerAppointments.Where(x => x.Id == w.AppointmentId)
                            .Select(x => x.StartsAt)
                            .SingleAsync();
                        q = q.Where(x =>
                            x.UploaderId == u.Id || x.ShareWithSuccessors || x.At >= start
                        );
                    }
                }
                if (orgId != null)
                    q = q.Where(x => x.OrgId == orgId);
                return new
                {
                    total = await q.CountAsync(),
                    items = await q.OrderByDescending(x => x.At)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.OrgId,
                            x.CommitteeId,
                            x.Title,
                            x.Kind,
                            x.FileName,
                            x.ContentType,
                            x.Status,
                            x.At,
                            x.RetainUntil,
                            x.ShareWithSuccessors,
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
                "/governance/documents",
                async (
                    IFormFile file,
                    [FromForm] Guid orgId,
                    [FromForm] string title,
                    [FromForm] string kind,
                    [FromForm] Guid? committeeId,
                    [FromForm] bool shareWithSuccessors,
                    [FromForm] DateTimeOffset? retainUntil,
                    GovernanceService s
                ) =>
                {
                    var u = await s.A.User();
                    Guid tenant;
                    if (committeeId != null)
                    {
                        var c = await s.CommitteeAccess(committeeId.Value, true);
                        Check(c.OrgId == orgId, "Document organization must match the committee.");
                        tenant = c.TenantId;
                    }
                    else
                    {
                        var w = await s.A.Workspace("governance.documents");
                        await s.Scope(w, orgId);
                        tenant = w.TenantId;
                    }
                    Check(
                        file.Length is > 0 and <= 5242880
                            && title.Length is > 0 and <= 200
                            && new[]
                            {
                                "Constitution",
                                "Charter",
                                "Policy",
                                "Resolution",
                                "Circular",
                                "Appointment",
                                "CommitteeOrder",
                                "Correspondence",
                            }.Contains(kind),
                        "Invalid governance document (maximum 5 MB)."
                    );
                    Check(
                        retainUntil == null || retainUntil > DateTimeOffset.UtcNow,
                        "Retention must be in the future."
                    );
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    var type = MemberMediaEndpoints.FileType(bytes);
                    var name = Path.GetFileName(file.FileName);
                    var ext = Path.GetExtension(name).ToLowerInvariant();
                    Check(
                        type != ""
                            && (
                                (type == "application/pdf" && ext == ".pdf")
                                || (type == "image/png" && ext == ".png")
                                || (type == "image/jpeg" && new[] { ".jpg", ".jpeg" }.Contains(ext))
                            ),
                        "Only matching PDF/PNG/JPEG files are accepted."
                    );
                    var d = new GovernanceDocument
                    {
                        TenantId = tenant,
                        OrgId = orgId,
                        CommitteeId = committeeId,
                        Title = title,
                        Kind = kind,
                        FileName = name,
                        ContentType = type,
                        Content = bytes,
                        Sha256 = Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(bytes)
                        ),
                        UploaderId = u.Id,
                        ShareWithSuccessors = shareWithSuccessors,
                        RetainUntil = retainUntil?.ToUniversalTime(),
                    };
                    s.Db.Add(d);
                    s.Db.Audits.Add(
                        new()
                        {
                            TenantId = tenant,
                            OrgId = orgId,
                            ActorId = u.Id,
                            Action = "governance.document.uploaded",
                            Resource = d.Id.ToString(),
                        }
                    );
                    await s.Db.SaveChangesAsync();
                    return Results.Ok(
                        new
                        {
                            d.Id,
                            d.Title,
                            d.Kind,
                            d.Status,
                            d.At,
                        }
                    );
                }
            )
            .DisableAntiforgery();
        api.MapGet(
            "/governance/documents/{id:guid}",
            async (Guid id, GovernanceService s) =>
            {
                var d = await s.Db.GovernanceDocuments.SingleOrDefaultAsync(x => x.Id == id);
                Check(d != null, "Document not found.", 404);
                await Authorize(d, s);
                Check(
                    d.Status == "Available"
                        && (d.RetainUntil == null || d.RetainUntil > DateTimeOffset.UtcNow),
                    "Document retention has ended or document is withdrawn.",
                    410
                );
                return Results.File(d.Content, d.ContentType, d.FileName);
            }
        );
        api.MapPost(
            "/governance/documents/{id:guid}/withdraw",
            async (Guid id, GovernanceService s) =>
            {
                var d = await s.Db.GovernanceDocuments.SingleOrDefaultAsync(x => x.Id == id);
                Check(d != null, "Document not found.", 404);
                await Authorize(d, s);
                var u = await s.A.User();
                Check(d.UploaderId == u.Id, "Only the uploader may withdraw this document.", 403);
                d.Status = "Withdrawn";
                s.Db.Audits.Add(
                    new()
                    {
                        TenantId = d.TenantId,
                        OrgId = d.OrgId,
                        ActorId = u.Id,
                        Action = "governance.document.withdrawn",
                        Resource = d.Id.ToString(),
                    }
                );
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/governance/resolutions",
            async (ResolutionInput r, GovernanceService s) =>
            {
                var w = await s.A.Workspace("governance.documents");
                await using var tx = await s.A.Mutation(w);
                await s.Scope(w, r.OrgId);
                Check(
                    r.Number.Length is > 0 and <= 60
                        && r.Title.Length is > 0 and <= 200
                        && r.Subject.Length is > 0 and <= 200
                        && r.Text.Length is > 0 and <= 20000,
                    "Invalid resolution details."
                );
                await s.ValidateDocument(r.DocumentId, w, r.OrgId);
                var x = new GovernanceResolution
                {
                    TenantId = w.TenantId,
                    OrgId = r.OrgId,
                    Number = r.Number,
                    Title = r.Title,
                    Subject = r.Subject,
                    Text = r.Text,
                    Date = r.Date.ToUniversalTime(),
                    EffectiveAt = r.EffectiveAt.ToUniversalTime(),
                    ProposerId = w.UserId,
                };
                s.Db.Add(x);
                var request = await s.Request(
                    w,
                    r.OrgId,
                    "Resolution",
                    "Resolution " + x.Number + ": " + x.Title,
                    GovernanceService.Pack(r),
                    resolution: x.Id,
                    document: r.DocumentId
                );
                s.Audit(w, r.OrgId, "resolution.created", x.Id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        x.Id,
                        x.Status,
                        request,
                    }
                );
            }
        );
        api.MapGet(
            "/governance/resolutions",
            async (GovernanceService s, Guid? orgId = null, int page = 1) =>
            {
                var w = await s.A.Workspace("governance.documents");
                var q = s.Db.GovernanceResolutions.Where(x =>
                    x.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(x.OrgId)
                );
                if (orgId != null)
                    q = q.Where(x => x.OrgId == orgId);
                if (w.AppointmentId != null)
                {
                    var start = await s
                        .Db.OfficerAppointments.Where(x => x.Id == w.AppointmentId)
                        .Select(x => x.StartsAt)
                        .SingleAsync();
                    q = q.Where(x => x.Date >= start || x.ProposerId == w.UserId);
                }
                return new
                {
                    total = await q.CountAsync(),
                    items = await q.OrderByDescending(x => x.Date)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.OrgId,
                            x.Number,
                            x.Title,
                            x.Subject,
                            x.Text,
                            x.Date,
                            x.EffectiveAt,
                            x.Status,
                            x.Version,
                            supportingDocumentId = s
                                .Db.GovernanceRequests.Where(r =>
                                    r.ResolutionId == x.Id && r.DocumentId != null
                                )
                                .Select(r => r.DocumentId)
                                .FirstOrDefault(),
                        })
                        .ToListAsync(),
                };
            }
        );
    }

    internal static async Task Authorize(GovernanceDocument d, GovernanceService s)
    {
        var user = await s.A.User();
        if(d.ObjectKey != "") throw new ApiError(404,"Use the protected document vault endpoint.");
        if (d.CommitteeId != null)
        {
            await s.CommitteeAccess(d.CommitteeId.Value);
            var start = await s
                .Db.CommitteeMemberships.Where(x =>
                    x.CommitteeId == d.CommitteeId
                    && x.Status == "Active"
                    && x.StartsAt <= DateTimeOffset.UtcNow
                    && x.EndsAt > DateTimeOffset.UtcNow
                    && s.Db.Persons.Any(p => p.Id == x.PersonId && p.UserId == user.Id)
                )
                .MinAsync(x => (DateTimeOffset?)x.StartsAt);
            Check(
                start == null || d.ShareWithSuccessors || d.At >= start || d.UploaderId == user.Id,
                "Historical document is not shared with successor committee members.",
                404
            );
        }
        else
        {
            var w = await s.A.Workspace("governance.documents");
            Check(d.TenantId == w.TenantId, "Document not found.", 404);
            await s.Scope(w, d.OrgId);
            if (w.AppointmentId != null)
            {
                var start = await s
                    .Db.OfficerAppointments.Where(x => x.Id == w.AppointmentId)
                    .Select(x => x.StartsAt)
                    .SingleAsync();
                Check(
                    d.ShareWithSuccessors || d.At >= start || d.UploaderId == user.Id,
                    "Historical document not shared with this successor.",
                    404
                );
            }
        }
    }
}
