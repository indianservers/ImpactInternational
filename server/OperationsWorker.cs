using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class OperationsWorker(IServiceScopeFactory scopes, ILogger<OperationsWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Db>();
                var notice = scope.ServiceProvider.GetRequiredService<NotificationService>();
                var storage = scope.ServiceProvider.GetRequiredService<CommunicationStorage>();
                await using var tx = await db.Database.BeginTransactionAsync(stop);
                var acquired = await db
                    .Database.SqlQueryRaw<bool>(
                        "SELECT pg_try_advisory_xact_lock(6492016) AS \"Value\""
                    )
                    .SingleAsync(stop);
                if (acquired)
                {
                    var now = DateTimeOffset.UtcNow;
                    var tasks = await db
                        .OperationTasks.Where(t =>
                            t.Status != "Completed"
                            && t.Status != "Cancelled"
                            && t.PersonId != null
                            && t.DueAt < now.AddDays(1)
                            && (t.RemindedAt == null || t.RemindedAt < now.AddDays(-1))
                        )
                        .OrderBy(t => t.DueAt)
                        .Take(100)
                        .ToListAsync(stop);
                    foreach (var t in tasks)
                    {
                        await notice.Operation(
                            t.PersonId,
                            "A task assigned to you is due. Open your private operations workspace.",
                            t,
                            t.CreatorId,
                            scope.ServiceProvider.GetRequiredService<Access>()
                        );
                        t.RemindedAt = now;
                    }
                    var expired = await db
                        .GovernanceDocuments.Where(d =>
                            d.ObjectKey != "" && d.RetainUntil < now && d.Status != "Expired"
                        )
                        .OrderBy(d => d.RetainUntil)
                        .Take(50)
                        .ToListAsync(stop);
                    foreach (var d in expired)
                    {
                        var keys = await db
                            .DocumentVersions.Where(v => v.DocumentId == d.Id)
                            .Select(v => v.ObjectKey)
                            .ToListAsync(stop);
                        foreach (var key in keys)
                            File.Delete(storage.PathFor(key));
                        d.Status = "Expired";
                        db.Audits.Add(
                            new()
                            {
                                TenantId = d.TenantId,
                                OrgId = d.OrgId,
                                Action = "operations.document.retention",
                                Resource = d.Id.ToString(),
                                Changes =
                                    "Expired private object versions removed; metadata and hashes retained.",
                            }
                        );
                    }
                    await db.SaveChangesAsync(stop);
                    await tx.CommitAsync(stop);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (Exception e)
            {
                log.LogError(e, "Operations maintenance cycle failed");
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), stop);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
