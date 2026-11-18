using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public record CommunityPolicyInput(bool RequirePublicApproval, long Version = 1);
public record ActivityReviewInput(string Decision, long Version, string Note = "");
public record FollowInput(string Kind, string Target, bool Following);
public record DigestPreferenceInput(bool Enabled);
public static partial class CommunityGrowthEndpoints
{
    public static void MapCommunityGrowth(this RouteGroupBuilder api)
    {
        MapReview(api);
        MapParticipation(api);
        MapDiscovery(api);
        MapStorageHealth(api);
        api.MapPost("/community/posts/{id:guid}/import-event-media",async(Guid id,Access a,GoogleMediaService google,HttpContext ctx)=>{
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);var p=await CommunityGrowthService.Editable(id,a,s);Check(p.SourceEventId!=null,"This activity has no source event.");
            var docs=await a.Db.GovernanceDocuments.Where(d=>d.TenantId==s.W.TenantId&&d.EventId==p.SourceEventId&&d.Status=="Available"&&(d.Kind=="Gallery"||d.Kind=="EventBanner")&&(d.ContentType=="image/jpeg"||d.ContentType=="image/png")&&(d.RetainUntil==null||d.RetainUntil>DateTimeOffset.UtcNow)&&!a.Db.CommunityMedia.Any(m=>m.PostId==id&&m.SourceDocumentId==d.Id)).OrderBy(d=>d.At).Take(20).ToListAsync();
            if(docs.Count==0)return Results.Ok(new{imported=0});
            var c=await a.Db.GoogleMediaConnections.SingleOrDefaultAsync(c=>c.TenantId==s.W.TenantId&&c.Connected&&(p.GoogleConnectionId==null?c.ActiveForUploads:c.Id==p.GoogleConnectionId));Check(c!=null,"Connect Google storage to import the event gallery.",409);
            if(c.RootFolderId=="")c.RootFolderId=await google.Folder(c,"Impact International · Activity media",null,ctx.RequestAborted);
            if(p.DriveFolderId==""){p.DriveFolderId=await google.Folder(c,p.ActivityOn+" · "+p.Title+" · "+p.Id.ToString()[..8],c.RootFolderId,ctx.RequestAborted);p.GoogleConnectionId=c.Id;}
            var count=await a.Db.CommunityMedia.CountAsync(m=>m.PostId==id&&m.Status!="Removed");Check(count+docs.Count<=20,"The imported gallery would exceed 20 attachments.");
            foreach(var d in docs){Check(d.Content.Length is >0 and <=5242880,"Event gallery file is unavailable or too large.");var m=new CommunityMedia{TenantId=s.W.TenantId,PostId=id,ConnectionId=c.Id,UploadedBy=s.W.UserId,FileName=d.FileName,MimeType=d.ContentType,Size=d.Content.LongLength,SourceDocumentId=d.Id};await google.ImportExisting(c,p,m,d.Content,ctx.RequestAborted);a.Db.CommunityMedia.Add(m);}
            await a.Log(s.W,"community.event.gallery.imported",id.ToString(),docs.Count.ToString());await tx.CommitAsync();return Results.Ok(new{imported=docs.Count});
        });
        api.MapGet("/community/events", async (Access a) => {
            var s = await CommunityEndpoints.GetScope(a); Check(s.Manage, "A publishing role is required.", 403);
            return await a.Db.Events.Where(e => e.TenantId == s.W.TenantId && (s.Platform || e.OrgId == s.W.OrgId) && e.EndsAt <= DateTimeOffset.UtcNow && e.Publication == "Published" && e.Approval == "Approved" && e.Lifecycle != "Cancelled" && !a.Db.CommunityPosts.Any(p => p.SourceEventId == e.Id))
                .OrderByDescending(e => e.EndsAt).Take(100).Select(e => new { e.Id, e.Title, e.OrgId, e.EndsAt }).ToListAsync();
        });
        api.MapGet("/community/events/{id:guid}/draft", async (Guid id, Access a) => {
            var s = await CommunityEndpoints.GetScope(a); var e = await CommunityGrowthService.SourceEvent(id,a,s);
            var people = await a.Db.EventRegistrations.Where(r => r.EventId == id && r.Status == "Confirmed" && r.PersonId != null && a.Db.EventCheckins.Any(c => c.RegistrationId == r.Id && !c.Voided && c.SessionId == null) && NetworkingEndpoints.Active(a.Db,s.W.TenantId).Any(m => m.PersonId == r.PersonId && a.Tree(s.W.TenantId,e.OrgId).Any(o => o.Id == m.ClubId))).Select(r => r.PersonId!.Value).Distinct().ToArrayAsync();
            return new { sourceEventId=e.Id, e.OrgId, e.Title, body=e.Description.Length>=5 ? e.Description : "Highlights from " + e.Title, category=CommunityEndpoints.Categories.Contains(e.ActivityType) ? e.ActivityType : e.ActivityType=="Service" ? "Community service":"Club activity", activityOn=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(e.StartsAt,TimeZoneInfo.FindSystemTimeZoneById(e.TimeZone)).DateTime), location=e.Venue, participants=await a.Db.EventCheckins.Where(c=>c.EventId==id&&!c.Voided&&c.SessionId==null).Select(c=>c.RegistrationId).Distinct().CountAsync(), participantIds=people, e.Latitude,e.Longitude };
        });
        api.MapGet("/community/participant-options", async (Access a, Guid orgId, string search="") => {
            var s=await CommunityEndpoints.GetScope(a);Check(CommunityEndpoints.CanModerate(s,orgId),"Choose a unit in your publishing scope.",403);Check(search.Length<=100,"Search too long.");
            var units=a.Tree(s.W.TenantId,orgId).Select(o=>o.Id); var term=search.Trim().ToLower();
            return await NetworkingEndpoints.Active(a.Db,s.W.TenantId).Where(m=>units.Contains(m.ClubId)&&(term==""||m.Person.DisplayName.ToLower().Contains(term)||m.Number.ToLower().Contains(term))).GroupBy(m=>new{m.PersonId,m.Person.DisplayName}).Select(g=>new{id=g.Key.PersonId,name=g.Key.DisplayName,number=g.Select(m=>m.Number).Min()}).OrderBy(m=>m.name).Take(50).ToListAsync();
        });
        api.MapGet("/community/following", async (Access a) => {
            var s=await CommunityEndpoints.GetScope(a);
            return new { follows=await a.Db.CommunityFollows.Where(f=>f.TenantId==s.W.TenantId&&f.UserId==s.W.UserId).Select(f=>new{f.Kind,f.Target}).ToListAsync(), topics=CommunityEndpoints.Categories,
                organizations=await a.Db.Orgs.Where(o=>o.TenantId==s.W.TenantId&&o.Status=="Active"&&(s.ReadIds.Contains(o.Id)||CommunityEndpoints.Posts(a.Db,s).Any(p=>p.OrgId==o.Id))).OrderBy(o=>o.Name).Select(o=>new{o.Id,o.Name}).ToListAsync(),
                digest=await a.Db.CommunityDigestPreferences.Where(f=>f.TenantId==s.W.TenantId&&f.UserId==s.W.UserId).Select(f=>new{f.Enabled,f.NextDigestAt,f.LastQueuedAt}).SingleOrDefaultAsync() };
        });
        api.MapPut("/community/following", async (FollowInput i,Access a) => {
            var s=await CommunityEndpoints.GetScope(a);Check(i.Kind is "Organization" or "Topic"&&i.Target is {Length:>0 and <=100},"Choose an organization or topic.");
            if(i.Kind=="Organization")Check(Guid.TryParse(i.Target,out var org)&&await a.Db.Orgs.AnyAsync(o=>o.TenantId==s.W.TenantId&&o.Id==org&&o.Status=="Active"),"Organization unavailable.",404);
            else Check(CommunityEndpoints.Categories.Contains(i.Target),"Unsupported topic.");
            await using var tx=await a.Mutation(s.W); var f=await a.Db.CommunityFollows.SingleOrDefaultAsync(f=>f.TenantId==s.W.TenantId&&f.UserId==s.W.UserId&&f.Kind==i.Kind&&f.Target==i.Target);
            if(i.Following&&f==null){Check(await a.Db.CommunityFollows.CountAsync(f=>f.TenantId==s.W.TenantId&&f.UserId==s.W.UserId)<100,"Follow at most 100 spaces or topics.");a.Db.CommunityFollows.Add(new(){TenantId=s.W.TenantId,UserId=s.W.UserId,Kind=i.Kind,Target=i.Target});}
            if(!i.Following&&f!=null)a.Db.CommunityFollows.Remove(f);
            await a.Log(s.W,"community.follow.changed",i.Target);await tx.CommitAsync();return Results.NoContent();
        });
        api.MapPut("/community/digest", async (DigestPreferenceInput i,Access a) => {
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);
            var p=await a.Db.CommunityDigestPreferences.SingleOrDefaultAsync(p=>p.TenantId==s.W.TenantId&&p.UserId==s.W.UserId);
            if(p==null){p=new(){TenantId=s.W.TenantId,UserId=s.W.UserId};a.Db.CommunityDigestPreferences.Add(p);}
            p.AssignmentId=s.W.Id;if(i.Enabled&&!p.Enabled)p.NextDigestAt=DateTimeOffset.UtcNow.AddDays(7);p.Enabled=i.Enabled;
            if(!i.Enabled)await a.Db.Mails.Where(m=>m.TenantId==s.W.TenantId&&m.UserId==s.W.UserId&&m.Kind=="community.digest"&&m.SentAt==null).ExecuteUpdateAsync(x=>x.SetProperty(m=>m.Suppressed,true));
            await a.Log(s.W,"community.digest.preference",s.W.UserId.ToString());await tx.CommitAsync();return Results.NoContent();
        });
        api.MapGet("/community/digest/preview", async(Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);var since=DateTimeOffset.UtcNow.AddDays(-7);
            var q=CommunityGrowthService.Followed(a.Db,s.W.TenantId,s.W.UserId,CommunityEndpoints.Posts(a.Db,s)).Where(p=>p.CreatedAt>=since);
            return new{ total=await q.CountAsync(),items=await CommunityEndpoints.Rows(q.OrderByDescending(p=>p.CreatedAt).Take(20),a.Db,s).ToListAsync() };
        });
    }
    static void MapReview(RouteGroupBuilder api)
    {
        api.MapGet("/community/policy",async(Access a)=>{var s=await CommunityEndpoints.GetScope(a);return new{requirePublicApproval=await a.Db.CommunityPolicies.Where(p=>p.TenantId==s.W.TenantId).Select(p=>(bool?)p.RequirePublicApproval).SingleOrDefaultAsync()??true,version=await a.Db.CommunityPolicies.Where(p=>p.TenantId==s.W.TenantId).Select(p=>(long?)p.Version).SingleOrDefaultAsync()??1,canConfigure=s.Platform};});
        api.MapPut("/community/policy",async(CommunityPolicyInput i,Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);Check(s.Platform,"Only Platform Admin can change publication policy.",403);await using var tx=await a.Mutation(s.W);
            var p=await a.Db.CommunityPolicies.SingleOrDefaultAsync(p=>p.TenantId==s.W.TenantId);if(p==null){p=new(){TenantId=s.W.TenantId};a.Db.CommunityPolicies.Add(p);}Check(p.Version==i.Version,"Policy changed. Refresh.",409);
            p.RequirePublicApproval=i.RequirePublicApproval;p.Version++;await a.Log(s.W,"community.policy.changed",p.Id.ToString());await tx.CommitAsync();return new{p.Version};
        });
        api.MapGet("/community/review",async(Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);Check(s.Manage,"Reviewer role required.",403);
            var q=a.Db.CommunityPosts.Where(p=>p.TenantId==s.W.TenantId&&p.Status=="Pending"&&(s.Platform||p.ReviewOrgId==s.W.OrgId&&p.AuthorId!=s.W.UserId));
            return await CommunityEndpoints.Rows(q.OrderBy(p=>p.CreatedAt).Take(100),a.Db,s).ToListAsync();
        });
        api.MapPost("/community/posts/{id:guid}/review",async(Guid id,ActivityReviewInput i,Access a)=>{
            var s=await CommunityEndpoints.GetScope(a);await using var tx=await a.Mutation(s.W);var p=await a.Db.CommunityPosts.SingleOrDefaultAsync(p=>p.Id==id&&p.TenantId==s.W.TenantId);
            Check(p!=null&&CommunityGrowthService.CanReview(s,p),"Review belongs to the parent unit's authorized leadership.",403);Check(p.Status=="Pending"&&p.Version==i.Version,"Activity changed. Refresh the review queue.",409);
            Check(i.Decision is "Approve" or "Reject"&&i.Note!=null&&i.Note.Length<=1000&&(i.Decision!="Reject"||i.Note.Trim().Length>=5),"Approve or reject with a reason of at least five characters.");
            p.Status=i.Decision=="Approve"?"Published":"Rejected";p.ReviewNote=i.Note.Trim();p.ReviewedBy=s.W.UserId;p.ReviewedAt=DateTimeOffset.UtcNow;p.Version++;p.UpdatedAt=DateTimeOffset.UtcNow;
            await a.Log(s.W,"community.post."+i.Decision.ToLowerInvariant(),id.ToString(),p.ReviewNote);await tx.CommitAsync();return new{p.Id,p.Status,p.Version};
        });
    }
}
