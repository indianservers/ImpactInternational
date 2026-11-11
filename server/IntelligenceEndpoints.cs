using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record IntelligenceReasonInput(string Reason);

public record IntelligenceVersionInput(long Version);

public static partial class IntelligenceEndpoints
{
    public static void MapIntelligence(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/intelligence")
            .RequireRateLimiting("intelligence")
            .AddEndpointFilter<IntelligenceInputFilter>();
        api.MapGet(
            "/metrics",
            async (IntelligenceService s) =>
            {
                await s.Workspace();
                return IntelligenceService.Catalog;
            }
        );
        api.MapGet(
            "/dashboard",
            async (
                IntelligenceService s,
                Guid? orgId = null,
                DateOnly? from = null,
                DateOnly? to = null
            ) => await s.Dashboard(orgId, from, to)
        );
        api.MapGet(
            "/export",
            async (
                IntelligenceService s,
                Guid? orgId = null,
                DateOnly? from = null,
                DateOnly? to = null
            ) =>
            {
                var w = await s.Scoped(orgId);
                var result = await s.Dashboard(orgId, from, to);
                var json = JsonSerializer.SerializeToElement(result, Json);
                var rows = new List<string> { "Metric,Value,Unit,Unavailable" };
                string Cell(string value) =>
                    "\""
                    + (value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" : "")
                    + value.Replace("\"", "\"\"")
                    + "\"";
                foreach (var m in json.GetProperty("metrics").EnumerateArray())
                    rows.Add(
                        string.Join(
                            ",",
                            new[]
                            {
                                Cell(m.GetProperty("name").GetString() ?? ""),
                                Cell(m.GetProperty("value").ToString()),
                                Cell(m.GetProperty("unit").GetString() ?? ""),
                                Cell(m.GetProperty("unavailable").ToString()),
                            }
                        )
                    );
                s.Audit(w, "intelligence.export", "metrics", "catalog", rows.Count - 1);
                await s.Db.SaveChangesAsync();
                return Results.File(
                    Encoding.UTF8.GetBytes(string.Join("\r\n", rows)),
                    "text/csv",
                    "intelligence-metrics.csv"
                );
            }
        );
        api.MapGet(
            "/search",
            async (string query, IntelligenceService s, string kind = "all", Guid? orgId = null) =>
            {
                var w = await s.Scoped(orgId, "intelligence.assistant");
                var rows = await IntelligenceSearch.Find(s, query, kind, orgId);
                s.Audit(w, "search.read", query, kind, rows.Count);
                await s.Db.SaveChangesAsync();
                return new
                {
                    items = rows,
                    limit = 100,
                    privacy = "Only authorized labels; no private source excerpts or unfiltered counts.",
                };
            }
        );
        api.MapPost(
            "/assistant",
            async (IntelligenceQuestionInput i, IntelligenceAssistant assistant) =>
                await assistant.Answer(i)
        );
        api.MapPost(
            "/knowledge/{id:guid}/index",
            async (Guid id, IntelligenceKnowledge knowledge) => await knowledge.Index(id)
        );
        api.MapGet(
            "/knowledge",
            async (
                string query,
                IntelligenceKnowledge knowledge,
                IntelligenceService s,
                Guid? orgId = null
            ) =>
            {
                var w = await s.Scoped(orgId, "intelligence.knowledge");
                var rows = await knowledge.Search(query, orgId);
                s.Audit(w, "knowledge.read", query, "authorized-ft-search", rows.Count);
                await s.Db.SaveChangesAsync();
                return new
                {
                    items = rows,
                    policy = "Current version only. Permissions/retention rechecked. Extracts are untrusted evidence. No OCR or external vector service.",
                };
            }
        );
        api.MapGet(
            "/knowledge/indexes",
            async (IntelligenceService s) =>
            {
                var w = await s.A.Workspace("operations.documents");
                var candidates = await s
                    .Db.KnowledgePassages.Where(p =>
                        p.TenantId == w.TenantId
                        && s.Db.GovernanceDocuments.Any(d =>
                            d.Id == p.DocumentId
                            && s.A.VisibleIds(w).Contains(d.OrgId)
                            && d.CurrentVersion == p.DocumentVersion
                            && d.Sha256 == p.SourceHash
                            && d.Status != "Expired"
                            && (d.RetainUntil == null || d.RetainUntil > DateTimeOffset.UtcNow)
                        )
                    )
                    .GroupBy(p => new
                    {
                        p.DocumentId,
                        p.DocumentVersion,
                        p.SourceHash,
                    })
                    .Select(g => new
                    {
                        g.Key.DocumentId,
                        g.Key.DocumentVersion,
                        g.Key.SourceHash,
                        pages = g.Count(),
                    })
                    .OrderBy(x => x.DocumentId)
                    .Take(100)
                    .ToListAsync();
                var output = new List<object>();
                foreach (var row in candidates)
                {
                    var document = await s.Db.GovernanceDocuments.SingleAsync(d =>
                        d.Id == row.DocumentId
                    );
                    try
                    {
                        await s.Operations.Document(document);
                        output.Add(row);
                    }
                    catch (ApiError) { }
                }
                return output;
            }
        );
        api.MapGet(
            "/branding/current",
            async (Access a, TenantExperience branding, IConfiguration configuration) =>
            {
                var w = await a.WorkspaceAny(
                    "intelligence.assistant",
                    "settings.view",
                    "finance.self"
                );
                var row = await a.Db.TenantBrandings.SingleOrDefaultAsync(b =>
                    b.TenantId == w.TenantId
                );
                return new
                {
                    identity = await branding.Public(w.TenantId),
                    disabledFeatures = row == null
                        ? Array.Empty<string>()
                        : Unpack<string[]>(row.DisabledFeatures),
                    remoteAiEnabled = row?.RemoteAiEnabled == true,
                    remoteAiConfigured = configuration["Intelligence:OpenAI:ApiKey"]
                        is { Length: > 0 }
                        && configuration["Intelligence:OpenAI:Model"] is { Length: > 0 },
                    aiPolicy = row?.AiPolicy ?? "",
                    version = row?.Version ?? 1,
                };
            }
        );
        api.MapGet(
            "/branding",
            async (Access a) =>
            {
                var w = await a.TenantWorkspace("settings.manage");
                return new
                {
                    branding = await a.Db.TenantBrandings.SingleOrDefaultAsync(b =>
                        b.TenantId == w.TenantId
                    ),
                    domains = await a
                        .Db.TenantDomains.Where(d => d.TenantId == w.TenantId)
                        .OrderBy(d => d.Host)
                        .ToListAsync(),
                    features = TenantExperience.Features,
                };
            }
        );
        api.MapPut(
            "/branding",
            async (IntelligenceBrandingInput i, TenantExperience branding) => await branding.Save(i)
        );
        app.MapGet(
            "/api/v1/branding",
            async (HttpContext context, TenantExperience branding) =>
                await branding.Public(context.Items["tenant-domain"] as Guid?)
        );
        api.MapPost(
            "/domains",
            async (IntelligenceDomainInput i, Access a, IConfiguration configuration) =>
            {
                var w = await a.TenantWorkspace("settings.manage");
                var host = TenantExperience.Normalize(i.Host);
                Check(
                    !Uri.TryCreate(configuration["PublicOrigin"], UriKind.Absolute, out var primary)
                        || !string.Equals(primary.Host, host, StringComparison.OrdinalIgnoreCase),
                    "The primary application hostname cannot be registered as a tenant domain."
                );
                Check(
                    await a.Db.TenantDomains.CountAsync(d => d.TenantId == w.TenantId) < 10,
                    "Domain limit reached."
                );
                var d = new TenantDomain
                {
                    TenantId = w.TenantId,
                    Host = host,
                    Challenge = Token(),
                    CreatorId = w.UserId,
                };
                a.Db.TenantDomains.Add(d);
                await a.Log(w, "tenant.domain.create", d.Id.ToString(), host);
                return new
                {
                    domain = d,
                    record = "_clubs-verification." + host,
                    type = "TXT",
                    value = "clubs-verification=" + d.Challenge,
                };
            }
        );
        api.MapPost(
            "/domains/{id:guid}/verify",
            async (Guid id, TenantExperience branding) => await branding.Verify(id)
        );
        api.MapPost(
            "/domains/{id:guid}/activate",
            async (Guid id, Access a, TenantExperience branding) =>
            {
                Check((await a.User()).PlatformAdmin, "Platform operator authority required.", 403);
                var d = await a.Db.TenantDomains.SingleOrDefaultAsync(x => x.Id == id);
                Check(
                    d != null
                        && d.Status == "Verified"
                        && d.VerifiedAt != null
                        && branding.Provisioned(d.Host),
                    "Verified ownership and operator-provisioned TLS/ingress host configuration are required.",
                    409
                );
                d.Status = "Active";
                d.Version++;
                var w = await a.Workspace("settings.manage");
                Check(
                    w.TenantId == d.TenantId,
                    "Activate within the owning association workspace.",
                    403
                );
                await a.Log(w, "tenant.domain.activate", d.Id.ToString(), d.Host);
                return d;
            }
        );
        api.MapPost(
            "/domains/{id:guid}/deactivate",
            async (Guid id, Access a) =>
            {
                var w = await a.TenantWorkspace("settings.manage");
                var d = await a.Db.TenantDomains.SingleOrDefaultAsync(d =>
                    d.Id == id && d.TenantId == w.TenantId
                );
                Check(d != null, "Domain unavailable.", 404);
                d.Status = "Disabled";
                d.Version++;
                await a.Log(
                    w,
                    "tenant.domain.deactivate",
                    id.ToString(),
                    "Domain routing disabled"
                );
                return Results.NoContent();
            }
        );
        MapWorkflows(api);
        MapOperations(app, api);
    }

    static void MapWorkflows(RouteGroupBuilder api)
    {
        api.MapGet(
            "/workflows",
            async (IntelligenceService s) =>
            {
                var w = await s.Workspace("intelligence.automations");
                var candidates = await s
                    .Db.CommunicationAutomations.Where(r =>
                        r.TenantId == w.TenantId
                        && r.Definition != ""
                        && s.A.VisibleIds(w).Contains(r.OrgId)
                    )
                    .OrderBy(r => r.Name)
                    .Take(100)
                    .ToListAsync();
                var result = new List<CommunicationAutomation>();
                foreach (var r in candidates)
                    try
                    {
                        await IntelligenceWorkflows.Authority(
                            s.A,
                            w,
                            Unpack<IntelligenceWorkflowDefinition>(r.Definition),
                            r.OrgId
                        );
                        result.Add(r);
                    }
                    catch (ApiError) { }
                return result;
            }
        );
        api.MapGet(
            "/workflows/options",
            async (IntelligenceService s) =>
            {
                var w = await s.Workspace("intelligence.automations");
                return new
                {
                    triggers = IntelligenceWorkflows.Triggers,
                    actions = IntelligenceWorkflows.Actions,
                    organizations = await s
                        .A.Visible(w)
                        .OrderBy(o => o.Name)
                        .Take(100)
                        .Select(o => new { o.Id, o.Name })
                        .ToListAsync(),
                    entities = await s
                        .Db.FinanceEntitys.Where(e =>
                            e.TenantId == w.TenantId
                            && s.Db.FinanceGrants.Any(g =>
                                g.EntityId == e.Id && g.UserId == w.UserId
                            )
                        )
                        .OrderBy(e => e.Name)
                        .Take(100)
                        .Select(e => new
                        {
                            e.Id,
                            e.Name,
                            e.OrgId,
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/workflows",
            async (
                IntelligenceWorkflowInput i,
                IntelligenceService s,
                CommunicationService communication
            ) =>
            {
                var w = await s.Workspace("intelligence.automations");
                IntelligenceWorkflows.Validate(i.Definition);
                await IntelligenceWorkflows.Authority(s.A, w, i.Definition, i.OrgId);
                Check(
                    i.Name.Length is > 1 and <= 120 && i.DailyLimit is > 0 and <= 50,
                    "Invalid workflow name or daily limit."
                );
                var template = await s.Db.CommunicationTemplates.FirstOrDefaultAsync(t =>
                    t.TenantId == w.TenantId
                    && t.OrgId == i.OrgId
                    && t.Name == "Reviewed workflow follow-up (system)"
                );
                if (template == null)
                {
                    template = new()
                    {
                        TenantId = w.TenantId,
                        OrgId = i.OrgId,
                        Name = "Reviewed workflow follow-up (system)",
                        Channel = "InApp",
                        Body = "A reviewed workflow follow-up requires attention.",
                        Subject = "Workflow follow-up",
                        Active = true,
                    };
                    s.Db.CommunicationTemplates.Add(template);
                }
                var r = new CommunicationAutomation
                {
                    TenantId = w.TenantId,
                    OrgId = i.OrgId,
                    CreatorId = w.UserId,
                    Name = i.Name,
                    Trigger = i.Definition.Trigger,
                    Definition = Pack(i.Definition),
                    TemplateId = template.Id,
                    TemplateVersion = template.Version,
                    DailyLimit = i.DailyLimit,
                    Active = false,
                };
                s.Db.CommunicationAutomations.Add(r);
                communication.Revision(r, r.Version, "Automation", w.UserId);
                s.Audit(w, "workflow.draft", r.Id.ToString(), r.Trigger);
                await s.Db.SaveChangesAsync();
                return r;
            }
        );
        api.MapPut(
            "/workflows/{id:guid}",
            async (
                Guid id,
                IntelligenceWorkflowInput i,
                IntelligenceService s,
                CommunicationService communication
            ) =>
            {
                var (w, r) = await Workflow(id, s);
                await using var tx = await s.A.Mutation(w);
                await s.Db.Entry(r).ReloadAsync();
                Check(
                    r.Status == "Draft" && !r.Active && r.Version == i.Version,
                    "Only the current inactive draft may be edited. Copy reviewed rules to a fresh revision.",
                    409
                );
                Check(
                    i.OrgId == r.OrgId
                        && i.Name.Length is > 1 and <= 120
                        && i.DailyLimit is > 0 and <= 50,
                    "Invalid draft. Organization changes require a new workflow."
                );
                IntelligenceWorkflows.Validate(i.Definition);
                await IntelligenceWorkflows.Authority(s.A, w, i.Definition, i.OrgId);
                r.Name = i.Name;
                r.Trigger = i.Definition.Trigger;
                r.Definition = Pack(i.Definition);
                r.DailyLimit = i.DailyLimit;
                r.Version++;
                communication.Revision(r, r.Version, "Automation", w.UserId);
                s.Audit(w, "workflow.draft.update", r.Id.ToString(), r.Trigger);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapPost(
            "/workflows/{id:guid}/test",
            async (Guid id, IntelligenceService s) =>
            {
                var (w, r) = await Workflow(id, s);
                var d = Unpack<IntelligenceWorkflowDefinition>(r.Definition);
                await IntelligenceWorkflows.Authority(s.A, w, d, r.OrgId);
                var sources = await IntelligenceWorkflows.Sources(s.Db, w, d, r.OrgId, 50);
                return new
                {
                    dryRun = true,
                    actionsExecuted = 0,
                    sources = sources
                        .Select(x => new
                        {
                            sourceKey = Hash(x.Key),
                            matches = IntelligenceWorkflows.Matches(x, d),
                            plannedActions = d.Steps.Select(step => new
                            {
                                step.Action,
                                step.DelayMinutes,
                                step.Branch,
                            }),
                        })
                        .ToArray(),
                    limit = 50,
                };
            }
        );
        api.MapPost(
            "/workflows/{id:guid}/submit",
            async (Guid id, IntelligenceService s, CommunicationService communication) =>
            {
                var (w, r) = await Workflow(id, s);
                Check(r.Status == "Draft", "Only an unmodified draft can be submitted.", 409);
                var d = Unpack<IntelligenceWorkflowDefinition>(r.Definition);
                IntelligenceWorkflows.Validate(d);
                await IntelligenceWorkflows.Authority(s.A, w, d, r.OrgId);
                await using var tx = await s.A.Mutation(w);
                r.ContentHash = IntelligenceWorkflows.Digest(r);
                r.Version++;
                r.Status = "Review";
                var request = await s.Operations.Governance.Request(
                    w,
                    r.OrgId,
                    "CommunicationAutomation",
                    "Review workflow: " + r.Name,
                    Pack(new BroadcastReference(r.Id, r.Version, r.ContentHash))
                );
                r.ApprovalRequestId = request.Id;
                await s.Operations.Governance.Submit(request, w);
                communication.Revision(r, r.Version, "Automation", w.UserId);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return new { rule = r, request };
            }
        );
        api.MapPost(
            "/workflows/{id:guid}/cancel",
            async (Guid id, IntelligenceService s) =>
            {
                var (w, r) = await Workflow(id, s);
                await using var tx = await s.A.Mutation(w);
                await s.Db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(511105)");
                r.Active = false;
                r.Status = "Cancelled";
                r.Version++;
                await s
                    .Db.CommunicationExecutions.Where(e =>
                        e.AutomationId == id && e.Status == "Pending"
                    )
                    .ExecuteUpdateAsync(e => e.SetProperty(x => x.Status, "Cancelled"));
                s.Audit(w, "workflow.cancel", id.ToString(), r.Trigger);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapPost(
            "/workflows/{id:guid}/copy",
            async (Guid id, IntelligenceService s, CommunicationService communication) =>
            {
                var (w, r) = await Workflow(id, s);
                var copy = new CommunicationAutomation
                {
                    TenantId = r.TenantId,
                    OrgId = r.OrgId,
                    CreatorId = w.UserId,
                    Name = r.Name + " / revision",
                    Trigger = r.Trigger,
                    Definition = r.Definition,
                    TemplateId = r.TemplateId,
                    TemplateVersion = r.TemplateVersion,
                    DailyLimit = r.DailyLimit,
                };
                s.Db.CommunicationAutomations.Add(copy);
                communication.Revision(copy, copy.Version, "Automation", w.UserId);
                await s.Db.SaveChangesAsync();
                return copy;
            }
        );
        api.MapGet(
            "/workflows/{id:guid}/executions",
            async (Guid id, IntelligenceService s) =>
            {
                await Workflow(id, s);
                return await s
                    .Db.CommunicationExecutions.Where(e => e.AutomationId == id)
                    .OrderByDescending(e => e.At)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/executions/{id:guid}/retry",
            async (Guid id, IntelligenceService s) =>
            {
                var run = await s.Db.CommunicationExecutions.SingleOrDefaultAsync(x => x.Id == id);
                Check(run != null, "Execution unavailable.", 404);
                var (w, r) = await Workflow(run.AutomationId, s);
                Check(
                    run.Status == "Failed"
                        && r.Active
                        && r.Version == run.RuleVersion
                        && run.DefinitionSnapshot == r.Definition,
                    "Only failed executions of the current active revision may retry.",
                    409
                );
                await IntelligenceWorkflows.Authority(
                    s.A,
                    w,
                    Unpack<IntelligenceWorkflowDefinition>(r.Definition),
                    r.OrgId
                );
                run.Status = "Pending";
                run.Error = "";
                run.NextAttemptAt = DateTimeOffset.UtcNow;
                await s.Db.SaveChangesAsync();
                return run;
            }
        );
    }

    static async Task<(Assignment, CommunicationAutomation)> Workflow(
        Guid id,
        IntelligenceService s
    )
    {
        var w = await s.Workspace("intelligence.automations");
        var r = await s.Db.CommunicationAutomations.SingleOrDefaultAsync(r =>
            r.Id == id && r.TenantId == w.TenantId && r.Definition != ""
        );
        Check(
            r != null && await s.A.VisibleIds(w).ContainsAsync(r.OrgId),
            "Workflow unavailable.",
            404
        );
        await IntelligenceWorkflows.Authority(
            s.A,
            w,
            Unpack<IntelligenceWorkflowDefinition>(r.Definition),
            r.OrgId
        );
        return (w, r);
    }
}
