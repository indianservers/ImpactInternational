using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static partial class EventEndpoints
{
    private static void MapInvitations(RouteGroupBuilder api)
    {
        api.MapPost(
            "/events/{id:guid}/invitations",
            async (Guid id, InvitationBatchInput r, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                Check(
                    e.Publication == "Published"
                        && e.Approval == "Approved"
                        && e.Lifecycle is not "Cancelled" and not "Completed" and not "Archived"
                        && e.EndsAt > DateTimeOffset.UtcNow,
                    "Publish an active approved event before inviting.",
                    409
                );
                Check(
                    r.Message.Length <= 2000 && (r.Guests?.Length ?? 0) <= 1000,
                    "Invitation batch is too large."
                );
                var user = await s.A.User();
                var send = (r.SendAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
                Check(send < e.EndsAt, "Send before event ends.");
                var people = await s.Audience(e);
                var audienceRules = await s
                    .Db.EventAudiences.Where(rule => rule.EventId == id)
                    .Select(rule => rule.Id)
                    .ToListAsync();
                var audienceContext =
                    audienceRules.Count == 0
                        ? "Active membership in owner subtree " + e.OrgId
                        : "Eligible union of frozen audience rules: "
                            + string.Join(",", audienceRules);

                var added = 0;
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var acquired = await s
                    .Db.Database.SqlQuery<bool>(
                        $"SELECT pg_try_advisory_xact_lock(hashtextextended({"events.invites:" + id}, 0)) AS \"Value\""
                    )
                    .SingleAsync();
                Check(acquired, "An invitation batch is being prepared. Retry shortly.", 409);
                // Bounded keyset pages; no all-member load and no synchronous email delivery.
                Guid? cursor = null;
                while (true)
                {
                    var q = people.Where(person =>
                        !s.Db.EventInvitations.Any(i => i.EventId == id && i.PersonId == person)
                    );
                    if (cursor != null)
                        q = q.Where(person => person.CompareTo(cursor.Value) > 0);
                    var page = await q.OrderBy(person => person).Take(500).ToListAsync();
                    if (page.Count == 0)
                        break;
                    var invitations = new List<EventInvitation>();
                    foreach (var p in page)
                    {
                        var token = Token();
                        invitations.Add(
                            new()
                            {
                                TenantId = e.TenantId,
                                EventId = id,
                                PersonId = p,
                                RecipientKey = "person:" + p,
                                InviterId = user.Id,
                                TokenHash = Hash(token),
                                ProtectedToken = s.Protect(token),
                                Context = audienceContext,
                                Message = r.Message,
                                SendAt = send,
                                ExpiresAt = e.EndsAt.AddDays(30),
                            }
                        );
                    }
                    added += await s.InsertInvitations(invitations);
                    cursor = page[^1];
                }
                foreach (var guest in r.Guests ?? [])
                {
                    Check(
                        e.AllowGuests
                            && guest.Consent
                            && guest.Name.Length is > 1 and <= 120
                            && MailAddress.TryCreate(guest.Email, out _),
                        "Guest must consent and have a valid email/name."
                    );
                    var email = guest.Email.Trim().ToLowerInvariant();
                    var identity = await s.RecipientIdentity(email);
                    var key = identity.Key;
                    if (
                        await s.Db.EventInvitations.AnyAsync(x =>
                            x.EventId == id && x.RecipientKey == key
                        )
                    )
                        continue;
                    var token = Token();
                    added += await s.InsertInvitations([
                        new()
                        {
                            TenantId = e.TenantId,
                            EventId = id,
                            GuestName = guest.Name,
                            PersonId = identity.PersonId,
                            Email = email,
                            RecipientKey = key,
                            InviterId = user.Id,
                            TokenHash = Hash(token),
                            ProtectedToken = s.Protect(token),
                            Context = "Explicit consenting guest invitation",
                            Message = r.Message,
                            SendAt = send,
                            ExpiresAt = e.EndsAt.AddDays(30),
                        },
                    ]);
                }
                await s.Audit(e, "invitations.queued", user.Id, added + " deduplicated recipients");
                await s.Db.SaveChangesAsync();
                await s.Lock(id);
                await s.Db.Entry(e).ReloadAsync();
                Check(
                    e.Publication == "Published"
                        && e.Approval == "Approved"
                        && e.Lifecycle is not "Cancelled" and not "Completed" and not "Archived"
                        && e.EndsAt > DateTimeOffset.UtcNow,
                    "Event is no longer accepting invitations.",
                    409
                );
                await s.Manage(e);
                await tx.CommitAsync();
                return Results.Ok(new { queued = added });
            }
        );
        api.MapGet(
            "/events/{id:guid}/invitations",
            async (Guid id, EventService s, Guid? after = null) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                var q = s.Db.EventInvitations.Where(x => x.EventId == id);
                if (after != null)
                    q = q.Where(x => x.Id.CompareTo(after.Value) > 0);
                var rows = await q.OrderBy(x => x.Id)
                    .Take(51)
                    .Select(x => new
                    {
                        x.Id,
                        x.PersonId,
                        name = x.PersonId == null
                            ? x.GuestName
                            : s
                                .Db.Persons.Where(p => p.Id == x.PersonId)
                                .Select(p => p.DisplayName)
                                .First(),
                        x.Context,
                        x.Status,
                        x.SendAt,
                        x.SentAt,
                        x.Rsvp,
                        x.RespondedAt,
                        delivery = x.MailId == null
                            ? "Scheduled"
                            : s
                                .Db.Mails.Where(m => m.Id == x.MailId)
                                .Select(m =>
                                    m.Suppressed ? "Suppressed"
                                    : m.SentAt != null ? "Delivered"
                                    : m.Attempts > 0 ? "Retrying"
                                    : "Queued"
                                )
                                .First(),
                    })
                    .ToListAsync();
                var more = rows.Count > 50;
                if (more)
                    rows.RemoveAt(50);
                return new
                {
                    items = rows,
                    next = more ? rows.Last().Id : (Guid?)null,
                    total = await s.Db.EventInvitations.CountAsync(x => x.EventId == id),
                };
            }
        );
        api.MapGet(
            "/my/invitations",
            async (EventService s, Guid? after = null) =>
            {
                var person = await s.Me();
                var q = s.Db.EventInvitations.Where(x => x.PersonId == person && person != null);
                if (after != null)
                    q = q.Where(x => x.Id.CompareTo(after.Value) > 0);
                return await q.OrderBy(x => x.Id)
                    .Take(50)
                    .Select(x => new
                    {
                        x.Id,
                        x.EventId,
                        x.Rsvp,
                        x.ExpiresAt,
                        x.SentAt,
                        x.Status,
                        x.RespondedAt,
                        startsAt = s.Db.Events.Where(e => e.Id == x.EventId).Select(e => e.StartsAt).First(),
                        venue = s.Db.Events.Where(e => e.Id == x.EventId).Select(e => e.Venue).First(),
                        organization = s.Db.Events.Where(e => e.Id == x.EventId).Join(s.Db.Orgs, e => e.OrgId, o => o.Id, (e,o) => o.Name).First(),
                        title = s
                            .Db.Events.Where(e => e.Id == x.EventId)
                            .Select(e => e.Title)
                            .First(),
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
                "/event-invitations/redeem",
                async (EventToken r, EventService s) =>
                {
                    var i = await s.Invitation(r.Token);
                    var e = await s.Find(i.EventId);
                    Check(
                        e.Publication == "Published" && e.Lifecycle != "Cancelled",
                        "Event is unavailable.",
                        410
                    );
                    return new
                    {
                        invitation = new
                        {
                            i.Id,
                            i.EventId,
                            i.GuestName,
                            i.Rsvp,
                            i.ExpiresAt,
                        },
                        eventInfo = e,
                        registration = await s.Db.EventRegistrations.SingleOrDefaultAsync(x =>
                            x.EventId == e.Id && x.InvitationId == i.Id
                        ),
                        sessions = await s
                            .Db.EventSessions.Where(x => x.EventId == e.Id && x.Status == "Active")
                            .OrderBy(x => x.StartsAt)
                            .ToListAsync(),
                        feedbackForm = await s.Db.EventFeedbackForms.SingleOrDefaultAsync(x =>
                            x.EventId == e.Id
                        ),
                    };
                }
            )
            .RequireRateLimiting("cards");
        api.MapPost(
            "/events/{id:guid}/rsvps",
            async (Guid id, EventRsvpInput r, EventService s) =>
            {
                var e = await s.Find(id);
                Check(
                    e.Publication == "Published"
                        && e.Lifecycle is not "Cancelled" and not "Completed" and not "Archived"
                        && e.EndsAt > DateTimeOffset.UtcNow
                        && DateTimeOffset.UtcNow <= e.RsvpDeadline,
                    "RSVP deadline passed or event unavailable.",
                    409
                );
                Check(
                    new[] { "Going", "Not Going", "Maybe" }.Contains(r.Response),
                    "Invalid RSVP."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                EventInvitation i;
                if (r.Token != null)
                {
                    i = await s.Invitation(r.Token);
                    Check(i.EventId == id, "Wrong event.", 403);
                }
                else
                {
                    var person = await s.Me();
                    Check(
                        person != null && await s.Eligible(e, person.Value),
                        "Not eligible.",
                        403
                    );
                    i =
                        await s.Db.EventInvitations.SingleOrDefaultAsync(x =>
                            x.EventId == id && x.PersonId == person
                        )
                        ?? new()
                        {
                            TenantId = e.TenantId,
                            EventId = id,
                            PersonId = person,
                            RecipientKey = "person:" + person,
                            InviterId = (await s.A.User()).Id,
                            Context = "Eligible self-service RSVP",
                            TokenHash = Hash(Token()),
                            ExpiresAt = e.EndsAt.AddDays(30),
                            Status = "Self service",
                        };
                    if (s.Db.Entry(i).State == EntityState.Detached)
                        s.Db.EventInvitations.Add(i);
                }
                i.Rsvp = r.Response;
                i.RespondedAt = DateTimeOffset.UtcNow;
                s.Db.EventRsvpHistory.Add(
                    new()
                    {
                        TenantId = e.TenantId,
                        InvitationId = i.Id,
                        Response = r.Response,
                    }
                );
                if (i.PersonId != null)
                    await s.RsvpNotify(i.PersonId.Value, e, "RSVP recorded: " + r.Response);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return new
                {
                    i.Id,
                    i.Rsvp,
                    i.RespondedAt,
                    seatReserved = false,
                };
            }
        );
        api.MapGet(
            "/events/{id:guid}/rsvps",
            async (Guid id, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                return await s
                    .Db.EventInvitations.Where(x => x.EventId == id)
                    .GroupBy(x => x.Rsvp)
                    .Select(g => new { response = g.Key, count = g.Count() })
                    .ToListAsync();
            }
        );
        api.MapPost(
                "/events/{id:guid}/guest-link",
                async (Guid id, EventGuestInput r, EventService s, NotificationService n) =>
                {
                    var e = await s.Find(id);
                    Check(
                        e.Visibility == "Public"
                            && e.AllowGuests
                            && EventService.Window(e) == "Open",
                        "Public guest registration is unavailable.",
                        403
                    );
                    Check(
                        r.Consent
                            && r.Name.Length is > 1 and <= 120
                            && MailAddress.TryCreate(r.Email, out _),
                        "A consenting guest name and email are required."
                    );
                    var email = r.Email.Trim().ToLowerInvariant();
                    var identity = await s.RecipientIdentity(email);
                    var key = identity.Key;
                    await using var tx = await s.Db.Database.BeginTransactionAsync();
                    await s.Lock(id);
                    if (
                        !await s.Db.EventInvitations.AnyAsync(x =>
                            x.EventId == id && x.RecipientKey == key
                        )
                    )
                    {
                        var token = Token();
                        s.Db.EventInvitations.Add(
                            new()
                            {
                                TenantId = e.TenantId,
                                EventId = id,
                                RecipientKey = key,
                                GuestName = r.Name,
                                PersonId = identity.PersonId,
                                Email = email,
                                InviterId = e.CreatorId,
                                TokenHash = Hash(token),
                                ProtectedToken = s.Protect(token),
                                Context = "Public guest email confirmation",
                                Message =
                                    "Confirm your event registration using this private link.",
                                ExpiresAt = e.EndsAt.AddDays(30),
                            }
                        );
                        await s.Db.SaveChangesAsync();
                    }
                    await tx.CommitAsync();
                    return Results.Ok(
                        new
                        {
                            detail = "If eligible, a private confirmation link will be delivered to that email.",
                        }
                    );
                }
            )
            .RequireRateLimiting("auth");
    }
}
