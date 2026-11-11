using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record IdCardTemplateInput(Guid OrgId, string Name, IdCardLayout Design, long Version = 1);

public record IdCardIssueInput(
    Guid MembershipId,
    Guid TemplateVersionId,
    DateTimeOffset? ExpiryAt = null,
    Guid? ReplacesId = null,
    string Reason = ""
);

public record IdCardSelection(
    Guid OrgId,
    Guid? CategoryId = null,
    string Search = "",
    Guid[]? MembershipIds = null,
    DateTimeOffset? JoinedFrom = null,
    DateTimeOffset? JoinedTo = null,
    bool Reissue = false,
    string Country = "",
    Guid? PositionId = null,
    string MemberStatus = "",
    string CardStatus = "",
    DateTimeOffset? ExpiryFrom = null,
    DateTimeOffset? ExpiryTo = null
);

public record IdCardJobInput(
    Guid TemplateVersionId,
    IdCardSelection Selection,
    string Format = "Zip",
    IdCardPrintLayout? Print = null,
    bool ReuseIssued = false
);

public record IdCardReason(string Reason, long Version = 0);

public record IdCardReview(Guid Id, string Hash);

public class IdCardService(
    Access a,
    MembershipService members,
    GovernanceService governance,
    OperationsService operations,
    CommunicationStorage storage,
    IdCardRenderer renderer,
    NotificationService notifications,
    IConfiguration config
)
{
    public Access A => a;
    public Db Db => a.Db;
    public MembershipService Members => members;
    public GovernanceService Governance => governance;
    public CommunicationStorage Storage => storage;
    public IdCardRenderer Renderer => renderer;
    public OperationsService Operations => operations;

    public async Task<Assignment> Workspace(string permission = "cards.issue") =>
        await a.Workspace(permission);

    public Task<Guid[]> Ancestors(Assignment w) =>
        Db
            .Database.SqlQuery<Guid>(
                $"""WITH RECURSIVE parent AS(SELECT "Id","ParentId" FROM "Orgs" WHERE "Id"={w.OrgId} AND "TenantId"={w.TenantId} UNION ALL SELECT o."Id",o."ParentId" FROM "Orgs" o JOIN parent p ON o."Id"=p."ParentId" WHERE o."TenantId"={w.TenantId}) SELECT "Id" AS "Value" FROM parent"""
            )
            .ToArrayAsync();

    public async Task<CertificateTemplate> Template(Guid id, string permission = "cards.design")
    {
        var w = await Workspace(permission);
        var ancestors = permission == "members.cards" ? await Ancestors(w) : Array.Empty<Guid>();
        return await Db.CertificateTemplates.SingleOrDefaultAsync(t =>
                t.Id == id
                && t.Type == "IdCard"
                && t.TenantId == w.TenantId
                && (a.VisibleIds(w).Contains(t.OrgId) || ancestors.Contains(t.OrgId))
            ) ?? throw new ApiError(404, "Card template unavailable.");
    }

    public async Task<IdCardPolicy> Policy(Guid tenant) =>
        await Db.IdCardPolicies.SingleOrDefaultAsync(p => p.TenantId == tenant)
        ?? new() { TenantId = tenant };

    public async Task<IdCardTemplateVersion> Version(Guid id, Assignment w) =>
        await Db.IdCardTemplateVersions.SingleOrDefaultAsync(v =>
            v.Id == id && v.TenantId == w.TenantId
        ) ?? throw new ApiError(404, "Published template unavailable.");

    public async Task ScopeTemplate(IdCardTemplateVersion v, Membership m)
    {
        var included = await Db
            .Database.SqlQuery<Guid>(
                $"""WITH RECURSIVE parent AS(SELECT "Id","ParentId" FROM "Orgs" WHERE "Id"={m.ClubId} AND "TenantId"={m.TenantId} UNION ALL SELECT o."Id",o."ParentId" FROM "Orgs" o JOIN parent p ON o."Id"=p."ParentId" WHERE o."TenantId"={m.TenantId}) SELECT "Id" AS "Value" FROM parent"""
            )
            .ContainsAsync(v.OrgId);
        Check(included, "Template belongs to another organizational branch.", 403);
        var d = Unpack<IdCardPublished>(v.Layout).Design;
        Check(
            d.CategoryId == null || d.CategoryId == m.CategoryId,
            "Template category does not match membership."
        );
        if (d.PositionId != null)
            Check(
                await Current(Db)
                    .AnyAsync(p =>
                        p.PersonId == m.PersonId
                        && p.PositionId == d.PositionId
                        && p.TenantId == m.TenantId
                        && p.OrgId == m.ClubId
                    ),
                "Required current leadership position is missing."
            );
    }

    public static void Eligible(Membership m) =>
        Check(
            m.Status == "Active" && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow),
            "Only an active, unexpired membership can receive an official card.",
            409
        );

    public void Audit(
        Guid tenant,
        Guid org,
        Guid user,
        string action,
        Guid resource,
        string detail = ""
    )
    {
        Db.Audits.Add(
            new()
            {
                TenantId = tenant,
                OrgId = org,
                ActorId = user,
                Action = "idcard." + action,
                Resource = resource.ToString(),
                Changes = detail,
            }
        );
    }

    public async Task<IdCardPublished> Freeze(IdCardLayout d)
    {
        IdCardDesign.Validate(d);
        var assets = new Dictionary<string, string>();
        IdCardTemplateVersion? inherited = null;
        if (d.ParentVersionId != null)
        {
            var workspace = await Workspace("cards.design");
            inherited = await Version(d.ParentVersionId.Value, workspace);
            await Template(inherited.TemplateId, "members.cards");
        }
        var inheritedAssets =
            inherited == null
                ? new Dictionary<string, string>()
                : Unpack<IdCardPublished>(inherited.Layout).Assets;
        foreach (
            var id in d.Front!.Elements.Concat(d.Back!.Elements)
                .Where(e => e.Asset != "")
                .Select(e => e.Asset)
                .Distinct()
        )
        {
            if (inheritedAssets.TryGetValue(id, out var frozenKey))
            {
                assets[id] = frozenKey;
                continue;
            }
            var doc = await Db.GovernanceDocuments.SingleOrDefaultAsync(x =>
                x.Id == Guid.Parse(id)
            );
            Check(doc != null, "Asset unavailable.", 404);
            await operations.Document(doc);
            Check(
                doc.Visibility == "Organization"
                    && doc.CommitteeId == null
                    && doc.OperationId == null,
                "Published design assets must be organizational branding images, not private or meeting/committee documents.",
                403
            );
            Check(
                doc.ContentType is "image/png" or "image/jpeg",
                "Only validated raster image assets are allowed."
            );
            var bytes =
                doc.ObjectKey != ""
                    ? await File.ReadAllBytesAsync(storage.PathFor(doc.ObjectKey))
                    : doc.Content;
            using var decoded = IdCardRenderer.Decode(bytes);
            Check(
                IntelligenceKnowledge.HashBytes(bytes) == doc.Sha256,
                "Asset integrity check failed.",
                409
            );
            var key = IntelligenceKnowledge.HashBytes(bytes)[..32].ToLowerInvariant();
            Directory.CreateDirectory(storage.Root);
            if (!File.Exists(storage.PathFor(key)))
                await File.WriteAllBytesAsync(storage.PathFor(key), bytes);
            assets[id] = key;
        }
        return new(d, assets);
    }

    public async Task<IdCardSnapshot> Snapshot(Membership m, DigitalCard card)
    {
        var tenant = await Db.Tenants.SingleAsync(t => t.Id == m.TenantId);
        var brand = await Db.TenantBrandings.SingleOrDefaultAsync(b => b.TenantId == m.TenantId);
        var pos = await Current(Db)
            .Where(p => p.PersonId == m.PersonId && p.TenantId == m.TenantId && p.OrgId == m.ClubId)
            .Join(Db.OfficerPositions, p => p.PositionId, p => p.Id, (p, n) => n.Title)
            .OrderBy(n => n)
            .FirstOrDefaultAsync();
        var chain = await Db
            .Database.SqlQuery<Guid>(
                $"""WITH RECURSIVE parent AS(SELECT "Id","ParentId" FROM "Orgs" WHERE "Id"={m.ClubId} AND "TenantId"={m.TenantId} UNION ALL SELECT o."Id",o."ParentId" FROM "Orgs" o JOIN parent p ON o."Id"=p."ParentId" WHERE o."TenantId"={m.TenantId}) SELECT "Id" AS "Value" FROM parent"""
            )
            .ToArrayAsync();
        var orgs = await (
            from o in Db.Orgs
            join l in Db.Levels on o.LevelId equals l.Id
            where chain.Contains(o.Id)
            select new { o.Name, Level = l.Name }
        ).ToListAsync();
        var values = new Dictionary<string, string>
        {
            ["member.full_name"] = m.Person.DisplayName,
            ["member.member_id"] = m.Number,
            ["member.membership_category"] = m.Category.Name,
            ["member.joining_date"] = m.JoinedAt.ToString("yyyy-MM-dd"),
            ["member.valid_until"] = m.EndsAt?.ToString("yyyy-MM-dd") ?? "",
            ["member.current_designation"] = pos ?? "Member",
            ["member.club_name"] = m.Club.Name,
            ["member.club_code"] = m.Club.Code,
            ["member.district_name"] =
                orgs.FirstOrDefault(o =>
                    o.Level.Contains(
                        brand?.DistrictTerm ?? "District",
                        StringComparison.OrdinalIgnoreCase
                    )
                )?.Name
                ?? "",
            ["member.national_organization"] =
                orgs.FirstOrDefault(o =>
                    o.Level.Contains("National", StringComparison.OrdinalIgnoreCase)
                )?.Name
                ?? "",
            ["association.name"] = brand?.DisplayName is { Length: > 0 }
                ? brand.DisplayName
                : tenant.Name,
            ["card.issue_date"] = card.IssuedAt.ToString("yyyy-MM-dd"),
            ["card.expiry_date"] = card.ExpiryAt?.ToString("yyyy-MM-dd") ?? "",
            ["card.serial_number"] = card.Serial,
            ["card.qr_code"] =
                (config["PublicOrigin"] ?? "http://127.0.0.1:5175").TrimEnd('/')
                + "/verify/id/"
                + members.Unprotect(card.ProtectedToken),
        };
        var images = new Dictionary<string, string>();
        if (m.Person.Photo != null)
        {
            using var image = IdCardRenderer.Decode(m.Person.Photo);
            var size = Math.Min(1f, 800f / Math.Max(image.Width, image.Height));
            using var resized = image.Resize(
                new SKSizeI((int)(image.Width * size), (int)(image.Height * size)),
                SkiaSharp.SKSamplingOptions.Default
            );
            using var data = resized.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            var photoBytes = data.ToArray();
            var key = IntelligenceKnowledge.HashBytes(photoBytes)[..32].ToLowerInvariant();
            Directory.CreateDirectory(storage.Root);
            if (!File.Exists(storage.PathFor(key)))
                await File.WriteAllBytesAsync(storage.PathFor(key), photoBytes);
            images["member.photo"] = key;
        }
        return new(values, images);
    }

    public async Task<Membership> Load(Guid id, Assignment w)
    {
        return await Db
                .Memberships.Include(m => m.Person)
                .Include(m => m.Club)
                .Include(m => m.Category)
                .SingleOrDefaultAsync(m =>
                    m.Id == id && m.TenantId == w.TenantId && a.VisibleIds(w).Contains(m.ClubId)
                )
            ?? throw new ApiError(404, "Membership outside authorized card scope.");
    }

    public async Task<DigitalCard> Draft(IdCardIssueInput input, Assignment w)
    {
        await members.Lock(input.MembershipId);
        var m = await Load(input.MembershipId, w);
        await governance.Scope(w, m.ClubId);
        Eligible(m);
        Check(
            !string.IsNullOrWhiteSpace(m.Number)
                && !string.IsNullOrWhiteSpace(m.Person.DisplayName),
            "Official cards require a member name and membership identifier.",
            409
        );
        if (input.TemplateVersionId == Guid.Empty)
            input = input with
            {
                TemplateVersionId =
                    (await Policy(w.TenantId)).DefaultTemplateVersionId
                    ?? throw new ApiError(
                        400,
                        "Choose a published template or configure the association default."
                    ),
            };
        var v = await Version(input.TemplateVersionId, w);
        await ScopeTemplate(v, m);
        Check(
            await Db.CertificateTemplates.AnyAsync(t => t.Id == v.TemplateId && t.Active),
            "Template archived.",
            409
        );
        var policy = await Policy(w.TenantId);
        var expiry = input.ExpiryAt ?? DateTimeOffset.UtcNow.AddMonths(policy.ValidityMonths);
        if (m.EndsAt != null && expiry > m.EndsAt)
            expiry = m.EndsAt.Value;
        Check(
            expiry > DateTimeOffset.UtcNow && expiry <= DateTimeOffset.UtcNow.AddYears(5),
            "Invalid card expiry."
        );
        if (input.ReplacesId != null)
            Check(
                input.Reason.Length is >= 5 and <= 500,
                "Explain the replacement in 5–500 characters."
            );
        if (input.ReplacesId != null)
            Check(
                await Db.DigitalCards.AnyAsync(c =>
                    c.Id == input.ReplacesId && c.MembershipId == m.Id
                ),
                "Replacement must reference the same membership."
            );
        var token = Token();
        var card = new DigitalCard
        {
            TenantId = w.TenantId,
            MembershipId = m.Id,
            TemplateVersionId = v.Id,
            Serial = "IC-" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
            State = "Draft",
            CreatorId = w.UserId,
            ExpiryAt = expiry,
            ReplacesId = input.ReplacesId,
            TokenHash = Hash(token),
            ProtectedToken = members.Protect(token),
        };
        var snapshot = await Snapshot(m, card);
        var design = Unpack<IdCardPublished>(v.Layout).Design;
        foreach (
            var e in design
                .Front!.Elements.Concat(design.DoubleSided ? design.Back!.Elements : [])
                .Where(e => e.Required)
        )
        {
            Check(
                e.Kind == "Image"
                    ? snapshot.Images.ContainsKey(e.Binding) || e.Asset != ""
                    : !string.IsNullOrWhiteSpace(IdCardDesign.Resolve(e, snapshot.Values)),
                "Required field missing: " + (e.Binding == "" ? e.Id : e.Binding),
                409
            );
        }
        card.Snapshot = Pack(snapshot);
        Db.DigitalCards.Add(card);
        Audit(w.TenantId, m.ClubId, w.UserId, "draft", card.Id, input.Reason);
        await Db.SaveChangesAsync();
        return card;
    }

    public async Task<(DigitalCard Card, Membership Member)> Card(
        Guid id,
        bool own = true,
        string permission = "cards.issue"
    )
    {
        var user = await a.User();
        var card = await Db.DigitalCards.SingleOrDefaultAsync(c => c.Id == id);
        Check(card != null, "Card unavailable.", 404);
        if (a.BoundTenant != null)
            Check(a.BoundTenant == card.TenantId, "Card unavailable.", 404);
        var m = await members.Find(card.MembershipId, permission, own);
        return (card, m);
    }

    public static string Status(DigitalCard c) =>
        c.State == "Replaced" ? "Replaced"
        : c.RevokedAt != null ? "Revoked"
        : c.ExpiryAt < DateTimeOffset.UtcNow ? "Expired"
        : c.State;

    public async Task Generate(DigitalCard card, Assignment w)
    {
        Check(
            card.State == "Approved" && card.RevokedAt == null,
            "Only approved cards may be generated.",
            409
        );
        await members.Lock(card.MembershipId);
        var m = await Load(card.MembershipId, w);
        await governance.Scope(w, m.ClubId);
        Eligible(m);
        Check(card.ExpiryAt > DateTimeOffset.UtcNow, "Card expired before generation.", 409);
        var v = await Version(card.TemplateVersionId!.Value, w);
        await ScopeTemplate(v, m);
        var old = await Db
            .DigitalCards.Where(c =>
                c.MembershipId == m.Id
                && c.Id != card.Id
                && c.RevokedAt == null
                && (c.State == "Legacy" || c.State == "Issued" || c.State == "Generated")
            )
            .ToListAsync();
        Check(
            old.Count == 0 || card.ReplacesId != null && old.All(c => c.Id == card.ReplacesId),
            "Existing official card must be explicitly replaced.",
            409
        );
        Check(
            await Db.CertificateTemplates.AnyAsync(t => t.Id == v.TemplateId && t.Active),
            "Template archived before generation.",
            409
        );
        var t = Unpack<IdCardPublished>(v.Layout);
        IdCardDesign.Publishable(t.Design);
        var snapshot = Unpack<IdCardSnapshot>(card.Snapshot);
        card.IssuedAt = DateTimeOffset.UtcNow;
        snapshot.Values["card.issue_date"] = card.IssuedAt.ToString("yyyy-MM-dd");
        card.Snapshot = Pack(snapshot);
        Directory.CreateDirectory(storage.Root);
        var pdf = Guid.NewGuid().ToString("N");
        var front = Guid.NewGuid().ToString("N");
        var back = t.Design.DoubleSided ? Guid.NewGuid().ToString("N") : "";
        using (var stream = File.Create(storage.PathFor(pdf)))
            renderer.Pdf(stream, t, snapshot);
        await File.WriteAllBytesAsync(storage.PathFor(front), renderer.Png(t, snapshot));
        if (back != "")
            await File.WriteAllBytesAsync(storage.PathFor(back), renderer.Png(t, snapshot, true));
        card.Files = Pack(new IdCardFiles(pdf, front, back));
        card.State = "Generated";
        card.Version++;
        Audit(w.TenantId, m.ClubId, w.UserId, "generated", card.Id);
        await Db.SaveChangesAsync();
    }

    public async Task Issue(DigitalCard card, Assignment w)
    {
        Check(
            card.State == "Generated"
                && card.RevokedAt == null
                && card.ExpiryAt > DateTimeOffset.UtcNow,
            "Generate a current approved card before issuance.",
            409
        );
        await members.Lock(card.MembershipId);
        var m = await Load(card.MembershipId, w);
        Eligible(m);
        Check(
            card.IssuedAt.UtcDateTime.Date == DateTime.UtcNow.Date,
            "The generated issue date is no longer current; prepare a freshly reviewed card instead of releasing outdated artwork.",
            409
        );
        var previous = await Db
            .DigitalCards.Where(c =>
                c.MembershipId == m.Id
                && c.Id != card.Id
                && c.RevokedAt == null
                && (c.State == "Legacy" || c.State == "Issued")
            )
            .ToListAsync();
        Check(
            previous.Count == 0
                || card.ReplacesId != null && previous.All(c => c.Id == card.ReplacesId),
            "Another current card must be explicitly replaced.",
            409
        );
        foreach (var oldCard in previous)
        {
            oldCard.RevokedAt = DateTimeOffset.UtcNow;
            oldCard.State = "Replaced";
            oldCard.Version++;
            Audit(
                w.TenantId,
                m.ClubId,
                w.UserId,
                "replaced",
                oldCard.Id,
                "Replaced by " + card.Serial
            );
        }
        await Db.SaveChangesAsync();
        card.State = "Issued";
        card.IssuedAt = DateTimeOffset.UtcNow;
        card.Version++;
        Audit(w.TenantId, m.ClubId, w.UserId, "issued", card.Id);
        if (m.Person.UserId != null)
            await notifications.User(
                m.Person.UserId.Value,
                "membership.activated",
                "Your new membership ID card is available in My Digital ID.",
                "/app/my-id",
                w.TenantId,
                m.ClubId
            );
        await Db.SaveChangesAsync();
    }

    public static async Task Approve(Access a, GovernanceRequest r, Assignment w)
    {
        var reference = Unpack<IdCardReview>(r.Payload);
        var needed = r.Kind == "IdCardBatch" ? "cards.bulk" : "cards.issue";
        Check((await a.Grants(w)).Contains(needed), "Card review permission required.", 403);
        Check(await a.VisibleIds(w).ContainsAsync(r.OrgId), "Card scope unavailable.", 404);
        if (r.Kind == "IdCardBatch")
        {
            var job = await a.Db.IdCardJobs.SingleAsync(j =>
                j.Id == reference.Id && j.TenantId == w.TenantId
            );
            Check(
                job.RequestId == r.Id
                    && job.Status == "Pending Approval"
                    && Hash(
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
                    ) == reference.Hash,
                "Generation request changed.",
                409
            );
            job.Status = "Queued";
            job.Version++;
        }
        else
        {
            var card = await a.Db.DigitalCards.SingleAsync(c =>
                c.Id == reference.Id && c.TenantId == w.TenantId
            );
            Check(
                card.RequestId == r.Id
                    && card.State == "Pending Approval"
                    && Hash(
                        card.Snapshot
                            + ":"
                            + card.TemplateVersionId
                            + ":"
                            + card.ExpiryAt?.ToString("O")
                    ) == reference.Hash,
                "Card snapshot changed.",
                409
            );
            card.State = "Approved";
            card.Version++;
        }
    }
}
