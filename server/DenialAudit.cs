using Microsoft.EntityFrameworkCore;

namespace Clubs;

public static class DenialAudit
{
    public static async Task Record(IServiceProvider services, HttpContext context)
    {
        // Use an independent context: rejected requests must never flush tracked business changes.
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var hash = Access.Hash(context.Request.Cookies["clubs_session"] ?? "");
            var session = await db
                .Sessions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == hash && x.ExpiresAt > DateTimeOffset.UtcNow);
            if (session == null)
                return;
            var w = await db
                .Assignments.AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.Id == session.WorkspaceId && x.UserId == session.UserId
                );
            db.Audits.Add(
                new()
                {
                    ActorId = session.UserId,
                    TenantId = w?.TenantId,
                    OrgId = w?.OrgId,
                    Action = "access.denied",
                    Outcome = "Denied",
                    Resource = context.Request.Path,
                }
            );
            await db.SaveChangesAsync();
        }
        catch
        {
            services
                .GetRequiredService<ILogger<Access>>()
                .LogError("Failed to persist authorization-denial audit event");
        }
    }
}
