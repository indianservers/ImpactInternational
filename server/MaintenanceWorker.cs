using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class MaintenanceWorker(IServiceScopeFactory scopes, ILogger<MaintenanceWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Db>();
                await db
                    .Sessions.Where(x => x.ExpiresAt < DateTimeOffset.UtcNow)
                    .ExecuteDeleteAsync(token);
                await db
                    .Mails.Where(x => x.ExpiresAt < DateTimeOffset.UtcNow || x.Used)
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.Link, "")
                                .SetProperty(x => x.Body, "")
                                .SetProperty(x => x.Recipient, ""),
                        token
                    );
                // Preserve provenance IDs and counters, while removing retained source PII.
                var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
                var oldJobs = db.MemberImportJobs.Where(x =>
                    x.CreatedAt < cutoff
                    && (x.Status == "Completed" || x.Status == "Cancelled" || x.Status == "Failed")
                );
                await db
                    .MemberImportRows.Where(x => oldJobs.Select(j => j.Id).Contains(x.JobId))
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.Data, "{}")
                                .SetProperty(x => x.Candidates, "[]")
                                .SetProperty(x => x.Error, ""),
                        token
                    );
                await oldJobs.ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Payload, Array.Empty<byte>())
                            .SetProperty(x => x.Error, ""),
                    token
                );
                await db
                    .MembershipDocuments.Where(x =>
                        x.RetainUntil != null
                        && x.RetainUntil < DateTimeOffset.UtcNow
                        && x.Status != "Expired"
                    )
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.Content, Array.Empty<byte>())
                                .SetProperty(x => x.Status, "Expired"),
                        token
                    );
                await db
                    .DigitalCardVerifications.Where(x => x.At < DateTimeOffset.UtcNow.AddDays(-90))
                    .ExecuteDeleteAsync(token);
                await db
                    .GovernanceDocuments.Where(x =>
                        x.RetainUntil != null
                        && x.RetainUntil < DateTimeOffset.UtcNow
                        && x.Status != "Expired"
                    )
                    .ExecuteUpdateAsync(
                        u =>
                            u.SetProperty(x => x.Content, Array.Empty<byte>())
                                .SetProperty(x => x.Status, "Expired"),
                        token
                    );
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError("Expiry cleanup failed ({Type})", e.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromHours(1), token);
        }
    }
}
