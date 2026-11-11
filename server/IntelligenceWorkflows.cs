using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record IntelligenceWorkflowCondition(
    string Status = "",
    decimal? MinimumAmount = null,
    Guid? CategoryId = null
);

public record IntelligenceWorkflowStep(
    string Action,
    int DelayMinutes = 0,
    string Branch = "Match"
);

public record IntelligenceWorkflowDefinition(
    string Trigger,
    IntelligenceWorkflowCondition Condition,
    IntelligenceWorkflowStep[] Steps,
    int Days = 7,
    Guid? EntityId = null
);

public record IntelligenceWorkflowInput(
    Guid OrgId,
    string Name,
    IntelligenceWorkflowDefinition Definition,
    int DailyLimit = 25,
    long Version = 1
);

public record WorkflowSource(
    string Key,
    Guid Id,
    string Status,
    decimal Amount,
    Guid? CategoryId,
    bool TriggerMatches
);

public static class IntelligenceWorkflows
{
    public static readonly string[] Triggers =
    [
        "MemberJoined",
        "RenewalDue",
        "TaskOverdue",
        "OfficerExpiring",
        "EventUpcoming",
        "ProjectApproved",
        "MinutesApproved",
        "InvoiceOverdue",
    ];
    public static readonly string[] Actions = ["NotifyCreator", "CreateFollowUpTask"];

    public static string Digest(CommunicationAutomation r) =>
        Hash(
            Pack(
                new
                {
                    r.OrgId,
                    r.Name,
                    r.Trigger,
                    r.Definition,
                    r.DailyLimit,
                    r.TemplateId,
                    r.TemplateVersion,
                }
            )
        );

    public static void Validate(IntelligenceWorkflowDefinition d)
    {
        Check(
            Triggers.Contains(d.Trigger)
                && d.Days is >= 1 and <= 90
                && d.Steps.Length is > 0 and <= 8
                && d.Condition.Status.Length <= 50,
            "Invalid supported workflow definition."
        );
        Check(
            d.Trigger == "InvoiceOverdue" ? d.EntityId != null : d.EntityId == null,
            "Only finance invoice workflows accept an entity."
        );
        Check(
            d.Condition.MinimumAmount == null
                || d.Trigger == "InvoiceOverdue" && d.Condition.MinimumAmount >= 0,
            "Amount conditions require a finance trigger."
        );
        Check(
            d.Condition.CategoryId == null || d.Trigger is "MemberJoined" or "RenewalDue",
            "Category conditions require a membership trigger."
        );
        Check(
            d.Steps.Count(s => s.Action == "CreateFollowUpTask") <= 1,
            "Only one task-producing step is permitted; generated tasks never retrigger workflows."
        );
        foreach (var step in d.Steps)
            Check(
                Actions.Contains(step.Action)
                    && step.DelayMinutes is >= 0 and <= 10080
                    && step.Branch is "Match" or "Otherwise",
                "Only bounded, read-only-source personal follow-ups are permitted."
            );
    }

    public static async Task Authority(
        Access a,
        Assignment w,
        IntelligenceWorkflowDefinition d,
        Guid org
    )
    {
        var g = await a.Grants(w);
        Check(
            g.Contains("intelligence.automations")
                && g.Contains("communications.manage")
                && await a.VisibleIds(w).ContainsAsync(org),
            "Workflow authority no longer covers this organization.",
            403
        );
        var required = d.Trigger switch
        {
            "MemberJoined" or "RenewalDue" => "members.view",
            "OfficerExpiring" => "leadership.view",
            "EventUpcoming" => "events.manage",
            "InvoiceOverdue" => "finance.view",
            _ => "operations.manage",
        };
        Check(g.Contains(required), "Source permission required: " + required, 403);
        if (d.Steps.Any(x => x.Action == "CreateFollowUpTask"))
            Check(g.Contains("operations.manage"), "Task action requires operations.manage.", 403);
        if (d.EntityId != null)
            Check(
                await a.Db.FinanceEntitys.AnyAsync(e =>
                    e.Id == d.EntityId
                    && e.TenantId == w.TenantId
                    && e.Active
                    && e.OrgId == org
                    && a.Db.FinanceGrants.Any(g => g.EntityId == e.Id && g.UserId == w.UserId)
                ),
                "Explicit finance entity access required.",
                403
            );
    }

    public static bool Matches(WorkflowSource s, IntelligenceWorkflowDefinition d) =>
        s.TriggerMatches
        && (d.Condition.Status == "" || s.Status == d.Condition.Status)
        && (d.Condition.MinimumAmount == null || s.Amount >= d.Condition.MinimumAmount)
        && (d.Condition.CategoryId == null || s.CategoryId == d.Condition.CategoryId);

    public static async Task<List<WorkflowSource>> Sources(
        Db db,
        Assignment w,
        IntelligenceWorkflowDefinition d,
        Guid org,
        int take = 50,
        Guid? sourceId = null,
        Guid? ruleId = null,
        long ruleVersion = 0
    )
    {
        var now = DateTimeOffset.UtcNow;
        var after = sourceId == null ? now.AddDays(-d.Days) : now.AddYears(-100);
        var before = sourceId == null ? now.AddDays(d.Days) : now.AddYears(100);
        switch (d.Trigger)
        {
            case "MemberJoined":
                return await db
                    .Memberships.Where(m =>
                        (sourceId == null || m.Id == sourceId)
                        && (
                            ruleId == null
                            || !db.CommunicationExecutions.Any(run =>
                                run.AutomationId == ruleId
                                && run.RuleVersion == ruleVersion
                                && run.SourceKey.StartsWith("v" + ruleVersion + ":member:" + m.Id)
                            )
                        )
                        && m.TenantId == w.TenantId
                        && m.ClubId == org
                        && m.JoinedAt >= after
                        && m.JoinedAt <= now
                    )
                    .OrderBy(m => m.Id)
                    .Take(take)
                    .Select(m => new WorkflowSource(
                        "member:" + m.Id,
                        m.Id,
                        m.Status,
                        0,
                        m.CategoryId,
                        m.Status == "Active"
                    ))
                    .ToListAsync();
            case "RenewalDue":
                return await db
                    .Memberships.Where(m =>
                        (sourceId == null || m.Id == sourceId)
                        && (
                            ruleId == null
                            || !db.CommunicationExecutions.Any(run =>
                                run.AutomationId == ruleId
                                && run.RuleVersion == ruleVersion
                                && run.SourceKey.StartsWith("v" + ruleVersion + ":renewal:" + m.Id)
                            )
                        )
                        && m.TenantId == w.TenantId
                        && m.ClubId == org
                        && m.EndsAt != null
                        && m.EndsAt >= now
                        && m.EndsAt <= before
                    )
                    .OrderBy(m => m.Id)
                    .Take(take)
                    .Select(m => new WorkflowSource(
                        "renewal:" + m.Id + ":" + m.EndsAt,
                        m.Id,
                        m.Status,
                        0,
                        m.CategoryId,
                        m.Status == "Active"
                    ))
                    .ToListAsync();
            case "TaskOverdue":
                return await db
                    .OperationTasks.Where(t =>
                        (sourceId == null || t.Id == sourceId)
                        && (
                            ruleId == null
                            || !db.CommunicationExecutions.Any(run =>
                                run.AutomationId == ruleId
                                && run.RuleVersion == ruleVersion
                                && run.SourceKey.StartsWith("v" + ruleVersion + ":task:" + t.Id)
                            )
                        )
                        && t.TenantId == w.TenantId
                        && t.OrgId == org
                        && t.DueAt < now
                        && t.DueAt >= after
                        && t.Status != "Completed"
                        && t.Status != "Cancelled"
                        && t.CommitteeId == null
                        && t.SourceType == "Administrative"
                        && !db.CommunicationExecutions.Any(e => e.GeneratedResourceId == t.Id)
                    )
                    .OrderBy(t => t.Id)
                    .Take(take)
                    .Select(t => new WorkflowSource("task:" + t.Id, t.Id, t.Status, 0, null, true))
                    .ToListAsync();
            case "OfficerExpiring":
                return await GovernanceService
                    .Current(db)
                    .Where(p =>
                        (sourceId == null || p.Id == sourceId)
                        && (
                            ruleId == null
                            || !db.CommunicationExecutions.Any(run =>
                                run.AutomationId == ruleId
                                && run.RuleVersion == ruleVersion
                                && run.SourceKey.StartsWith("v" + ruleVersion + ":officer:" + p.Id)
                            )
                        )
                        && p.TenantId == w.TenantId
                        && p.OrgId == org
                        && p.EndsAt <= before
                    )
                    .OrderBy(p => p.Id)
                    .Take(take)
                    .Select(p => new WorkflowSource(
                        "officer:" + p.Id,
                        p.Id,
                        p.Status,
                        0,
                        null,
                        true
                    ))
                    .ToListAsync();
            case "EventUpcoming":
                return await db
                    .Events.Where(e =>
                        (sourceId == null || e.Id == sourceId)
                        && (
                            ruleId == null
                            || !db.CommunicationExecutions.Any(run =>
                                run.AutomationId == ruleId
                                && run.RuleVersion == ruleVersion
                                && run.SourceKey.StartsWith("v" + ruleVersion + ":event:" + e.Id)
                            )
                        )
                        && e.TenantId == w.TenantId
                        && e.OrgId == org
                        && e.StartsAt >= now
                        && e.StartsAt <= before
                        && e.Publication == "Published"
                        && e.Lifecycle == "Scheduled"
                    )
                    .OrderBy(e => e.Id)
                    .Take(take)
                    .Select(e => new WorkflowSource(
                        "event:" + e.Id,
                        e.Id,
                        e.Lifecycle,
                        0,
                        null,
                        true
                    ))
                    .ToListAsync();
            case "ProjectApproved":
                return await db
                    .ServiceProjects.Where(p =>
                        (sourceId == null || p.Id == sourceId)
                        && (
                            ruleId == null
                            || !db.CommunicationExecutions.Any(run =>
                                run.AutomationId == ruleId
                                && run.RuleVersion == ruleVersion
                                && run.SourceKey.StartsWith("v" + ruleVersion + ":project:" + p.Id)
                            )
                        )
                        && p.TenantId == w.TenantId
                        && p.OrgId == org
                        && p.Status == "Approved"
                        && p.UpdatedAt >= after
                    )
                    .OrderBy(p => p.Id)
                    .Take(take)
                    .Select(p => new WorkflowSource(
                        "project:" + p.Id,
                        p.Id,
                        p.Status,
                        0,
                        null,
                        true
                    ))
                    .ToListAsync();
            case "MinutesApproved":
                return await db
                    .MeetingMinutes.Where(m =>
                        (sourceId == null || m.Id == sourceId)
                        && (
                            ruleId == null
                            || !db.CommunicationExecutions.Any(run =>
                                run.AutomationId == ruleId
                                && run.RuleVersion == ruleVersion
                                && run.SourceKey.StartsWith("v" + ruleVersion + ":minutes:" + m.Id)
                            )
                        )
                        && m.TenantId == w.TenantId
                        && m.OrgId == org
                        && m.Status == "Published"
                        && m.UpdatedAt >= after
                        && db.ClubMeetings.Any(meeting =>
                            meeting.Id == m.MeetingId
                            && meeting.Visibility != "Confidential"
                            && meeting.CommitteeId == null
                        )
                    )
                    .OrderBy(m => m.Id)
                    .Take(take)
                    .Select(m => new WorkflowSource(
                        "minutes:" + m.Id,
                        m.Id,
                        m.Status,
                        0,
                        null,
                        true
                    ))
                    .ToListAsync();
            case "InvoiceOverdue":
                var today = DateOnly.FromDateTime(now.UtcDateTime);
                return await db
                    .FinanceInvoices.Where(i =>
                        (sourceId == null || i.Id == sourceId)
                        && (
                            ruleId == null
                            || !db.CommunicationExecutions.Any(run =>
                                run.AutomationId == ruleId
                                && run.RuleVersion == ruleVersion
                                && run.SourceKey.StartsWith("v" + ruleVersion + ":invoice:" + i.Id)
                            )
                        )
                        && i.TenantId == w.TenantId
                        && i.EntityId == d.EntityId
                        && i.OrgId == org
                        && i.State == "Issued"
                        && i.DueOn < today
                        && i.DueOn >= today.AddDays(-d.Days)
                    )
                    .OrderBy(i => i.Id)
                    .Take(take)
                    .Select(i => new WorkflowSource(
                        "invoice:" + i.Id,
                        i.Id,
                        i.State,
                        i.Total - i.Paid - i.Credited,
                        null,
                        i.Total > i.Paid + i.Credited
                    ))
                    .ToListAsync();
            default:
                return [];
        }
    }

    public static async Task Process(CommunicationService communication, CancellationToken stop)
    {
        var db = communication.Db;
        var a = communication.A;
        var rules = await db
            .CommunicationAutomations.Where(r =>
                r.Definition != "" && r.Active && r.Status == "Approved"
            )
            .OrderBy(r => r.Id)
            .Take(50)
            .ToListAsync(stop);
        foreach (var rule in rules)
        {
            var d = Unpack<IntelligenceWorkflowDefinition>(rule.Definition);
            Assignment? actor = null;
            foreach (
                var w in await db
                    .Assignments.Where(w =>
                        w.UserId == rule.CreatorId && w.TenantId == rule.TenantId
                    )
                    .ToListAsync(stop)
            )
                try
                {
                    if (await a.Effective(w))
                    {
                        await Authority(a, w, d, rule.OrgId);
                        actor = w;
                        break;
                    }
                }
                catch (ApiError) { }
            if (actor == null)
            {
                rule.Active = false;
                rule.Version++;
                continue;
            }
            var template = await db.CommunicationTemplates.SingleOrDefaultAsync(
                t => t.Id == rule.TemplateId && t.Active && t.Version == rule.TemplateVersion,
                stop
            );
            if (template == null)
            {
                rule.Active = false;
                rule.Version++;
                continue;
            }
            var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
            var count = await db.CommunicationExecutions.CountAsync(
                e => e.AutomationId == rule.Id && e.At >= today,
                stop
            );
            var limit = Math.Min(50, Math.Max(0, rule.DailyLimit - count));
            if (limit > 0)
            {
                foreach (
                    var source in await Sources(
                        db,
                        actor,
                        d,
                        rule.OrgId,
                        200,
                        ruleId: rule.Id,
                        ruleVersion: rule.Version
                    )
                )
                {
                    var key = "v" + rule.Version + ":" + source.Key;
                    if (
                        await db.CommunicationExecutions.AnyAsync(
                            e => e.AutomationId == rule.Id && e.SourceKey == key,
                            stop
                        )
                    )
                        continue;
                    db.CommunicationExecutions.Add(
                        new()
                        {
                            TenantId = rule.TenantId,
                            OrgId = rule.OrgId,
                            AutomationId = rule.Id,
                            RuleVersion = rule.Version,
                            DefinitionSnapshot = rule.Definition,
                            SourceKey = key,
                            Status = "Pending",
                            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(
                                d.Steps[0].DelayMinutes
                            ),
                        }
                    );
                    if (--limit == 0)
                        break;
                }
            }
            await db.SaveChangesAsync(stop);
            var runs = await db
                .CommunicationExecutions.Where(e =>
                    e.AutomationId == rule.Id
                    && e.Status == "Pending"
                    && e.NextAttemptAt <= DateTimeOffset.UtcNow
                )
                .OrderBy(e => e.At)
                .Take(50)
                .ToListAsync(stop);
            foreach (var run in runs)
            {
                if (run.RuleVersion != rule.Version || run.DefinitionSnapshot != rule.Definition)
                {
                    run.Status = "Cancelled";
                    continue;
                }
                try
                {
                    await Authority(a, actor, d, rule.OrgId);
                    var sourceId = Guid.Parse(run.SourceKey.Split(':')[2]);
                    var sources = await Sources(db, actor, d, rule.OrgId, 1, sourceId: sourceId);
                    var source = sources.SingleOrDefault(x =>
                        run.SourceKey == "v" + rule.Version + ":" + x.Key
                    );
                    if (source == null)
                    {
                        run.Status = "Cancelled";
                        continue;
                    }
                    var step = d.Steps[run.Step];
                    var matches = Matches(source, d);
                    if (step.Branch == (matches ? "Match" : "Otherwise"))
                    {
                        if (step.Action == "NotifyCreator")
                        {
                            var mail = new Mail
                            {
                                TenantId = rule.TenantId,
                                OrgId = rule.OrgId,
                                UserId = rule.CreatorId,
                                Kind = "intelligence.workflow",
                                Channel = "InApp",
                                Purpose = "Transactional",
                                Subject = "Reviewed workflow follow-up",
                                Body =
                                    "A "
                                    + d.Trigger
                                    + " workflow needs your review. No source status or financial entry was changed.",
                                Link =
                                    d.Trigger == "InvoiceOverdue"
                                        ? "/app/finance"
                                        : "/app/intelligence/workflows",
                                SenderId = rule.CreatorId,
                                SenderAssignmentId = actor.Id,
                                TokenHash = Token(),
                                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                            };
                            db.Mails.Add(mail);
                            run.MailId = mail.Id;
                        }
                        else
                        {
                            var person = await db
                                .Persons.Where(p => p.UserId == rule.CreatorId)
                                .Select(p => (Guid?)p.Id)
                                .SingleOrDefaultAsync(stop);
                            var task = new OperationTask
                            {
                                TenantId = rule.TenantId,
                                OrgId = rule.OrgId,
                                CreatorId = rule.CreatorId,
                                PersonId = person,
                                Title = "Review " + d.Trigger + " follow-up",
                                Description =
                                    "Generated by an independently approved workflow. Review the source in its authorized module.",
                                SourceType = "Administrative",
                                DueAt = DateTimeOffset.UtcNow.AddDays(7),
                            };
                            db.OperationTasks.Add(task);
                            run.GeneratedResourceId = task.Id;
                        }
                    }
                    var trace = Unpack<List<System.Text.Json.JsonElement>>(run.Trace);
                    trace.Add(
                        System.Text.Json.JsonSerializer.SerializeToElement(
                            new
                            {
                                step = run.Step,
                                action = step.Action,
                                executed = step.Branch == (matches ? "Match" : "Otherwise"),
                                at = DateTimeOffset.UtcNow,
                            },
                            Json
                        )
                    );
                    run.Trace = Pack(trace);
                    run.Step++;
                    run.Attempts = 0;
                    run.Error = "";
                    if (run.Step >= d.Steps.Length)
                        run.Status = "Completed";
                    else
                        run.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(
                            d.Steps[run.Step].DelayMinutes
                        );
                }
                catch (ApiError ex)
                {
                    run.Status = "Failed";
                    run.Error = ex.Message;
                    run.Attempts++;
                }
                catch (Exception)
                {
                    run.Attempts++;
                    run.Error = "Execution stopped; inspect configuration before retry.";
                    run.Status = "Failed";
                }
            }
        }
        await db
            .CommunicationExecutions.Where(e =>
                e.Status == "Pending"
                && db.CommunicationAutomations.Any(r =>
                    r.Id == e.AutomationId && r.Definition != "" && !r.Active
                )
            )
            .ExecuteUpdateAsync(e => e.SetProperty(x => x.Status, "Cancelled"), stop);
        await db.SaveChangesAsync(stop);
    }
}
