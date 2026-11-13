using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

// Shared by the existing list and export handlers; all predicates narrow the authorized query.
public static class MembershipPresentation
{
    public static async Task<IQueryable<Membership>> Filter(MembershipService s, Assignment w, IQueryable<Membership> q,
        Guid? districtId, Guid? zoneId, string? membershipType, DateOnly? joinedFrom, DateOnly? joinedTo, DateOnly? renewalFrom, DateOnly? renewalTo, Guid? nationalId=null,string? country=null,string? state=null)
    {
        foreach(var (id,level) in new[]{(districtId,"District"),(zoneId,"Zone"),(nationalId,"National")})
        {
            if(id==null)continue;
            Check(await s.Db.Orgs.AnyAsync(o=>o.Id==id&&o.TenantId==w.TenantId&&s.Db.Levels.Any(l=>l.Id==o.LevelId&&l.Name==level))
                && (await s.Access.Visible(w).AnyAsync(o=>o.Id==id)||await s.Access.Tree(w.TenantId,id.Value).AnyAsync(o=>o.Id==w.OrgId)),level+" outside authorized scope.",404);
            var branch=id.Value;q=q.Where(m=>s.Access.Tree(w.TenantId,branch).Any(o=>o.Id==m.ClubId));
        }
        Check((country?.Length??0)<=120&&(state?.Length??0)<=120,"Geographic filter is too long.");
        if(!string.IsNullOrEmpty(country))q=q.Where(m=>m.Club.Country==country);
        if(!string.IsNullOrEmpty(state))q=q.Where(m=>m.Club.State==state);
        if(!string.IsNullOrWhiteSpace(membershipType)){Check(membershipType.Length<=60,"Membership type too long.");q=q.Where(m=>m.Club.ClubType==membershipType);}
        Check(joinedFrom==null||joinedTo==null||joinedFrom<=joinedTo,"Joined date range is reversed.");
        Check(renewalFrom==null||renewalTo==null||renewalFrom<=renewalTo,"Renewal date range is reversed.");
        var tz=TimeZoneInfo.FindSystemTimeZoneById(await s.Db.Tenants.Where(t=>t.Id==w.TenantId).Select(t=>t.TimeZone).SingleAsync());
        DateTimeOffset Boundary(DateOnly d)=>new(TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue,DateTimeKind.Unspecified),tz));
        if(joinedFrom!=null){var at=Boundary(joinedFrom.Value);q=q.Where(m=>m.JoinedAt>=at);}
        if(joinedTo!=null){Check(joinedTo.Value.Year<9999,"Joined date too late.");var at=Boundary(joinedTo.Value.AddDays(1));q=q.Where(m=>m.JoinedAt<at);}
        if(renewalFrom!=null){var at=Boundary(renewalFrom.Value);q=q.Where(m=>m.EndsAt>=at);}
        if(renewalTo!=null){Check(renewalTo.Value.Year<9999,"Renewal date too late.");var at=Boundary(renewalTo.Value.AddDays(1));q=q.Where(m=>m.EndsAt<at);}
        return q;
    }
    public static IOrderedQueryable<Membership> Sort(IQueryable<Membership> q,string sort)=>sort switch
    {
        "name-desc"=>q.OrderByDescending(m=>m.Person.DisplayName),"number"=>q.OrderBy(m=>m.Number),"number-desc"=>q.OrderByDescending(m=>m.Number),
        "club"=>q.OrderBy(m=>m.Club.Name),"club-desc"=>q.OrderByDescending(m=>m.Club.Name),"category"=>q.OrderBy(m=>m.Category.Name),"category-desc"=>q.OrderByDescending(m=>m.Category.Name),
        "joined"=>q.OrderByDescending(m=>m.JoinedAt),"joined-asc"=>q.OrderBy(m=>m.JoinedAt),"renewal"=>q.OrderBy(m=>m.EndsAt),"renewal-desc"=>q.OrderByDescending(m=>m.EndsAt),
        "status"=>q.OrderBy(m=>m.Status),"status-desc"=>q.OrderByDescending(m=>m.Status),"city"=>q.OrderBy(m=>m.Club.City),"city-desc"=>q.OrderByDescending(m=>m.Club.City),
        "updated"=>q.OrderByDescending(m=>m.UpdatedAt),_=>q.OrderBy(m=>m.Person.DisplayName)
    };
    public static async Task<object> Intelligence(MembershipService s,Assignment w,string? range,DateOnly? from,DateOnly? to)
    {
        var db=s.Db;var q=await s.ScopedList(w);var now=DateTimeOffset.UtcNow;
        var zone=TimeZoneInfo.FindSystemTimeZoneById(await db.Tenants.Where(t=>t.Id==w.TenantId).Select(t=>t.TimeZone).SingleAsync());
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,zone).DateTime);var period=range??"6m";
        Check(new[]{"30d","3m","6m","12m","custom"}.Contains(period),"Choose a supported membership period.");
        var endDate=period=="custom"?to??today:today;
        var startDate=period switch{"30d"=>today.AddDays(-29),"3m"=>new DateOnly(today.Year,today.Month,1).AddMonths(-2),"12m"=>new DateOnly(today.Year,today.Month,1).AddMonths(-11),"custom"=>from??today.AddDays(-29),_=>new DateOnly(today.Year,today.Month,1).AddMonths(-5)};
        Check(startDate>=new DateOnly(1900,1,1)&&startDate<=endDate&&endDate<=today&&endDate.DayNumber-startDate.DayNumber<=730,"Choose a valid membership date range of at most two years, ending today or earlier.");
        DateTimeOffset Bound(DateOnly d)=>new(TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue,DateTimeKind.Unspecified),zone));
        var start=Bound(startDate);var end=Bound(endDate.AddDays(1));var prior=Bound(startDate.AddDays(-(endDate.DayNumber-startDate.DayNumber+1)));
        var first=q.GroupBy(m=>m.PersonId).Select(g=>g.Min(m=>m.JoinedAt));
        var registered=await first.CountAsync(d=>d>=start&&d<end);var previous=await first.CountAsync(d=>d>=prior&&d<start);
        var statusCounts=await q.GroupBy(m=>m.Status).Select(g=>new {status=g.Key,count=g.Count()}).ToListAsync();
        var clubs=await q.GroupBy(m=>m.ClubId).Select(g=>new{clubId=g.Key,count=g.Count(),active=g.Count(m=>m.Status=="Active"&&(m.EndsAt==null||m.EndsAt>now))}).ToListAsync();
        var places=await RegionalDirectory.Places(s,w.TenantId,clubs.Select(c=>c.clubId).ToArray());
        var regions=clubs.GroupBy(c=>places[c.clubId].RegionId).Select(g=>new{id=g.Key,name=places[g.First().clubId].Region is ""?"Unassigned":places[g.First().clubId].Region,count=g.Sum(c=>c.count),active=g.Sum(c=>c.active),inactive=g.Sum(c=>c.count-c.active)}).OrderByDescending(g=>g.count).ToList();
        // Historical active counts use recorded status/expiry snapshots, not today's active status projected backwards.
        var scope=(await s.Access.User()).PlatformAdmin?await db.Orgs.Where(o=>o.TenantId==w.TenantId&&o.ParentId==null).Select(o=>o.Id).SingleAsync():w.OrgId;
        var chart=await db.Database.SqlQuery<MembershipTrendBucket>($"""
            WITH RECURSIVE orgscope AS (
              SELECT "Id" FROM "Orgs" WHERE "Id"={scope} AND "TenantId"={w.TenantId}
              UNION ALL SELECT o."Id" FROM "Orgs" o JOIN orgscope p ON o."ParentId"=p."Id" WHERE o."TenantId"={w.TenantId}
            ), members AS (SELECT m."Id",m."PersonId",m."JoinedAt" FROM "Memberships" m WHERE m."TenantId"={w.TenantId} AND m."ClubId" IN(SELECT "Id" FROM orgscope)),
            buckets AS (SELECT d AS left_at,LEAST(d+interval '1 month',{end}) AS right_at FROM generate_series(date_trunc('month',{start} AT TIME ZONE {zone.Id}) AT TIME ZONE {zone.Id},{end}-interval '1 microsecond',interval '1 month') d)
            SELECT b.left_at AS "At",COUNT(DISTINCT m."PersonId")::int AS "Total",
              COUNT(DISTINCT m."PersonId") FILTER(WHERE h."NewStatus"='Active' AND(h."EndsAt" IS NULL OR h."EndsAt">=b.right_at))::int AS "Active",
              (SELECT count(*)::int FROM(SELECT "PersonId",min("JoinedAt") AS joined FROM members GROUP BY "PersonId") n WHERE n.joined>=GREATEST(b.left_at,{start}) AND n.joined<b.right_at) AS "Registrations",
              (SELECT count(*)::int FROM "MembershipRenewals" r JOIN members rm ON rm."Id"=r."MembershipId" WHERE r."At">=GREATEST(b.left_at,{start}) AND r."At"<b.right_at AND r."TenantId"={w.TenantId}) AS "Renewals"
            FROM buckets b LEFT JOIN members m ON m."JoinedAt"<b.right_at
            LEFT JOIN LATERAL(SELECT hh."NewStatus",hh."EndsAt" FROM "MembershipHistory" hh WHERE hh."MembershipId"=m."Id" AND hh."TenantId"={w.TenantId} AND hh."EffectiveAt"<b.right_at ORDER BY hh."EffectiveAt" DESC,hh."RecordedAt" DESC LIMIT 1) h ON true
            GROUP BY b.left_at,b.right_at ORDER BY b.left_at
            """).ToListAsync();
        return new {range=period,from=startDate,to=endDate,timeZone=zone.Id,registered,previous,delta=registered-previous,changePercent=previous==0?(decimal?)null:Math.Round(100m*(registered-previous)/previous,1),statusCounts,regions,
            chart=chart.Select(b=>new{date=TimeZoneInfo.ConvertTime(b.At,zone).ToString("yyyy-MM-dd"),label=TimeZoneInfo.ConvertTime(b.At,zone).ToString("MMM yy"),b.Total,b.Active,b.Registrations,b.Renewals}),
            definition="KPIs show the current authorized scope. Comparisons count first registrations in equal periods. Trend totals are distinct people joined by each month end; active counts use the latest recorded membership status and expiry at that boundary. Regional/status counts are membership records (a person may have multiple affiliations). Renewal date filters use membership validity end dates. Membership type uses the existing club type; categories remain separate."};
    }
}
public class MembershipTrendBucket { public DateTimeOffset At {get;set;} public int Total {get;set;} public int Active {get;set;} public int Registrations {get;set;} public int Renewals {get;set;} }
