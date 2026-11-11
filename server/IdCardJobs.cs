using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public static partial class IdCardEndpoints
{
    public static IQueryable<Membership> Selection(IdCardService s, Assignment w, IdCardSelection i)
    {
        var scoped = new Assignment
        {
            TenantId = w.TenantId,
            OrgId = i.OrgId,
            RoleId = w.RoleId,
            UserId = w.UserId,
        };
        var q = s.Db.Memberships.Where(m =>
            m.TenantId == w.TenantId
            && s.A.VisibleIds(scoped).Contains(m.ClubId)
            && (i.CategoryId == null || m.CategoryId == i.CategoryId)
            && (i.JoinedFrom == null || m.JoinedAt >= i.JoinedFrom)
            && (i.JoinedTo == null || m.JoinedAt <= i.JoinedTo)
        );
        if (i.MembershipIds is { Length: > 0 })
            q = q.Where(m => i.MembershipIds.Contains(m.Id));
        if (i.Search != "")
            q = q.Where(m =>
                EF.Functions.ILike(m.Person.DisplayName, IntelligenceSearch.Pattern(i.Search), "\\")
                || EF.Functions.ILike(m.Number, IntelligenceSearch.Pattern(i.Search), "\\")
            );
        Check(
            i.Country.Length <= 100
                && new[]
                {
                    "",
                    "Active",
                    "Suspended",
                    "Inactive",
                    "Resigned",
                    "Terminated",
                    "Expired",
                    "Deceased",
                }.Contains(i.MemberStatus)
                && new[] { "", "None", "Issued", "Expired", "Revoked", "Pending" }.Contains(
                    i.CardStatus
                ),
            "Invalid membership/card selection filter."
        );
        if (i.Country != "")
            q = q.Where(m => m.Club.Country == i.Country);
        if (i.MemberStatus != "")
            q = q.Where(m => m.Status == i.MemberStatus);
        if (i.PositionId != null)
            q = q.Where(m =>
                Current(s.Db)
                    .Any(p =>
                        p.PersonId == m.PersonId
                        && p.TenantId == m.TenantId
                        && p.OrgId == m.ClubId
                        && p.PositionId == i.PositionId
                    )
            );
        if (i.ExpiryFrom != null || i.ExpiryTo != null)
            q = q.Where(m =>
                s.Db.DigitalCards.Any(c =>
                    c.MembershipId == m.Id
                    && c.RevokedAt == null
                    && c.State == "Issued"
                    && (i.ExpiryFrom == null || c.ExpiryAt >= i.ExpiryFrom)
                    && (i.ExpiryTo == null || c.ExpiryAt <= i.ExpiryTo)
                )
            );
        if (i.CardStatus == "None")
            q = q.Where(m =>
                !s.Db.DigitalCards.Any(c =>
                    c.MembershipId == m.Id
                    && c.RevokedAt == null
                    && (c.State == "Issued" || c.State == "Legacy")
                )
            );
        if (i.CardStatus == "Issued")
            q = q.Where(m =>
                s.Db.DigitalCards.Any(c =>
                    c.MembershipId == m.Id
                    && c.RevokedAt == null
                    && (c.State == "Issued" || c.State == "Legacy")
                    && (c.ExpiryAt == null || c.ExpiryAt > DateTimeOffset.UtcNow)
                )
            );
        if (i.CardStatus == "Expired")
            q = q.Where(m =>
                s.Db.DigitalCards.Any(c =>
                    c.MembershipId == m.Id
                    && c.ExpiryAt <= DateTimeOffset.UtcNow
                    && c.RevokedAt == null
                )
            );
        if (i.CardStatus == "Revoked")
            q = q.Where(m =>
                s.Db.DigitalCards.Any(c => c.MembershipId == m.Id && c.RevokedAt != null)
            );
        if (i.CardStatus == "Pending")
            q = q.Where(m =>
                s.Db.DigitalCards.Any(c =>
                    c.MembershipId == m.Id
                    && new[] { "Draft", "Pending Approval", "Approved", "Generated" }.Contains(
                        c.State
                    )
                    && c.RevokedAt == null
                )
            );
        return q;
    }

    static async Task<IdCardJob> Job(Guid id, IdCardService s)
    {
        var w = await s.Workspace("cards.bulk");
        return await s.Db.IdCardJobs.SingleOrDefaultAsync(j =>
                j.Id == id && j.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(j.OrgId)
            ) ?? throw new ApiError(404, "Generation job unavailable.");
    }

    static void MapJobs(RouteGroupBuilder api)
    {
        api.MapPost(
            "/bulk-preview",
            async (IdCardSelection i, IdCardService s) =>
            {
                var w = await s.Workspace("cards.bulk");
                await s.Governance.Scope(w, i.OrgId);
                Check(i.Search.Length <= 100, "Search too long.");
                var q = Selection(s, w, i);
                var now = DateTimeOffset.UtcNow;
                return new
                {
                    total = await q.CountAsync(),
                    eligible = await q.CountAsync(m =>
                        m.Status == "Active" && (m.EndsAt == null || m.EndsAt > now)
                    ),
                    missingPhotos = await q.CountAsync(m => m.Person.Photo == null),
                    missingIds = await q.CountAsync(m => m.Number == ""),
                    expired = await q.CountAsync(m => m.EndsAt <= now || m.Status != "Active"),
                    alreadyIssued = await q.CountAsync(m =>
                        s.Db.DigitalCards.Any(c =>
                            c.MembershipId == m.Id
                            && c.RevokedAt == null
                            && (
                                c.State == "Issued" || c.State == "Generated" || c.State == "Legacy"
                            )
                        )
                    ),
                    limit = 10000,
                    policy = "Ineligible memberships are excluded. Every generated official card requires independent job approval. Missing required data fails that item.",
                };
            }
        );
        api.MapPost(
            "/bulk-jobs",
            async (IdCardJobInput i, IdCardService s) =>
            {
                var w = await s.Workspace("cards.bulk");
                Check(
                    i.ReuseIssued || (await s.A.Grants(w)).Contains("cards.issue"),
                    "Issuance permission required.",
                    403
                );
                await s.Governance.Scope(w, i.Selection.OrgId);
                Check(
                    i.Selection.Search.Length <= 100
                        && new[]
                        {
                            "Zip",
                            "Pdf",
                            "Print",
                            "FrontImages",
                            "BackImages",
                            "Pairs",
                        }.Contains(i.Format),
                    "Invalid selection/output."
                );
                var v = await s.Version(i.TemplateVersionId, w);
                var d = Unpack<IdCardPublished>(v.Layout).Design;
                var print = i.Print ?? new();
                IdCardDesign.Positions(d, print, false);
                IdCardDesign.Positions(d, print, true);
                if (i.Format == "BackImages")
                    Check(d.DoubleSided, "Template has no back side.");
                var q = Selection(s, w, i.Selection)
                    .Where(m =>
                        m.Status == "Active"
                        && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                    );
                Check(!(i.ReuseIssued&&i.Selection.Reissue),"Choose existing-card export or replacement issuance, not both.");
                if(i.ReuseIssued)q=q.Where(m=>s.Db.DigitalCards.Any(c=>c.MembershipId==m.Id&&c.TemplateVersionId==v.Id&&c.State=="Issued"&&c.RevokedAt==null&&c.ExpiryAt>DateTimeOffset.UtcNow));
                if (!i.Selection.Reissue && !i.ReuseIssued)
                    q = q.Where(m =>
                        !s.Db.DigitalCards.Any(c =>
                            c.MembershipId == m.Id
                            && c.RevokedAt == null
                            && (
                                c.State == "Issued" || c.State == "Generated" || c.State == "Legacy"
                            )
                        )
                    );
                var ids = await q.OrderBy(m => m.ClubId)
                    .ThenBy(m => m.Number)
                    .ThenBy(m => m.Id)
                    .Select(m => m.Id)
                    .Take(10001)
                    .ToListAsync();
                Check(
                    ids.Count is > 0 and <= 10000,
                    "Select 1–10,000 eligible memberships. Existing cards require explicit replacement."
                );
                Check(
                    i.Format is not ("Pdf" or "Print") || ids.Count <= 1000,
                    "Single PDF outputs are bounded to 1,000 cards; use ZIP/images for larger jobs."
                );
                await using var tx = await s.A.Mutation(w);
                var job = new IdCardJob
                {
                    TenantId = w.TenantId,
                    OrgId = i.Selection.OrgId,
                    CreatorId = w.UserId,
                    TemplateVersionId = v.Id,
                    Format = i.Format,
                    PrintLayout = Pack(print),
                    Reissue = i.Selection.Reissue,
                    ReuseIssued = i.ReuseIssued,
                    Total = ids.Count,
                };
                s.Db.IdCardJobs.Add(job);
                s.Db.IdCardJobItems.AddRange(
                    ids.Select(
                        (id, n) =>
                            new IdCardJobItem
                            {
                                TenantId = w.TenantId,
                                JobId = job.Id,
                                MembershipId = id,
                                Ordinal = n,
                            }
                    )
                );
                var r = await s.Governance.Request(
                    w,
                    job.OrgId,
                    "IdCardBatch",
                    "Generate " + ids.Count + " official membership cards",
                    Pack(
                        new IdCardReview(
                            job.Id,
                            Hash(
                                job.TemplateVersionId
                                    + ":"
                                    + job.Total
                                    + ":"
                                    + job.PrintLayout
                                    + ":"
                                    + job.Format
                                    + ":"
                                    + job.Reissue
                                    + (job.ReuseIssued?":Existing":"")
                            )
                        )
                    )
                );
                job.RequestId = r.Id;
                await s.Governance.Submit(r, w);
                s.Audit(
                    w.TenantId,
                    job.OrgId,
                    w.UserId,
                    "bulk.submit",
                    job.Id,
                    ids.Count.ToString()
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return new { job, request = r };
            }
        );
        api.MapGet(
            "/bulk-jobs",
            async (IdCardService s) =>
            {
                var w = await s.Workspace("cards.bulk");
                return await s
                    .Db.IdCardJobs.Where(j =>
                        j.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(j.OrgId)
                    )
                    .OrderByDescending(j => j.CreatedAt)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/bulk-jobs/{id:guid}",
            async (Guid id, IdCardService s, int after = -1) =>
            {
                var j = await Job(id, s);
                var items = await s
                    .Db.IdCardJobItems.Where(i => i.JobId == id && i.Ordinal > after)
                    .OrderBy(i => i.Ordinal)
                    .Take(100)
                    .ToListAsync();
                return new
                {
                    job = j,
                    items,
                    next = items.Count == 100 ? items[^1].Ordinal : (int?)null,
                };
            }
        );
        api.MapPost(
            "/bulk-jobs/{id:guid}/cancel",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.Workspace("cards.bulk");
                var j = await Job(id, s);
                await using var tx = await s.A.Mutation(w);
                await s.Db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(511109)");
                await s.Db.Entry(j).ReloadAsync();
                Check(
                    j.Status is "Queued" or "Running" or "Pending Approval",
                    "Job already terminal.",
                    409
                );
                j.CancelRequested = true;
                j.Status = "Cancelled";
                j.Version++;
                s.Audit(w.TenantId, j.OrgId, w.UserId, "bulk.cancel", id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return j;
            }
        );
        api.MapPost(
            "/bulk-jobs/{id:guid}/retry",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.Workspace("cards.bulk");
                var j = await Job(id, s);
                Check(
                    j.Status is "Completed with errors" or "Failed"
                        && j.ExpiresAt > DateTimeOffset.UtcNow,
                    "Only failed, unexpired jobs may be retried.",
                    409
                );
                await using var tx = await s.A.Mutation(w);
                await s.Db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(511109)");
                await s
                    .Db.IdCardJobItems.Where(i => i.JobId == id && i.Status == "Failed")
                    .ExecuteUpdateAsync(u =>
                        u.SetProperty(i => i.Status, "Pending").SetProperty(i => i.Error, "")
                    );
                j.Status = "Queued";
                j.Failed = 0;
                j.Error = "";
                j.OutputKey = "";
                j.Version++;
                s.Audit(w.TenantId, j.OrgId, w.UserId, "bulk.retry", id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return j;
            }
        );
        api.MapGet(
            "/bulk-jobs/{id:guid}/download",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.Workspace("cards.bulk");
                var j = await Job(id, s);
                Check(
                    j.Status is "Completed" or "Completed with errors"
                        && j.ExpiresAt > DateTimeOffset.UtcNow
                        && j.OutputKey != "",
                    "Output unavailable or expired.",
                    409
                );
                var ids = s.A.VisibleIds(w);
                var invalid = await (
                    from i in s.Db.IdCardJobItems
                    join m in s.Db.Memberships on i.MembershipId equals m.Id
                    join c in s.Db.DigitalCards on i.CardId equals c.Id
                    where
                        i.JobId == id
                        && i.Status == "Succeeded"
                        && (
                            !ids.Contains(m.ClubId)
                            || m.Status != "Active"
                            || m.EndsAt <= DateTimeOffset.UtcNow
                            || c.RevokedAt != null
                            || c.State != "Issued"
                            || c.ExpiryAt <= DateTimeOffset.UtcNow
                        )
                    select i.Id
                ).AnyAsync();
                Check(
                    !invalid,
                    "Output contains cards no longer current or within scope; create a fresh authorized job.",
                    409
                );
                s.Audit(w.TenantId, j.OrgId, w.UserId, "bulk.download", id, j.Success.ToString());
                await s.Db.SaveChangesAsync();
                return Results.File(
                    s.Storage.PathFor(j.OutputKey),
                    j.Format is "Pdf" or "Print" ? "application/pdf" : "application/zip",
                    "id-cards-" + j.Id + (j.Format is "Pdf" or "Print" ? ".pdf" : ".zip"),
                    enableRangeProcessing: true
                );
            }
        );
    }

    static void MapPrint(RouteGroupBuilder api)
    {
        api.MapGet(
            "/print-presets",
            async (IdCardService s) =>
            {
                var w = await s.Workspace("members.cards");
                return await s
                    .Db.IdCardPrintPresets.Where(p =>
                        p.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(p.OrgId)
                    )
                    .OrderBy(p => p.Name)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/print-presets",
            async (IdCardPresetInput i, IdCardService s) =>
            {
                var w = await s.Workspace("cards.design");
                await s.Governance.Scope(w, i.OrgId);
                IdCardDesign.Positions(IdCardDesign.Default(), i.Layout, false);
                IdCardDesign.Positions(IdCardDesign.Default(), i.Layout, true);
                Check(i.Name.Length is > 0 and <= 120, "Invalid preset name.");
                var p = new IdCardPrintPreset
                {
                    TenantId = w.TenantId,
                    OrgId = i.OrgId,
                    Name = i.Name,
                    Layout = Pack(i.Layout),
                };
                s.Db.IdCardPrintPresets.Add(p);
                s.Audit(w.TenantId, i.OrgId, w.UserId, "print.preset", p.Id);
                await s.Db.SaveChangesAsync();
                return p;
            }
        );
        api.MapPost(
            "/print-preview",
            async (IdCardPresetInput i, IdCardService s) =>
            {
                var w = await s.Workspace("cards.design");
                await s.Governance.Scope(w, i.OrgId);
                var d = IdCardDesign.Default();
                return new
                {
                    front = IdCardDesign.Positions(d, i.Layout, false),
                    back = IdCardDesign.Positions(d, i.Layout, true),
                    card = d,
                    preset = i.Layout,
                };
            }
        );
    }

    static void MapVerification(WebApplication app)
    {
        app.MapGet(
                "/api/v1/public/id-cards/verify/{token}",
                async (string token, Db db) =>
                {
                    Check(token.Length == 64, "Invalid verification token.");
                    var c = await db
                        .DigitalCards.AsNoTracking()
                        .SingleOrDefaultAsync(c => c.TokenHash == Hash(token));
                    if (c == null)
                    {
                        db.DigitalCardVerifications.Add(new() { Valid = false });
                        await db.SaveChangesAsync();
                        return Results.Ok(new { valid = false, status = "Unrecognized" });
                    }
                    var m = await db
                        .Memberships.Include(m => m.Person)
                            .ThenInclude(p => p.Privacy)
                        .Include(m => m.Club)
                        .SingleAsync(m => m.Id == c.MembershipId && m.TenantId == c.TenantId);
                    var valid =
                        c.State == "Issued"
                        && c.RevokedAt == null
                        && c.ExpiryAt > DateTimeOffset.UtcNow
                        && m.Status == "Active"
                        && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                        && m.Club.Status == "Active";
                    db.DigitalCardVerifications.Add(new() { CardId = c.Id, Valid = valid });
                    await db.SaveChangesAsync();
                    return Results.Ok(
                        new
                        {
                            valid,
                            status = IdCardService.Status(c),
                            serial = c.Serial,
                            name = m.Person.Privacy.CardNameVisible && c.Snapshot != "{}"
                                ? Unpack<IdCardSnapshot>(c.Snapshot)
                                    .Values.GetValueOrDefault("member.full_name")
                                : "Verified member",
                            club = m.Club.Name,
                            association = await db
                                .Tenants.Where(t => t.Id == c.TenantId)
                                .Select(t => t.Name)
                                .SingleAsync(),
                            expiresAt = c.ExpiryAt,
                        }
                    );
                }
            )
            .RequireRateLimiting("cards");
    }
}
