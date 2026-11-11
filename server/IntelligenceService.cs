using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record MetricDefinition(
    string Id,
    string Name,
    string Description,
    string Numerator,
    string Denominator,
    string Filters,
    string Scope,
    string Period,
    string Method,
    string Freshness,
    string Privacy
);

public record IntelligenceMetric(
    string Id,
    string Name,
    decimal? Value,
    string Unit,
    string? Unavailable = null,
    decimal? Numerator = null,
    decimal? Denominator = null
);

public class IntelligenceCohort
{
    public int Opening { get; set; }
    public int Retained { get; set; }
    public int UnknownHistory { get; set; }
}

public class IntelligenceService(
    Access a,
    OperationsService operations,
    FinanceService finance,
    EventService events
)
{
    public Access A => a;
    public Db Db => a.Db;
    public OperationsService Operations => operations;
    public FinanceService Finance => finance;
    public EventService Events => events;
    public static readonly string[] DuesPurposes =
    [
        "ClubDues",
        "AssociationDues",
        "DistrictDues",
        "NationalDues",
        "InternationalDues",
        "JoiningFee",
        "Renewal",
    ];
    public static readonly MetricDefinition[] Catalog =
    [
        new(
            "active-memberships",
            "Active affiliations",
            "Current active, unexpired membership records; one person can have several.",
            "Active affiliations",
            "Not a ratio",
            "Status Active and EndsAt in future/null",
            "Authorized current subtree",
            "Current snapshot",
            "Count membership IDs",
            "Live SQL per request",
            "members.view"
        ),
        new(
            "active-people",
            "Unique active people",
            "Distinct people with at least one active affiliation in scope.",
            "Distinct PersonId",
            "Not a ratio",
            "Active/unexpired",
            "Authorized current subtree",
            "Current snapshot",
            "COUNT DISTINCT PersonId; never sum child person counts",
            "Live SQL",
            "members.view"
        ),
        new(
            "joined",
            "Affiliation starts",
            "Recorded affiliation start dates, not necessarily first ever network join.",
            "Membership IDs joined in range",
            "Not a ratio",
            "JoinedAt in range",
            "Membership owning club",
            "Selected interval",
            "Count membership IDs",
            "Live SQL",
            "members.view"
        ),
        new(
            "retention",
            "Observed cohort retention",
            "People active in recorded opening history who remain active in scope. Unknown opening history is disclosed.",
            "Opening people still active in scope now",
            "Observed active opening cohort",
            "Latest effective history before start; valid EndsAt",
            "Owning clubs; transfer source/destination records preserved",
            "Opening date to current snapshot",
            "Distinct PersonId intersection; null if no observed cohort",
            "Live SQL",
            "members.view; incomplete history is not inferred"
        ),
        new(
            "clubs",
            "Clubs",
            "Current organizations at levels explicitly marked IsClub.",
            "Club organization IDs",
            "Not a ratio",
            "Visible subtree",
            "Configured hierarchy, not level name assumptions",
            "Current snapshot",
            "Count IDs once",
            "Live SQL",
            "organization.view or clubs.view"
        ),
        new(
            "events",
            "Events organized",
            "Published events starting in selected period.",
            "Published event IDs",
            "Not a ratio",
            "Publication Published and StartsAt in range",
            "Owning event organization; staff/reporter authority",
            "Selected interval",
            "Count distinct event IDs",
            "Live SQL",
            "events.reports; no private event details"
        ),
        new(
            "attendance",
            "Event attendance rate",
            "Unique nonvoid event-level checked-in registrations / confirmed registrations in selected event cohort.",
            "Distinct checked-in RegistrationId",
            "Confirmed registration IDs",
            "Published owned events in interval; nonvoid SessionId null checkins",
            "Owning organization",
            "Selected event cohort",
            "100*N/D; null for empty denominator",
            "Live SQL",
            "events.reports"
        ),
        new(
            "projects",
            "Community projects",
            "Approved/active/completed owner projects; partner rows are not counted again.",
            "Distinct owned ProjectId",
            "Not a ratio",
            "Exclude Draft/Submitted/Rejected",
            "Lead organization only",
            "Selected start interval",
            "Count project IDs once",
            "Live SQL",
            "operations.view"
        ),
        new(
            "verified-hours",
            "Verified volunteer hours",
            "Only independently verified actual hours; submitted/planned hours excluded.",
            "Sum Hours",
            "Not a ratio",
            "Status Verified; StartsAt in range",
            "Hour-owning organization",
            "Selected interval",
            "Decimal sum",
            "Live SQL",
            "operations.view; no volunteer identity exposed"
        ),
        new(
            "impact",
            "Verified impact observations",
            "Verified non-estimate outcomes, grouped by metric/unit. People observations are not deduplicated beneficiaries.",
            "Sum reviewed observations",
            "Not a ratio",
            "Status Verified and Estimate false",
            "Project lead ownership only",
            "Project start interval",
            "Sum each observation once by metric",
            "Live SQL",
            "operations.view; not unique beneficiaries"
        ),
        new(
            "leadership",
            "Required office coverage",
            "Mandatory active position slots with a current approved holder.",
            "Occupied required org-position pairs",
            "Configured required org-position pairs",
            "Position LevelId matches org; current approved appointment",
            "Visible owning organizations",
            "Current snapshot",
            "100*N/D; null if no required slots",
            "Live SQL",
            "leadership.view"
        ),
        new(
            "delivery",
            "Communication delivery rate",
            "Delivered recipient-channel rows / attempted recipient-channel rows. Not unique human reach.",
            "Mails Delivered or SentAt",
            "Attempted, sent or dead-letter rows",
            "CreatedAt in interval; exclude suppressed",
            "Authorized sending organization",
            "Selected interval",
            "100*N/D; null when no attempts",
            "Live SQL",
            "communications.reports"
        ),
        new(
            "governance",
            "Governance completion",
            "Approved shared requests / terminal approved or rejected requests. Not a full regulatory compliance score.",
            "Approved requests",
            "Approved plus Rejected requests",
            "CreatedAt in range; authorized request visibility",
            "Visible organizations and entity grants",
            "Selected interval",
            "100*N/D; null if none",
            "Live SQL",
            "governance.approve/reports plus source permissions"
        ),
        new(
            "dues",
            "Outstanding dues",
            "Current unpaid dues by explicitly authorized entity/native currency.",
            "Total-Paid-Credited for issued dues",
            "Issued dues total minus credits",
            "Dues/joining/renewal purposes; issued only",
            "Explicit finance entity grants; never hierarchy alone",
            "Current snapshot",
            "Per-entity decimal aggregation; no bank consolidation",
            "Live SQL",
            "finance.view plus explicit entity grant"
        ),
    ];

    public async Task<Assignment> Workspace(string permission = "intelligence.view") =>
        await A.Workspace(permission);

    public async Task<Assignment> Scoped(Guid? org, string permission = "intelligence.view")
    {
        var w = await Workspace(permission);
        if (org != null && org != w.OrgId)
        {
            Check(
                await A.VisibleIds(w).ContainsAsync(org.Value),
                "Organizational scope unavailable.",
                404
            );
            w = new Assignment
            {
                Id = w.Id,
                UserId = w.UserId,
                TenantId = w.TenantId,
                OrgId = org.Value,
                RoleId = w.RoleId,
                MembershipId = w.MembershipId,
                AppointmentId = w.AppointmentId,
            };
        }
        return w;
    }

    public void Audit(Assignment w, string action, string query, string tool, int results = 0) =>
        Db.IntelligenceAudits.Add(
            new()
            {
                TenantId = w.TenantId,
                OrgId = w.OrgId,
                UserId = w.UserId,
                Action = action,
                QueryHash = Hash(query),
                Tool = tool,
                Results = results,
            }
        );

    public async Task<object> Dashboard(Guid? org, DateOnly? from, DateOnly? to)
    {
        var w = await Scoped(org);
        var grants = await A.Grants(w);
        var ids = A.VisibleIds(w);
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(
            (from ?? new DateOnly(now.Year, 1, 1)).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero
        );
        var end = new DateTimeOffset(
            (to ?? DateOnly.FromDateTime(now.UtcDateTime)).AddDays(1).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero
        );
        Check(
            start < end && end - start <= TimeSpan.FromDays(370) && end <= now.AddDays(1),
            "Choose a past/current reporting interval up to 370 days."
        );
        var metrics = new List<IntelligenceMetric>();
        var alerts = new List<object>();
        var members = Db.Memberships.Where(m => m.TenantId == w.TenantId && ids.Contains(m.ClubId));
        var active = members.Where(m =>
            m.Status == "Active" && (m.EndsAt == null || m.EndsAt > now)
        );
        if (grants.Contains("members.view"))
        {
            metrics.Add(
                new(
                    "active-memberships",
                    "Active affiliations",
                    await active.CountAsync(),
                    "affiliations"
                )
            );
            metrics.Add(
                new(
                    "active-people",
                    "Unique active people",
                    await active.Select(m => m.PersonId).Distinct().CountAsync(),
                    "people"
                )
            );
            metrics.Add(
                new(
                    "joined",
                    "Affiliation starts",
                    await members.CountAsync(m => m.JoinedAt >= start && m.JoinedAt < end),
                    "starts"
                )
            );
            var cohort = await Db
                .Database.SqlQuery<IntelligenceCohort>(
                    $"""
                    WITH RECURSIVE scope AS(SELECT "Id" FROM "Orgs" WHERE "Id"={w.OrgId} AND "TenantId"={w.TenantId} UNION ALL SELECT o."Id" FROM "Orgs" o JOIN scope s ON o."ParentId"=s."Id" WHERE o."TenantId"={w.TenantId}), ms AS(SELECT * FROM "Memberships" WHERE "TenantId"={w.TenantId} AND "ClubId" IN(SELECT "Id" FROM scope)), opening AS(SELECT DISTINCT ON(h."MembershipId") h."MembershipId",h."NewStatus",h."EndsAt" FROM "MembershipHistory" h JOIN ms m ON m."Id"=h."MembershipId" WHERE h."EffectiveAt"<={start} ORDER BY h."MembershipId",h."EffectiveAt" DESC,h."RecordedAt" DESC), initial AS(SELECT DISTINCT m."PersonId" FROM ms m JOIN opening o ON o."MembershipId"=m."Id" WHERE o."NewStatus"='Active' AND(o."EndsAt" IS NULL OR o."EndsAt">{start})), current_people AS(SELECT DISTINCT "PersonId" FROM ms WHERE "Status"='Active' AND("EndsAt" IS NULL OR "EndsAt">{now})) SELECT(SELECT count(*)::int FROM initial) AS "Opening",(SELECT count(*)::int FROM initial i JOIN current_people c ON c."PersonId"=i."PersonId") AS "Retained",(SELECT count(*)::int FROM ms m WHERE m."JoinedAt"<={start} AND NOT EXISTS(SELECT 1 FROM opening o WHERE o."MembershipId"=m."Id")) AS "UnknownHistory"
                    """
                )
                .SingleAsync();
            metrics.Add(
                new(
                    "retention",
                    "Observed cohort retention",
                    cohort.Opening == 0
                        ? null
                        : decimal.Round(100m * cohort.Retained / cohort.Opening, 2),
                    "%",
                    cohort.Opening == 0 ? "No observed opening cohort" : null,
                    cohort.Retained,
                    cohort.Opening
                )
            );
            if (cohort.UnknownHistory > 0)
                alerts.Add(
                    new
                    {
                        kind = "data-coverage",
                        title = "Opening membership history is incomplete",
                        detail = cohort.UnknownHistory
                            + " affiliations lack an observed opening state; no state was invented.",
                    }
                );
        }
        else
            foreach (
                var id in new[] { "active-memberships", "active-people", "joined", "retention" }
            )
                metrics.Add(
                    new(
                        id,
                        Catalog.Single(x => x.Id == id).Name,
                        null,
                        "",
                        "Membership reporting permission required"
                    )
                );
        var canOrg = grants.Contains("organization.view") || grants.Contains("clubs.view");
        object? organizations = null;
        object? levels = null;
        if (canOrg)
        {
            var oq = A.Visible(w);
            metrics.Add(
                new(
                    "clubs",
                    "Clubs",
                    await oq.CountAsync(o => Db.Levels.Any(l => l.Id == o.LevelId && l.IsClub)),
                    "clubs"
                )
            );
            metrics.Add(
                new(
                    "countries",
                    "Countries represented",
                    await oq.Where(o => o.Country != "")
                        .Select(o => o.Country)
                        .Distinct()
                        .CountAsync(),
                    "countries"
                )
            );
            levels = await (
                from o in oq
                join l in Db.Levels on o.LevelId equals l.Id
                group o by new
                {
                    l.Id,
                    l.Name,
                    l.IsClub,
                } into g
                select new
                {
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.IsClub,
                    count = g.Count(),
                }
            ).ToListAsync();
            organizations = await oq.Where(o => o.Id == w.OrgId || o.ParentId == w.OrgId)
                .OrderBy(o => o.Name)
                .Take(100)
                .Select(o => new
                {
                    o.Id,
                    o.Name,
                    o.ParentId,
                    o.LevelId,
                    o.Country,
                    o.Status,
                })
                .ToListAsync();
        }
        var eq = Db.Events.Where(e =>
            e.TenantId == w.TenantId
            && ids.Contains(e.OrgId)
            && e.Publication == "Published"
            && e.StartsAt >= start
            && e.StartsAt < end
        );
        if (grants.Contains("events.reports"))
        {
            var reg = Db.EventRegistrations.Where(r =>
                eq.Any(e => e.Id == r.EventId) && r.Status == "Confirmed"
            );
            var den = await reg.CountAsync();
            var num = await Db
                .EventCheckins.Where(c =>
                    eq.Any(e => e.Id == c.EventId)
                    && !c.Voided
                    && c.SessionId == null
                    && reg.Any(r => r.Id == c.RegistrationId)
                )
                .Select(c => c.RegistrationId)
                .Distinct()
                .CountAsync();
            metrics.Add(new("events", "Events organized", await eq.CountAsync(), "events"));
            metrics.Add(
                new(
                    "attendance",
                    "Event attendance rate",
                    den == 0 ? null : decimal.Round(100m * num / den, 2),
                    "%",
                    den == 0 ? "No confirmed registrations" : null,
                    num,
                    den
                )
            );
        }
        object? impact = null;
        if (grants.Contains("operations.view"))
        {
            var projects = Db.ServiceProjects.Where(p =>
                p.TenantId == w.TenantId
                && ids.Contains(p.OrgId)
                && new[] { "Approved", "Active", "Completed" }.Contains(p.Status)
                && p.StartsAt >= start
                && p.StartsAt < end
            );
            metrics.Add(
                new("projects", "Community projects", await projects.CountAsync(), "projects")
            );
            metrics.Add(
                new(
                    "verified-hours",
                    "Verified volunteer hours",
                    await Db
                        .VolunteerHours.Where(h =>
                            h.TenantId == w.TenantId
                            && ids.Contains(h.OrgId)
                            && h.Status == "Verified"
                            && h.StartsAt >= start
                            && h.StartsAt < end
                        )
                        .SumAsync(h => h.Hours),
                    "hours"
                )
            );
            impact = await (
                from i in Db.ProjectImpacts
                join m in Db.ImpactMetrics on i.MetricId equals m.Id
                where
                    i.TenantId == w.TenantId
                    && projects.Any(p => p.Id == i.ProjectId)
                    && i.Status == "Verified"
                    && !i.Estimate
                group i by new
                {
                    m.Id,
                    m.Name,
                    m.Unit,
                } into g
                select new
                {
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.Unit,
                    value = g.Sum(i => i.Value),
                }
            )
                .Take(100)
                .ToListAsync();
        }
        if (grants.Contains("leadership.view"))
        {
            var slots =
                from o in A.Visible(w)
                from p in Db.OfficerPositions
                where p.TenantId == w.TenantId && p.Active && p.Mandatory && p.LevelId == o.LevelId
                select new { o.Id, Position = p.Id };
            var den = await slots.CountAsync();
            var num = await slots.CountAsync(s =>
                GovernanceService
                    .Current(Db)
                    .Any(p => p.OrgId == s.Id && p.PositionId == s.Position)
            );
            metrics.Add(
                new(
                    "leadership",
                    "Required office coverage",
                    den == 0 ? null : decimal.Round(100m * num / den, 2),
                    "%",
                    den == 0 ? "No mandatory positions configured" : null,
                    num,
                    den
                )
            );
            var expiring = await GovernanceService
                .Current(Db)
                .CountAsync(p => ids.Contains(p.OrgId) && p.EndsAt <= now.AddDays(30));
            if (expiring > 0)
                alerts.Add(
                    new
                    {
                        kind = "rule",
                        title = "Officer terms expire within 30 days",
                        detail = expiring
                            + " current appointments. This is a dated rule, not a prediction.",
                    }
                );
        }
        if (grants.Contains("communications.reports"))
        {
            var mails = Db.Mails.Where(m =>
                m.TenantId == w.TenantId
                && m.OrgId != null
                && ids.Contains(m.OrgId.Value)
                && m.CreatedAt >= start
                && m.CreatedAt < end
                && !m.Suppressed
                && (m.Attempts > 0 || m.SentAt != null || m.DeadLetter)
            );
            var den = await mails.CountAsync();
            var num = await mails.CountAsync(m =>
                m.SentAt != null || m.DeliveryStatus == "Delivered"
            );
            metrics.Add(
                new(
                    "delivery",
                    "Communication delivery rate",
                    den == 0 ? null : decimal.Round(100m * num / den, 2),
                    "%",
                    den == 0 ? "No attempted deliveries" : null,
                    num,
                    den
                )
            );
        }
        if (grants.Contains("governance.approve") || grants.Contains("governance.reports"))
        {
            var requests = await operations.Governance.Requests(w);
            requests = requests.Where(r =>
                r.CreatedAt >= start
                && r.CreatedAt < end
                && new[] { "Approved", "Rejected" }.Contains(r.Status)
            );
            var den = await requests.CountAsync();
            var num = await requests.CountAsync(r => r.Status == "Approved");
            metrics.Add(
                new(
                    "governance",
                    "Governance completion",
                    den == 0 ? null : decimal.Round(100m * num / den, 2),
                    "%",
                    den == 0 ? "No terminal requests" : null,
                    num,
                    den
                )
            );
        }
        var financeRows = new List<object>();
        if (grants.Contains("finance.view"))
        {
            var es = await Db
                .FinanceEntitys.Where(e =>
                    e.TenantId == w.TenantId
                    && e.Active
                    && ids.Contains(e.OrgId)
                    && Db.FinanceGrants.Any(g => g.EntityId == e.Id && g.UserId == w.UserId)
                )
                .OrderBy(e => e.Name)
                .Take(50)
                .ToListAsync();
            foreach (var e in es)
            {
                await finance.Entity(e.Id);
                var invoices = Db.FinanceInvoices.Where(i =>
                    i.EntityId == e.Id && i.State == "Issued"
                );
                financeRows.Add(
                    new
                    {
                        e.Id,
                        e.Name,
                        e.Currency,
                        outstanding = await invoices.SumAsync(i => i.Total - i.Paid - i.Credited),
                        duesOutstanding = await invoices
                            .Where(i => DuesPurposes.Contains(i.Purpose))
                            .SumAsync(i => i.Total - i.Paid - i.Credited),
                        policy = "Independent native-currency entity books; not consolidated cash.",
                    }
                );
            }
        }
        object? trend = null;
        object? clubs = null;
        if (grants.Contains("members.view"))
        {
            trend = await members
                .Where(m => m.JoinedAt >= start && m.JoinedAt < end)
                .GroupBy(m => new { m.JoinedAt.Year, m.JoinedAt.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    affiliations = g.Count(),
                    uniquePeople = g.Select(m => m.PersonId).Distinct().Count(),
                })
                .OrderBy(g => g.Year)
                .ThenBy(g => g.Month)
                .ToListAsync();
            clubs = await active
                .GroupBy(m => new { m.ClubId, m.Club.Name })
                .Select(g => new
                {
                    id = g.Key.ClubId,
                    name = g.Key.Name,
                    affiliations = g.Count(),
                    uniquePeople = g.Select(m => m.PersonId).Distinct().Count(),
                })
                .OrderByDescending(g => g.affiliations)
                .Take(50)
                .ToListAsync();
        }
        return new
        {
            scope = w.OrgId,
            tenant = w.TenantId,
            from = start,
            to = end,
            generatedAt = now,
            metrics,
            organizations,
            levels,
            trend,
            clubs,
            impact,
            finance = financeRows,
            alerts,
            definitions = "/api/v1/intelligence/metrics",
            notes = "Stock metrics are current snapshots; flow metrics use selected dates. Scope follows membership-owning records and configured hierarchy. History coverage is disclosed; no retroactive organizational tree is invented. Joint projects count lead ownership once. Verified people observations are not unique beneficiaries. No finance consolidation.",
        };
    }
}
