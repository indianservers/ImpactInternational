using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static partial class OperationsEndpoints
{
    static void MapIntelligence(RouteGroupBuilder api)
    {
        api.MapGet(
            "/operations/options",
            async (OperationsService s, string? personSearch = null, string? orgSearch = null) =>
            {
                Check(
                    (personSearch?.Length ?? 0) <= 100 && (orgSearch?.Length ?? 0) <= 100,
                    "Search too long."
                );
                var w = await s.Workspace();
                var grants = await s.A.Grants(w);
                var coordinate = grants.Any(p =>
                    new[]
                    {
                        "operations.manage",
                        "operations.documents",
                        "operations.certificates",
                        "members.view",
                    }.Contains(p)
                );
                return new
                {
                    organizations = await s
                        .A.Visible(w)
                        .Where(o =>
                            orgSearch == null || EF.Functions.ILike(o.Name, "%" + orgSearch + "%")
                        )
                        .OrderBy(o => o.Name)
                        .Select(o => new { o.Id, o.Name })
                        .Take(200)
                        .ToListAsync(),
                    people = await s
                        .Db.Persons.Where(p =>
                            s.Db.Memberships.Any(m =>
                                m.PersonId == p.Id
                                && m.TenantId == w.TenantId
                                && s.A.VisibleIds(w).Contains(m.ClubId)
                            )
                        )
                        .Where(p =>
                            personSearch == null
                            || EF.Functions.ILike(p.DisplayName, "%" + personSearch + "%")
                        )
                        .Where(p => coordinate || p.UserId == w.UserId)
                        .OrderBy(p => p.DisplayName)
                        .Select(p => new
                        {
                            p.Id,
                            p.DisplayName,
                            p.UserId,
                        })
                        .Take(200)
                        .ToListAsync(),
                    membershipCategories = await s
                        .Db.MembershipCategories.Where(c => c.TenantId == w.TenantId && c.Active)
                        .Select(c => new { c.Id, c.Name })
                        .ToListAsync(),
                    meetingTypes = new[]
                    {
                        "Regular club meeting",
                        "Board meeting",
                        "Executive committee meeting",
                        "Membership committee meeting",
                        "Finance committee meeting",
                        "District review meeting",
                        "Regional meeting",
                        "National executive meeting",
                        "International committee meeting",
                        "Special general meeting",
                        "Annual general meeting",
                        "Emergency meeting",
                        "Custom",
                    },
                    projectCategories = new[]
                    {
                        "Health",
                        "Blood donation",
                        "Education",
                        "Environment",
                        "Food distribution",
                        "Disaster relief",
                        "Community infrastructure",
                        "Youth development",
                        "Skill training",
                        "Fundraising",
                        "Social awareness",
                        "Custom",
                    },
                };
            }
        );
        api.MapGet(
            "/operations/history/{id:guid}",
            async (Guid id, OperationsService s) =>
            {
                var w = await s.Workspace();
                if (await s.Db.ClubMeetings.AnyAsync(m => m.Id == id))
                    await s.Meeting(id);
                else if (await s.Db.ServiceProjects.AnyAsync(p => p.Id == id))
                    await s.Project(id);
                else if (await s.Db.OperationTasks.AnyAsync(t => t.Id == id))
                    await s.TaskRecord(id);
                else
                    throw new ApiError(404, "History unavailable.");
                return await s
                    .Db.OperationRevisions.Where(r => r.RecordId == id && r.TenantId == w.TenantId)
                    .OrderByDescending(r => r.UpdatedAt)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/operations/analytics",
            async (OperationsService s) =>
            {
                var w = await s.Workspace();
                var projects = s.Db.ServiceProjects.Where(p =>
                    p.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(p.OrgId)
                );
                var tasks = s.Db.OperationTasks.Where(t =>
                    t.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(t.OrgId)
                );
                var hours = s.Db.VolunteerHours.Where(h =>
                    h.TenantId == w.TenantId
                    && s.A.VisibleIds(w).Contains(h.OrgId)
                    && h.Status == "Verified"
                );
                var impact = s.Db.ProjectImpacts.Where(i =>
                    i.TenantId == w.TenantId
                    && s.A.VisibleIds(w).Contains(i.OrgId)
                    && i.Status == "Verified"
                    && !i.Estimate
                );
                var groups = await impact
                    .GroupBy(i => i.MetricId)
                    .Select(g => new
                    {
                        metricId = g.Key,
                        sum = g.Sum(i => i.Value),
                        maximum = g.Max(i => i.Value),
                        average = g.Average(i => i.Value),
                        count = g.Count(),
                    })
                    .ToListAsync();
                var ids = groups.Select(g => g.metricId).ToArray();
                var metrics = await s
                    .Db.ImpactMetrics.Where(m => ids.Contains(m.Id))
                    .ToDictionaryAsync(m => m.Id);
                var own = await s
                    .Db.Persons.Where(p => p.UserId == w.UserId)
                    .Select(p => (Guid?)p.Id)
                    .SingleOrDefaultAsync();
                var user = w.UserId;
                var meetingQuery = await s.Meetings(w);
                return new
                {
                    upcomingMeetings = await meetingQuery.CountAsync(m =>
                        m.StartsAt > DateTimeOffset.UtcNow && m.Status == "Scheduled"
                    ),
                    activeProjects = await projects.CountAsync(p =>
                        p.Status == "Planning" || p.Status == "In Progress"
                    ),
                    completedProjects = await projects.CountAsync(p =>
                        p.Status == "Completed"
                        || p.Status == "Impact Verified"
                        || p.Status == "Archived"
                    ),
                    jointProjects = await projects.CountAsync(p =>
                        s.Db.ProjectOrganizations.Count(o => o.ProjectId == p.Id) > 1
                    ),
                    openTasks = await tasks.CountAsync(t =>
                        t.Status != "Completed" && t.Status != "Cancelled"
                    ),
                    verifiedHours = await hours.SumAsync(h => (decimal?)h.Hours) ?? 0,
                    volunteersEngaged = await hours.Select(h => h.PersonId).Distinct().CountAsync(),
                    pendingImpact = await s.Db.ProjectImpacts.CountAsync(i =>
                        i.TenantId == w.TenantId
                        && s.A.VisibleIds(w).Contains(i.OrgId)
                        && i.Status == "Submitted"
                        && !i.Estimate
                    ),
                    impact = groups.Select(g => new
                    {
                        g.metricId,
                        name = metrics[g.metricId].Name,
                        unit = metrics[g.metricId].Unit,
                        aggregation = metrics[g.metricId].Aggregation,
                        value = metrics[g.metricId].Aggregation switch
                        {
                            "Maximum" => g.maximum,
                            "Average" => g.average,
                            "Count" => g.count,
                            _ => g.sum,
                        },
                    }),
                    projectsByCategory = await projects
                        .GroupBy(p => p.Category)
                        .Select(g => new { category = g.Key, count = g.Count() })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/members/{id:guid}/achievements",
            async (Guid id, OperationsService s) =>
            {
                var w = await s.Workspace();
                var membership = await s.Db.Memberships.SingleOrDefaultAsync(m =>
                    m.Id == id && m.TenantId == w.TenantId
                );
                Check(membership != null, "Member unavailable.", 404);
                var own = await s.IsPerson(membership.PersonId, w.UserId);
                Check(
                    own
                        || (await s.A.Grants(w)).Contains("members.view")
                            && await s.A.VisibleIds(w).ContainsAsync(membership.ClubId),
                    "Member unavailable.",
                    404
                );
                var hours = s.Db.VolunteerHours.Where(h =>
                    h.PersonId == membership.PersonId
                    && h.TenantId == w.TenantId
                    && h.Status == "Verified"
                    && (own || s.A.VisibleIds(w).Contains(h.OrgId))
                );
                return new
                {
                    verifiedHours = await hours.SumAsync(h => (decimal?)h.Hours) ?? 0,
                    projects = await hours.Select(h => h.ProjectId).Distinct().CountAsync(),
                    service = await hours
                        .OrderByDescending(h => h.StartsAt)
                        .Select(h => new
                        {
                            h.ProjectId,
                            h.Hours,
                            h.StartsAt,
                        })
                        .Take(100)
                        .ToListAsync(),
                    certificates = await s
                        .Db.IssuedCertificates.Where(c =>
                            c.PersonId == membership.PersonId
                            && c.TenantId == w.TenantId
                            && c.Status == "Issued"
                            && (own || s.A.VisibleIds(w).Contains(c.OrgId))
                        )
                        .Select(c => new
                        {
                            c.Id,
                            c.Number,
                            c.SourceType,
                            c.IssuedAt,
                        })
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );
    }
}
