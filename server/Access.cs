using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class ApiError(int status, string message) : Exception(message)
{
    public int Status => status;
}

public class Access(Db db, IHttpContextAccessor ctx)
{
    public Db Db => db;
    public Guid? BoundTenant=>ctx.HttpContext?.Items["tenant-domain"] as Guid?;
    public static readonly string[] All =
    [
        "organization.view",
        "organization.create",
        "organization.update",
        "organization.approve",
        "organization.archive",
        "users.view",
        "users.invite",
        "users.manage",
        "roles.view",
        "roles.assign",
        "roles.manage",
        "settings.view",
        "settings.manage",
        "reports.view",
        "audit.view",
        "members.view",
        "members.manage",
        "members.personal.view",
        "members.contacts.view",
        "members.export",
        "members.import",
        "members.invite",
        "members.approve",
        "members.transfer.approve",
        "members.link",
        "members.documents",
        "members.cards",
        "cards.design", "cards.publish", "cards.issue", "cards.bulk", "cards.settings",
        "members.workflow.manage",
        "directory.view",
        "clubs.view",
        "clubs.manage",
        "leadership.view",
        "leadership.manage",
        "governance.configure",
        "governance.approve",
        "governance.restructure",
        "governance.documents",
        "committees.view",
        "committees.manage",
        "governance.reports",
        "events.view",
        "events.manage",
        "events.checkin",
        "events.reports",
        "events.configure",
        "communications.view",
        "communications.send",
        "communications.manage",
        "communications.inbox",
        "communications.reports",
        "communications.configure",
        "intelligence.view", "intelligence.assistant", "intelligence.knowledge", "intelligence.automations",
        "finance.view", "finance.manage", "finance.configure", "finance.reconcile", "finance.post", "finance.approve", "finance.self",
        "operations.view", "operations.manage", "operations.verify", "operations.documents", "operations.certificates",
    ];

    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    public static readonly string Dummy = new PasswordHasher<User>().HashPassword(new(), Token());

    public async Task<Session> Session()
    {
        var id = Hash(ctx.HttpContext?.Request.Cookies["clubs_session"] ?? "");
        return await db.Sessions.SingleOrDefaultAsync(x =>
                x.Id == id && x.ExpiresAt > DateTimeOffset.UtcNow
            ) ?? throw new ApiError(401, "Sign in to continue.");
    }

    public async Task<User> User()
    {
        var s = await Session();
        return await db.Users.SingleAsync(x => x.Id == s.UserId);
    }

    public Task<Assignment> Workspace(string permission) => WorkspaceAny(permission);

    public async Task<bool> Effective(Assignment w)
    {
        var instant = DateTimeOffset.UtcNow;
        return (
                w.MembershipId == null
                || await db.Memberships.AnyAsync(m =>
                    m.Id == w.MembershipId
                    && m.Status == "Active"
                    && (m.EndsAt == null || m.EndsAt > instant)
                )
            )
            && (
                w.AppointmentId == null
                || await GovernanceService.Current(db).AnyAsync(p => p.Id == w.AppointmentId)
            );
    }

    public async Task<Assignment> WorkspaceAny(params string[] permissions)
    {
        var session = await Session();
        var w =
            await db.Assignments.SingleOrDefaultAsync(x =>
                x.Id == session.WorkspaceId && x.UserId == session.UserId
            ) ?? throw new ApiError(403, "Choose an authorized workspace.");
        Check(
            w.MembershipId == null
                || await db.Memberships.AnyAsync(m =>
                    m.Id == w.MembershipId
                    && m.Status == "Active"
                    && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                ),
            "This membership no longer grants an active workspace.",
            403
        );
        Check(BoundTenant==null||BoundTenant==w.TenantId,"Switch to this domain's authorized association workspace.",403);
        var grants = await Grants(w);
        Check(
            w.AppointmentId == null
                || await GovernanceService.Current(db).AnyAsync(x => x.Id == w.AppointmentId),
            "This office no longer grants effective authority.",
            403
        );
        Check(permissions.Any(grants.Contains), "Action not permitted in this workspace.", 403);
        return w;
    }

    public async Task<Assignment> TenantWorkspace(string permission)
    {
        var w = await Workspace(permission);
        if (await db.Orgs.AnyAsync(x => x.Id == w.OrgId && x.ParentId != null))
            throw new ApiError(
                403,
                "Association-wide configuration requires a headquarters workspace."
            );
        return w;
    }

    public async Task<string[]> Grants(Assignment w){var grants=await db.RolePermissions.Where(x=>x.RoleId==w.RoleId).Select(x=>x.PermissionId).ToArrayAsync();var disabled=await db.TenantBrandings.Where(b=>b.TenantId==w.TenantId).Select(b=>b.DisabledFeatures).SingleOrDefaultAsync();if(disabled==null||disabled=="[]")return grants;var features=GovernanceService.Unpack<string[]>(disabled);var prefixes=features.Where(TenantExperience.PermissionPrefixes.ContainsKey).SelectMany(f=>TenantExperience.PermissionPrefixes[f]).ToArray();return grants.Where(p=>!prefixes.Any(prefix=>p.StartsWith(prefix,StringComparison.Ordinal))).ToArray();}

#pragma warning disable EF1002 // Only canonical typed GUID literals enter this SQL; see below.
    public IQueryable<Org> Tree(Guid tenant, Guid root) =>
        // Typed GUID literals avoid EF raw-query parameter renaming collisions when
        // the hierarchy is composed repeatedly with parameterized search predicates.
        // Both values are Guid, never caller-provided SQL or text.
        db.Orgs.FromSqlRaw(
            $"""
            WITH RECURSIVE tree AS (
              SELECT * FROM "Orgs" WHERE "TenantId"='{tenant:D}'::uuid AND "Id"='{root:D}'::uuid
              UNION ALL SELECT o.* FROM "Orgs" o JOIN tree t
                ON o."ParentId"=t."Id" AND o."TenantId"=t."TenantId"
            ) SELECT * FROM tree
            """
        );
#pragma warning restore EF1002

    public IQueryable<Org> Visible(Assignment w) => Tree(w.TenantId, w.OrgId);

    // Keep the narrow recursive projection parameter-free, just like Tree above.
    // EF/Npgsql can reuse raw-query parameter names for composed LINQ search
    // values. Only canonical, strongly typed GUID literals enter this SQL.
#pragma warning disable EF1002
    public IQueryable<Guid> VisibleIds(Assignment w) =>
        db.Database.SqlQueryRaw<Guid>(
            $"""
            WITH RECURSIVE tree AS (
              SELECT "Id","TenantId" FROM "Orgs" WHERE "TenantId"='{w.TenantId:D}'::uuid AND "Id"='{w.OrgId:D}'::uuid
              UNION ALL SELECT o."Id",o."TenantId" FROM "Orgs" o JOIN tree t
                ON o."ParentId"=t."Id" AND o."TenantId"=t."TenantId"
            ) SELECT "Id" AS "Value" FROM tree
            """
        );
#pragma warning restore EF1002

    public async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> Mutation(
        Assignment w
    )
    {
        var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({w.TenantId.ToString()}))"
            );
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('clubs.actor', {w.UserId.ToString()}, true)"
            );
            return tx;
        }
        catch
        {
            await tx.DisposeAsync();
            throw;
        }
    }

    public async Task Log(Assignment w, string action, string resource, string changes = "")
    {
        db.Audits.Add(
            new()
            {
                TenantId = w.TenantId,
                OrgId = w.OrgId,
                ActorId = w.UserId,
                Action = action,
                Resource = resource,
                Changes = changes,
            }
        );
        await db.SaveChangesAsync();
    }

    public static void Check(
        [System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool valid,
        string message,
        int status = 400
    )
    {
        if (!valid)
            throw new ApiError(status, message);
    }

    public static void Queue(Db db, User u, string kind)
    {
        var token = Token();
        db.Mails.Add(
            new()
            {
                UserId = u.Id,
                Kind = kind,
                TokenHash = Hash(token),
                Link = $"/complete?token={token}&kind={kind}",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            }
        );
    }
}
