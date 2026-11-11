using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record IdCardPreviewInput(Guid MembershipId, IdCardLayout? Design = null);

public record IdCardPresetInput(Guid OrgId, string Name, IdCardPrintLayout Layout);

public record IdCardPolicyInput(
    bool AllowMemberDownload,
    bool AllowClubDesign,
    int ValidityMonths,
    long Version = 1,
    Guid? DefaultTemplateVersionId = null
);

public static partial class IdCardEndpoints
{
    public static void MapIdCards(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/id-cards")
            .RequireRateLimiting("idcards")
            .AddEndpointFilter<IdCardInputFilter>();
        api.MapGet(
            "/options",
            async (IdCardService s) =>
            {
                var w = await s.Workspace("members.cards");
                return new
                {
                    fields = IdCardDesign.Fields,
                    design = IdCardDesign.Default(),
                    organizations = await s
                        .A.Visible(w)
                        .OrderBy(o => o.Name)
                        .Select(o => new { o.Id, o.Name })
                        .Take(1000)
                        .ToListAsync(),
                    categories = await s
                        .Db.MembershipCategories.Where(c => c.TenantId == w.TenantId)
                        .Select(c => new { c.Id, c.Name })
                        .ToListAsync(),
                    positions = await s
                        .Db.OfficerPositions.Where(p => p.TenantId == w.TenantId && p.Active)
                        .Select(p => new { p.Id, name = p.Title })
                        .ToListAsync(),
                    policy = await s.Policy(w.TenantId),
                };
            }
        );
        api.MapGet(
            "/templates",
            async (IdCardService s) =>
            {
                var w = await s.Workspace("members.cards");
                var ancestors = await s.Ancestors(w);
                return await s
                    .Db.CertificateTemplates.Where(t =>
                        t.Type == "IdCard"
                        && t.TenantId == w.TenantId
                        && (s.A.VisibleIds(w).Contains(t.OrgId) || ancestors.Contains(t.OrgId))
                    )
                    .OrderBy(t => t.Name)
                    .Take(200)
                    .Select(t => new
                    {
                        t.Id,
                        t.Name,
                        t.OrgId,
                        t.Version,
                        t.Active,
                        t.Layout,
                        published = s
                            .Db.IdCardTemplateVersions.Where(v => v.TemplateId == t.Id)
                            .OrderByDescending(v => v.Number)
                            .Select(v => new
                            {
                                v.Id,
                                v.Number,
                                v.At,
                            })
                            .FirstOrDefault(),
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/templates",
            async (IdCardTemplateInput i, IdCardService s) =>
            {
                var w = await s.Workspace("cards.design");
                await s.Governance.Scope(w, i.OrgId);
                await DesignPolicy(s, w, i.OrgId);
                IdCardDesign.Validate(i.Design);
                Check(i.Name.Length is > 0 and <= 120, "Template name requires 1–120 characters.");
                var t = new CertificateTemplate
                {
                    TenantId = w.TenantId,
                    OrgId = i.OrgId,
                    Name = i.Name,
                    Type = "IdCard",
                    Layout = Pack(i.Design),
                };
                await Inheritance(s, i.Design, w);
                s.Db.CertificateTemplates.Add(t);
                s.Audit(w.TenantId, i.OrgId, w.UserId, "template.create", t.Id);
                await s.Db.SaveChangesAsync();
                return t;
            }
        );
        api.MapGet(
            "/templates/{id:guid}",
            async (Guid id, IdCardService s) =>
            {
                var t = await s.Template(id, "members.cards");
                return new
                {
                    template = t,
                    design = Unpack<IdCardLayout>(t.Layout),
                    versions = await s
                        .Db.IdCardTemplateVersions.Where(v => v.TemplateId == id)
                        .OrderByDescending(v => v.Number)
                        .Select(v => new
                        {
                            v.Id,
                            v.Number,
                            v.At,
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapPut(
            "/templates/{id:guid}",
            async (Guid id, IdCardTemplateInput i, IdCardService s) =>
            {
                var w = await s.Workspace("cards.design");
                var t = await s.Template(id);
                await using var tx = await s.A.Mutation(w);
                await s.Db.Entry(t).ReloadAsync();
                Check(
                    t.Version == i.Version && t.OrgId == i.OrgId && t.Active,
                    "Template changed or archived.",
                    409
                );
                await DesignPolicy(s, w, t.OrgId);
                IdCardDesign.Validate(i.Design);
                await Inheritance(s, i.Design, w);
                Check(i.Name.Length is > 0 and <= 120, "Invalid name.");
                t.Layout = Pack(i.Design);
                t.Name = i.Name;
                t.Version++;
                t.UpdatedAt = DateTimeOffset.UtcNow;
                s.Audit(w.TenantId, t.OrgId, w.UserId, "template.update", t.Id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return t;
            }
        );
        api.MapGet(
            "/versions/{id:guid}",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.Workspace("cards.design");
                var v = await s.Version(id, w);
                await s.Template(v.TemplateId, "members.cards");
                return new
                {
                    v.Id,
                    v.Number,
                    design = Unpack<IdCardPublished>(v.Layout).Design,
                };
            }
        );
        api.MapPost(
            "/templates/{id:guid}/duplicate",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.Workspace("cards.design");
                var t = await s.Template(id, "members.cards");
                var local = await s.A.VisibleIds(w).ContainsAsync(t.OrgId);
                var parent = local
                    ? null
                    : await s
                        .Db.IdCardTemplateVersions.Where(v => v.TemplateId == id)
                        .OrderByDescending(v => v.Number)
                        .FirstOrDefaultAsync();
                Check(
                    local || parent != null,
                    "Only published parent templates can be inherited.",
                    409
                );
                var copy = new CertificateTemplate
                {
                    TenantId = t.TenantId,
                    OrgId = local ? t.OrgId : w.OrgId,
                    Type = "IdCard",
                    Name = t.Name + " copy",
                    Layout =
                        parent == null
                            ? t.Layout
                            : Pack(
                                Unpack<IdCardPublished>(parent.Layout).Design with
                                {
                                    ParentVersionId = parent.Id,
                                }
                            ),
                };
                s.Db.CertificateTemplates.Add(copy);
                s.Audit(w.TenantId, t.OrgId, w.UserId, "template.duplicate", copy.Id);
                await s.Db.SaveChangesAsync();
                return copy;
            }
        );
        api.MapPost(
            "/templates/{id:guid}/archive",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.Workspace("cards.publish");
                var t = await s.Template(id, "cards.publish");
                t.Active = false;
                t.Version++;
                s.Audit(w.TenantId, t.OrgId, w.UserId, "template.archive", id);
                await s.Db.SaveChangesAsync();
                return t;
            }
        );
        api.MapPost(
            "/templates/{id:guid}/publish",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.Workspace("cards.publish");
                var t = await s.Template(id, "cards.publish");
                await using var tx = await s.A.Mutation(w);
                await s.Db.Entry(t).ReloadAsync();
                Check(t.Active, "Archived template cannot be published.", 409);
                await DesignPolicy(s, w, t.OrgId);
                var d = Unpack<IdCardLayout>(t.Layout);
                IdCardDesign.Publishable(d);
                await Inheritance(s, d, w);
                var frozen = await s.Freeze(d);
                var last =
                    await s
                        .Db.IdCardTemplateVersions.Where(v => v.TemplateId == id)
                        .MaxAsync(v => (long?)v.Number)
                    ?? 0;
                var version = new IdCardTemplateVersion
                {
                    TenantId = w.TenantId,
                    OrgId = t.OrgId,
                    TemplateId = id,
                    Number = last + 1,
                    Layout = Pack(frozen),
                    PublisherId = w.UserId,
                };
                s.Db.IdCardTemplateVersions.Add(version);
                s.Audit(w.TenantId, t.OrgId, w.UserId, "template.publish", version.Id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return new
                {
                    version.Id,
                    version.Number,
                    version.At,
                };
            }
        );
        api.MapPost(
            "/templates/{id:guid}/preview",
            async (Guid id, IdCardPreviewInput i, IdCardService s, bool back = false) =>
            {
                var w = await s.Workspace("cards.design");
                var t = await s.Template(id);
                var m = await s.Load(i.MembershipId, w);
                var d = i.Design ?? Unpack<IdCardLayout>(t.Layout);
                var published = await s.Freeze(d);
                var temp = new DigitalCard
                {
                    TenantId = w.TenantId,
                    MembershipId = m.Id,
                    Serial = "PREVIEW",
                    ProtectedToken = s.Members.Protect(Token()),
                    ExpiryAt = m.EndsAt,
                };
                var snapshot = await s.Snapshot(m, temp);
                snapshot.Values["card.qr_code"] = "https://example.invalid/unissued-preview";
                s.Audit(w.TenantId, m.ClubId, w.UserId, "preview", id);
                await s.Db.SaveChangesAsync();
                return Results.File(
                    s.Renderer.Png(published, snapshot, back, preview: true),
                    "image/png"
                );
            }
        );
        api.MapGet(
            "/members/{id:guid}/bindings",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.A.WorkspaceAny("cards.design", "cards.issue");
                var m = await s.Load(id, w);
                var policy = await s.Policy(w.TenantId);
                var temp = new DigitalCard
                {
                    Serial = "PREVIEW",
                    ProtectedToken = s.Members.Protect(Token()),
                    ExpiryAt = m.EndsAt ?? DateTimeOffset.UtcNow.AddMonths(policy.ValidityMonths),
                };
                var snapshot = await s.Snapshot(m, temp);
                snapshot.Values["card.qr_code"] = "https://example.invalid/unissued-preview";
                return new
                {
                    snapshot.Values,
                    hasPhoto = snapshot.Images.ContainsKey("member.photo"),
                };
            }
        );
        api.MapGet(
            "/versions/{id:guid}/assets/{asset:guid}",
            async (Guid id, Guid asset, IdCardService s) =>
            {
                var w = await s.Workspace("cards.design");
                var v = await s.Version(id, w);
                await s.Template(v.TemplateId, "members.cards");
                var frozen = Unpack<IdCardPublished>(v.Layout);
                Check(
                    frozen.Assets.TryGetValue(asset.ToString(), out var key),
                    "Template asset unavailable.",
                    404
                );
                var bytes = await File.ReadAllBytesAsync(s.Storage.PathFor(key!));
                return Results.File(bytes, MemberMediaEndpoints.FileType(bytes));
            }
        );
        api.MapGet(
            "/members",
            async (
                IdCardService s,
                Guid? orgId = null,
                Guid? categoryId = null,
                string search = "",
                Guid? after = null
            ) =>
            {
                var w = await s.Workspace("cards.issue");
                Check(search.Length <= 100, "Search too long.");
                if (orgId != null)
                    await s.Governance.Scope(w, orgId.Value);
                var scope =
                    orgId == null
                        ? w
                        : new Assignment
                        {
                            TenantId = w.TenantId,
                            OrgId = orgId.Value,
                            UserId = w.UserId,
                            RoleId = w.RoleId,
                        };
                var q = s.Db.Memberships.Where(m =>
                    m.TenantId == w.TenantId
                    && s.A.VisibleIds(scope).Contains(m.ClubId)
                    && (categoryId == null || m.CategoryId == categoryId)
                    && (after == null || m.Id.CompareTo(after.Value) > 0)
                );
                if (search != "")
                    q = q.Where(m =>
                        EF.Functions.ILike(
                            m.Person.DisplayName,
                            IntelligenceSearch.Pattern(search),
                            "\\"
                        ) || EF.Functions.ILike(m.Number, IntelligenceSearch.Pattern(search), "\\")
                    );
                var rows = await q.OrderBy(m => m.Id)
                    .Take(51)
                    .Select(m => new
                    {
                        m.Id,
                        m.Number,
                        name = m.Person.DisplayName,
                        club = m.Club.Name,
                        m.ClubId,
                        m.Status,
                        m.EndsAt,
                        hasPhoto = m.Person.Photo != null,
                        currentCard = s
                            .Db.DigitalCards.Where(c =>
                                c.MembershipId == m.Id
                                && c.RevokedAt == null
                                && (
                                    c.State == "Issued"
                                    || c.State == "Legacy"
                                    || c.State == "Generated"
                                )
                            )
                            .Select(c => new
                            {
                                c.Id,
                                c.State,
                                c.ExpiryAt,
                            })
                            .FirstOrDefault(),
                    })
                    .ToListAsync();
                return new
                {
                    items = rows.Take(50),
                    next = rows.Count > 50 ? rows[49].Id : (Guid?)null,
                };
            }
        );
        api.MapPost(
            "/issuances",
            async (IdCardIssueInput i, IdCardService s) =>
            {
                var w = await s.Workspace();
                await using var tx = await s.A.Mutation(w);
                var c = await s.Draft(i, w);
                await tx.CommitAsync();
                return c;
            }
        );
        api.MapGet(
            "/issuances",
            async (
                IdCardService s,
                Guid? after = null,
                string state = "",
                Guid? membershipId = null
            ) =>
            {
                var w = await s.Workspace("members.cards");
                return await (
                    from c in s.Db.DigitalCards
                    join m in s.Db.Memberships on c.MembershipId equals m.Id
                    where
                        c.TenantId == w.TenantId
                        && s.A.VisibleIds(w).Contains(m.ClubId)
                        && (after == null || c.Id.CompareTo(after.Value) > 0)
                        && (state == "" || c.State == state)
                        && (membershipId == null || m.Id == membershipId)
                    orderby c.Id
                    select new
                    {
                        c.Id,
                        c.Serial,
                        c.State,
                        c.ExpiryAt,
                        c.IssuedAt,
                        c.RevokedAt,
                        c.TemplateVersionId,
                        c.RequestId,
                        c.Version,
                        c.MembershipId,
                        name = m.Person.DisplayName,
                        club = m.Club.Name,
                    }
                )
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/issuances/{id:guid}",
            async (Guid id, IdCardService s) =>
            {
                var (c, m) = await s.Card(id);
                return new
                {
                    card = c,
                    status = IdCardService.Status(c),
                    values = c.TemplateVersionId == null
                        ? null
                        : Unpack<IdCardSnapshot>(c.Snapshot).Values,
                    history = await s
                        .Db.Audits.Where(a =>
                            a.TenantId == c.TenantId
                            && a.Resource == id.ToString()
                            && a.Action.StartsWith("idcard.")
                        )
                        .OrderBy(a => a.At)
                        .Select(a => new
                        {
                            a.Action,
                            a.At,
                            a.Changes,
                        })
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/issuances/{id:guid}/submit",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.Workspace();
                var (c, m) = await s.Card(id, false);
                await using var tx = await s.A.Mutation(w);
                await s.Db.Entry(c).ReloadAsync();
                Check(
                    c.State == "Draft" && c.RevokedAt == null && c.CreatorId == w.UserId,
                    "Only the creator can submit an editable card draft.",
                    409
                );
                IdCardService.Eligible(m);
                var r = await s.Governance.Request(
                    w,
                    m.ClubId,
                    "IdCardIssue",
                    "Issue membership ID " + c.Serial,
                    Pack(
                        new IdCardReview(
                            c.Id,
                            Hash(
                                c.Snapshot
                                    + ":"
                                    + c.TemplateVersionId
                                    + ":"
                                    + c.ExpiryAt?.ToString("O")
                            )
                        )
                    ),
                    subject: m.Person.UserId
                );
                c.RequestId = r.Id;
                c.State = "Pending Approval";
                c.Version++;
                await s.Governance.Submit(r, w);
                s.Audit(w.TenantId, m.ClubId, w.UserId, "submit", id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return new { card = c, request = r };
            }
        );
        api.MapPost(
            "/issuances/{id:guid}/generate",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.Workspace();
                var (c, m) = await s.Card(id, false);
                await using var tx = await s.A.Mutation(w);
                await s.Members.Lock(m.Id);
                await s.Db.Entry(c).ReloadAsync();
                await s.Generate(c, w);
                await tx.CommitAsync();
                return c;
            }
        );
        api.MapPost(
            "/issuances/{id:guid}/issue",
            async (Guid id, IdCardService s) =>
            {
                var w = await s.Workspace();
                var (c, m) = await s.Card(id, false);
                await using var tx = await s.A.Mutation(w);
                await s.Members.Lock(m.Id);
                await s.Db.Entry(c).ReloadAsync();
                await s.Issue(c, w);
                await tx.CommitAsync();
                return c;
            }
        );
        api.MapPost(
            "/issuances/{id:guid}/revoke",
            async (Guid id, IdCardReason i, IdCardService s) =>
            {
                var w = await s.Workspace();
                await using var tx = await s.A.Mutation(w);
                var (c, m) = await s.Card(id, false);
                Check(i.Version == 0 || i.Version == c.Version, "Card changed. Refresh.", 409);
                Check(
                    i.Reason.Length is >= 5 and <= 500,
                    "Revocation reason requires 5–500 characters."
                );
                c.RevokedAt = DateTimeOffset.UtcNow;
                c.State = "Revoked";
                c.Version++;
                s.Audit(w.TenantId, m.ClubId, w.UserId, "revoke", id, i.Reason);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return c;
            }
        );
        api.MapGet(
            "/issuances/{id:guid}/download",
            async (Guid id, IdCardService s, string format = "pdf") => await Download(id, s, format)
        );
        api.MapGet(
            "/issuances/{id:guid}/preview",
            async (Guid id, IdCardService s, bool back = false) =>
            {
                var (c, m) = await s.Card(id);
                Check(
                    c.TemplateVersionId != null,
                    "Legacy card uses the original digital-card endpoint.",
                    409
                );
                var v = await s.Db.IdCardTemplateVersions.SingleAsync(v =>
                    v.Id == c.TemplateVersionId
                );
                s.Audit(c.TenantId, m.ClubId, (await s.A.User()).Id, "preview", id);
                await s.Db.SaveChangesAsync();
                return Results.File(
                    s.Renderer.Png(
                        Unpack<IdCardPublished>(v.Layout),
                        Unpack<IdCardSnapshot>(c.Snapshot),
                        back,
                        preview: c.State is not ("Issued" or "Revoked" or "Replaced")
                    ),
                    "image/png"
                );
            }
        );
        app.MapGet(
            "/api/v1/my/id-card",
            async (IdCardService s) =>
            {
                var u = await s.A.User();
                var people = s.Db.Persons.Where(p => p.UserId == u.Id).Select(p => p.Id);
                return await (
                    from c in s.Db.DigitalCards
                    join m in s.Db.Memberships on c.MembershipId equals m.Id
                    where
                        people.Contains(m.PersonId)
                        && (s.A.BoundTenant == null || c.TenantId == s.A.BoundTenant)
                    orderby c.IssuedAt descending
                    select new
                    {
                        c.Id,
                        c.Serial,
                        c.State,
                        c.RevokedAt,
                        c.ExpiryAt,
                        c.TemplateVersionId,
                        c.MembershipId,
                        club = m.Club.Name,
                        canDownload = !s.Db.IdCardPolicies.Any(p =>
                            p.TenantId == c.TenantId && !p.AllowMemberDownload
                        ),
                    }
                )
                    .Take(100)
                    .ToListAsync();
            }
        );
        app.MapGet(
            "/api/v1/my/id-card/download",
            async (Guid id, IdCardService s, string format = "pdf") =>
                await Download(id, s, format, true)
        );
        api.MapPost(
            "/issuances/{id:guid}/requests",
            async (Guid id, IdCardReason i, IdCardService s) =>
            {
                var (c, m) = await s.Card(id);
                Check(
                    i.Reason.Length is >= 5 and <= 500,
                    "Describe the correction or lost card in 5–500 characters."
                );
                s.Audit(
                    c.TenantId,
                    m.ClubId,
                    (await s.A.User()).Id,
                    "member.request",
                    id,
                    i.Reason
                );
                await s.Db.SaveChangesAsync();
                return Results.Ok(
                    new
                    {
                        message = "Request recorded for the issuing authority. Replacement still requires reviewed issuance.",
                    }
                );
            }
        );
        api.MapGet(
            "/analytics",
            async (IdCardService s) =>
            {
                var w = await s.Workspace("members.cards");
                var ms = s.Db.Memberships.Where(m =>
                    m.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(m.ClubId)
                );
                var cards = s.Db.DigitalCards.Where(c =>
                    c.TenantId == w.TenantId && ms.Any(m => m.Id == c.MembershipId)
                );
                var now = DateTimeOffset.UtcNow;
                return new
                {
                    eligible = await ms.CountAsync(m =>
                        m.Status == "Active" && (m.EndsAt == null || m.EndsAt > now)
                    ),
                    missingPhotos = await ms.CountAsync(m => m.Person.Photo == null),
                    issued = await cards.CountAsync(c =>
                        c.State == "Issued" && c.RevokedAt == null && c.ExpiryAt > now
                    ),
                    pending = await cards.CountAsync(c =>
                        c.State == "Draft" || c.State == "Pending Approval" || c.State == "Approved"
                    ),
                    expired = await cards.CountAsync(c => c.ExpiryAt < now && c.RevokedAt == null),
                    revoked = await cards.CountAsync(c => c.RevokedAt != null),
                    expiring = await cards.CountAsync(c =>
                        c.State == "Issued"
                        && c.RevokedAt == null
                        && c.ExpiryAt > now
                        && c.ExpiryAt < now.AddDays(30)
                    ),
                    failedJobs = await s.Db.IdCardJobs.CountAsync(j =>
                        j.TenantId == w.TenantId
                        && s.A.VisibleIds(w).Contains(j.OrgId)
                        && j.Status == "Failed"
                    ),
                };
            }
        );
        api.MapGet(
            "/policy",
            async (IdCardService s) => await s.Policy((await s.Workspace("members.cards")).TenantId)
        );
        api.MapPut(
            "/policy",
            async (IdCardPolicyInput i, IdCardService s) =>
            {
                var w = await s.A.TenantWorkspace("cards.settings");
                var p = await s.Db.IdCardPolicies.SingleOrDefaultAsync(p =>
                    p.TenantId == w.TenantId
                );
                Check(i.ValidityMonths is >= 1 and <= 60, "Validity is 1–60 months.");
                if (p == null)
                {
                    p = new() { TenantId = w.TenantId };
                    s.Db.IdCardPolicies.Add(p);
                }
                else
                    Check(p.Version == i.Version, "Policy changed.", 409);
                if (i.DefaultTemplateVersionId != null)
                    await s.Version(i.DefaultTemplateVersionId.Value, w);
                p.DefaultTemplateVersionId = i.DefaultTemplateVersionId;
                p.AllowMemberDownload = i.AllowMemberDownload;
                p.AllowClubDesign = i.AllowClubDesign;
                p.ValidityMonths = i.ValidityMonths;
                p.Version++;
                s.Audit(w.TenantId, w.OrgId, w.UserId, "policy", p.Id);
                await s.Db.SaveChangesAsync();
                return p;
            }
        );
        api.MapGet(
            "/qr-preview",
            async (IdCardService s) =>
            {
                await s.Workspace("members.cards");
                using var qr = QRCoder.QRCodeGenerator.GenerateQrCode(
                    "https://example.invalid/unissued-preview",
                    QRCoder.QRCodeGenerator.ECCLevel.M
                );
                return Results.File(new QRCoder.PngByteQRCode(qr).GetGraphic(6), "image/png");
            }
        );
        api.MapGet(
            "/assets",
            async (IdCardService s) =>
            {
                var w = await s.Workspace("cards.design");
                var docs = await s
                    .Db.GovernanceDocuments.Where(d =>
                        d.TenantId == w.TenantId
                        && d.Folder == "ID Card Assets"
                        && d.Status == "Available"
                        && (d.RetainUntil == null || d.RetainUntil > DateTimeOffset.UtcNow)
                        && s.A.VisibleIds(w).Contains(d.OrgId)
                    )
                    .OrderByDescending(d => d.At)
                    .Take(50)
                    .ToListAsync();
                var output = new List<object>();
                foreach (var d in docs)
                {
                    try
                    {
                        await s.Operations.Document(d);
                        output.Add(
                            new
                            {
                                d.Id,
                                d.Title,
                                url = "/api/v1/documents/" + d.Id + "/download?preview=true",
                            }
                        );
                    }
                    catch (ApiError) { }
                }
                return output;
            }
        );
        api.MapPost(
                "/assets",
                async (IFormFile file, [FromForm] Guid orgId, IdCardService s) =>
                {
                    var w = await s.Workspace("cards.design");
                    Check(
                        (await s.A.Grants(w)).Contains("operations.documents"),
                        "Document asset permission required.",
                        403
                    );
                    await s.Governance.Scope(w, orgId);
                    Check(file.Length is > 0 and <= 2097152, "Raster assets must be at most 2 MB.");
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var original = ms.ToArray();
                    using var image = IdCardRenderer.Decode(original);
                    var sanitized = MemberMediaEndpoints.StripPhoto(original);
                    var key = Guid.NewGuid().ToString("N");
                    Directory.CreateDirectory(s.Storage.Root);
                    await File.WriteAllBytesAsync(s.Storage.PathFor(key), sanitized.Bytes);
                    var doc = new GovernanceDocument
                    {
                        TenantId = w.TenantId,
                        OrgId = orgId,
                        Title = Path.GetFileName(file.FileName)[
                            ..Math.Min(120, Path.GetFileName(file.FileName).Length)
                        ],
                        FileName = "card-asset" + (sanitized.Type == "image/png" ? ".png" : ".jpg"),
                        ContentType = sanitized.Type,
                        ObjectKey = key,
                        Sha256 = IntelligenceKnowledge.HashBytes(sanitized.Bytes),
                        UploaderId = w.UserId,
                        Kind = "Supporting",
                        Folder = "ID Card Assets",
                        Visibility = "Organization",
                    };
                    s.Db.GovernanceDocuments.Add(doc);
                    s.Db.DocumentVersions.Add(
                        new()
                        {
                            TenantId = w.TenantId,
                            OrgId = orgId,
                            DocumentId = doc.Id,
                            Number = 1,
                            ObjectKey = key,
                            FileName = doc.FileName,
                            ContentType = doc.ContentType,
                            Sha256 = doc.Sha256,
                            Size = sanitized.Bytes.Length,
                            UploaderId = w.UserId,
                        }
                    );
                    s.Audit(w.TenantId, orgId, w.UserId, "asset.upload", doc.Id);
                    await s.Db.SaveChangesAsync();
                    return new
                    {
                        doc.Id,
                        doc.Title,
                        width = image.Width,
                        height = image.Height,
                        url = "/api/v1/documents/" + doc.Id + "/download?preview=true",
                    };
                }
            )
            .DisableAntiforgery();
        api.MapPost(
                "/members/{id:guid}/photo",
                async (Guid id, IFormFile file, IdCardService s) =>
                {
                    var w = await s.Workspace();
                    Check(
                        (await s.A.Grants(w)).Contains("members.manage"),
                        "Member photo management permission required.",
                        403
                    );
                    var m = await s.Load(id, w);
                    Check(file.Length is > 0 and <= 2097152, "Photo must be at most 2 MB.");
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    using var image = IdCardRenderer.Decode(bytes);
                    var clean = MemberMediaEndpoints.StripPhoto(bytes);
                    m.Person.Photo = clean.Bytes;
                    m.Person.PhotoType = clean.Type;
                    m.Person.Version++;
                    s.Audit(w.TenantId, m.ClubId, w.UserId, "photo.update", m.Id);
                    await s.Db.SaveChangesAsync();
                    return Results.Ok(new { m.Person.Version });
                }
            )
            .DisableAntiforgery();
        MapJobs(api);
        MapPrint(api);
        MapVerification(app);
    }

    static async Task DesignPolicy(IdCardService s, Assignment w, Guid org)
    {
        var policy = await s.Policy(w.TenantId);
        Check(
            policy.AllowClubDesign
                || await s.Db.Orgs.AnyAsync(o => o.Id == w.OrgId && o.ParentId == null),
            "Association policy restricts local card designs.",
            403
        );
    }

    static async Task Inheritance(IdCardService s, IdCardLayout d, Assignment w)
    {
        if (d.ParentVersionId == null)
            return;
        var parent = await s.Version(d.ParentVersionId.Value, w);
        await s.Template(parent.TemplateId, "members.cards");
        var original = Unpack<IdCardPublished>(parent.Layout).Design;
        foreach (var pair in new[] { (original.Front!, d.Front!), (original.Back!, d.Back!) })
        foreach (var locked in pair.Item1.Elements.Where(e => e.Locked))
            Check(
                pair.Item2.Elements.Any(e => Pack(e) == Pack(locked)),
                "Inherited protected branding layers must remain unchanged.",
                409
            );
        Check(
            d.Width == original.Width && d.Height == original.Height,
            "Inherited card dimensions are fixed."
        );
    }

    static async Task<IResult> Download(
        Guid id,
        IdCardService s,
        string format,
        bool onlyOwn = false
    )
    {
        var (c, m) = await s.Card(id, true);
        var user = await s.A.User();
        var own = m.Person.UserId == user.Id;
        Check(!onlyOwn || own, "Card unavailable.", 404);
        if (own)
            Check(
                (await s.Policy(c.TenantId)).AllowMemberDownload,
                "Member downloads are disabled by association policy.",
                403
            );
        Check(
            c.State is "Issued" or "Replaced" or "Revoked" && c.Files != "{}",
            "No issued file is available.",
            409
        );
        var files = Unpack<IdCardFiles>(c.Files);
        var key =
            format == "pdf" ? files.Pdf
            : format == "front" ? files.Front
            : format == "back" ? files.Back
            : "";
        Check(key != "", "Unsupported or unavailable output.", 404);
        s.Audit(c.TenantId, m.ClubId, user.Id, "download", id, format);
        await s.Db.SaveChangesAsync();
        return Results.File(
            s.Storage.PathFor(key),
            format == "pdf" ? "application/pdf" : "image/png",
            c.Serial + "-" + format + (format == "pdf" ? ".pdf" : ".png"),
            enableRangeProcessing: true
        );
    }
}
