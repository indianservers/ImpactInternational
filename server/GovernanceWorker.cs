using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class GovernanceWorker(IServiceScopeFactory scopes, ILogger<GovernanceWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var s = scope.ServiceProvider.GetRequiredService<GovernanceService>();
                var db = s.Db;
                var ids = await db
                    .OfficerAppointments.Where(x =>
                        new[] { "Approved", "Scheduled", "Active" }.Contains(x.Status)
                        && (
                            x.EndsAt <= DateTimeOffset.UtcNow
                            || x.Status != "Active" && x.EffectiveAt <= DateTimeOffset.UtcNow
                            || x.Status == "Active"
                                && !GovernanceService.Current(db).Any(p => p.Id == x.Id)
                        )
                    )
                    .OrderBy(x => x.EndsAt)
                    .Select(x => x.Id)
                    .Take(100)
                    .ToArrayAsync(token);
                foreach (var id in ids)
                {
                    await using var tx = await db.Database.BeginTransactionAsync(token);
                    var tenant = await db
                        .OfficerAppointments.Where(x => x.Id == id)
                        .Select(x => x.TenantId)
                        .SingleAsync(token);
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock(hashtext({tenant.ToString()}))",
                        token
                    );
                    var p = await db.OfficerAppointments.SingleAsync(x => x.Id == id, token);
                    if (new[] { "Approved", "Scheduled", "Active" }.Contains(p.Status))
                    {
                        if (p.EndsAt <= DateTimeOffset.UtcNow)
                            await s.End(p, "Completed", "Term ended", null);
                        else if (p.Status == "Active")
                            await s.End(
                                p,
                                "Revoked",
                                "Membership eligibility or organization status ceased",
                                null
                            );
                        else
                        {
                            try
                            {
                                await s.Activate(p, null);
                            }
                            catch (ApiError e)
                            {
                                await s.End(
                                    p,
                                    "Cancelled",
                                    "Scheduled activation blocked: " + e.Message,
                                    null
                                );
                            }
                        }
                        db.Audits.Add(
                            new()
                            {
                                TenantId = p.TenantId,
                                OrgId = p.OrgId,
                                Action = "leadership.scheduled-transition",
                                Resource = p.Id.ToString(),
                                Changes = p.Status,
                            }
                        );
                        await db.SaveChangesAsync(token);
                    }
                    await tx.CommitAsync(token);
                    db.ChangeTracker.Clear();
                }
                var handovers = await db
                    .LeadershipHandovers.Where(h =>
                        h.Status == "Approved"
                        && GovernanceService.Current(db).Any(p => p.Id == h.IncomingAppointmentId)
                    )
                    .Take(50)
                    .ToListAsync(token);
                foreach (var h in handovers)
                {
                    await using var tx = await db.Database.BeginTransactionAsync(token);
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock(hashtext({h.TenantId.ToString()}))",
                        token
                    );
                    await db.Entry(h).ReloadAsync(token);
                    if (h.Status == "Approved")
                    {
                        h.Status = "Archived";
                        h.ArchivedAt = DateTimeOffset.UtcNow;
                        h.Version++;
                        db.Audits.Add(
                            new()
                            {
                                TenantId = h.TenantId,
                                OrgId = h.OrgId,
                                Action = "handover.archived",
                                Resource = h.Id.ToString(),
                            }
                        );
                        await db.SaveChangesAsync(token);
                    }
                    await tx.CommitAsync(token);
                }
                await db
                    .CommitteeMemberships.Where(x =>
                        x.Status == "Active" && x.EndsAt <= DateTimeOffset.UtcNow
                    )
                    .ExecuteUpdateAsync(
                        u =>
                            u.SetProperty(x => x.Status, "Completed")
                                .SetProperty(x => x.Version, x => x.Version + 1),
                        token
                    );
                await db
                    .Committees.Where(x =>
                        x.Status == "Active" && x.EndsAt <= DateTimeOffset.UtcNow
                    )
                    .ExecuteUpdateAsync(
                        u =>
                            u.SetProperty(x => x.Status, "Completed")
                                .SetProperty(x => x.Version, x => x.Version + 1),
                        token
                    );
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError("Governance worker cycle failed ({Type})", e.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(3), token);
        }
    }
}
