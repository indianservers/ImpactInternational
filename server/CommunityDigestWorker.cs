using Microsoft.EntityFrameworkCore;
namespace Clubs;
// Durable weekly opt-in scheduling. Messages link to a live authorized digest rather than freezing private content in email.
public class CommunityDigestWorker(IServiceScopeFactory scopes,ILogger<CommunityDigestWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested){try{await QueueOne(scopes,ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}catch(Exception e){log.LogWarning("Community digest deferred ({Type})",e.GetType().Name);}await Task.Delay(TimeSpan.FromSeconds(30),ct);}
    }
    public static async Task<bool> QueueOne(IServiceScopeFactory scopes,CancellationToken ct=default)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<Db>();await using var tx=await db.Database.BeginTransactionAsync(ct);
        var pref=await db.CommunityDigestPreferences.FromSqlRaw("SELECT * FROM \"CommunityDigestPreferences\" WHERE \"Enabled\" AND \"NextDigestAt\"<=now() ORDER BY \"NextDigestAt\" FOR UPDATE SKIP LOCKED LIMIT 1").FirstOrDefaultAsync(ct);if(pref==null)return false;
        var a=scope.ServiceProvider.GetRequiredService<Access>();var w=await db.Assignments.SingleOrDefaultAsync(w=>w.Id==pref.AssignmentId&&w.TenantId==pref.TenantId&&w.UserId==pref.UserId,ct);
        pref.NextDigestAt=DateTimeOffset.UtcNow.AddDays(7);
        if(w==null||!await a.Effective(w)||!(await a.Grants(w)).Contains("directory.view")||!await db.Orgs.AnyAsync(o=>o.Id==w.OrgId&&o.Status=="Active",ct)){pref.Enabled=false;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;}
        var s=await CommunityEndpoints.ForWorkspace(a,w);var since=DateTimeOffset.UtcNow.AddDays(-7);var q=CommunityGrowthService.Followed(db,w.TenantId,w.UserId,CommunityEndpoints.Posts(db,s));
        if(await q.AnyAsync(p=>p.CreatedAt>=since,ct)){
            var notify=scope.ServiceProvider.GetRequiredService<NotificationService>();await notify.User(w.UserId,"community.digest","Your opted-in weekly Impact digest is ready. Open your personalized digest to view the activities currently available to your membership and role.","/app/community/following",w.TenantId,w.OrgId);
            var mail=db.Mails.Local.Last(m=>m.UserId==w.UserId&&m.Kind=="community.digest");mail.Subject="Your weekly Impact community digest";
            pref.LastQueuedAt=DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
}
