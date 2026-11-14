using Microsoft.EntityFrameworkCore;

namespace Clubs;

public static class EventSetup
{
    public static readonly string[] Permissions =
    [
        "events.view",
        "events.manage",
        "events.checkin",
        "events.reports",
        "events.configure",
    ];

    public static async Task Tenant(Db db, Guid tenant)
    {
        if (!await db.EventTypes.AnyAsync(x => x.TenantId == tenant))
            foreach (
                var name in new[]
                {
                    "Regular Club Meeting",
                    "Fellowship Meeting",
                    "Board Meeting",
                    "Committee Meeting",
                    "Service Activity",
                    "Training Program",
                    "Workshop",
                    "Leadership Program",
                    "Networking Event",
                    "District Conference",
                    "Regional Conference",
                    "National Convention",
                    "International Convention",
                    "Charter Anniversary",
                    "Awards Ceremony",
                    "Fundraising Event",
                    "Guest Speaker Event",
                    "Other",
                }
            )
                db.EventTypes.Add(new() { TenantId = tenant, Name = name });
        await db.SaveChangesAsync();
    }

    public static async Task Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        foreach (var t in await db.Tenants.Select(x => x.Id).ToListAsync())
            await Tenant(db, t);
    }
}

public class EventWorker(IServiceScopeFactory scopes, ILogger<EventWorker> log) : BackgroundService
{
    private static string InvitationMessage(ClubEvent e, string message, string name)
    {
        var start = TimeZoneInfo.ConvertTime(
            e.StartsAt,
            TimeZoneInfo.FindSystemTimeZoneById(e.TimeZone)
        );
        return e.Title
            + ": "
            + message
                .Replace("{name}", name)
                .Replace("{event}", e.Title)
                .Replace("{venue}", e.VenueType == "Online" ? "Online" : e.Venue)
                .Replace("{start}", start.ToString("yyyy-MM-dd HH:mm") + " " + e.TimeZone);
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var s = scope.ServiceProvider.GetRequiredService<EventService>();
                var n = scope.ServiceProvider.GetRequiredService<NotificationService>();
                await using var tx = await s.Db.Database.BeginTransactionAsync(stop);
                await s.Db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(481104)",
                    stop
                );
                var now = DateTimeOffset.UtcNow;
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    WITH due AS (SELECT "Id" FROM "Events" WHERE "Publication"='Published' AND "Lifecycle"='Scheduled' AND "StartsAt"<={now} AND "EndsAt">{now} ORDER BY "StartsAt" LIMIT 100 FOR UPDATE SKIP LOCKED)
                    UPDATE "Events" e SET "Lifecycle"='In Progress' FROM due WHERE e."Id"=due."Id"
                    """,
                    stop
                );
                await s
                    .Db.EventInvitations.Where(i =>
                        i.Status == "Outbox"
                        && i.MailId != null
                        && s.Db.Mails.Any(m => m.Id == i.MailId && m.SentAt != null)
                    )
                    .ExecuteUpdateAsync(
                        x =>
                            x.SetProperty(i => i.Status, "Delivered")
                                .SetProperty(
                                    i => i.SentAt,
                                    i =>
                                        s.Db.Mails.Where(m => m.Id == i.MailId)
                                            .Select(m => m.SentAt)
                                            .First()
                                ),
                        stop
                    );
                var authority = new Dictionary<(Guid, Guid), bool>();
                var pending = await s
                    .Db.EventInvitations.Where(x => x.Status == "Queued" && x.SendAt <= now)
                    .OrderBy(x => x.PersonId != null)
                    .ThenBy(x => x.SendAt)
                    .Take(100)
                    .ToListAsync();
                foreach (var i in pending)
                {
                    var e = await s.Find(i.EventId);
                    if (e.Lifecycle == "Cancelled" || i.ExpiresAt <= now)
                    {
                        i.Status = "Cancelled";
                        continue;
                    }
                    if (i.Context != "Public guest email confirmation")
                    {
                        var key = (e.Id, i.InviterId);
                        if (!authority.TryGetValue(key, out var allowed))
                        {
                            allowed = await CanInvite(s, e, i.InviterId);
                            authority[key] = allowed;
                        }
                        if (!allowed)
                        {
                            i.Status = "Authority expired";
                            continue;
                        }
                    }
                    var link = "/event-invitation?token=" + s.Unprotect(i.ProtectedToken);
                    var before = s
                        .Db.ChangeTracker.Entries<Mail>()
                        .Select(x => x.Entity.Id)
                        .ToHashSet();
                    if (i.PersonId != null)
                    {
                        var recipient = await s
                            .Db.Persons.Where(x => x.Id == i.PersonId)
                            .Select(x => new { x.UserId, x.DisplayName })
                            .SingleAsync();
                        await n.User(
                            recipient.UserId,
                            "event.invitation",
                            InvitationMessage(e, i.Message, recipient.DisplayName),
                            link,
                            e.TenantId,
                            e.OrgId,
                            e.Id
                        );
                    }
                    else
                        await n.Recipient(
                            i.Email,
                            "event.invitation",
                            InvitationMessage(e, i.Message, i.GuestName),
                            link,
                            e.TenantId,
                            e.OrgId,
                            e.Id
                        );
                    var mail = s
                        .Db.ChangeTracker.Entries<Mail>()
                        .Select(x => x.Entity)
                        .LastOrDefault(x => !before.Contains(x.Id));
                    i.MailId = mail?.Id;
                    i.Status =
                        mail == null ? "No linked account"
                        : mail.Suppressed ? "Suppressed"
                        : "Outbox";
                    i.SentAt = mail?.SentAt;
                }
                var offers = await s
                    .Db.EventRegistrations.Where(x =>
                        x.Status == "Offered" && x.OfferExpiresAt <= now
                    )
                    .Take(100)
                    .ToListAsync();
                foreach (var r in offers)
                {
                    var e = await s.Find(r.EventId);
                    await s.Lock(e.Id);
                    r.Status = "Cancelled";
                    r.Version++;
                    await s.Db.SaveChangesAsync(stop);
                    await s.Promote(e);
                }
                var due = await s
                    .Db.Events.Where(x =>
                        x.Publication == "Published"
                        && x.Lifecycle != "Cancelled"
                        && x.Lifecycle != "Archived"
                        && (
                            (x.ReminderAt <= now && !x.ReminderSent)
                            || (x.EndsAt <= now && !x.CompletionSent)
                        )
                    )
                    .OrderBy(x => x.StartsAt)
                    .Take(10)
                    .ToListAsync();
                foreach (var e in due)
                {
                    await s.Lock(e.Id);
                    var completed = e.EndsAt <= now;
                    if (completed)
                    {
                        e.Lifecycle = "Completed";
                        e.RegistrationEnabled = false;
                        e.CompletionSent = true;
                    }
                    else
                        e.ReminderSent = true;
                    s.QueueNotice(
                        e,
                        completed
                            ? "Event completed. If you attended, please share feedback."
                            : "Event reminder: " + e.StartsAt.ToString("u") + " / " + e.Venue
                    );
                }
                var notices = await s
                    .Db.EventNotices.Where(x => !x.Completed)
                    .OrderBy(x => x.Id)
                    .Take(3)
                    .ToListAsync();
                foreach (var job in notices)
                {
                    var e = await s.Find(job.EventId);
                    if (job.Audience.StartsWith("MemberUpdate|"))
                    {
                        if(e.Lifecycle is "Cancelled" or "Completed" or "Archived"||e.EndsAt<=now){job.Completed=true;continue;}
                        var parts=job.Audience.Split('|');
                        Guid? target=Guid.TryParse(parts.ElementAtOrDefault(1),out var unit)?unit:null;
                        var people=await EventEndpoints.CommunicationPeople(s,e,target,parts.ElementAtOrDefault(2)??"All",true);
                        if(job.Cursor!=null)people=people.Where(p=>p.CompareTo(job.Cursor.Value)>0);
                        var recipients=await people.OrderBy(p=>p).Take(100).ToListAsync();
                        foreach(var person in recipients)await s.RsvpNotify(person,e,job.Message);
                        job.Cursor=recipients.Count>0?recipients[^1]:job.Cursor;
                        job.Completed=recipients.Count<100;
                    }
                    else if (job.Audience == "Registrations")
                    {
                        var q = s.Db.EventRegistrations.Where(r => r.EventId == e.Id);
                        if (
                            job.Message.StartsWith("Event reminder")
                            || job.Message.StartsWith("Event completed")
                        )
                            q = q.Where(r => r.Status == "Confirmed");
                        if (job.Cursor != null)
                            q = q.Where(r => r.Id.CompareTo(job.Cursor.Value) > 0);
                        var page = await q.OrderBy(r => r.Id).Take(100).ToListAsync();
                        foreach (var r in page)
                            await s.Notify(e, r, job.Message);
                        job.Cursor = page.LastOrDefault()?.Id ?? job.Cursor;
                        job.Completed = page.Count < 100;
                    }
                    else
                    {
                        var q = s.Db.EventInvitations.Where(i =>
                            i.EventId == e.Id
                            && !s.Db.EventRegistrations.Any(r =>
                                r.EventId == e.Id
                                && (
                                    r.InvitationId == i.Id
                                    || (i.PersonId != null && r.PersonId == i.PersonId)
                                )
                            )
                        );
                        if (job.Cursor != null)
                            q = q.Where(i => i.Id.CompareTo(job.Cursor.Value) > 0);
                        var page = await q.OrderBy(i => i.Id).Take(100).ToListAsync();
                        foreach (var i in page)
                        {
                            if (i.PersonId != null)
                                await s.RsvpNotify(i.PersonId.Value, e, job.Message);
                            else if (i.Email != "")
                                await n.Recipient(
                                    i.Email,
                                    "event.update",
                                    e.Title + ": " + job.Message,
                                    "/event-invitation?token=" + s.Unprotect(i.ProtectedToken),
                                    e.TenantId,
                                    e.OrgId,
                                    e.Id
                                );
                        }
                        job.Cursor = page.LastOrDefault()?.Id ?? job.Cursor;
                        job.Completed = page.Count < 100;
                    }
                }

                await s.Db.SaveChangesAsync(stop);
                await tx.CommitAsync(stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Event background processing failed; transaction rolled back.");
            }
            try
            {
                await Task.Delay(2000, stop);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task<bool> CanInvite(EventService s, ClubEvent e, Guid user)
    {
        if (!await s.Db.Users.AnyAsync(u => u.Id == user && u.Verified))
            return false;
        var candidates = await s
            .Db.Assignments.Where(a =>
                a.UserId == user
                && a.TenantId == e.TenantId
                && s.Db.RolePermissions.Any(g =>
                    g.RoleId == a.RoleId && g.PermissionId == "events.manage"
                )
            )
            .ToListAsync();
        foreach (var assignment in candidates)
            if (
                await s.A.Effective(assignment)
                && await s.A.Tree(e.TenantId, assignment.OrgId).AnyAsync(o => o.Id == e.OrgId)
            )
                return true;
        var instant = DateTimeOffset.UtcNow;
        return await s.Db.EventStaff.AnyAsync(a =>
            a.EventId == e.Id
            && a.Active
            && a.Capability == "Manage"
            && a.StartsAt <= instant
            && a.EndsAt > instant
            && s.Db.Persons.Any(p => p.Id == a.PersonId && p.UserId == user)
        );
    }
}
