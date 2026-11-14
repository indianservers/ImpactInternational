using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public static class PlatformDashboard
{
    public static async Task<object> Routing(Access a, Assignment w)
    {
        var user = await a.User(); var db = a.Db; var grants = await a.Grants(w);
        var org = await (from o in db.Orgs join l in db.Levels on o.LevelId equals l.Id where o.Id == w.OrgId && o.TenantId == w.TenantId select new { o.Id, o.Name, level = l.Name }).SingleAsync();
        var role = await db.Roles.Where(r => r.Id == w.RoleId).Select(r => r.Name).SingleAsync();
        var instant = DateTimeOffset.UtcNow;
        var committees = await (from m in db.CommitteeMemberships join p in db.Persons on m.PersonId equals p.Id
            join c in db.Committees on m.CommitteeId equals c.Id join r in db.CommitteeRoles on m.RoleId equals r.Id
            where p.UserId == user.Id && m.TenantId == w.TenantId && c.TenantId == w.TenantId && m.Status == "Active" && r.Active
                && m.StartsAt <= instant && m.EndsAt > instant && c.Status == "Active" && c.StartsAt <= instant && c.EndsAt > instant
            select new { c.Id, c.Name, r.CanManage }).Distinct().ToListAsync();
        var kind = user.PlatformAdmin ? "platform"
            : role == "Club Treasurer" && grants.Contains("finance.view") ? "finance"
            : grants.Contains("members.view") ? org.level switch { "National" => "national", "Region" => "regional", "District" => "district", "Zone" => "zonal", "Club" => role == "Club Secretary" ? "club-operations" : "club", _ => "organization" }
            : committees.Count > 0 ? "committee" : "member";
        var title = kind switch { "platform" => "Platform Administration", "finance" => "Club finance dashboard", "national" => "National leadership dashboard", "regional" => "Regional dashboard", "district" => "District dashboard", "zonal" => "Zonal dashboard", "club-operations" => "Club operations dashboard", "club" => "Club dashboard", "committee" => "My committee dashboard", "member" => "My Impact dashboard", _ => "Organization dashboard" };
        return new { kind, title, role, scope = org, platformAdmin = user.PlatformAdmin, committees, workspaceId = w.Id };
    }

    public static async Task<IResult> Endpoint(Access a, CommunicationService comm, string? view, string? range, DateOnly? from, DateOnly? to,
        Guid? orgId, Guid? roleId, string? format)
    {
        var w = await a.Workspace("organization.view");
        await LeaderCommunications.CheckAssignment(comm, w);
        Check(roleId == null || roleId == w.RoleId, "Role identifiers cannot change your active authority.", 403);
        Check(view is null or "route" or "platform" or "scoped", "Unknown dashboard view.");
        if (view == "platform")
        {
            Check((await a.User()).PlatformAdmin, "Platform administrator authority required.", 403);
            Check(orgId == null || orgId == w.OrgId, "Platform dashboard uses the authorized organization context.", 403);
            return await Intelligence(a, w, range, from, to, format);
        }
        Check(string.IsNullOrEmpty(format), "Exports require the authorized platform dashboard view.", 403);
        if (orgId != null) Check(await a.Visible(w).AnyAsync(o => o.Id == orgId), "Organization outside your scope.", 404);
        if (view == "route") return Results.Ok(await Routing(a, w));
        var grants = await a.Grants(w);
        if (!grants.Contains("members.view"))
            return Results.Ok(new { dashboard = await Routing(a, w), organizations = (int?)null, clubs = (int?)null, pending = (int?)null, recent = Array.Empty<object>() });
        var q = a.Visible(w);
        if (orgId != null) q = q.Where(o => a.Tree(w.TenantId, orgId.Value).Any(v => v.Id == o.Id));
        var ids = q.Select(o => o.Id);
        return Results.Ok(new { dashboard = await Routing(a, w), organizations = await q.CountAsync(),
            clubs = await q.CountAsync(o => a.Db.Levels.Any(l => l.Id == o.LevelId && l.IsClub)), pending = await q.CountAsync(o => o.Status == "Pending"),
            activeMembers = await a.Db.Memberships.Where(m => m.TenantId == w.TenantId && ids.Contains(m.ClubId) && m.Status == "Active" && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)).Select(m => m.PersonId).Distinct().CountAsync(),
            recent = grants.Contains("audit.view") ? await a.Db.Audits.Where(x => x.TenantId == w.TenantId && x.OrgId != null && ids.Contains(x.OrgId.Value)).OrderByDescending(x => x.At).Take(6).ToListAsync() : [] });
    }

    record Metric(string Id, string Label, decimal Value, decimal PeriodValue, decimal Previous, string Comparison, string Href, bool Percent = false)
    {
        public decimal Delta => PeriodValue - Previous;
        public decimal? DeltaPercent => Previous == 0 ? null : Math.Round(100 * (PeriodValue - Previous) / Previous, 1);
    }
    static async Task<IResult> Intelligence(Access a, Assignment w, string? range, DateOnly? from, DateOnly? to, string? format)
    {
        var db = a.Db; var now = DateTimeOffset.UtcNow;
        var tenant = await db.Tenants.SingleAsync(t => t.Id == w.TenantId);
        var organizationCount = await db.Tenants.CountAsync(t => t.Id == w.TenantId);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var selected = range ?? "6m";
        Check(new[] { "7d", "30d", "90d", "6m", "12m", "custom" }.Contains(selected), "Choose a supported dashboard date range.");
        var endDate = selected == "custom" ? to ?? today : today;
        var startDate = selected switch { "7d" => today.AddDays(-6), "30d" => today.AddDays(-29), "90d" => today.AddDays(-89), "12m" => new DateOnly(today.Year, today.Month, 1).AddMonths(-11), "custom" => from ?? today.AddDays(-29), _ => new DateOnly(today.Year, today.Month, 1).AddMonths(-5) };
        Check(startDate >= new DateOnly(1900,1,1) && startDate <= endDate && endDate <= today && endDate.DayNumber - startDate.DayNumber <= 730, "Choose a date range since 1900, of at most two years, ending today or earlier.");
        DateTimeOffset Bound(DateOnly date) => new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone));
        var start = Bound(startDate); var end = Bound(endDate.AddDays(1)); var priorStart = Bound(startDate.AddDays(-(endDate.DayNumber - startDate.DayNumber + 1)));
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var orgs = await (from o in db.Orgs join l in db.Levels on o.LevelId equals l.Id where o.TenantId == w.TenantId select new { o.Id, o.ParentId, o.Name, o.Status, o.Registration, o.Logo, o.CreatedAt, o.Code, o.City, o.Country, o.Website, level = l.Name, l.IsClub }).AsNoTracking().ToListAsync();
        var members = await db.Memberships.Where(m => m.TenantId == w.TenantId).Select(m => new { m.Id, m.PersonId, m.ClubId, m.Status, m.JoinedAt, m.EndsAt }).ToListAsync();
        var appointments = await db.OfficerAppointments.Where(o => o.TenantId == w.TenantId).Select(o => new { o.Id, o.OrgId, o.PositionId, o.StartsAt, o.EndsAt, o.CreatedAt, o.Status }).ToListAsync();
        var current = await GovernanceService.Current(db).Where(o => o.TenantId == w.TenantId).Select(o => new { o.Id, o.OrgId, o.PositionId, o.EndsAt }).ToListAsync();
        var positions = await db.OfficerPositions.Where(p => p.TenantId == w.TenantId && p.Active && p.Mandatory).Select(p => new { p.Id, p.LevelId }).ToListAsync();
        var levels = await db.Levels.Where(l => l.TenantId == w.TenantId).ToDictionaryAsync(l => l.Id, l => l.Name);
        var requests = await db.GovernanceRequests.Where(r => r.TenantId == w.TenantId).Select(r => new { r.Id, r.Status, r.CreatedAt, r.UpdatedAt, r.OrgId }).ToListAsync();
        var applications = await db.MembershipApplications.Where(r => r.TenantId == w.TenantId).Select(r => new { r.Status, r.CreatedAt }).ToListAsync();
        var committees = await db.Committees.Where(c => c.TenantId == w.TenantId).Select(c => new { c.Id, c.Status, c.StartsAt, c.EndsAt }).ToListAsync();
        var events = await db.Events.Where(e => e.TenantId == w.TenantId).Select(e => new { e.Id, e.OrgId, e.StartsAt, e.CreatedAt, e.Publication, e.Lifecycle }).ToListAsync();
        var attendance = await (from check in db.EventCheckins join registration in db.EventRegistrations on check.RegistrationId equals registration.Id
            where check.TenantId == w.TenantId && !check.Voided && registration.PersonId != null && check.At >= priorStart && check.At < end
            select new { check.EventId, check.At, registration.PersonId }).Distinct().ToListAsync();
        var renewals = await db.MembershipRenewals.Where(r => r.TenantId == w.TenantId && r.At >= start && r.At < end).Select(r => r.MembershipId).Distinct().ToListAsync();
        var active = members.Where(m => m.Status == "Active" && (m.EndsAt == null || m.EndsAt > now)).ToList();
        var registrants = members.GroupBy(m => m.PersonId).Select(g => new { id = g.Key, joined = g.Min(m => m.JoinedAt) }).ToList();
        var activeIds = active.Select(m => m.PersonId).Distinct().ToHashSet();
        var clubs = orgs.Where(o => o.IsClub).ToList();
        var activeCommittees = committees.Count(c => c.Status == "Active" && c.StartsAt <= now && c.EndsAt > now);
        bool Pending(string status) => status is "Submitted" or "Under Review";
        var openRequests = requests.Count(r => Pending(r.Status)); var openApplications = applications.Count(r => MembershipStatistics.PendingApplicationStates.Contains(r.Status)); var openOrgApprovals = orgs.Count(o => o.Status == "Pending");
        int InPeriod(IEnumerable<DateTimeOffset> values) => values.Count(v => v >= start && v < end);
        int Previous(IEnumerable<DateTimeOffset> values) => values.Count(v => v >= priorStart && v < start);
        var monthStart = Bound(new DateOnly(today.Year, today.Month, 1)); var priorMonth = Bound(new DateOnly(today.Year, today.Month, 1).AddMonths(-1));
        decimal Rate(decimal value, decimal total) => total == 0 ? 0 : Math.Round(100 * value / total, 1);
        var participated = attendance.Where(c => c.At >= start && activeIds.Contains(c.PersonId!.Value)).Select(c => c.PersonId).Distinct().Count();
        var previousParticipated = attendance.Where(c => c.At < start && activeIds.Contains(c.PersonId!.Value)).Select(c => c.PersonId).Distinct().Count();
        var engagement = Rate(participated, activeIds.Count);
        var metrics = new List<Metric>
        {
            new("organizations", "Organizations", organizationCount, tenant.CreatedAt >= start && tenant.CreatedAt < end ? organizationCount : 0, tenant.CreatedAt >= priorStart && tenant.CreatedAt < start ? organizationCount : 0, "Organizations created in each period", "/app/organizations"),
            new("clubs", "Connected clubs", clubs.Count, InPeriod(clubs.Select(c => c.CreatedAt)), Previous(clubs.Select(c => c.CreatedAt)), "Clubs created in each period", "/app/clubs"),
            new("registered", "Registered members", registrants.Count, InPeriod(registrants.Select(m => m.joined)), Previous(registrants.Select(m => m.joined)), "First member registrations in each period", "/app/members"),
            new("active", "Active members", activeIds.Count, InPeriod(registrants.Where(r => activeIds.Contains(r.id)).Select(r => r.joined)), Previous(registrants.Where(r => activeIds.Contains(r.id)).Select(r => r.joined)), "Currently active members first joining in each period", "/app/members?status=Active"),
            new("officers", "Current officers", current.Count, InPeriod(appointments.Select(o => o.CreatedAt)), Previous(appointments.Select(o => o.CreatedAt)), "Appointments created in each period", "/app/leadership"),
            new("decisions", "Pending decisions", openRequests, InPeriod(requests.Select(r => r.CreatedAt)), Previous(requests.Select(r => r.CreatedAt)), "Governance requests created in each period", "/app/governance"),
            new("joined", "Joined this month", registrants.Count(m => m.joined >= monthStart && m.joined <= now), registrants.Count(m => m.joined >= monthStart && m.joined <= now), registrants.Count(m => m.joined >= priorMonth && m.joined < monthStart), "Calendar month vs previous calendar month", "/app/members"),
            new("committees", "Active committees", activeCommittees, InPeriod(committees.Select(c => c.StartsAt)), Previous(committees.Select(c => c.StartsAt)), "Committee terms starting in each period", "/app/committees"),
            new("approvals", "Open approvals", openRequests + openApplications + openOrgApprovals, InPeriod(requests.Select(r => r.CreatedAt)) + InPeriod(applications.Select(r => r.CreatedAt)), Previous(requests.Select(r => r.CreatedAt)) + Previous(applications.Select(r => r.CreatedAt)), "Governance and membership requests created in each period", "/app/governance"),
            new("engagement", "Event engagement", engagement, engagement, Rate(previousParticipated, activeIds.Count), "Unique attending active members / current active members, compared across equal periods", "/app/event-intelligence", true),
        };
        var mandatorySlots = (from o in orgs.Where(o => o.Status == "Active") from p in positions where levels.GetValueOrDefault(p.LevelId) == o.level select new { org = o.Id, position = p.Id }).ToList();
        var vacancies = mandatorySlots.Count(slot => !current.Any(c => c.OrgId == slot.org && c.PositionId == slot.position));
        var occupied = mandatorySlots.Count - vacancies;
        var expiringMemberships = active.Count(m => m.EndsAt != null && m.EndsAt <= now.AddDays(30));
        var priorities = new[] {
            new { id="vacancies", label="Mandatory office vacancies", count=vacancies, priority="High", href="/app/leadership", meaning="Active organizational units with an unfilled mandatory position" },
            new { id="memberships", label="Memberships expiring in 30 days", count=expiringMemberships, priority="Medium", href="/app/members?status=Renewal%20Due", meaning="Active membership records ending within 30 days" },
            new { id="applications", label="Pending member applications", count=openApplications, priority="Medium", href="/app/approvals?status=Pending", meaning="Current Submitted, Under Review or Changes Requested applications; drafts excluded" },
            new { id="successors", label="Scheduled successors", count=appointments.Count(o=>o.Status=="Scheduled"&&o.EndsAt>now), priority="Low", href="/app/leadership-calendar", meaning="Scheduled appointments not yet ended" },
            new { id="org-approvals", label="Pending organization approvals", count=openOrgApprovals, priority="Medium", href="/app/organizations?status=Pending", meaning="Organizational units with Pending status" },
            new { id="configuration", label="Incomplete unit profiles", count=orgs.Count(o=>o.Status=="Active"&&(o.Country==""||o.City=="")), priority="Medium", href="/app/organizations", meaning="Active units missing city or country" },
            new { id="overdue", label="Governance waiting over 30 days", count=requests.Count(r=>Pending(r.Status)&&r.CreatedAt<now.AddDays(-30)), priority="High", href="/app/governance", meaning="Open governance requests created over 30 days ago; this is an ageing rule, not a configured SLA" },
        };
        var nodeById = orgs.ToDictionary(o => o.Id);
        Guid Ancestor(Guid id, string level) { for (int n=0; n<16 && nodeById.TryGetValue(id,out var o); n++) { if(o.level==level)return id; if(o.ParentId==null)break;id=o.ParentId.Value; }return Guid.Empty; }
        object[] Distribution(string level) => clubs.GroupBy(c=>Ancestor(c.Id,level)).Where(g=>g.Key!=Guid.Empty).OrderByDescending(g=>g.Count()).ThenBy(g=>nodeById[g.Key].Name).Select(g=>(object)new { id=g.Key,name=nodeById[g.Key].Name,clubs=g.Count(),href=level=="Region"?"/app/regions":"/app/districts" }).ToArray();
        var buckets = new List<object>(); var days = endDate.DayNumber-startDate.DayNumber+1;
        for(var day=startDate;day<=endDate;)
        {
            var next=days>100?new DateOnly(day.Year,day.Month,1).AddMonths(1):day.AddDays(days>32?7:1);if(next>endDate.AddDays(1))next=endDate.AddDays(1);
            var left=Bound(day);var right=Bound(next);
            buckets.Add(new { date=day.ToString("yyyy-MM-dd"), label=day.ToString(days>100?"MMM yy":"dd MMM",CultureInfo.InvariantCulture),
                members=registrants.Count(m=>m.joined<right), newMembers=registrants.Count(m=>m.joined>=left&&m.joined<right),
                organizations=tenant.CreatedAt>=left&&tenant.CreatedAt<right?1:0,
                events=events.Count(e=>e.StartsAt>=left&&e.StartsAt<right), attendance=attendance.Where(c=>c.At>=left&&c.At<right).Select(c=>c.PersonId).Distinct().Count() });
            day=next;
        }
        var completed = requests.Where(r=>r.Status is "Approved" or "Rejected" && r.UpdatedAt>=start && r.UpdatedAt<end).ToList();
        var renewalEligible = members.Count(m=>m.EndsAt>=start&&m.EndsAt<end)+renewals.Count;
        var clubPreview = clubs.OrderByDescending(c=>c.CreatedAt).ThenBy(c=>c.Name).Take(4).Select(c=>new {
            c.Id,c.Name,c.Status,region=nodeById.GetValueOrDefault(Ancestor(c.Id,"Region"))?.Name??"Unassigned",
            district=nodeById.GetValueOrDefault(Ancestor(c.Id,"District"))?.Name??"Unassigned",
            members=active.Where(m=>m.ClubId==c.Id).Select(m=>m.PersonId).Distinct().Count(),officers=current.Count(o=>o.OrgId==c.Id),href="/app/clubs/"+c.Id });
        var response = new { snapshotAt=now, scope=new { tenant.Id,tenant.Name,authorization="PlatformAdmin",context="Impact International · authorized single organization" },
            dates=new { range=selected,from=startDate,to=endDate,previousFrom=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(priorStart,zone).DateTime),previousTo=startDate.AddDays(-1),timeZone=tenant.TimeZone },
            metrics,priorities,trend=buckets,
            clubStatus=clubs.GroupBy(c=>c.Status).Select(g=>new { name=g.Key,count=g.Count() }),
            memberStatus=members.GroupBy(m=>m.Status).Select(g=>new { name=g.Key,count=g.Select(m=>m.PersonId).Distinct().Count() }),
            regions=Distribution("Region"),districts=Distribution("District"),
            pipeline=requests.GroupBy(r=>r.Status).Select(g=>new { name=g.Key,count=g.Count() }),
            structure=orgs.GroupBy(o=>o.level).Select(g=>new { name=g.Key,count=g.Count() }),
            clubs=clubPreview, organizations=orgs.Where(o=>o.ParentId==null).Select(o=>new { tenant.Id,tenant.Name,logo=string.IsNullOrEmpty(o.Logo)?"/brand/impact-logo.webp":o.Logo,status=o.Status,registration=o.Registration,regions=orgs.Count(o=>o.level=="Region"),districts=orgs.Count(o=>o.level=="District"),clubs=clubs.Count,members=registrants.Count }),
            health=new { engagement,participated,activeMembers=activeIds.Count,clubActivation=Rate(clubs.Count(c=>c.Status=="Active"),clubs.Count),officerCoverage=mandatorySlots.Count==0?(decimal?)null:Rate(occupied,mandatorySlots.Count),mandatoryPositions=mandatorySlots.Count,occupiedPositions=occupied,
                renewalRate=renewalEligible==0?(decimal?)null:Rate(renewals.Count,renewalEligible),renewals=renewals.Count,renewalOpportunities=renewalEligible,pendingGovernance=openRequests,
                averageApprovalDays=completed.Count==0?(double?)null:Math.Round(completed.Average(r=>(r.UpdatedAt-r.CreatedAt).TotalDays),1),
                definition="Engagement = distinct current active members with non-voided event check-ins in the selected period / current active members. Officer coverage = filled mandatory positions / mandatory positions. Renewal rate = renewal records / (renewal records + memberships with current end date in range); it is an operational indicator, not a historical cohort reconstruction. Approval time uses completed request UpdatedAt minus CreatedAt." },
            comparisonPolicy="KPI values are current snapshots unless labeled as period activity. Comparisons explicitly describe activity in equal-length current and previous periods; they are not invented historical snapshots. Joined this month compares calendar months. Null health values mean no denominator or completed decisions. Charts and distributions use actual records only." };
        if(format=="csv")
        {
            string Cell(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
            var csv="Metric,Current value,Period activity,Previous activity,Comparison\r\n"+string.Join("\r\n",metrics.Select(m=>string.Join(",",Cell(m.Label),m.Value.ToString(CultureInfo.InvariantCulture),m.PeriodValue.ToString(CultureInfo.InvariantCulture),m.Previous.ToString(CultureInfo.InvariantCulture),Cell(m.Comparison))));
            // Authentication audit persists independently of this read-only snapshot.
            await snapshot.CommitAsync();await a.Log(w,"platform.dashboard.export",tenant.Id.ToString());
            return Results.File(Encoding.UTF8.GetBytes(csv),"text/csv","impact-platform-dashboard.csv");
        }
        Check(string.IsNullOrEmpty(format),"Choose CSV for dashboard export.");
        return Results.Ok(response);
    }
}
