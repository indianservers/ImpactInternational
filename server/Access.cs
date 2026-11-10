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

    public async Task<Assignment> Workspace(string permission)
    {
        var s = await Session();
        var w =
            await db.Assignments.SingleOrDefaultAsync(x =>
                x.Id == s.WorkspaceId && x.UserId == s.UserId
            ) ?? throw new ApiError(403, "Choose an authorized workspace.");
        if (!(await Grants(w)).Contains(permission))
            throw new ApiError(403, "Action not permitted in this workspace.");
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

    public Task<string[]> Grants(Assignment w) =>
        db
            .RolePermissions.Where(x => x.RoleId == w.RoleId)
            .Select(x => x.PermissionId)
            .ToArrayAsync();

    public IQueryable<Org> Tree(Guid tenant, Guid root) =>
        db.Orgs.FromSqlRaw(
            "WITH RECURSIVE tree AS (SELECT * FROM \"Orgs\" WHERE \"TenantId\"=@scope_tenant AND \"Id\"=@scope_root UNION ALL SELECT o.* FROM \"Orgs\" o JOIN tree t ON o.\"ParentId\"=t.\"Id\" AND o.\"TenantId\"=t.\"TenantId\") SELECT * FROM tree",
            new Npgsql.NpgsqlParameter("scope_tenant", tenant),
            new Npgsql.NpgsqlParameter("scope_root", root)
        );

    public IQueryable<Org> Visible(Assignment w) => Tree(w.TenantId, w.OrgId);

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
