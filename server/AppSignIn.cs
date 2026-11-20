using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class AppSignIn
{
    public static async Task<IResult> Complete(User u, Db db, Access access, HttpContext c, IHostEnvironment env, string action = "auth.login")
    {
        u.FailedAttempts = 0;
        u.LockedUntil = null;
        var raw = Token();
        Guid? defaultWorkspace = null;
        foreach (var assignment in await db.Assignments.Where(x => x.UserId == u.Id)
            .OrderBy(x => db.Roles.Any(r => r.Id == x.RoleId && LeaderCommunications.SendingRoles.Contains(r.Name)) ? 0 : 1)
            .ThenBy(x => x.Id).ToListAsync())
        {
            if (access.BoundTenant != null && assignment.TenantId != access.BoundTenant) continue;
            if (!await access.Effective(assignment)) continue;
            defaultWorkspace = assignment.Id;
            break;
        }
        db.Sessions.Add(
            new()
            {
                Id = Hash(raw),
                UserId = u.Id,
                WorkspaceId = defaultWorkspace,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
            }
        );
        db.AuthenticationAudits.Add(new() { ActorId = u.Id, Action = action });
        await db.SaveChangesAsync();
        c.Response.Cookies.Append(
            "clubs_session",
            raw,
            new()
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Path = "/",
                MaxAge = TimeSpan.FromHours(8),
            }
        );
        return Results.Ok(new { u.Name,
            pendingMembership = defaultWorkspace == null && await db.MembershipApplications.AnyAsync(a => a.ApplicantUserId == u.Id
                && new[] { "Draft", "Submitted", "Under Review", "Changes Requested" }.Contains(a.Status)) });
    }
}
