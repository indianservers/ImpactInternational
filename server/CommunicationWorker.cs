using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class CommunicationWorker(
    IServiceScopeFactory scopes,
    ILogger<CommunicationWorker> log,
    CommunicationStorage storage
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            var work = false;
            try
            {
                using var scope = scopes.CreateScope();
                var s = scope.ServiceProvider.GetRequiredService<CommunicationService>();
                await using var tx = await s.Db.Database.BeginTransactionAsync(stop);
                await s.Db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(511105)",
                    stop
                );
                var expired = await s
                    .Db.CommunicationAttachments.Where(f =>
                        f.ExpiresAt < DateTimeOffset.UtcNow && f.StorageKey != ""
                    )
                    .Take(100)
                    .ToListAsync(stop);
                foreach (var file in expired)
                {
                    var path = storage.PathFor(file.StorageKey);
                    if (File.Exists(path))
                        File.Delete(path);
                    file.StorageKey = "";
                }
                var retentionNow = DateTimeOffset.UtcNow;
                await s
                    .Db.CommunicationMessages.Where(m =>
                        m.Body != "[Expired by retention]"
                        && m.At
                            < retentionNow.AddDays(
                                -(
                                    s.Db.CommunicationPolicies.Where(p => p.OrgId == m.OrgId)
                                        .Select(p => (int?)p.RetentionDays)
                                        .FirstOrDefault()
                                    ?? 365
                                )
                            )
                    )
                    .OrderBy(m => m.At)
                    .Take(500)
                    .ExecuteUpdateAsync(
                        x =>
                            x.SetProperty(m => m.Body, "[Expired by retention]")
                                .SetProperty(m => m.Subject, ""),
                        stop
                    );
                var due = await s
                    .Db.CommunicationBroadcasts.Where(b =>
                        new[] { "Scheduled", "Sending" }.Contains(b.Status)
                        && b.SendAt <= DateTimeOffset.UtcNow
                    )
                    .OrderBy(b => b.SendAt)
                    .Take(10)
                    .ToListAsync();
                foreach (var b in due)
                {
                    work = true;
                    await s.Db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM \"CommunicationBroadcasts\" WHERE \"Id\"={b.Id} FOR NO KEY UPDATE",
                        stop
                    );
                    await s.Db.Entry(b).ReloadAsync();
                    if (b.Status is not "Scheduled" and not "Sending")
                        continue;
                    var sender = await s.SendingAssignment(b.TenantId, b.OrgId, b.CreatorId);
                    if (sender == null)
                    {
                        b.Status = "Failed";
                        b.Version++;
                        continue;
                    }
                    if (b.ExpiresAt <= DateTimeOffset.UtcNow)
                    {
                        b.Status = "Completed";
                        b.CompletedAt = DateTimeOffset.UtcNow;
                        continue;
                    }
                    var previousStatus = b.Status;
                    b.Status = "Sending";
                    var page = await s
                        .Db.CommunicationRecipients.Where(r =>
                            r.BroadcastId == b.Id && r.QueuedCycle < b.CurrentCycle
                        )
                        .OrderBy(r => r.Id)
                        .Take(250)
                        .ToListAsync();
                    var reference = GovernanceService.Unpack<BroadcastReference>(
                        await s
                            .Db.GovernanceRequests.Where(r => r.Id == b.ApprovalRequestId)
                            .Select(r => r.Payload)
                            .SingleAsync()
                    );
                    var snapshot = await s
                        .Db.CommunicationRevisions.Where(r =>
                            r.ResourceId == b.Id && r.Version == reference.Version
                        )
                        .Select(r => r.Snapshot)
                        .SingleAsync();
                    using var document = JsonDocument.Parse(snapshot);
                    var context = document.RootElement.TryGetProperty(
                        "ResolvedContext",
                        out var resolved
                    )
                        ? JsonSerializer.Deserialize<Dictionary<string, string>>(
                            resolved.GetRawText()
                        )!
                        : await s.Context(b);
                    var template =
                        b.TemplateId == null
                            ? null
                            : await s.Db.CommunicationTemplates.SingleAsync(t =>
                                t.Id == b.TemplateId
                            );
                    if (template != null && template.Version != b.TemplateVersion)
                    {
                        b.Status = "Failed";
                        b.Version++;
                        continue;
                    }
                    foreach (var r in page)
                    {
                        r.QueuedCycle = b.CurrentCycle;
                        if (r.UserId == null)
                        {
                            r.Exclusion = "No linked account";
                            continue;
                        }
                        string Render(string text) =>
                            CommunicationService.RenderFrozen(text, r, context);
                        var body = Render(b.Body);
                        var message = new CommunicationMessage
                        {
                            TenantId = b.TenantId,
                            OrgId = b.OrgId,
                            BroadcastId = b.Id,
                            SenderId = b.CreatorId,
                            PersonId = r.PersonId,
                            Body = body,
                            Subject = Render(b.Subject),
                            ClientKey =
                                "broadcast:" + b.Id + ":" + b.CurrentCycle + ":" + r.PersonId,
                        };
                        s.Db.CommunicationMessages.Add(message);
                        s.Db.Mails.Add(
                            new()
                            {
                                TenantId = b.TenantId,
                                OrgId = b.OrgId,
                                CommunicationId = message.Id,
                                EventId = b.EventId,
                                AccountId = b.AccountId,
                                SenderId = b.CreatorId,
                                SenderAssignmentId = sender.Id,
                                UserId = r.UserId,
                                Channel = b.Channel,
                                Purpose = b.Purpose,
                                Body = body,
                                Subject = message.Subject,
                                Kind = "communication.broadcast",
                                TokenHash = Access.Hash(Access.Token()),
                                ExpiresAt = b.ExpiresAt ?? DateTimeOffset.UtcNow.AddDays(30),
                                TemplateName = template?.ProviderName ?? "",
                                TemplateVersion = template?.Version ?? 0,
                                Language = template?.Language ?? "en",
                                TemplateParameters = JsonSerializer.Serialize(
                                    template?.Body.Contains("{{member_name}}") == true
                                        ? new[] { r.Name }
                                        : Array.Empty<string>()
                                ),
                            }
                        );
                    }
                    if (page.Count == 0)
                    {
                        var pending = await s.Db.Mails.AnyAsync(m =>
                            m.CommunicationId != null
                            && s.Db.CommunicationMessages.Any(cm =>
                                cm.Id == m.CommunicationId && cm.BroadcastId == b.Id
                            )
                            && m.SentAt == null
                            && !m.Suppressed
                            && !m.DeadLetter
                        );
                        if (!pending)
                        {
                            if (b.CurrentCycle < b.Cycles)
                            {
                                b.CurrentCycle++;
                                b.SendAt = CommunicationService.NextCycle(
                                    b.SendAt,
                                    b.TimeZone,
                                    b.RepeatDays
                                );
                                b.Status = "Scheduled";
                            }
                            else
                            {
                                var failures = await s.Db.Mails.AnyAsync(m =>
                                    m.CommunicationId != null
                                    && s.Db.CommunicationMessages.Any(cm =>
                                        cm.Id == m.CommunicationId && cm.BroadcastId == b.Id
                                    )
                                    && (m.DeadLetter || m.Suppressed)
                                );
                                b.Status = failures ? "Partially Completed" : "Completed";
                                b.CompletedAt = DateTimeOffset.UtcNow;
                            }
                        }
                    }
                    if (page.Count > 0 || b.Status != previousStatus)
                        b.Version++;
                }
                await s.Db.SaveChangesAsync(stop);
                await Automations(s, stop);
                await IntelligenceWorkflows.Process(s,stop);
                await IntelligenceKnowledge.Purge(s.Db,stop);
                await DeliverInApp(s, stop);
                await tx.CommitAsync(stop);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError(e,"Communication queue failed ({Type})", e.GetType().Name);
            }
            await Task.Delay(work ? 100 : 1500, stop);
        }
    }

    private static async Task DeliverInApp(CommunicationService s, CancellationToken stop)
    {
        var jobs = await s
            .Db.Mails.FromSqlRaw(
                "SELECT * FROM \"Mails\" WHERE \"Channel\"='InApp' AND \"SentAt\" IS NULL AND NOT \"Suppressed\" AND NOT \"DeadLetter\" AND \"ExpiresAt\">now() AND \"NextAttemptAt\"<=now() ORDER BY \"NextAttemptAt\" LIMIT 500 FOR UPDATE SKIP LOCKED"
            )
            .ToListAsync(stop);
        var uids = jobs.Select(m => m.UserId).ToArray();
        var prefs = await s
            .Db.CommunicationPreferences.Where(p => uids.Contains(p.UserId) && p.Channel == "InApp")
            .ToDictionaryAsync(p => p.UserId, stop);
        var tenantIds = jobs.Select(m => m.TenantId).ToArray();
        var suppressions = await s
            .Db.CommunicationSuppressions.Where(p =>
                p.Channel == "InApp" && tenantIds.Contains(p.TenantId)
            )
            .Select(p => new { p.TenantId, p.AddressHash })
            .ToListAsync(stop);
        var authority = new Dictionary<(Guid?, Guid, string), bool>();
        foreach (var m in jobs)
        {
            if(m.UserId==null||m.OrgId==null){m.Suppressed=true;m.DeliveryStatus="Suppressed";m.LastError="In-app recipient or organization unavailable.";continue;}
            var key = (
                m.SenderAssignmentId,
                m.OrgId!.Value,
                m.Kind.StartsWith("operations.") ? "operations.manage" : "communications.manage"
            );
            if (!authority.TryGetValue(key, out var allowed))
            {
                allowed = await s.Sender(
                    m.SenderAssignmentId,
                    m.OrgId.Value,
                    m.Kind.StartsWith("operations.") ? "operations.manage" : "communications.manage"
                );
                authority[key] = allowed;
            }
            if(m.Kind.StartsWith("finance.")){
 allowed=false;
 if(Guid.TryParse(m.Kind[8..],out var financialEntity)&&m.SenderAssignmentId!=null){var sender=await s.Db.Assignments.SingleOrDefaultAsync(x=>x.Id==m.SenderAssignmentId&&x.UserId==m.SenderId);allowed=sender!=null&&await s.A.Effective(sender)&&await s.Db.RolePermissions.AnyAsync(p=>p.RoleId==sender.RoleId&&p.PermissionId=="finance.manage")&&await s.Db.FinanceGrants.AnyAsync(g=>g.EntityId==financialEntity&&g.UserId==sender.UserId&&new[]{"Owner","Treasurer","Billing"}.Contains(g.Role))&&await s.Db.FinanceEntitys.AnyAsync(e=>e.Id==financialEntity&&e.TenantId==m.TenantId&&e.OrgId==m.OrgId&&e.Active);}
 }
            prefs.TryGetValue(m.UserId!.Value, out var pref);
            if (
                !allowed
                || pref?.Transactional == false
                || (m.Purpose == "Promotional" && pref?.Promotional != true)
                || suppressions.Any(p =>
                    p.TenantId == m.TenantId && p.AddressHash == Access.Hash(m.UserId.ToString()!)
                )
            )
            {
                m.Suppressed = true;
                m.DeliveryStatus = "Suppressed";
                continue;
            }
            if (pref != null && pref.QuietEnabled)
            {
                var zone = await s
                    .Db.Users.Where(u => u.Id == m.UserId)
                    .Select(u => u.TimeZone)
                    .SingleAsync(stop);
                var until = CommunicationService.QuietUntil(pref, zone, DateTimeOffset.UtcNow);
                if (until > DateTimeOffset.UtcNow)
                {
                    m.NextAttemptAt = until;
                    continue;
                }
            }
            m.SentAt = DateTimeOffset.UtcNow;
            m.DeliveryStatus = "Delivered";
            m.Attempts++;
            s.Db.MailDeliveryAttempts.Add(new() { MailId = m.Id, Succeeded = true });
        }
        await s.Db.SaveChangesAsync(stop);
    }

    private static async Task Automations(CommunicationService s, CancellationToken stop)
    {
        var rules = await s
            .Db.CommunicationAutomations.Where(r => r.Definition=="" && r.Active && r.Status == "Approved")
            .OrderBy(r => r.Id)
            .Take(100)
            .ToListAsync(stop);
        foreach (var rule in rules)
        {
            var w = await s.SendingAssignment(rule.TenantId, rule.OrgId, rule.CreatorId);
            if (w == null)
                continue;
            var count = await s.Db.CommunicationExecutions.CountAsync(
                e => e.AutomationId == rule.Id && e.At >= DateTimeOffset.UtcNow.Date,
                stop
            );
            var remaining = Math.Min(50, rule.DailyLimit - count);
            if (remaining <= 0)
                continue;
            var template = await s.Db.CommunicationTemplates.SingleAsync(
                t => t.Id == rule.TemplateId,
                stop
            );
            if (!template.Active || template.Version != rule.TemplateVersion)
                continue;
            var members = s.Db.Memberships.Where(m =>
                m.TenantId == rule.TenantId
                && m.Status == "Active"
                && s.A.Tree(rule.TenantId, rule.OrgId).Select(o => o.Id).Contains(m.ClubId)
            );
            var sources = new List<(string Key, Guid? User)>();
            var today = DateTimeOffset.UtcNow;
            var dayStart = new DateTimeOffset(today.UtcDateTime.Date, TimeSpan.Zero);
            if (rule.Trigger is "Birthday" or "Anniversary" or "RenewalDue" or "OfficeExpiring")
            {
                var persons = members.Select(m => m.PersonId);
                var q = s.Db.Persons.Where(p => persons.Contains(p.Id) && p.UserId != null);
                if (rule.Trigger == "Birthday")
                    q = q.Where(p =>
                        p.Privacy.BirthdayAnnouncements
                        && p.BirthDate != null
                        && p.BirthDate.Value.Month == today.Month
                        && p.BirthDate.Value.Day == today.Day
                    );
                if (rule.Trigger == "Anniversary")
                    q = q.Where(p =>
                        p.Anniversary != null
                        && p.Anniversary.Value.Month == today.Month
                        && p.Anniversary.Value.Day == today.Day
                    );
                if (rule.Trigger == "RenewalDue")
                    q = q.Where(p =>
                        members.Any(m =>
                            m.PersonId == p.Id
                            && (
                                (
                                    m.EndsAt >= dayStart.AddDays(30)
                                    && m.EndsAt < dayStart.AddDays(31)
                                )
                                || (
                                    m.EndsAt >= dayStart.AddDays(7)
                                    && m.EndsAt < dayStart.AddDays(8)
                                )
                            )
                        )
                    );
                if (rule.Trigger == "OfficeExpiring")
                    q = q.Where(p =>
                        GovernanceService
                            .Current(s.Db)
                            .Any(o =>
                                o.TenantId == rule.TenantId
                                && o.PersonId == p.Id
                                && (
                                    (
                                        o.EndsAt >= dayStart.AddDays(30)
                                        && o.EndsAt < dayStart.AddDays(31)
                                    )
                                    || (
                                        o.EndsAt >= dayStart.AddDays(7)
                                        && o.EndsAt < dayStart.AddDays(8)
                                    )
                                )
                            )
                    );
                var rows = await q.Where(p =>
                        s.Db.CommunicationPreferences.Any(n =>
                            n.UserId == p.UserId && n.Channel == template.Channel && n.Birthday
                        )
                        && !s.Db.CommunicationExecutions.Any(e =>
                            e.AutomationId == rule.Id
                            && e.SourceKey == today.ToString("yyyy-MM-dd") + ":" + p.Id
                        )
                    )
                    .OrderBy(p => p.Id)
                    .Take(remaining)
                    .Select(p => new { p.Id, p.UserId })
                    .ToListAsync(stop);
                sources.AddRange(
                    rows.Select(p => (today.ToString("yyyy-MM-dd") + ":" + p.Id, p.UserId))
                );
            }
            else
            {
                var rows = await s
                    .Db.Mails.Where(m =>
                        m.TenantId == rule.TenantId
                        && m.OrgId != null
                        && s.A.Tree(rule.TenantId, rule.OrgId)
                            .Select(o => o.Id)
                            .Contains(m.OrgId.Value)
                        && (
                            (
                                rule.Trigger == "event.registration"
                                && m.Kind == "event.update"
                                && m.Body.Contains("Registration confirmed")
                            )
                            || (
                                rule.Trigger == "event.notice"
                                && m.Kind == "event.update"
                                && m.Body.Contains("Event reminder")
                            )
                            || (
                                rule.Trigger == "event.cancelled"
                                && m.Kind == "event.update"
                                && m.Body.Contains("cancelled")
                            )
                            || m.Kind == rule.Trigger
                        )
                        && m.CommunicationId == null
                        && m.CreatedAt >= rule.StartsAt
                        && m.UserId != null
                        && !s.Db.CommunicationExecutions.Any(e =>
                            e.AutomationId == rule.Id && e.SourceKey == m.Id.ToString()
                        )
                    )
                    .OrderBy(m => m.Id)
                    .Take(remaining)
                    .Select(m => new { m.Id, m.UserId })
                    .ToListAsync(stop);
                sources.AddRange(rows.Select(m => (m.Id.ToString(), m.UserId)));
            }
            foreach (var source in sources)
            {
                var contact = await s.Contact(
                    rule.TenantId,
                    source.User,
                    template.Channel,
                    "Promotional"
                );
                var execution = new CommunicationExecution
                {
                    TenantId = rule.TenantId,
                    AutomationId = rule.Id,
                    SourceKey = source.Key,
                };
                s.Db.CommunicationExecutions.Add(execution);
                if (!contact.Allowed)
                {
                    execution.Status = "Suppressed";
                    continue;
                }
                var person = await s.Db.Persons.SingleAsync(p => p.UserId == source.User, stop);
                var member = await members
                    .OrderBy(m => m.Id)
                    .FirstAsync(m => m.PersonId == person.Id, stop);
                var club = await s.Db.Orgs.SingleAsync(o => o.Id == member.ClubId, stop);
                var b = new CommunicationBroadcast
                {
                    TenantId = rule.TenantId,
                    OrgId = rule.OrgId,
                    CreatorId = rule.CreatorId,
                };
                if (Guid.TryParse(source.Key, out var sourceId))
                    b.EventId = await s
                        .Db.Mails.Where(m => m.Id == sourceId)
                        .Select(m => m.EventId)
                        .SingleOrDefaultAsync(stop);
                string body;
                string subject;
                try
                {
                    body = await s.Render(
                        b,
                        new()
                        {
                            Name = person.DisplayName,
                            Club = club.Name,
                            MemberNumber = member.Number,
                        },
                        template.Body
                    );
                    subject = await s.Render(b, new()
                    {
                        Name = person.DisplayName,
                        Club = club.Name,
                        MemberNumber = member.Number,
                    }, template.Subject);
                }
                catch (ApiError)
                {
                    execution.Status = "Missing template context";
                    continue;
                }
                var m = new Mail
                {
                    TenantId = rule.TenantId,
                    OrgId = rule.OrgId,
                    SenderId = rule.CreatorId,
                    SenderAssignmentId = w.Id,
                    UserId = source.User,
                    Recipient = contact.Address,
                    AccountId = rule.AccountId,
                    Channel = template.Channel,
                    Purpose = "Promotional",
                    Subject = subject,
                    Body = body,
                    Kind = "communication.automation",
                    EventId = b.EventId,
                    TemplateVersion = template.Version,
                    TemplateParameters = JsonSerializer.Serialize(
                        template.Body.Contains("{{member_name}}")
                            ? new[] { person.DisplayName }
                            : Array.Empty<string>()
                    ),
                    NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(rule.DelayMinutes),
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                    TokenHash = Access.Hash(Access.Token()),
                    TemplateName = template.ProviderName,
                    Language = template.Language,
                };
                s.Db.Mails.Add(m);
                execution.MailId = m.Id;
            }
        }
        await s.Db.SaveChangesAsync(stop);
    }
}
