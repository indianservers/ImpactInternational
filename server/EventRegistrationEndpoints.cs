using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static partial class EventEndpoints
{
    private static void MapRegistration(RouteGroupBuilder api)
    {
        api.MapPost(
                "/events/{id:guid}/registrations",
                async (Guid id, EventRegistrationInput input, EventService s) =>
                {
                    await using var tx = await s.Db.Database.BeginTransactionAsync();
                    await s.Lock(id);
                    var e = await s.Find(id);
                    var r = await s.Register(e, input);
                    await BookSessions(e, r, input.SessionIds ?? [], s);
                    await s.Db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return r;
                }
            )
            .RequireRateLimiting("events");
        api.MapGet(
            "/events/{id:guid}/registrations",
            async (
                Guid id,
                EventService s,
                string search = "",
                string status = "",
                Guid? after = null
            ) =>
            {
                var e = await s.Find(id);
                try
                {
                    await s.Manage(e, "Registration");
                }
                catch (ApiError)
                {
                    await s.Manage(e, "Checkin");
                }
                var q = s.Db.EventRegistrations.Where(x => x.EventId == id);
                if (search != "")
                    q = q.Where(x => EF.Functions.ILike(x.Name, "%" + search + "%"));
                if (status != "")
                    q = q.Where(x => x.Status == status);
                if (after != null)
                    q = q.Where(x => x.Id.CompareTo(after.Value) > 0);
                return new
                {
                    items = await q.OrderBy(x => x.Id).Take(50).ToListAsync(),
                    total = await s.Db.EventRegistrations.CountAsync(x => x.EventId == id),
                };
            }
        );
        api.MapGet(
            "/my/registrations",
            async (EventService s, Guid? after = null, bool summary = false, string status = "", DateTimeOffset? from = null, DateTimeOffset? to = null) =>
            {
                var p = await s.Me();
                var q = s.Db.EventRegistrations.Where(x => x.PersonId == p && p != null);
                if (summary) {
                    var begin = (from ?? DateTimeOffset.UtcNow.AddMonths(-1)).ToUniversalTime();
                    var end = (to ?? begin.AddMonths(3)).ToUniversalTime();
                    Check(begin < end && end <= begin.AddYears(1), "Invalid personal schedule range.");
                    var confirmed = q.Where(r => r.Status == "Confirmed");
                    var next = await confirmed.Join(s.Db.Events.Where(e => e.Lifecycle != "Cancelled" && e.Lifecycle != "Archived"), r => r.EventId, e => e.Id, (r,e) => new { r.Id, r.EventId, r.Status, e.Title, e.StartsAt, e.EndsAt, e.Venue }).Where(e => e.StartsAt > DateTimeOffset.UtcNow).OrderBy(e => e.StartsAt).FirstOrDefaultAsync();
                    return Results.Ok(new {
                        scheduleRegistrations = await confirmed.Join(s.Db.Events.Where(e => e.Lifecycle != "Cancelled" && e.Lifecycle != "Archived"), r => r.EventId, e => e.Id, (r,e) => new { r.Id, eventId = e.Id, e.Title, e.StartsAt, e.EndsAt, e.Venue, kind = "Registration" }).Where(e => e.StartsAt < end && e.EndsAt > begin).OrderBy(e => e.StartsAt).Take(500).ToListAsync(),
                        scheduleSessions = await s.Db.EventSessionBookings.Where(b => b.Active && confirmed.Any(r => r.Id == b.RegistrationId)).Join(s.Db.EventSessions.Where(e => e.Status == "Active"), b => b.SessionId, e => e.Id, (b,e) => new { e.Id, e.EventId, e.Title, e.StartsAt, e.EndsAt, venue = e.Room, kind = "Session" }).Where(e => e.StartsAt < end && e.EndsAt > begin).OrderBy(e => e.StartsAt).Take(500).ToListAsync(),
                        scheduleMeetings = await s.Db.MeetingAttendances.Where(a => a.PersonId == p && p != null && a.Rsvp != "Declined").Join(s.Db.ClubMeetings.Where(m => m.Status == "Scheduled" || m.Status == "In Progress" || m.Status == "Completed"), a => a.MeetingId, m => m.Id, (a,m) => new { m.Id, m.EventId, m.Title, m.StartsAt, m.EndsAt, venue = m.Location, kind = "Meeting" }).Where(e => e.StartsAt < end && e.EndsAt > begin).OrderBy(e => e.StartsAt).Take(500).ToListAsync(),
                        scheduleInvitations = await s.Db.EventInvitations.Where(i => i.PersonId == p && p != null && i.Rsvp == "Going").Join(s.Db.Events.Where(e => e.Publication == "Published" && e.Lifecycle != "Cancelled" && e.Lifecycle != "Archived"), i => i.EventId, e => e.Id, (i,e) => new { i.Id, eventId = e.Id, e.Title, e.StartsAt, e.EndsAt, e.Venue, kind = "Invitation" }).Where(e => e.StartsAt < end && e.EndsAt > begin).OrderBy(e => e.StartsAt).Take(500).ToListAsync(),
                        scheduleDuties = await s.Db.EventStaff.Where(t => t.PersonId == p && p != null && t.Active && t.StartsAt < end && t.EndsAt > begin).Select(t => new { t.Id, t.EventId, title = t.Role, t.StartsAt, t.EndsAt, venue = t.Responsibilities, kind = "Duty" }).OrderBy(e => e.StartsAt).Take(500).ToListAsync(),
                        volunteerHours = await s.Db.VolunteerHours.Where(h => h.PersonId == p && p != null && h.Status == "Verified").SumAsync(h => (decimal?)h.Hours) ?? 0,
                        categoryPreferences = await confirmed.Where(r => s.Db.EventCheckins.Any(c => c.RegistrationId == r.Id && c.SessionId == null && !c.Voided)).Join(s.Db.Events, r => r.EventId, e => e.Id, (r,e) => new { e.TypeId }).GroupBy(e => e.TypeId).Select(g => new { name = s.Db.EventTypes.Where(t => t.Id == g.Key).Select(t => t.Name).First(), count = g.Count() }).ToListAsync(),
                        participationTrend = await confirmed.Join(s.Db.Events, r => r.EventId, e => e.Id, (r,e) => new { r.Id, e.StartsAt.Year, e.StartsAt.Month }).GroupBy(e => new { e.Year, e.Month }).Select(g => new { g.Key.Year, g.Key.Month, registered = g.Count(), attended = g.Count(r => s.Db.EventCheckins.Any(c => c.RegistrationId == r.Id && c.SessionId == null && !c.Voided)) }).OrderBy(e => e.Year).ThenBy(e => e.Month).ToListAsync(),
                        upcoming = await confirmed.CountAsync(r => s.Db.Events.Any(e => e.Id == r.EventId && e.StartsAt > DateTimeOffset.UtcNow && e.Lifecycle != "Cancelled" && e.Lifecycle != "Archived")),
                        confirmed = await confirmed.CountAsync(),
                        waitlisted = await q.CountAsync(r => r.Status == "Waitlisted"),
                        passes = await q.CountAsync(r => r.Status == "Confirmed" && s.Db.EventTickets.Any(t => t.RegistrationId == r.Id && !t.Revoked)),
                        attended = await q.CountAsync(r => s.Db.EventCheckins.Any(c => c.RegistrationId == r.Id && c.SessionId == null && !c.Voided)),
                        next,
                        pendingInvitations = await s.Db.EventInvitations.CountAsync(i => i.PersonId == p && p != null && i.Rsvp == "Awaiting Response" && i.ExpiresAt > DateTimeOffset.UtcNow),
                        dutiesCompleted = await s.Db.EventStaff.CountAsync(t => t.PersonId == p && p != null && t.Active && t.TaskStatus == "Completed"),
                        duties = await s.Db.EventStaff.CountAsync(t => t.PersonId == p && p != null && t.Active && t.EndsAt > DateTimeOffset.UtcNow)
                    });
                }
                if (status == "Upcoming") q = q.Where(r => r.Status == "Confirmed" && s.Db.Events.Any(e => e.Id == r.EventId && e.EndsAt > DateTimeOffset.UtcNow && e.Lifecycle != "Cancelled" && e.Lifecycle != "Archived"));
                else if (status == "Completed") q = q.Where(r => r.Status == "Confirmed" && s.Db.Events.Any(e => e.Id == r.EventId && e.EndsAt <= DateTimeOffset.UtcNow && e.Lifecycle != "Cancelled"));
                else if (status == "Cancelled") q = q.Where(r => r.Status == "Cancelled" || s.Db.Events.Any(e => e.Id == r.EventId && e.Lifecycle == "Cancelled"));
                else if (status != "" && status != "All") q = q.Where(r => r.Status == status);
                if (after != null)
                    q = q.Where(x => x.Id.CompareTo(after.Value) > 0);
                return Results.Ok(await q.OrderBy(x => x.Id)
                    .Take(50)
                    .Select(x => new
                    {
                        x.Id,
                        x.EventId,
                        x.Status,
                        x.Category,
                        eventLifecycle = s.Db.Events.Where(e => e.Id == x.EventId).Select(e => e.Lifecycle).First(),
                        x.OfferExpiresAt,
                        x.Companions,
                        attended = s.Db.EventCheckins.Any(c => c.RegistrationId == x.Id && c.SessionId == null && !c.Voided),
                        passIssued = s.Db.EventTickets.Any(t => t.RegistrationId == x.Id && !t.Revoked),
                        venue = s.Db.Events.Where(e => e.Id == x.EventId).Select(e => e.Venue).First(),
                        endsAt = s.Db.Events.Where(e => e.Id == x.EventId).Select(e => e.EndsAt).First(),
                        registrationEndsAt = s.Db.Events.Where(e => e.Id == x.EventId).Select(e => e.RegistrationEndsAt).First(),
                        title = s
                            .Db.Events.Where(e => e.Id == x.EventId)
                            .Select(e => e.Title)
                            .First(),
                        startsAt = s
                            .Db.Events.Where(e => e.Id == x.EventId)
                            .Select(e => e.StartsAt)
                            .First(),
                    })
                    .ToListAsync());
            }
        );
        api.MapPost(
            "/events/{id:guid}/registrations/{registrationId:guid}/cancel",
            async (Guid id, Guid registrationId, EventToken? input, EventService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var e = await s.Find(id);
                EventRegistration r;
                try
                {
                    r = await s.Own(e, registrationId, input?.Token);
                }
                catch (ApiError)
                {
                    await s.Manage(e, "Registration");
                    r =
                        await s.Db.EventRegistrations.SingleOrDefaultAsync(x =>
                            x.Id == registrationId && x.EventId == id
                        ) ?? throw new ApiError(404, "Registration not found.");
                }
                Check(
                    DateTimeOffset.UtcNow <= e.RegistrationEndsAt && r.Status != "Cancelled",
                    "Cancellation deadline passed or already cancelled.",
                    409
                );
                Check(
                    !await s.Db.EventCheckins.AnyAsync(x =>
                        x.RegistrationId == registrationId && !x.Voided
                    ),
                    "An attended registration cannot be cancelled.",
                    409
                );
                r.Status = "Cancelled";
                r.Version++;
                await s
                    .Db.GovernanceRequests.Where(x =>
                        x.Id == r.ApprovalRequestId
                        && new[]
                        {
                            "Draft",
                            "Submitted",
                            "Under Review",
                            "Changes Requested",
                        }.Contains(x.Status)
                    )
                    .ExecuteUpdateAsync(x =>
                        x.SetProperty(g => g.Status, "Cancelled")
                            .SetProperty(g => g.Version, g => g.Version + 1)
                    );

                await s
                    .Db.EventTickets.Where(x => x.RegistrationId == r.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(t => t.Revoked, true));
                await s
                    .Db.EventSessionBookings.Where(x => x.RegistrationId == r.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(t => t.Active, false));
                await s.Db.SaveChangesAsync();
                await s.Promote(e);
                await s.Notify(e, r, "Registration cancelled.");
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapPost(
            "/events/{id:guid}/registrations/{registrationId:guid}/accept-offer",
            async (Guid id, Guid registrationId, EventToken? input, EventService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var e = await s.Find(id);
                var r = await s.Own(e, registrationId, input?.Token);
                Check(
                    r.Status == "Offered"
                        && r.OfferExpiresAt > DateTimeOffset.UtcNow
                        && e.Lifecycle != "Cancelled",
                    "Seat offer expired.",
                    409
                );
                r.Status = "Confirmed";
                r.OfferExpiresAt = null;
                r.Version++;
                await s.Issue(e, r);
                await s.Notify(e, r, "Seat offer accepted. Your pass is ready.");
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapPost(
            "/events/{id:guid}/registrations/{registrationId:guid}/sessions",
            async (Guid id, Guid registrationId, SessionBookingInput input, EventService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var e = await s.Find(id);
                var r = await s.Own(e, registrationId, input.Token);
                Check(
                    EventService.Window(e) == "Open" && r.Status == "Confirmed",
                    "Session selection unavailable.",
                    409
                );
                await s
                    .Db.EventSessionBookings.Where(x => x.RegistrationId == r.Id)
                    .ExecuteDeleteAsync();
                await BookSessions(e, r, input.SessionIds, s);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { saved = true });
            }
        );
        api.MapPost(
                "/events/{id:guid}/tickets/{registrationId:guid}",
                async (Guid id, Guid registrationId, EventToken? input, EventService s) =>
                {
                    await using var tx = await s.Db.Database.BeginTransactionAsync();
                    await s.Lock(id);
                    var e = await s.Find(id);
                    var r = await s.Own(e, registrationId, input?.Token);
                    Check(
                        r.Status == "Confirmed"
                            && e.Lifecycle != "Cancelled"
                            && e.EndsAt > DateTimeOffset.UtcNow,
                        "Pass unavailable.",
                        409
                    );
                    var t = await s.Issue(e, r);
                    await s.Db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return new
                    {
                        t.Id,
                        token = s.Unprotect(t.ProtectedToken),
                        eventId = id,
                        title = e.Title,
                        e.StartsAt,
                        e.EndsAt,
                        e.TimeZone,
                        e.Venue,
                        participant = r.Name,
                        reference = r.Id,
                        category = r.Category,
                        reservedSeats = 1 + r.Companions,
                        sessions = await s
                            .Db.EventSessionBookings.Where(x =>
                                x.RegistrationId == r.Id && x.Active
                            )
                            .Join(
                                s.Db.EventSessions,
                                b => b.SessionId,
                                v => v.Id,
                                (b, v) =>
                                    new
                                    {
                                        v.Id,
                                        v.Title,
                                        v.StartsAt,
                                        v.Room,
                                    }
                            )
                            .ToListAsync(),
                    };
                }
            )
            .RequireRateLimiting("events");
        api.MapPost(
            "/events/{id:guid}/tickets/{registrationId:guid}/reissue",
            async (Guid id, Guid registrationId, EventToken? input, EventService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var e = await s.Find(id);
                EventRegistration r;
                try
                {
                    r = await s.Own(e, registrationId, input?.Token);
                }
                catch (ApiError)
                {
                    await s.Manage(e, "Registration");
                    r =
                        await s.Db.EventRegistrations.SingleOrDefaultAsync(x =>
                            x.Id == registrationId && x.EventId == id
                        ) ?? throw new ApiError(404, "Registration not found.");
                }
                Check(
                    r.Status == "Confirmed" && e.Lifecycle != "Cancelled",
                    "Pass unavailable.",
                    409
                );
                await s
                    .Db.EventTickets.Where(x => x.RegistrationId == r.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(t => t.Revoked, true));
                var ticket = await s.Issue(e, r);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { ticket.Id });
            }
        );
        api.MapPost(
            "/events/{id:guid}/tickets/{registrationId:guid}/revoke",
            async (Guid id, Guid registrationId, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e, "Registration");
                Check(
                    await s.Db.EventRegistrations.AnyAsync(x =>
                        x.Id == registrationId && x.EventId == id
                    ),
                    "Registration not found.",
                    404
                );
                await s
                    .Db.EventTickets.Where(x =>
                        x.EventId == id && x.RegistrationId == registrationId
                    )
                    .ExecuteUpdateAsync(x => x.SetProperty(t => t.Revoked, true));
                await s.Audit(
                    e,
                    "ticket.revoked",
                    (await s.A.User()).Id,
                    registrationId.ToString()
                );
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
                "/events/{id:guid}/checkins/verify",
                async (Guid id, EventCheckinInput input, EventService s) =>
                {
                    var e = await s.Find(id);
                    await s.Manage(e, "Checkin");
                    var result = await TicketState(e, input, s);
                    return Results.Ok(result);
                }
            )
            .RequireRateLimiting("events");
        api.MapPost(
                "/events/{id:guid}/checkins",
                async (Guid id, EventCheckinInput input, EventService s) =>
                {
                    var e = await s.Find(id);
                    await s.Manage(e, "Checkin");
                    Check(
                        input.Device.Length <= 120 && input.EntryPoint.Length <= 120,
                        "Invalid operator context."
                    );
                    await using var tx = await s.Db.Database.BeginTransactionAsync();
                    await s.Lock(id);
                    var result = await TicketState(e, input, s);
                    Check(result.State == "Valid", result.State, 409);
                    var uid = (await s.A.User()).Id;
                    var prior = await s.Db.EventCheckins.SingleOrDefaultAsync(x =>
                        x.RegistrationId == result.RegistrationId && x.SessionId == input.SessionId
                    );
                    if (prior == null)
                        s.Db.EventCheckins.Add(
                            new()
                            {
                                TenantId = e.TenantId,
                                EventId = id,
                                RegistrationId = result.RegistrationId!.Value,
                                SessionId = input.SessionId,
                                OperatorId = uid,
                                Device = input.Device,
                                EntryPoint = input.EntryPoint,
                            }
                        );
                    else
                    {
                        prior.Voided = false;
                        prior.At = DateTimeOffset.UtcNow;
                        prior.OperatorId = uid;
                        prior.CorrectionReason = "Authorized re-entry after void";
                    }
                    await s.Audit(e, "checkin", uid, result.RegistrationId.ToString()!);
                    await s.Db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return Results.Ok(
                        new
                        {
                            state = "Checked In",
                            result.Name,
                            result.RegistrationId,
                        }
                    );
                }
            )
            .RequireRateLimiting("events");
        api.MapGet(
            "/events/{id:guid}/checkins",
            async (Guid id, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e, "Checkin");
                return await s
                    .Db.EventCheckins.Where(x => x.EventId == id)
                    .OrderByDescending(x => x.At)
                    .Take(100)
                    .Select(x => new
                    {
                        x.Id,
                        x.RegistrationId,
                        x.SessionId,
                        x.At,
                        x.Voided,
                        x.EntryPoint,
                        x.Device,
                        name = s
                            .Db.EventRegistrations.Where(r => r.Id == x.RegistrationId)
                            .Select(r => r.Name)
                            .First(),
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/events/{id:guid}/checkins/{checkinId:guid}/correct",
            async (Guid id, Guid checkinId, ReasonInput input, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                Check(
                    input.Reason.Trim().Length >= 5 && input.Reason.Length <= 1000,
                    "Explain the attendance correction."
                );
                var c =
                    await s.Db.EventCheckins.SingleOrDefaultAsync(x =>
                        x.Id == checkinId && x.EventId == id
                    ) ?? throw new ApiError(404, "Check-in not found.");
                Check(!c.Voided, "Already voided.", 409);
                c.Voided = true;
                c.CorrectionReason = input.Reason;
                await s.Audit(
                    e,
                    "attendance.corrected",
                    (await s.A.User()).Id,
                    checkinId + ": " + input.Reason
                );
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
    }

    private static async Task BookSessions(
        ClubEvent e,
        EventRegistration r,
        Guid[] ids,
        EventService s
    )
    {
        Check(
            ids.Length <= 30 && ids.Distinct().Count() == ids.Length,
            "Invalid session selection."
        );
        var selected = await s
            .Db.EventSessions.Where(x =>
                x.EventId == e.Id && ids.Contains(x.Id) && x.Status == "Active"
            )
            .ToListAsync();
        Check(selected.Count == ids.Length, "Session not available.");
        foreach (var v in selected)
        {
            Check(!v.MembersOnly || r.MembershipId != null, "Session is for members only.", 403);
            Check(
                !selected.Any(x => x.Id != v.Id && x.StartsAt < v.EndsAt && x.EndsAt > v.StartsAt),
                "Selected sessions overlap.",
                409
            );
            Check(r.Status == "Confirmed", "Select sessions after registration is confirmed.", 409);
            var count = await s.Db.EventSessionBookings.CountAsync(x =>
                x.SessionId == v.Id && x.Active
            );
            Check(count < v.Capacity, "Session is at capacity.", 409);
            s.Db.EventSessionBookings.Add(
                new()
                {
                    TenantId = e.TenantId,
                    EventId = e.Id,
                    SessionId = v.Id,
                    RegistrationId = r.Id,
                }
            );
        }
    }

    private static async Task<TicketResult> TicketState(
        ClubEvent e,
        EventCheckinInput input,
        EventService s
    )
    {
        if (
            e.Publication != "Published"
            || e.Lifecycle is "Cancelled" or "Completed" or "Archived"
            || DateTimeOffset.UtcNow < e.StartsAt.AddHours(-12)
            || DateTimeOffset.UtcNow > e.EndsAt
        )
            return new("Event check-in is closed");
        Guid? id = input.RegistrationId;
        if (input.Token != null)
        {
            var hash = Hash(input.Token);
            var t = await s.Db.EventTickets.SingleOrDefaultAsync(x => x.TokenHash == hash);
            if (t == null)
                return new("Invalid Ticket");
            if (t.EventId != e.Id)
                return new("Wrong Event");
            if (t.Revoked)
                return new("Revoked Ticket");
            id = t.RegistrationId;
        }
        else
            Check(id != null, "Provide a scanned ticket or authorized registration reference.");
        var r = await s.Db.EventRegistrations.SingleOrDefaultAsync(x =>
            x.Id == id && x.EventId == e.Id
        );
        if (r == null)
            return new("Invalid Ticket");
        if (r.Status == "Cancelled")
            return new("Registration Cancelled");
        if (r.Status != "Confirmed")
            return new("Registration Pending");
        if (
            await s.Db.EventCheckins.AnyAsync(x =>
                x.RegistrationId == r.Id && x.SessionId == input.SessionId && !x.Voided
            )
        )
            return new("Already Checked In", r.Id, r.Name, 1 + r.Companions);
        if (input.SessionId != null)
        {
            var v = await s.Db.EventSessions.SingleOrDefaultAsync(x =>
                x.Id == input.SessionId && x.EventId == e.Id && x.Status == "Active"
            );
            if (v == null)
                return new("Wrong Session");
            if (
                DateTimeOffset.UtcNow < v.StartsAt.AddMinutes(-30)
                || DateTimeOffset.UtcNow > v.EndsAt.AddHours(1)
            )
                return new("Session check-in is closed");
            if (
                v.RegistrationRequired
                && !await s.Db.EventSessionBookings.AnyAsync(x =>
                    x.SessionId == v.Id && x.RegistrationId == r.Id && x.Active
                )
            )
                return new("Session Registration Required");
            if (v.MembersOnly && r.MembershipId == null)
                return new("Session Not Eligible");
            if (
                !await s.Db.EventCheckins.AnyAsync(x =>
                    x.RegistrationId == r.Id && x.SessionId == null && !x.Voided
                )
            )
                return new("Event Check-in Required");
            var count = await s.Db.EventCheckins.CountAsync(x => x.SessionId == v.Id && !x.Voided);
            if (count >= v.Capacity)
                return new("Session At Capacity");
        }
        return new("Valid", r.Id, r.Name, 1 + r.Companions);
    }
}

public record TicketResult(
    string State,
    Guid? RegistrationId = null,
    string? Name = null,
    int? ReservedSeats = null
);

public record SessionBookingInput(Guid[] SessionIds, string? Token = null);
