using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record TaskInput(
    Guid OrgId,
    string Title,
    string Description = "",
    string SourceType = "Administrative",
    Guid? SourceId = null,
    Guid? PersonId = null,
    Guid? CommitteeId = null,
    string Priority = "Normal",
    DateTimeOffset? StartsAt = null,
    DateTimeOffset? DueAt = null,
    Guid[]? Dependencies = null,
    long Version = 1
);

public record ProjectInput(
    Guid OrgId,
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    Guid ManagerId,
    string Category = "Community",
    string Description = "",
    string Objectives = "",
    string Location = "",
    Guid? CommitteeId = null,
    int TargetBeneficiaries = 0,
    int PlannedVolunteers = 0,
    decimal PlannedHours = 0,
    decimal EstimatedBudget = 0,
    string Currency = "USD",
    Guid[]? ParticipatingClubs = null,
    long Version = 1
);

public record ActivityInput(
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Description = ""
);

public record ImpactCorrectionInput(decimal Value, string Evidence, string Reason, long Version);

public record ImpactInput(
    Guid MetricId,
    string SourceKey,
    decimal Value,
    string Evidence = "",
    bool Estimate = false
);

public record MetricInput(
    Guid OrgId,
    string Name,
    string Unit = "people",
    string Description = "",
    string Aggregation = "Sum",
    string Category = "Community",
    bool EvidenceRequired = true
);

public static partial class OperationsEndpoints
{
    static void MapTasks(RouteGroupBuilder api)
    {
        api.MapGet(
            "/tasks",
            async (
                OperationsService s,
                string? status = null,
                Guid? sourceId = null,
                bool mine = false,
                bool overdue = false,
                Guid? after = null
            ) =>
            {
                var w = await s.Workspace();
                var manage = (await s.A.Grants(w)).Contains("operations.manage");
                var p = await s
                    .Db.Persons.Where(p => p.UserId == w.UserId)
                    .Select(p => (Guid?)p.Id)
                    .SingleOrDefaultAsync();
                var q = s.Db.OperationTasks.Where(t =>
                    t.TenantId == w.TenantId
                    && (s.A.VisibleIds(w).Contains(t.OrgId) || t.PersonId == p)
                    && (manage && !mine || t.PersonId == p)
                );
                if (status != null)
                    q = q.Where(t => t.Status == status);
                if (sourceId != null)
                    q = q.Where(t => t.SourceId == sourceId);
                if (overdue)
                    q = q.Where(t =>
                        t.DueAt < DateTimeOffset.UtcNow
                        && t.Status != "Completed"
                        && t.Status != "Cancelled"
                    );
                if (after != null)
                    q = q.Where(t => t.Id.CompareTo(after) > 0);
                var visibleMeetings = await s.Meetings(w);
                q = q.Where(t =>
                    t.SourceType != "Meeting" || visibleMeetings.Any(m => m.Id == t.SourceId)
                );
                return await q.OrderBy(t => t.Id).Take(100).ToListAsync();
            }
        );
        api.MapPost(
            "/tasks",
            async (TaskInput i, OperationsService s) =>
            {
                var w = await s.Scope(i.OrgId, true);
                await ValidateTask(i, s, w);
                var t = new OperationTask
                {
                    TenantId = w.TenantId,
                    OrgId = i.OrgId,
                    Title = i.Title,
                    Description = i.Description,
                    SourceType = i.SourceType,
                    SourceId = i.SourceId,
                    PersonId = i.PersonId,
                    CommitteeId = i.CommitteeId,
                    Priority = i.Priority,
                    StartsAt = i.StartsAt?.ToUniversalTime(),
                    DueAt = i.DueAt?.ToUniversalTime(),
                    Dependencies = Pack(i.Dependencies ?? []),
                    CreatorId = w.UserId,
                };
                s.Db.OperationTasks.Add(t);
                s.Audit(w, t, "task.created");
                await s.Notice(t.PersonId, "You have a new task: " + t.Title, t);
                await s.Db.SaveChangesAsync();
                return t;
            }
        );
        api.MapGet(
            "/tasks/{id:guid}",
            async (Guid id, OperationsService s) =>
            {
                var t = await s.TaskRecord(id);
                return new
                {
                    task = t,
                    comments = await s
                        .Db.OperationComments.Where(c => c.TaskId == id)
                        .OrderByDescending(c => c.UpdatedAt)
                        .Take(100)
                        .ToListAsync(),
                    history = await s
                        .Db.OperationRevisions.Where(h => h.RecordId == id)
                        .OrderByDescending(h => h.UpdatedAt)
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );
        api.MapPut(
            "/tasks/{id:guid}",
            async (Guid id, TaskInput i, OperationsService s) =>
            {
                var t = await s.TaskRecord(id);
                var w = await s.Scope(t.OrgId, true);
                Check(t.Version == i.Version && t.OrgId == i.OrgId, "Task changed.", 409);
                await ValidateTask(i, s, w);
                Check(!(i.Dependencies ?? []).Contains(id), "Task cannot depend on itself.");
                var visited = new HashSet<Guid> { id };
                async Task Walk(Guid dependency, int depth)
                {
                    Check(
                        depth < 50 && visited.Add(dependency),
                        "Dependency cycle or graph too deep."
                    );
                    var d = await s.Db.OperationTasks.SingleAsync(t => t.Id == dependency);
                    foreach (var x in Unpack<Guid[]>(d.Dependencies))
                        await Walk(x, depth + 1);
                    visited.Remove(dependency);
                }
                foreach (var d in i.Dependencies ?? [])
                    await Walk(d, 0);
                s.Revision(t, "Task", Pack(t), w.UserId);
                t.Title = i.Title;
                t.Description = i.Description;
                t.PersonId = i.PersonId;
                t.Priority = i.Priority;
                t.DueAt = i.DueAt?.ToUniversalTime();
                t.Dependencies = Pack(i.Dependencies ?? []);
                t.Version++;
                s.Audit(w, t, "task.updated");
                await s.Db.SaveChangesAsync();
                return t;
            }
        );
        api.MapPost(
            "/tasks/{id:guid}/status",
            async (Guid id, StateInput i, OperationsService s) =>
            {
                var t = await s.TaskRecord(id, true);
                var w = await s.Workspace();
                Check(t.Version == i.Version, "Task changed.", 409);
                Check(
                    new[] { "To Do", "In Progress", "Blocked", "Completed", "Cancelled" }.Contains(
                        i.Status
                    ),
                    "Invalid task status."
                );
                Check(i.Status != "Blocked" || i.Reason.Length >= 5, "Describe the blocker.");
                if (i.Status == "Completed")
                {
                    var deps = Unpack<Guid[]>(t.Dependencies);
                    Check(
                        !await s.Db.OperationTasks.AnyAsync(x =>
                            deps.Contains(x.Id) && x.Status != "Completed"
                        ),
                        "Complete dependencies first.",
                        409
                    );
                    Check(i.Reason.Length >= 5, "Completion evidence is required.");
                    t.Evidence = i.Reason;
                }
                s.Revision(t, "Task", Pack(t), w.UserId);
                t.Status = i.Status;
                t.Blocker = i.Status == "Blocked" ? i.Reason : "";
                t.Version++;
                s.Audit(w, t, "task.status", i.Reason);
                await s.Db.SaveChangesAsync();
                return t;
            }
        );
        api.MapPost(
            "/tasks/{id:guid}/comments",
            async (Guid id, TextInput i, OperationsService s) =>
            {
                var t = await s.TaskRecord(id);
                var w = await s.Workspace();
                Check(i.Body.Length is > 0 and <= 5000, "Invalid comment.");
                var c = new OperationComment
                {
                    TenantId = t.TenantId,
                    OrgId = t.OrgId,
                    TaskId = id,
                    AuthorId = w.UserId,
                    Body = i.Body,
                };
                s.Db.OperationComments.Add(c);
                await s.Db.SaveChangesAsync();
                return c;
            }
        );
    }

    static async Task ValidateTask(TaskInput i, OperationsService s, Assignment w)
    {
        OperationsService.Title(i.Title);
        Check(
            i.Description.Length <= 10000
                && (i.Dependencies?.Length ?? 0) <= 20
                && new[] { "Normal", "High", "Urgent", "Low" }.Contains(i.Priority),
            "Invalid task details."
        );
        Check(
            i.StartsAt == null || i.DueAt == null || i.StartsAt < i.DueAt,
            "Due date must follow start."
        );
        if (i.PersonId != null)
            await s.Person(i.PersonId.Value, w.TenantId);
        if (i.CommitteeId != null)
            await s.Governance.CommitteeAccess(i.CommitteeId.Value);
        if (i.SourceType != "Administrative")
        {
            Check(i.SourceId != null, "Source record required.");
            var id = i.SourceId!.Value;
            switch (i.SourceType)
            {
                case "Meeting":
                    var meeting = await s.Meeting(id, true);
                    Check(meeting.OrgId == i.OrgId, "Source ownership mismatch.");
                    if (meeting.Visibility == "Confidential" && i.PersonId != null)
                    {
                        var assigned = await s.Person(i.PersonId.Value, w.TenantId);
                        Check(
                            assigned.UserId == meeting.OrganizerId
                                || i.PersonId == meeting.ChairId
                                || i.PersonId == meeting.SecretaryId
                                || Unpack<Guid[]>(meeting.RequiredPeople).Contains(i.PersonId.Value)
                                || Unpack<Guid[]>(meeting.OptionalPeople)
                                    .Contains(i.PersonId.Value),
                            "Invite the assignee to the confidential meeting before assigning its tasks.",
                            403
                        );
                    }
                    break;
                case "Project":
                    Check(
                        (await s.Project(id, true)).OrgId == i.OrgId,
                        "Source ownership mismatch."
                    );
                    break;
                case "Committee":
                    var c = await s.Governance.CommitteeAccess(id);
                    Check(c.OrgId == i.OrgId, "Source ownership mismatch.");
                    break;
                case "Resolution":
                    Check(
                        await s.Db.GovernanceResolutions.AnyAsync(r =>
                            r.Id == id && r.TenantId == w.TenantId && r.OrgId == i.OrgId
                        ),
                        "Resolution unavailable."
                    );
                    break;
                case "Handover":
                    Check(
                        await s.Db.LeadershipHandovers.AnyAsync(r =>
                            r.Id == id && r.TenantId == w.TenantId && r.OrgId == i.OrgId
                        ),
                        "Handover unavailable."
                    );
                    break;
                default:
                    throw new ApiError(400, "Invalid task source.");
            }
        }
        foreach (var d in i.Dependencies ?? [])
        {
            var t = await s.TaskRecord(d);
            Check(t.OrgId == i.OrgId, "Dependencies must share task ownership.");
        }
    }

    static void MapProjects(RouteGroupBuilder api)
    {
        api.MapGet(
            "/projects",
            async (
                OperationsService s,
                string? status = null,
                string? search = null,
                Guid? after = null
            ) =>
            {
                var w = await s.Workspace();
                var q = s
                    .Db.ServiceProjects.AsNoTracking()
                    .Where(p =>
                        p.TenantId == w.TenantId
                        && (
                            s.A.VisibleIds(w).Contains(p.OrgId)
                            || (
                                new[]
                                {
                                    "Approved",
                                    "Planning",
                                    "In Progress",
                                    "Completed",
                                    "Impact Verified",
                                    "Archived",
                                    "On Hold",
                                    "Cancelled",
                                }.Contains(p.Status)
                                && s.Db.ProjectOrganizations.Any(o =>
                                    o.ProjectId == p.Id && s.A.VisibleIds(w).Contains(o.OrgId)
                                )
                            )
                        )
                    );
                if (status != null)
                    q = q.Where(p => p.Status == status);
                if (search != null)
                {
                    Check(search.Length <= 100, "Search too long.");
                    q = q.Where(p => EF.Functions.ILike(p.Name, "%" + search + "%"));
                }
                if (after != null)
                    q = q.Where(p => p.Id.CompareTo(after) > 0);
                return await q.OrderBy(p => p.Id)
                    .Take(100)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Category,
                        p.OrgId,
                        p.Status,
                        p.StartsAt,
                        p.EndsAt,
                        p.PlannedVolunteers,
                        p.TargetBeneficiaries,
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/projects",
            async (ProjectInput i, OperationsService s) =>
            {
                var w = await s.Scope(i.OrgId, true);
                OperationsService.Title(i.Name);
                OperationsService.Dates(i.StartsAt, i.EndsAt);
                await s.Person(i.ManagerId, w.TenantId);
                Check(
                    i.EstimatedBudget >= 0
                        && i.PlannedHours >= 0
                        && i.TargetBeneficiaries >= 0
                        && i.PlannedVolunteers >= 0
                        && i.Currency.Length == 3
                        && i.Description.Length <= 20000,
                    "Invalid project plan."
                );
                var clubs = (i.ParticipatingClubs ?? []).Append(i.OrgId).Distinct().ToArray();
                Check(clubs.Length <= 100, "Maximum 100 participating organizations.");
                foreach (var club in clubs)
                    await s.Governance.Org(club, "operations.manage");
                if (i.CommitteeId != null)
                    await s.Governance.CommitteeAccess(i.CommitteeId.Value);
                var p = new ServiceProject
                {
                    TenantId = w.TenantId,
                    OrgId = i.OrgId,
                    Name = i.Name,
                    Category = i.Category,
                    Description = i.Description,
                    Objectives = i.Objectives,
                    ManagerId = i.ManagerId,
                    CommitteeId = i.CommitteeId,
                    StartsAt = i.StartsAt.ToUniversalTime(),
                    EndsAt = i.EndsAt.ToUniversalTime(),
                    Location = i.Location,
                    TargetBeneficiaries = i.TargetBeneficiaries,
                    PlannedVolunteers = i.PlannedVolunteers,
                    PlannedHours = i.PlannedHours,
                    EstimatedBudget = i.EstimatedBudget,
                    Currency = i.Currency.ToUpperInvariant(),
                };
                s.Db.ServiceProjects.Add(p);
                foreach (var club in clubs)
                    s.Db.ProjectOrganizations.Add(
                        new()
                        {
                            TenantId = w.TenantId,
                            OrgId = club,
                            ProjectId = p.Id,
                        }
                    );
                s.Audit(w, p, "project.created");
                await s.Db.SaveChangesAsync();
                return p;
            }
        );
        api.MapGet(
            "/projects/{id:guid}",
            async (Guid id, OperationsService s) =>
            {
                var p = await s.Project(id);
                var w = await s.Workspace();
                var lead = await s.A.VisibleIds(w).ContainsAsync(p.OrgId);
                return new
                {
                    project = lead ? p : null,
                    summary = new
                    {
                        p.Id,
                        p.Name,
                        p.Category,
                        p.Status,
                        p.Description,
                        p.StartsAt,
                        p.EndsAt,
                        p.Objectives,
                    },
                    organizations = await s
                        .Db.ProjectOrganizations.Where(o =>
                            o.ProjectId == id && (lead || s.A.VisibleIds(w).Contains(o.OrgId))
                        )
                        .Select(o => new
                        {
                            o.Id,
                            o.OrgId,
                            o.ProjectId,
                            o.Contribution,
                            o.EstimatedContribution,
                            o.Version,
                            organizationName = s
                                .Db.Orgs.Where(org => org.Id == o.OrgId)
                                .Select(org => org.Name)
                                .First(),
                        })
                        .ToListAsync(),
                    activities = await s
                        .Db.ProjectActivities.Where(a => a.ProjectId == id && lead)
                        .OrderBy(a => a.StartsAt)
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/projects/{id:guid}/submit",
            async (Guid id, OperationsService s) =>
            {
                var p = await s.Project(id, true);
                var w = await s.Workspace(true);
                Check(p.Status == "Draft", "Only drafts can be submitted.", 409);
                p.Status = "Submitted";
                var r = await s.Submit(p, "OperationProject", Pack(p), w);
                p.RequestId = r.Id;
                await s.Db.SaveChangesAsync();
                return r;
            }
        );
        api.MapPost(
            "/projects/{id:guid}/status",
            async (Guid id, StateInput i, OperationsService s) =>
            {
                var p = await s.Project(id, true);
                var w = await s.Workspace(true);
                Check(p.Version == i.Version, "Project changed.", 409);
                var map = new Dictionary<string, string[]>
                {
                    { "Approved", ["Planning", "Cancelled"] },
                    { "Planning", ["In Progress", "On Hold", "Cancelled"] },
                    { "In Progress", ["Completed", "On Hold", "Cancelled"] },
                    { "On Hold", ["Planning", "In Progress", "Cancelled"] },
                    { "Completed", ["Impact Verified"] },
                    { "Impact Verified", ["Archived"] },
                };
                Check(
                    map.TryGetValue(p.Status, out var next) && next.Contains(i.Status),
                    "Invalid project transition.",
                    409
                );
                if (i.Status == "Impact Verified")
                {
                    Check(
                        (await s.A.Grants(w)).Contains("operations.verify"),
                        "Impact verifier required.",
                        403
                    );
                    Check(
                        await s.Db.ProjectImpacts.AnyAsync(x =>
                            x.ProjectId == id && x.Status == "Verified"
                        )
                            && !await s.Db.ProjectImpacts.AnyAsync(x =>
                                x.ProjectId == id && !x.Estimate && x.Status != "Verified"
                            ),
                        "Verify all actual impact first.",
                        409
                    );
                }
                s.Revision(p, "Project", Pack(p), w.UserId);
                p.Status = i.Status;
                p.Version++;
                s.Audit(w, p, "project.status", i.Reason);
                await s.Db.SaveChangesAsync();
                return p;
            }
        );
        api.MapPost(
            "/projects/{id:guid}/activities",
            async (Guid id, ActivityInput i, OperationsService s) =>
            {
                var p = await s.Project(id, true);
                var w = await s.Workspace(true);
                OperationsService.Title(i.Title);
                OperationsService.Dates(i.StartsAt, i.EndsAt);
                Check(
                    i.StartsAt >= p.StartsAt && i.EndsAt <= p.EndsAt,
                    "Activity must fit project dates."
                );
                var a = new ProjectActivity
                {
                    TenantId = p.TenantId,
                    OrgId = p.OrgId,
                    ProjectId = id,
                    Title = i.Title,
                    Description = i.Description,
                    StartsAt = i.StartsAt.ToUniversalTime(),
                    EndsAt = i.EndsAt.ToUniversalTime(),
                };
                s.Db.ProjectActivities.Add(a);
                s.Audit(w, a, "activity.created");
                await s.Db.SaveChangesAsync();
                return a;
            }
        );
        api.MapPost(
            "/projects/{id:guid}/impact/{impactId:guid}/corrections",
            async (
                Guid id,
                Guid impactId,
                ImpactCorrectionInput input,
                OperationsService service
            ) =>
            {
                var project = await service.Project(id, true);
                var workspace = await service.Workspace(true);
                var impact = await service.Db.ProjectImpacts.SingleOrDefaultAsync(x =>
                    x.Id == impactId && x.ProjectId == id
                );
                Check(
                    impact != null
                        && impact.Version == input.Version
                        && impact.Status == "Verified",
                    "Current verified impact required.",
                    409
                );
                Check(
                    input.Value >= 0 && input.Evidence.Length >= 5 && input.Reason.Length >= 5,
                    "A correction requires evidence and reason."
                );
                var body = Pack(input);
                var request = await service.Submit(
                    impact,
                    "OperationImpactCorrection",
                    body,
                    workspace
                );
                service.Revision(
                    impact,
                    "ImpactCorrection",
                    body,
                    workspace.UserId,
                    "Submitted:" + request.Id
                );
                await service.Db.SaveChangesAsync();
                return request;
            }
        );
        api.MapGet(
            "/impact-metrics",
            async (OperationsService s) =>
            {
                var w = await s.Workspace();
                return await s
                    .Db.ImpactMetrics.Where(x =>
                        x.TenantId == w.TenantId && s.A.VisibleIds(w).Contains(x.OrgId)
                    )
                    .OrderBy(x => x.Name)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/impact-metrics",
            async (MetricInput i, OperationsService s) =>
            {
                var w = await s.Scope(i.OrgId, true);
                OperationsService.Title(i.Name);
                Check(
                    new[] { "Sum", "Maximum", "Average", "Count" }.Contains(i.Aggregation),
                    "Unsupported aggregation."
                );
                var m = new ImpactMetric
                {
                    TenantId = w.TenantId,
                    OrgId = i.OrgId,
                    Name = i.Name,
                    Unit = i.Unit,
                    Description = i.Description,
                    Aggregation = i.Aggregation,
                    Category = i.Category,
                    EvidenceRequired = i.EvidenceRequired,
                };
                s.Db.ImpactMetrics.Add(m);
                await s.Db.SaveChangesAsync();
                return m;
            }
        );
        api.MapGet(
            "/projects/{id:guid}/impact",
            async (Guid id, OperationsService s) =>
            {
                var project = await s.Project(id);
                var w = await s.Workspace();
                var lead = await s.A.VisibleIds(w).ContainsAsync(project.OrgId);
                return await s
                    .Db.ProjectImpacts.Where(x =>
                        x.ProjectId == id && (lead || x.Status == "Verified")
                    )
                    .OrderByDescending(x => x.UpdatedAt)
                    .Take(100)
                    .Select(x => new
                    {
                        x.Id,
                        x.ProjectId,
                        x.MetricId,
                        x.Value,
                        x.Estimate,
                        x.Status,
                        evidence = lead ? x.Evidence : "",
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/projects/{id:guid}/impact",
            async (Guid id, ImpactInput i, OperationsService s) =>
            {
                var p = await s.Project(id, true);
                var w = await s.Workspace(true);
                var m = await s.Db.ImpactMetrics.SingleOrDefaultAsync(x =>
                    x.Id == i.MetricId
                    && x.TenantId == w.TenantId
                    && s.A.VisibleIds(w).Contains(x.OrgId)
                );
                Check(m != null, "Metric unavailable.");
                Check(
                    i.Value >= 0
                        && i.SourceKey.Length is > 0 and <= 100
                        && i.Evidence.Length <= 5000
                        && (!m.EvidenceRequired || i.Evidence.Length >= 5),
                    "Invalid value, evidence or deduplication key."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                Check(
                    !await s.Db.ProjectImpacts.AnyAsync(x =>
                        x.ProjectId == id && x.MetricId == i.MetricId && x.SourceKey == i.SourceKey
                    ),
                    "This source has already been reported for the joint project.",
                    409
                );
                var row = new ProjectImpact
                {
                    TenantId = w.TenantId,
                    OrgId = p.OrgId,
                    ProjectId = id,
                    MetricId = m.Id,
                    SourceKey = i.SourceKey,
                    Value = i.Value,
                    Evidence = i.Evidence,
                    Estimate = i.Estimate,
                    SubmittedBy = w.UserId,
                };
                s.Db.ProjectImpacts.Add(row);
                s.Audit(w, row, "impact.submitted");
                if (!i.Estimate)
                {
                    var r = await s.Submit(row, "OperationImpact", Pack(row), w);
                    row.RequestId = r.Id;
                }
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return row;
            }
        );
    }
}
