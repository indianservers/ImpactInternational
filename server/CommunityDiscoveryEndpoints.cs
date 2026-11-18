using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;
public static partial class CommunityGrowthEndpoints
{
    static void MapDiscovery(RouteGroupBuilder api)
    {
        api.MapGet("/community/discover",async(Access a,DateOnly? from=null,DateOnly? to=null,string category="",Guid? orgId=null,string city="",bool following=false,int page=1)=>{
            var s=await CommunityEndpoints.GetScope(a);var today=await CommunityGrowthService.Today(a.Db,s.W.TenantId);var start=from??today.AddDays(-90);var end=to??today;
            Check(start<=end&&end.DayNumber-start.DayNumber<=1096&&city.Length<=100&&page is >=1 and <=10000&&(category==""||CommunityEndpoints.Categories.Contains(category)),"Choose a valid activity search and date range of up to three years.");
            var q=CommunityEndpoints.Posts(a.Db,s).Where(p=>p.ActivityOn>=start&&p.ActivityOn<=end);if(orgId!=null){var branch=a.Tree(s.W.TenantId,orgId.Value).Select(o=>o.Id);q=q.Where(p=>branch.Contains(p.OrgId));}if(category!="")q=q.Where(p=>p.Category==category);
            if(city.Trim()!=""){var term=city.Trim().ToLower();q=q.Where(p=>p.Location.ToLower().Contains(term)||a.Db.Orgs.Any(o=>o.Id==p.OrgId&&o.City.ToLower().Contains(term)));}
            if(following)q=CommunityGrowthService.Followed(a.Db,s.W.TenantId,s.W.UserId,q);
            var points=await q.Where(p=>p.Latitude!=null&&p.Longitude!=null||a.Db.Orgs.Any(o=>o.Id==p.OrgId&&o.Latitude!=null&&o.Longitude!=null)).OrderByDescending(p=>p.ActivityOn).ThenBy(p=>p.Id).Take(500).Select(p=>new{p.Id,p.Title,p.OrgId,p.Category,p.ActivityOn,p.Visibility,p.Location,latitude=p.Latitude??a.Db.Orgs.Where(o=>o.Id==p.OrgId).Select(o=>o.Latitude).First(),longitude=p.Longitude??a.Db.Orgs.Where(o=>o.Id==p.OrgId).Select(o=>o.Longitude).First(),approximate=p.Latitude==null}).ToListAsync();
            return new{total=await q.CountAsync(),page,pageSize=15,mapLimit=500,points,items=await CommunityEndpoints.Rows(q.OrderByDescending(p=>p.ActivityOn).ThenBy(p=>p.Id).Skip((page-1)*15).Take(15),a.Db,s).ToListAsync()};
        });
        api.MapGet("/community/impact",async(Access a,int days=30)=>{
            var s=await CommunityEndpoints.GetScope(a);Check(s.Reports,"Leadership reporting permission required.",403);Check(new[]{30,90,365}.Contains(days),"Choose 30, 90 or 365 days.");
            var today=await CommunityGrowthService.Today(a.Db,s.W.TenantId);var since=today.AddDays(1-days);
            var units=await a.Db.Orgs.Where(o=>o.TenantId==s.W.TenantId&&s.ReportIds.Contains(o.Id)&&o.Status=="Active").Select(o=>new{o.Id,o.Name,level=a.Db.Levels.Where(l=>l.Id==o.LevelId).Select(l=>l.Name).First()}).ToListAsync();
            var q=a.Db.CommunityPosts.Where(p=>p.TenantId==s.W.TenantId&&s.ReportIds.Contains(p.OrgId)&&p.Status=="Published"&&p.ActivityOn>=since&&p.ActivityOn<=today);
            var by=await q.GroupBy(p=>p.OrgId).Select(g=>new{orgId=g.Key,updates=g.Count(),attendances=g.Sum(p=>p.Participants),hours=g.Sum(p=>p.VolunteerHours),last=g.Max(p=>p.ActivityOn)}).ToListAsync();
            var verified=await a.Db.ActivityParticipations.Where(t=>t.TenantId==s.W.TenantId&&t.Verified&&t.Consent=="Accepted"&&q.Any(p=>p.Id==t.PostId)).Select(t=>t.PersonId).Distinct().CountAsync();
            return new{days,from=since,to=today,summary=new{updates=by.Sum(b=>b.updates),recordedAttendances=by.Sum(b=>b.attendances),volunteerHours=by.Sum(b=>b.hours),uniqueVerifiedMembers=verified,activeClubs=units.Count(o=>o.level=="Club"&&by.Any(b=>b.orgId==o.Id)),silentClubs=units.Count(o=>o.level=="Club"&&!by.Any(b=>b.orgId==o.Id))},units=units.Select(o=>new{o.Id,o.Name,o.level,updates=by.FirstOrDefault(b=>b.orgId==o.Id)?.updates??0,recordedAttendances=by.FirstOrDefault(b=>b.orgId==o.Id)?.attendances??0,hours=by.FirstOrDefault(b=>b.orgId==o.Id)?.hours??0,last=by.FirstOrDefault(b=>b.orgId==o.Id)?.last}),categories=await q.GroupBy(p=>p.Category).Select(g=>new{name=g.Key,count=g.Count()}).OrderByDescending(g=>g.count).ToListAsync(),trend=await q.GroupBy(p=>p.ActivityOn).Select(g=>new{date=g.Key,count=g.Count()}).OrderBy(g=>g.date).ToListAsync(),definition="Recorded attendance and aggregate volunteer hours are post totals, not deduplicated people. Unique verified members require accepted participation tags and verified attendance. Silent clubs have no published activity in the selected period."};
        });
    }
    static void MapStorageHealth(RouteGroupBuilder api)
    {
        api.MapGet("/google-media/health",async(Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);Check(s.Platform,"Only Platform Admin can inspect storage health.",403);var now=DateTimeOffset.UtcNow;
            var q=a.Db.CommunityMedia.Where(m=>m.TenantId==s.W.TenantId);
            var accounts=await a.Db.GoogleMediaConnections.Where(c=>c.TenantId==s.W.TenantId).Select(c=>new{c.Id,c.Email,c.Connected,c.ActiveForUploads,readyFiles=q.Count(m=>m.ConnectionId==c.Id&&m.Status=="Ready"),bytes=q.Where(m=>m.ConnectionId==c.Id&&m.Status=="Ready").Sum(m=>(long?)m.Size)??0}).ToListAsync();
            return new{accounts,summary=new{ready=await q.CountAsync(m=>m.Status=="Ready"),bytes=await q.Where(m=>m.Status=="Ready").SumAsync(m=>(long?)m.Size)??0,failed=await q.CountAsync(m=>m.Status=="Failed"),stalled=await q.CountAsync(m=>m.Status=="Uploading"&&m.CreatedAt<now.AddHours(-1))},issues=await q.Where(m=>m.Status=="Failed"||m.Status=="Uploading"&&m.CreatedAt<now.AddHours(-1)||m.Status=="Ready"&&!a.Db.GoogleMediaConnections.Any(c=>c.Id==m.ConnectionId&&c.Connected)).OrderByDescending(m=>m.CreatedAt).Take(100).Select(m=>new{m.Id,m.PostId,m.FileName,m.Size,m.MimeType,m.Status,m.LastError,m.Attempts,m.CreatedAt,title=a.Db.CommunityPosts.Where(p=>p.Id==m.PostId).Select(p=>p.Title).First(),connected=a.Db.GoogleMediaConnections.Where(c=>c.Id==m.ConnectionId).Select(c=>c.Connected).First()}).ToListAsync(),note="Usage reflects attachments known to this app, not the entire Google account quota. Retry requires reselecting the original local file. No media bytes are retained on this server."};
        });
        api.MapPost("/community/media/{id:guid}/failure",async(Guid id,Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);var m=await a.Db.CommunityMedia.SingleOrDefaultAsync(m=>m.Id==id&&m.TenantId==s.W.TenantId);Check(m!=null,"Upload unavailable.",404);await CommunityGrowthService.Editable(m.PostId,a,s);if(m.Status=="Uploading"){m.Status="Failed";m.LastError="Browser upload interrupted. Reselect the file or retry the pending session.";}await a.Log(s.W,"community.media.upload.failed",id.ToString());await tx.CommitAsync();return Results.NoContent();
        });
        api.MapPost("/community/media/{id:guid}/retry",async(Guid id,Access a,GoogleMediaService google,HttpContext ctx)=>{
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);var m=await a.Db.CommunityMedia.SingleOrDefaultAsync(m=>m.Id==id&&m.TenantId==s.W.TenantId);Check(m!=null&&m.Status is "Failed" or "Uploading","Pending upload unavailable.",404);var p=await CommunityGrowthService.Editable(m.PostId,a,s);var c=await a.Db.GoogleMediaConnections.SingleOrDefaultAsync(c=>c.Id==m.ConnectionId&&c.TenantId==s.W.TenantId&&c.Connected);Check(c!=null,"Reconnect the original Google account first.",409);
            m.Status="Uploading";m.LastError="";m.Attempts++;m.CreatedAt=DateTimeOffset.UtcNow;var url=await google.BeginUpload(c,p,m,ctx.RequestAborted);await a.Log(s.W,"community.media.upload.retried",id.ToString());await tx.CommitAsync();ctx.Response.Headers.CacheControl="no-store";return new{m.Id,uploadUrl=url,chunkSize=8*1024*1024};
        });
    }
}
