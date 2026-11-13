using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record BroadcastInput(
    Guid OrgId,
    string Title,
    string Body,
    string Channel = "InApp",
    string Kind = "Campaign",
    string Category = "General",
    string Purpose = "Promotional",
    string Subject = "",
    string Rules = "{}",
    Guid? AccountId = null,
    Guid? TemplateId = null,
    Guid? EventId = null,
    DateTimeOffset? SendAt = null,
    string TimeZone = "UTC",
    int RepeatDays = 0,
    int Cycles = 1,
    bool Acknowledgment = false,
    bool Pinned = false,
    DateTimeOffset? ExpiresAt = null,
    long Version = 1,
    int? EventOffsetMinutes = null
);

public record BroadcastActionInput(string Action, long Version);

public record TemplateInput(
    Guid OrgId,
    string Name,
    string Body,
    string Channel = "InApp",
    string Subject = "",
    string Category = "General",
    string Language = "en",
    bool Active = true,
    long Version = 1,
    string ProviderName = ""
);

public record AudienceInput(Guid OrgId, string Name, string Rules = "{}", long Version = 1);

public record AudiencePreviewInput(
    Guid OrgId,
    string Rules = "{}",
    string Channel = "InApp",
    string Purpose = "Promotional"
);

public record GreetingInput(AutomationInput Rule, TemplateInput Template);

public record AutomationInput(
    Guid OrgId,
    string Name,
    string Trigger,
    Guid TemplateId,
    Guid? AccountId = null,
    int DelayMinutes = 0,
    int DailyLimit = 100,
    bool Active = false,
    long Version = 1
);

public static partial class CommunicationEndpoints
{
    private static void MapBroadcasts(RouteGroupBuilder api)
    {
        api.MapGet(
            "/audiences",
            async (CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.manage");
                return await s
                    .Db.CommunicationAudiences.Where(x =>
                        x.TenantId == w.TenantId
                        && s.A.Visible(w).Select(o => o.Id).Contains(x.OrgId)
                    )
                    .OrderBy(x => x.Name)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/audiences",
            async (AudienceInput r, CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.manage");
                await s.Scope(w, r.OrgId);
                await s.Audience(
                    CommunicationService.Within(w, r.OrgId),
                    CommunicationService.Parse(r.Rules)
                );
                Check(r.Name.Length is > 1 and <= 120, "Name required.");
                var row = new CommunicationAudience
                {
                    TenantId = w.TenantId,
                    OrgId = r.OrgId,
                    Name = r.Name,
                    Rules = r.Rules,
                };
                s.Db.CommunicationAudiences.Add(row);
                await s.Db.SaveChangesAsync();
                return row;
            }
        );
        api.MapPost(
            "/audiences/preview",
            async (AudiencePreviewInput r, CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.manage");
                await s.Scope(w, r.OrgId);
                Check(CommunicationService.Channels.Contains(r.Channel), "Invalid channel.");
                var ids = await s.Audience(
                    CommunicationService.Within(w, r.OrgId),
                    CommunicationService.Parse(r.Rules)
                );
                var rows = await s
                    .Db.Persons.Where(p => ids.Contains(p.Id))
                    .OrderBy(p => p.Id)
                    .Take(30)
                    .Select(p => new
                    {
                        p.Id,
                        p.UserId,
                        p.DisplayName,
                    })
                    .ToListAsync();
                var preview = new List<object>();
                foreach (var p in rows)
                {
                    var contact = await s.Contact(w.TenantId, p.UserId, r.Channel, r.Purpose);
                    preview.Add(
                        new
                        {
                            p.Id,
                            p.DisplayName,
                            reachable = contact.Allowed,
                            reason = contact.Reason,
                        }
                    );
                }
                return new
                {
                    eligible = await ids.CountAsync(),
                    sample = preview,
                    sampleSize = rows.Count,
                    identity = "Distinct PersonId across overlapping rules",
                    note = "Reachability is a bounded sample; suppression and current consent are rechecked immediately before dispatch. Raw contacts are never exposed.",
                };
            }
        );
        api.MapGet(
            "/templates",
            async (CommunicationService s) =>
            {
                var w = await s.A.WorkspaceAny("communications.manage", "communications.inbox");
                return await s
                    .Db.CommunicationTemplates.Where(t =>
                        t.TenantId == w.TenantId
                        && s.A.Visible(w).Select(o => o.Id).Contains(t.OrgId)
                    )
                    .OrderBy(t => t.Name)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/templates",
            async (TemplateInput r, CommunicationService s) => await SaveTemplate(null, r, s)
        );
        api.MapPut(
            "/templates/{id:guid}",
            async (Guid id, TemplateInput r, CommunicationService s) => await SaveTemplate(id, r, s)
        );
        api.MapGet(
            "/revisions/{id:guid}",
            async (Guid id, CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.manage");
                var org =
                    await s
                        .Db.CommunicationTemplates.Where(t =>
                            t.Id == id && t.TenantId == w.TenantId
                        )
                        .Select(t => (Guid?)t.OrgId)
                        .SingleOrDefaultAsync()
                    ?? await s
                        .Db.CommunicationBroadcasts.Where(b =>
                            b.Id == id && b.TenantId == w.TenantId
                        )
                        .Select(b => (Guid?)b.OrgId)
                        .SingleOrDefaultAsync();
                Check(org != null, "Resource unavailable.", 404);
                await s.Scope(w, org.Value);
                return await s
                    .Db.CommunicationRevisions.Where(v => v.ResourceId == id)
                    .OrderByDescending(v => v.Version)
                    .Take(30)
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/campaigns",
            async (CommunicationService s, string kind = "Campaign", Guid? after = null) =>
            {
                var w = await s.A.Workspace("communications.manage");
                var q = s.Db.CommunicationBroadcasts.Where(b =>
                    b.TenantId == w.TenantId
                    && b.Kind == kind
                    && s.A.Visible(w).Select(o => o.Id).Contains(b.OrgId)
                );
                if (after != null)
                    q = q.Where(b => b.Id.CompareTo(after.Value) > 0);
                return await q.OrderBy(b => b.Id)
                    .Take(51)
                    .Select(b => new
                    {
                        broadcast = b,
                        recipients = s.Db.CommunicationRecipients.Count(r => r.BroadcastId == b.Id),
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/campaigns",
            async (BroadcastInput r, CommunicationService s) => await SaveBroadcast(null, r, s)
        );
        api.MapPut(
            "/campaigns/{id:guid}",
            async (Guid id, BroadcastInput r, CommunicationService s) =>
                await SaveBroadcast(id, r, s)
        );
        api.MapGet(
            "/campaigns/{id:guid}",
            async (Guid id, CommunicationService s) =>
            {
                var b = await s.Broadcast(id);
                return new
                {
                    broadcast = b,
                    recipients = await s.Db.CommunicationRecipients.CountAsync(r =>
                        r.BroadcastId == id
                    ),
                    metrics = await BroadcastMetrics(id, s),
                };
            }
        );
        api.MapGet(
            "/campaigns/{id:guid}/recipients",
            async (Guid id, CommunicationService s, Guid? after = null) =>
            {
                await s.Broadcast(id);
                var q = s.Db.CommunicationRecipients.Where(r => r.BroadcastId == id);
                if (after != null)
                    q = q.Where(r => r.Id.CompareTo(after.Value) > 0);
                var rows = await q.OrderBy(r => r.Id)
                    .Take(51)
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.Club,
                        r.QueuedCycle,
                        r.Exclusion,
                        r.SeenAt,
                        r.AcknowledgedAt,
                    })
                    .ToListAsync();
                return new
                {
                    items = rows.Take(50),
                    next = rows.Count > 50 ? rows[49].Id : (Guid?)null,
                };
            }
        );
        api.MapPost(
            "/campaigns/{id:guid}/status",
            async (Guid id, BroadcastActionInput r, CommunicationService s) =>
            {
                var b = await s.Broadcast(id);
                var w = await s.A.Workspace("communications.manage");
                Check(b.Version == r.Version, "Broadcast changed.", 409);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"CommunicationBroadcasts\" WHERE \"Id\"={id} FOR UPDATE"
                );
                await s.Db.Entry(b).ReloadAsync();
                Check(b.Version == r.Version, "Broadcast changed.", 409);
                Dictionary<string, string>? frozenContext = null;
                if (r.Action == "Submit")
                {
                    Check(b.Status == "Draft", "Only a draft can be submitted.", 409);
                    await s.Db.SaveChangesAsync();
                    await s.Freeze(b, w);
                    Check(
                        await s.Db.CommunicationRecipients.AnyAsync(x => x.BroadcastId == id),
                        "Audience is empty."
                    );
                    frozenContext = await s.Context(b);
                    b.ContentHash = Hash(
                        GovernanceService.Pack(
                            new
                            {
                                b.Title,
                                b.Subject,
                                b.Body,
                                b.Rules,
                                b.Channel,
                                b.AccountId,
                                b.EventId,
                                b.SendAt,
                                b.RepeatDays,
                                b.Cycles,
                                b.Purpose,
                                b.Acknowledgment,
                                b.ExpiresAt,
                                frozenContext,
                            }
                        )
                    );
                    var req = await s.Governance.Request(
                        w,
                        b.OrgId,
                        "CommunicationBroadcast",
                        "Authorize " + b.Kind + ": " + b.Title,
                        GovernanceService.Pack(
                            new BroadcastReference(id, b.Version + 1, b.ContentHash)
                        )
                    );
                    b.ApprovalRequestId = req.Id;
                    b.Status = "Review";
                    await s.Governance.Submit(req, w);
                }
                else if (r.Action == "Schedule")
                {
                    Check(
                        b.Status == "Approved",
                        "Approve this immutable content and audience first.",
                        409
                    );
                    b.Status = "Scheduled";
                }
                else if (r.Action == "Cancel")
                {
                    Check(b.Status is not "Cancelled" and not "Archived", "Already closed.", 409);
                    b.Status = "Cancelled";
                    var mids = s
                        .Db.CommunicationMessages.Where(m => m.BroadcastId == id)
                        .Select(m => m.Id);
                    await s
                        .Db.Mails.Where(m =>
                            m.CommunicationId != null
                            && mids.Contains(m.CommunicationId.Value)
                            && m.SentAt == null
                        )
                        .ExecuteUpdateAsync(x =>
                            x.SetProperty(m => m.Suppressed, true)
                                .SetProperty(m => m.DeliveryStatus, "Cancelled")
                        );
                    if (b.ApprovalRequestId != null)
                    {
                        var req = await s.Db.GovernanceRequests.SingleAsync(x =>
                            x.Id == b.ApprovalRequestId
                        );
                        if (req.Status is "Submitted" or "Under Review" or "Changes Requested")
                            req.Status = "Cancelled";
                    }
                }
                else if (r.Action == "Archive")
                {
                    Check(
                        b.Status is "Completed" or "Cancelled" or "Partially Completed" or "Failed",
                        "Complete or cancel first.",
                        409
                    );
                    b.Status = "Archived";
                }
                else if (r.Action == "Duplicate")
                {
                    var copy = JsonSerializer.Deserialize<CommunicationBroadcast>(
                        JsonSerializer.Serialize(b)
                    )!;
                    copy.Id = Guid.NewGuid();
                    copy.Title += " · copy";
                    copy.Status = "Draft";
                    copy.Version = 1;
                    copy.ApprovalRequestId = null;
                    copy.ContentHash = "";
                    copy.CompletedAt = null;
                    copy.CurrentCycle = 1;
                    copy.At = DateTimeOffset.UtcNow;
                    s.Db.CommunicationBroadcasts.Add(copy);
                    await s.Db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return Results.Ok(copy);
                }
                else
                    throw new ApiError(400, "Unknown broadcast action.");
                b.Version++;
                s.Revision(b, b.Version, "Broadcast", w.UserId, frozenContext);
                s.Audit(w, b.OrgId, "broadcast." + r.Action.ToLowerInvariant(), id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(b);
            }
        );
        api.MapGet(
            "/announcements",
            async (CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.view");
                var u = await s.A.User();
                return await s
                    .Db.CommunicationBroadcasts.Where(b =>
                        b.TenantId == w.TenantId
                        && b.Kind == "Announcement"
                        && new[] { "Sending", "Completed", "Partially Completed" }.Contains(
                            b.Status
                        )
                        && b.SendAt <= DateTimeOffset.UtcNow
                        && (b.ExpiresAt == null || b.ExpiresAt > DateTimeOffset.UtcNow)
                        && s.Db.CommunicationRecipients.Any(r =>
                            r.BroadcastId == b.Id && r.UserId == u.Id && r.Exclusion == ""
                        )
                    )
                    .OrderByDescending(b => b.Pinned)
                    .ThenByDescending(b => b.SendAt)
                    .Take(50)
                    .Select(b => new
                    {
                        b.Id,
                        b.Title,
                        Body = s.Db.CommunicationMessages.Where(m =>
                                m.BroadcastId == b.Id
                                && s.Db.Persons.Any(p => p.Id == m.PersonId && p.UserId == u.Id)
                            )
                            .OrderByDescending(m => m.At)
                            .Select(m => m.Body)
                            .FirstOrDefault()
                            ?? b.Body,
                        b.Category,
                        b.Acknowledgment,
                        b.Pinned,
                        b.SendAt,
                        b.ExpiresAt,
                        b.Version,
                        publisher = s
                            .Db.Users.Where(u => u.Id == b.CreatorId)
                            .Select(u => u.Name)
                            .First(),
                        acknowledged = s.Db.CommunicationRecipients.Any(r =>
                            r.BroadcastId == b.Id && r.UserId == u.Id && r.Exclusion == "" && r.AcknowledgedAt != null
                        ),
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/announcements/{id:guid}/acknowledge",
            async (Guid id, CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.view");
                var u = await s.A.User();
                var b = await s.Db.CommunicationBroadcasts.SingleOrDefaultAsync(b =>
                    b.Id == id
                    && b.TenantId == w.TenantId
                    && b.Kind == "Announcement"
                    && new[] { "Sending", "Completed", "Partially Completed" }.Contains(b.Status)
                    && (b.ExpiresAt == null || b.ExpiresAt > DateTimeOffset.UtcNow)
                );
                Check(b != null, "Announcement unavailable.", 404);
                var r = await s.Db.CommunicationRecipients.SingleOrDefaultAsync(r =>
                    r.BroadcastId == id && r.UserId == u.Id && r.Exclusion == ""
                );
                Check(r != null, "Announcement is outside your audience.", 404);
                r.SeenAt ??= DateTimeOffset.UtcNow;
                if (b.Acknowledgment)
                    r.AcknowledgedAt ??= DateTimeOffset.UtcNow;
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapGet(
            "/schedules",
            async (CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.manage");
                return await s
                    .Db.CommunicationBroadcasts.Where(b =>
                        b.TenantId == w.TenantId
                        && s.A.Visible(w).Select(o => o.Id).Contains(b.OrgId)
                        && new[] { "Scheduled", "Sending", "Approved" }.Contains(b.Status)
                    )
                    .OrderBy(b => b.SendAt)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/automations",
            async (CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.manage");
                return await s
                    .Db.CommunicationAutomations.Where(r =>
                        r.TenantId == w.TenantId
                        && s.A.Visible(w).Select(o => o.Id).Contains(r.OrgId)
                    )
                    .OrderBy(r => r.Name)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/greetings",
            async (GreetingInput r, CommunicationService s) =>
            {
                Check(r.Rule.Trigger is "Birthday" or "Anniversary", "Choose birthday or marriage anniversary.");
                Check(r.Rule.OrgId == r.Template.OrgId, "Greeting and template must use the same organization.");
                Check(r.Template.Channel is "InApp" or "Email", "Use in-app or email for a custom greeting.");
                Check(!System.Text.RegularExpressions.Regex.IsMatch(r.Template.Body + r.Template.Subject,
                    @"\{\{(event_name|event_date|event_location|rsvp_link)\}\}"), "Greeting messages cannot require an event.");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var template = await SaveTemplate(null, r.Template with { Active = true }, s);
                var rule = await SaveAutomation(null, r.Rule with { TemplateId = template.Id }, s);
                await tx.CommitAsync();
                return rule;
            }
        );
        api.MapPost(
            "/automations",
            async (AutomationInput r, CommunicationService s) => await SaveAutomation(null, r, s)
        );
        api.MapPut(
            "/automations/{id:guid}",
            async (Guid id, AutomationInput r, CommunicationService s) =>
                await SaveAutomation(id, r, s)
        );
        api.MapPost(
            "/automations/{id:guid}/submit",
            async (Guid id, CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.manage");
                var rule = await s.Db.CommunicationAutomations.SingleOrDefaultAsync(r =>
                    r.Id == id && r.TenantId == w.TenantId
                );
                Check(rule != null, "Rule unavailable.", 404);
                await s.Scope(w, rule.OrgId);
                Check(rule.Definition=="","Use the reviewed cross-module workflow designer.",409);
                Check(rule.Status == "Draft", "Only drafts may be submitted.", 409);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                rule.ContentHash = Hash(
                    GovernanceService.Pack(
                        new
                        {
                            rule.Trigger,
                            rule.TemplateId,
                            rule.TemplateVersion,
                            rule.AccountId,
                            rule.DelayMinutes,
                            rule.DailyLimit,
                        }
                    )
                );
                rule.Status = "Review";
                rule.Version++;
                var req = await s.Governance.Request(
                    w,
                    rule.OrgId,
                    "CommunicationAutomation",
                    "Authorize automation: " + rule.Name,
                    GovernanceService.Pack(
                        new
                        {
                            id,
                            version = rule.Version,
                            hash = rule.ContentHash,
                            type = "Automation",
                            title = rule.Name,
                            rule.Trigger,
                            rule.DelayMinutes,
                            rule.DailyLimit,
                            rule.TemplateId,
                            rule.TemplateVersion,
                            body = s
                                .Db.CommunicationTemplates.Where(t => t.Id == rule.TemplateId)
                                .Select(t => t.Body)
                                .Single(),
                            subject = s
                                .Db.CommunicationTemplates.Where(t => t.Id == rule.TemplateId)
                                .Select(t => t.Subject)
                                .Single(),
                            channel = s
                                .Db.CommunicationTemplates.Where(t => t.Id == rule.TemplateId)
                                .Select(t => t.Channel)
                                .Single(),
                            purpose = "Promotional / opt-in only",
                        }
                    )
                );
                rule.ApprovalRequestId = req.Id;
                await s.Governance.Submit(req, w);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return rule;
            }
        );
        api.MapGet(
            "/automations/{id:guid}/executions",
            async (Guid id, CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.manage");
                var rule = await s.Db.CommunicationAutomations.SingleOrDefaultAsync(r =>
                    r.Id == id && r.TenantId == w.TenantId
                );
                Check(rule != null, "Rule unavailable.", 404);
                await s.Scope(w, rule.OrgId);
                return await s
                    .Db.CommunicationExecutions.Where(e => e.AutomationId == id)
                    .OrderByDescending(e => e.At)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/analytics",
            async (CommunicationService s, int days = 30, string channel = "") =>
            {
                var w = await s.A.Workspace("communications.reports");
                days = Math.Clamp(days, 1, 366);
                var start = DateTimeOffset.UtcNow.AddDays(-days);
                var q = s.Db.Mails.Where(m =>
                    m.TenantId == w.TenantId
                    && m.OrgId != null
                    && s.A.Visible(w).Select(o => o.Id).Contains(m.OrgId.Value)
                    && m.CreatedAt >= start
                );
                if (channel != "")
                    q = q.Where(m => m.Channel == channel);
                var statuses = await q.GroupBy(m => new { m.Channel, m.DeliveryStatus })
                    .Select(g => new
                    {
                        g.Key.Channel,
                        g.Key.DeliveryStatus,
                        count = g.Count(),
                    })
                    .ToListAsync();
                var campaigns = s.Db.CommunicationBroadcasts.Where(b =>
                    b.TenantId == w.TenantId && s.A.Visible(w).Select(o => o.Id).Contains(b.OrgId)
                );
                return new
                {
                    statuses,
                    campaigns = await campaigns.CountAsync(),
                    acknowledgments = await s.Db.CommunicationRecipients.CountAsync(r =>
                        campaigns.Select(b => b.Id).Contains(r.BroadcastId)
                        && r.AcknowledgedAt != null
                    ),
                    cost = await q.SumAsync(m => m.Cost),
                    costAvailable = await q.AnyAsync(m => m.Cost != null),
                    opens = "Not Available",
                    clicks = "Not Available",
                    denominators = "Attempted: outbox records with Attempts > 0 or SentAt. Accepted: provider/SMTP accepted, not verified delivery. Delivered/read: persisted in-app visibility or authenticated provider status only. No tracking pixels. Legacy mail without a created timestamp is excluded from period reports.",
                    attempted = await q.CountAsync(m => m.Attempts > 0 || m.SentAt != null),
                    pending = await q.CountAsync(m =>
                        m.DeliveryStatus == "Pending" && !m.Suppressed && !m.DeadLetter
                    ),
                    trend = await q.Where(m => m.SentAt != null)
                        .GroupBy(m => new
                        {
                            year = m.SentAt!.Value.Year,
                            month = m.SentAt.Value.Month,
                            day = m.SentAt.Value.Day,
                        })
                        .Select(g => new
                        {
                            g.Key.year,
                            g.Key.month,
                            g.Key.day,
                            count = g.Count(),
                        })
                        .OrderBy(g => g.year)
                        .ThenBy(g => g.month)
                        .ThenBy(g => g.day)
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/audit",
            async (CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.reports");
                return await s
                    .Db.Audits.Where(a =>
                        a.TenantId == w.TenantId
                        && a.OrgId != null
                        && s.A.Visible(w).Select(o => o.Id).Contains(a.OrgId.Value)
                        && a.Action.StartsWith("communications.")
                    )
                    .OrderByDescending(a => a.At)
                    .Take(100)
                    .ToListAsync();
            }
        );
    }

    private static async Task<object> BroadcastMetrics(Guid id, CommunicationService s)
    {
        var ids = s.Db.CommunicationMessages.Where(m => m.BroadcastId == id).Select(m => m.Id);
        return new
        {
            delivery = await s
                .Db.Mails.Where(m =>
                    m.CommunicationId != null && ids.Contains(m.CommunicationId.Value)
                )
                .GroupBy(m => m.DeliveryStatus)
                .Select(g => new { status = g.Key, count = g.Count() })
                .ToListAsync(),
            queued = await s.Db.CommunicationRecipients.CountAsync(r =>
                r.BroadcastId == id && r.QueuedCycle > 0
            ),
            acknowledged = await s.Db.CommunicationRecipients.CountAsync(r =>
                r.BroadcastId == id && r.AcknowledgedAt != null
            ),
            excluded = await s.Db.CommunicationRecipients.CountAsync(r =>
                r.BroadcastId == id && r.Exclusion != ""
            ),
        };
    }

    private static async Task<CommunicationTemplate> SaveTemplate(
        Guid? id,
        TemplateInput r,
        CommunicationService s
    )
    {
        var w = await s.A.Workspace("communications.manage");
        await s.Scope(w, r.OrgId);
        CommunicationService.Content(r.Body, r.Subject);
        Check(
            r.Name.Length is > 1 and <= 120
                && CommunicationService.Channels.Contains(r.Channel)
                && r.Language.Length is > 0 and <= 20
                && r.ProviderName.Length <= 100,
            "Invalid template."
        );
        var row =
            id == null
                ? new CommunicationTemplate { TenantId = w.TenantId, OrgId = r.OrgId }
                : await s.Db.CommunicationTemplates.SingleOrDefaultAsync(t =>
                    t.Id == id && t.TenantId == w.TenantId
                ) ?? throw new ApiError(404, "Template unavailable.");
        await s.Scope(w, row.OrgId);
        Check(id == null || row.Version == r.Version, "Template changed.", 409);
        if (id == null)
            s.Db.CommunicationTemplates.Add(row);
        else
            row.Version++;
        row.Name = r.Name;
        row.Body = r.Body;
        row.Subject = r.Subject;
        row.Channel = r.Channel;
        row.Category = r.Category;
        row.Language = r.Language;
        row.Active = r.Active;
        row.ProviderName = r.ProviderName;
        row.ProviderStatus = "Not synchronized";
        s.Revision(row, row.Version, "Template", w.UserId);
        await s.Db.SaveChangesAsync();
        return row;
    }

    private static async Task<CommunicationBroadcast> SaveBroadcast(
        Guid? id,
        BroadcastInput r,
        CommunicationService s
    )
    {
        var w = await s.A.Workspace("communications.manage");
        await s.Scope(w, r.OrgId);
        CommunicationService.Content(r.Body, r.Subject);
        Check(
            r.Title.Length is > 1 and <= 160
                && CommunicationService.Channels.Contains(r.Channel)
                && new[] { "Campaign", "Announcement" }.Contains(r.Kind)
                && new[] { "Promotional", "Transactional" }.Contains(r.Purpose)
                && r.RepeatDays is >= 0 and <= 365
                && r.Cycles is >= 1 and <= 52,
            "Invalid broadcast."
        );
        Check(
            r.Kind != "Announcement" || r.Channel == "InApp",
            "Announcements publish in-app; create a related campaign for external channels."
        );
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(r.TimeZone);
        }
        catch
        {
            throw new ApiError(400, "Unknown time zone.");
        }
        var rules = CommunicationService.Parse(r.Rules);
        await s.Audience(CommunicationService.Within(w, r.OrgId), rules);
        if (r.AccountId != null)
        {
            var account = await s.Db.CommunicationAccounts.SingleOrDefaultAsync(a =>
                a.Id == r.AccountId
                && a.TenantId == w.TenantId
                && a.Channel == r.Channel
                && a.OrgId == r.OrgId
            );
            Check(
                account != null && account.Enabled && account.Verified,
                "Use a verified sending account at the originating organization.",
                409
            );
        }
        Check(
            r.Channel == "InApp" || r.AccountId != null,
            "Select a verified provider account.",
            409
        );
        if (r.TemplateId != null)
        {
            var t = await s.Db.CommunicationTemplates.SingleOrDefaultAsync(t =>
                t.Id == r.TemplateId
                && t.TenantId == w.TenantId
                && t.Channel == r.Channel
                && t.Active
            );
            Check(t != null, "Template unavailable.");
            await s.Scope(w, t.OrgId);
            Check(
                t.Body == r.Body && t.Subject == r.Subject,
                "Use the selected template's current content."
            );
            if (r.Channel == "WhatsApp")
                Check(
                    t.ProviderStatus == "APPROVED" && t.AccountId == r.AccountId,
                    "Synchronize an approved WhatsApp template first.",
                    409
                );
        }
        Check(
            r.Channel != "WhatsApp" || r.TemplateId != null,
            "WhatsApp broadcasts require an approved template.",
            409
        );
        ClubEvent? ev = null;
        if (r.EventId != null)
        {
            ev = await s.Db.Events.SingleOrDefaultAsync(e =>
                e.Id == r.EventId && e.TenantId == w.TenantId
            );
            Check(ev != null, "Event unavailable.");
            await s.Scope(w, ev.OrgId);
        }
        Check(
            ev != null
                || !System.Text.RegularExpressions.Regex.IsMatch(
                    r.Body + r.Subject,
                    @"\{\{(event_name|event_date|event_location|rsvp_link)\}\}"
                ),
            "Selected variables require an event."
        );
        var row =
            id == null
                ? new CommunicationBroadcast
                {
                    TenantId = w.TenantId,
                    OrgId = r.OrgId,
                    CreatorId = w.UserId,
                }
                : await s.Broadcast(id.Value);
        Check(
            id == null || row.Status == "Draft",
            "Approved/submitted content is immutable. Duplicate it for a new reviewed revision.",
            409
        );
        Check(id == null || row.Version == r.Version, "Broadcast changed.", 409);
        if (id == null)
            s.Db.CommunicationBroadcasts.Add(row);
        else
            row.Version++;
        row.Title = r.Title;
        row.Body = r.Body;
        row.Subject = r.Subject;
        row.Channel = r.Channel;
        row.Kind = r.Kind;
        row.Category = r.Category;
        row.Purpose = r.Purpose;
        row.Rules = r.Rules;
        row.AccountId = r.AccountId;
        row.TemplateId = r.TemplateId;
        row.TemplateVersion =
            r.TemplateId == null
                ? 0
                : await s
                    .Db.CommunicationTemplates.Where(t => t.Id == r.TemplateId)
                    .Select(t => t.Version)
                    .SingleAsync();
        row.EventId = r.EventId;
        row.SendAt =
            r.EventOffsetMinutes != null && ev != null
                ? ev.StartsAt.AddMinutes(r.EventOffsetMinutes.Value)
                : (r.SendAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        row.TimeZone = r.TimeZone;
        row.RepeatDays = r.RepeatDays;
        row.Cycles = r.Cycles;
        Check(row.Cycles == 1 || row.RepeatDays > 0, "Recurring campaigns need an interval.");
        row.Acknowledgment = r.Acknowledgment;
        row.Pinned = r.Pinned;
        row.ExpiresAt = r.ExpiresAt;
        s.Revision(row, row.Version, "Broadcast", w.UserId);
        s.Audit(w, row.OrgId, "broadcast.saved", row.Id);
        await s.Db.SaveChangesAsync();
        return row;
    }

    private static async Task<CommunicationAutomation> SaveAutomation(
        Guid? id,
        AutomationInput r,
        CommunicationService s
    )
    {
        var w = await s.A.Workspace("communications.manage");
        await s.Scope(w, r.OrgId);
        Check(
            r.Name.Length is > 1 and <= 120
                && new[]
                {
                    "membership.activated",
                    "application.submitted",
                    "application.approved",
                    "event.invitation",
                    "event.registration",
                    "event.notice",
                    "event.cancelled",
                    "leadership.transition",
                    "membership.renewed",
                    "Birthday",
                    "Anniversary",
                    "RenewalDue",
                    "OfficeExpiring",
                }.Contains(r.Trigger)
                && r.DelayMinutes is >= 0 and <= 525600
                && r.DailyLimit is > 0 and <= 1000,
            "Invalid rule."
        );
        var t = await s.Db.CommunicationTemplates.SingleOrDefaultAsync(t =>
            t.Id == r.TemplateId && t.TenantId == w.TenantId && t.Active
        );
        Check(t != null, "Template unavailable.");
        await s.Scope(w, t.OrgId);
        Check(t.Channel == "InApp" || r.AccountId != null, "Select a provider account.");
        if (r.AccountId != null)
            Check(
                await s.Db.CommunicationAccounts.AnyAsync(a =>
                    a.Id == r.AccountId
                    && a.TenantId == w.TenantId
                    && a.OrgId == r.OrgId
                    && a.Channel == t.Channel
                    && a.Verified
                    && a.Enabled
                ),
                "Account unavailable."
            );
        // Automations are personal optional notifications, never a way around broadcast approval.
        var row =
            id == null
                ? new CommunicationAutomation
                {
                    TenantId = w.TenantId,
                    OrgId = r.OrgId,
                    CreatorId = w.UserId,
                }
                : await s.Db.CommunicationAutomations.SingleOrDefaultAsync(a =>
                    a.Id == id && a.TenantId == w.TenantId
                ) ?? throw new ApiError(404, "Rule unavailable.");
        Check(row.Definition=="","Use the cross-module workflow revision flow.",409);
        await s.Scope(w, row.OrgId);
        Check(id == null || row.Version == r.Version, "Rule changed.", 409);
        if (id == null)
            s.Db.CommunicationAutomations.Add(row);
        else
            row.Version++;
        row.Name = r.Name;
        row.Trigger = r.Trigger;
        row.TemplateId = r.TemplateId;
        row.AccountId = r.AccountId;
        row.DelayMinutes = r.DelayMinutes;
        row.DailyLimit = r.DailyLimit;
        row.Active = false;
        row.Status = "Draft";
        row.TemplateVersion = t.Version;
        row.ApprovalRequestId = null;
        s.Revision(row, row.Version, "Automation", w.UserId);
        s.Audit(w, row.OrgId, "automation.saved", row.Id);
        await s.Db.SaveChangesAsync();
        return row;
    }
}
