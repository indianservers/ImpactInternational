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
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Link, ""), token);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError("Expiry cleanup failed ({Type})", e.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromHours(1), token);
        }
    }
}
