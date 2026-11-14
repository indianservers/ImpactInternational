using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public record EventMemberUpdate(Guid RequestId, string Message, Guid? OrgId=null, string Segment="All");
public static partial class EventEndpoints
{
    internal static async Task<IQueryable<Guid>> CommunicationPeople(EventService s, ClubEvent e, Guid? orgId, string segment, bool linkedOnly=false)
    {
        Check(new[]{"All","Awaiting RSVP","Confirmed","Not registered"}.Contains(segment),"Unknown communication audience.");
        var q=await s.Audience(e);
        if(orgId!=null){
            Check(await s.Db.Orgs.AnyAsync(o=>o.Id==orgId&&o.TenantId==e.TenantId),"Organization not found.",404);
            q=q.Where(p=>s.Db.Memberships.Any(m=>m.PersonId==p&&m.TenantId==e.TenantId&&m.Status=="Active"&&s.A.Tree(e.TenantId,orgId.Value).Any(o=>o.Id==m.ClubId)));
        }
        if(segment=="Awaiting RSVP")q=q.Where(p=>s.Db.EventInvitations.Any(i=>i.EventId==e.Id&&i.PersonId==p&&i.Rsvp=="Awaiting Response"&&i.Status!="Suppressed"));
        if(segment=="Confirmed")q=q.Where(p=>s.Db.EventRegistrations.Any(r=>r.EventId==e.Id&&r.PersonId==p&&r.Status=="Confirmed"));
        if(segment=="Not registered")q=q.Where(p=>!s.Db.EventRegistrations.Any(r=>r.EventId==e.Id&&r.PersonId==p&&new[]{"Confirmed","Pending","Waitlisted","Offered"}.Contains(r.Status)));
        if(linkedOnly)q=q.Where(p=>s.Db.Persons.Any(person=>person.Id==p&&person.UserId!=null));
        return q.Distinct();
    }
    static void MapCommunicationCenter(RouteGroupBuilder api)
    {
        api.MapGet("/event-hierarchy",async(EventService s)=>{
            var w=await s.A.WorkspaceAny("events.view","events.manage","events.reports","events.checkin");
            var q=s.A.Visible(w).Where(o=>o.Status=="Active");
            Check(await q.CountAsync()<=5000,"Narrow your workspace to load up to 5,000 units.");
            return await q.OrderBy(o=>o.Name).Select(o=>new{o.Id,o.ParentId,o.Name,type=s.Db.Levels.Where(l=>l.Id==o.LevelId).Select(l=>l.Name).First()}).ToListAsync();
        });
        api.MapGet("/events/{id:guid}/communication",async(Guid id,EventService s,Guid? orgId=null,string segment="All",int page=1)=>{
            var e=await s.Find(id);var assignment=await s.Manage(e);
            var visibleOrgs=assignment==null?s.A.Tree(e.TenantId,e.OrgId).Select(o=>o.Id):s.A.Visible(assignment).Select(o=>o.Id);
            Check(page>=1&&page<=10000,"Invalid page.");
            var people=await CommunicationPeople(s,e,orgId,segment);var linked=await CommunicationPeople(s,e,orgId,segment,true);
            var total=await people.CountAsync();var linkedCount=await linked.CountAsync();
            var invited=s.Db.EventInvitations.Where(i=>i.EventId==id&&i.PersonId!=null&&people.Contains(i.PersonId.Value));
            var regs=s.Db.EventRegistrations.Where(r=>r.EventId==id&&r.PersonId!=null&&people.Contains(r.PersonId.Value));
            return Results.Ok(new{total,linked=linkedCount,unlinked=total-linkedCount,invited=await invited.CountAsync(),newRecipients=await people.CountAsync(p=>!s.Db.EventInvitations.Any(i=>i.EventId==id&&i.PersonId==p)),
                awaiting=await invited.CountAsync(i=>i.Rsvp=="Awaiting Response"),responded=await invited.CountAsync(i=>i.Rsvp!="Awaiting Response"),confirmed=await regs.CountAsync(r=>r.Status=="Confirmed"),
                page,items=await s.Db.Persons.Where(p=>people.Contains(p.Id)).OrderBy(p=>p.DisplayName).ThenBy(p=>p.Id).Skip((page-1)*30).Take(30).Select(p=>new{p.Id,p.DisplayName,linked=p.UserId!=null,
                    clubs=s.Db.Memberships.Where(m=>m.PersonId==p.Id&&m.TenantId==e.TenantId&&m.Status=="Active"&&visibleOrgs.Contains(m.ClubId)&&(orgId==null||s.A.Tree(e.TenantId,orgId.Value).Any(o=>o.Id==m.ClubId))).Select(m=>m.Club.Name).Take(3).ToArray(),
                    rsvp=s.Db.EventInvitations.Where(i=>i.EventId==id&&i.PersonId==p.Id).Select(i=>i.Rsvp).FirstOrDefault(),
                    registration=s.Db.EventRegistrations.Where(r=>r.EventId==id&&r.PersonId==p.Id).Select(r=>r.Status).FirstOrDefault()}).ToListAsync(),
                jobs=await s.Db.EventNotices.Where(n=>n.EventId==id&&n.Audience.StartsWith("MemberUpdate|")).OrderByDescending(n=>n.Id).Take(10).Select(n=>new{n.Id,n.Message,n.Completed}).ToListAsync(),
                notice="Recipients are deduplicated people within the approved event audience, intersected with the selected unit. Updates reach linked member accounts; email uses existing notification preferences and configured delivery. WhatsApp/SMS are not connected. Invitation queuing is not proof of email delivery."});
        });
        api.MapPost("/events/{id:guid}/member-updates",async(Guid id,EventMemberUpdate input,EventService s)=>{
            var e=await s.Find(id);await s.Manage(e);
            Check(e.Publication=="Published"&&e.Approval=="Approved"&&e.Lifecycle is not "Cancelled" and not "Archived" and not "Completed"&&e.EndsAt>DateTimeOffset.UtcNow,"Publish an active approved event before messaging members.",409);
            Check(input.RequestId!=Guid.Empty&&(input.Message??" ").Trim().Length is >=5 and <=2000,"Write a message between 5 and 2,000 characters.");
            var recipients=await CommunicationPeople(s,e,input.OrgId,input.Segment,true);var count=await recipients.CountAsync();Check(count>0,"No linked members match this selection.");
            var audience="MemberUpdate|"+(input.OrgId?.ToString()??"")+"|"+input.Segment;
            await using var tx=await s.Db.Database.BeginTransactionAsync();await s.Lock(e.Id);
            var old=await s.Db.EventNotices.SingleOrDefaultAsync(n=>n.Id==input.RequestId);
            if(old!=null){Check(old.TenantId==e.TenantId&&old.EventId==id&&old.Audience==audience&&old.Message==(input.Message??" ").Trim(),"Request key already used for another message.",409);return Results.Ok(new{queued=count,id=old.Id,duplicate=true});}
            var job=new EventNotice{Id=input.RequestId,TenantId=e.TenantId,EventId=id,Audience=audience,Message=(input.Message??" ").Trim()};s.Db.EventNotices.Add(job);
            await s.Audit(e,"member-update.queued",(await s.A.User()).Id,count+" linked recipients · "+input.Segment+" · "+input.OrgId);await s.Db.SaveChangesAsync();await tx.CommitAsync();
            return Results.Ok(new{queued=count,id=job.Id,duplicate=false});
        });
    }
}
