using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public static class CommunityGrowthService
{
    public static async Task<DateOnly> Today(Db db, Guid tenant)
    {
        var tz = await db.Tenants.Where(t => t.Id == tenant).Select(t => t.TimeZone).SingleAsync();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(tz)).DateTime);
    }
    public static IQueryable<CommunityPost> Managed(Db db, CommunityEndpoints.Scope s) => db.CommunityPosts.Where(p => p.TenantId == s.W.TenantId && p.Status != "Archived" && s.Manage && s.ReportIds.Contains(p.OrgId));
    public static async Task<CommunityPost> Editable(Guid id, Access a, CommunityEndpoints.Scope s) => await Managed(a.Db, s).SingleOrDefaultAsync(p => p.Id == id) ?? throw new ApiError(404, "Activity unavailable in your publishing scope.");
    public static bool CanReview(CommunityEndpoints.Scope s, CommunityPost p) => s.Platform || s.Manage && p.AuthorId != s.W.UserId && p.ReviewOrgId == s.W.OrgId;
    public static async Task ApplyReview(Access a, CommunityEndpoints.Scope s, CommunityPost p)
    {
        var required = await a.Db.CommunityPolicies.Where(x => x.TenantId == p.TenantId).Select(x => (bool?)x.RequirePublicApproval).SingleOrDefaultAsync() ?? true;
        p.ReviewNote = ""; p.ReviewedBy = null; p.ReviewedAt = null;
        if (p.Visibility != "Public" || !required || s.Platform) { p.Status = "Published"; p.ReviewOrgId = null; return; }
        p.ReviewOrgId = await a.Db.Orgs.Where(o => o.Id == p.OrgId && o.TenantId == p.TenantId).Select(o => o.ParentId).SingleAsync();
        Check(p.ReviewOrgId != null, "Public updates at headquarters require Platform Admin approval.", 409);
        p.Status = "Pending";
    }
    public static async Task<ClubEvent> SourceEvent(Guid id, Access a, CommunityEndpoints.Scope s, Guid? org = null)
    {
        var e = await a.Db.Events.SingleOrDefaultAsync(e => e.Id == id && e.TenantId == s.W.TenantId && s.ReportIds.Contains(e.OrgId));
        Check(e != null && s.Manage && (s.Platform || e.OrgId == s.W.OrgId) && (org == null || e.OrgId == org), "Choose an event you are authorized to publish for this unit.", 404);
        Check(e.EndsAt < DateTimeOffset.UtcNow && e.Approval == "Approved" && e.Publication == "Published" && e.Lifecycle != "Cancelled", "Only completed, approved and published events can become activity updates.", 409);
        return e;
    }
    public static async Task SetTags(Access a, CommunityEndpoints.Scope s, CommunityPost p, Guid[]? people)
    {
        if (people == null) return;
        Check(people.Length <= 100 && people.Distinct().Count() == people.Length, "Tag up to 100 distinct members.");
        var units = a.Tree(s.W.TenantId, p.OrgId).Select(o => o.Id);
        var valid = await NetworkingEndpoints.Active(a.Db, s.W.TenantId).Where(m => people.Contains(m.PersonId) && units.Contains(m.ClubId)).Select(m => m.PersonId).Distinct().ToArrayAsync();
        Check(valid.Length == people.Length, "Participants must be active members of this unit or its descendant clubs.");
        var existing = await a.Db.ActivityParticipations.Where(t => t.PostId == p.Id).ToListAsync();
        foreach (var tag in existing.Where(t => !people.Contains(t.PersonId))) {
            Check(!await a.Db.ActivityCertificates.AnyAsync(c => c.ParticipationId == tag.Id && c.Status == "Issued"), "Revoke a participant's certificate before removing their tag.", 409);
            if(await a.Db.ActivityCertificates.AnyAsync(c=>c.ParticipationId==tag.Id)){tag.Consent="Removed";tag.Verified=false;tag.PublicName=false;tag.Version++;}
            else a.Db.ActivityParticipations.Remove(tag);
        }
        foreach(var tag in existing.Where(t=>people.Contains(t.PersonId)&&t.Consent=="Removed")){tag.Consent="Pending";tag.Verified=false;tag.PublicName=false;tag.Version++;}
        foreach (var person in people.Where(id => !existing.Any(t => t.PersonId == id))) {
            var checkedIn = p.SourceEventId != null && await a.Db.EventRegistrations.AnyAsync(r => r.TenantId == p.TenantId && r.EventId == p.SourceEventId && r.PersonId == person && r.Status == "Confirmed" && a.Db.EventCheckins.Any(c => c.RegistrationId == r.Id && !c.Voided && c.SessionId == null));
            a.Db.ActivityParticipations.Add(new() { TenantId = p.TenantId, PostId = p.Id, PersonId = person, Verified = checkedIn, VerifiedBy = checkedIn ? s.W.UserId : null, Evidence = checkedIn ? "Verified event check-in" : "" });
        }
    }
    public static IQueryable<Person> Discoverable(Db db, Guid tenant) => db.Persons.Where(p => p.Privacy.DirectoryVisible && p.Privacy.ProfessionalVisible && NetworkingEndpoints.Active(db, tenant).Any(m => m.PersonId == p.Id));
    public static IQueryable<CommunityPost> Followed(Db db, Guid tenant, Guid user, IQueryable<CommunityPost> posts) => posts.Where(p => db.CommunityFollows.Any(f => f.TenantId == tenant && f.UserId == user && (f.Kind == "Organization" && f.Target == p.OrgId.ToString() || f.Kind == "Topic" && f.Target == p.Category)));
}
