using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static partial class EventEndpoints
{
    private static void MapOperations(RouteGroupBuilder api)
    {
        api.MapGet(
            "/events/{id:guid}/activity",
            async (Guid id, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                return await s
                    .Db.Audits.Where(x =>
                        x.TenantId == e.TenantId
                        && x.Resource == id.ToString()
                        && x.Action.StartsWith("events.")
                    )
                    .OrderByDescending(x => x.At)
                    .Take(100)
                    .Select(x => new
                    {
                        x.Id,
                        x.Action,
                        x.Changes,
                        x.ActorId,
                        x.At,
                    })
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/my/event-duties",
            async (EventService s) =>
            {
                var p = await s.Me();
                return await s
                    .Db.EventStaff.Where(x =>
                        x.PersonId == p && x.Active && x.EndsAt > DateTimeOffset.UtcNow
                    )
                    .OrderBy(x => x.StartsAt)
                    .Take(50)
                    .Select(x => new
                    {
                        x.Id,
                        x.EventId,
                        x.Role,
                        x.Capability,
                        x.StartsAt,
                        x.EndsAt,
                        title = s
                            .Db.Events.Where(e => e.Id == x.EventId)
                            .Select(e => e.Title)
                            .First(),
                    })
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/events/{id:guid}/team",
            async (Guid id, EventService s) =>
            {
                var e = await s.Find(id);
                try
                {
                    await s.Manage(e);
                }
                catch (ApiError)
                {
                    var p = await s.Me();
                    return Results.Ok(
                        await s
                            .Db.EventStaff.Where(x => x.EventId == id && x.PersonId == p)
                            .ToListAsync()
                    );
                }
                return Results.Ok(
                    await s
                        .Db.EventStaff.Where(x => x.EventId == id)
                        .OrderBy(x => x.Role)
                        .Take(200)
                        .Select(x => new
                        {
                            x.Id,
                            x.PersonId,
                            x.Role,
                            x.Capability,
                            x.StartsAt,
                            x.EndsAt,
                            x.Active,
                            x.Responsibilities,
                            x.Tasks,
                            x.TaskStatus,
                            x.Notes,
                            x.ShiftCheckinAt,
                            x.Version,
                            name = s
                                .Db.Persons.Where(p => p.Id == x.PersonId)
                                .Select(p => p.DisplayName)
                                .First(),
                        })
                        .ToListAsync()
                );
            }
        );
        api.MapPost(
            "/events/{id:guid}/team",
            async (Guid id, EventStaffInput input, EventService s) =>
            {
                var e = await s.Find(id);
                var w = await s.A.Workspace("events.manage");
                await s.Governance.Scope(w, e.OrgId);
                Check(
                    new[] { "Manage", "Registration", "Checkin", "Volunteer" }.Contains(
                        input.Capability
                    )
                        && input.StartsAt < input.EndsAt
                        && input.StartsAt >= e.StartsAt.AddDays(-90)
                        && input.EndsAt <= e.EndsAt.AddDays(2)
                        && input.Role.Length is > 1 and <= 100
                        && input.Responsibilities.Length <= 2000
                        && input.Tasks.Length <= 6000
                        && input.Notes.Length <= 2000,
                    "Invalid event assignment."
                );
                Check(
                    await (await s.Governance.Members(w)).AnyAsync(x =>
                        x.PersonId == input.PersonId && x.Status == "Active"
                    ),
                    "Team member outside scope.",
                    403
                );
                var person = await s.Db.Persons.SingleAsync(x => x.Id == input.PersonId);
                Check(person.UserId != null, "Operational access requires a linked account.");
                var t = await s.Db.EventStaff.SingleOrDefaultAsync(x =>
                    x.EventId == id && x.PersonId == input.PersonId
                );
                if (t == null)
                {
                    t = new()
                    {
                        TenantId = e.TenantId,
                        EventId = id,
                        PersonId = input.PersonId,
                    };
                    s.Db.EventStaff.Add(t);
                }
                t.Role = input.Role;
                t.Capability = input.Capability;
                t.StartsAt = input.StartsAt.ToUniversalTime();
                t.EndsAt = input.EndsAt.ToUniversalTime();
                t.Responsibilities = input.Responsibilities;
                t.Tasks = input.Tasks;
                t.Notes = input.Notes;
                t.Active = true;
                t.Version++;
                await s.Audit(
                    e,
                    "staff.assigned",
                    w.UserId,
                    input.PersonId + ": " + input.Capability
                );
                await s.Db.SaveChangesAsync();
                return t;
            }
        );
        api.MapPost(
            "/events/{id:guid}/volunteer",
            async (Guid id, EventService s) =>
            {
                var e = await s.Find(id);
                await s.View(e);
                Check(
                    e.EndsAt > DateTimeOffset.UtcNow
                        && e.Lifecycle is not "Completed" and not "Cancelled" and not "Archived",
                    "Volunteer applications are closed.",
                    409
                );
                var p = await s.Me();
                Check(p != null && await s.Eligible(e, p.Value), "Not eligible to volunteer.", 403);
                Check(
                    !await s.Db.EventStaff.AnyAsync(x => x.EventId == id && x.PersonId == p),
                    "Volunteer interest already recorded.",
                    409
                );
                var t = new EventStaff
                {
                    TenantId = e.TenantId,
                    EventId = id,
                    PersonId = p!.Value,
                    Role = "Volunteer applicant",
                    Capability = "Volunteer",
                    StartsAt = e.StartsAt,
                    EndsAt = e.EndsAt,
                    Active = false,
                };
                s.Db.EventStaff.Add(t);
                await s.Db.SaveChangesAsync();
                return t;
            }
        );
        api.MapPost(
            "/events/{id:guid}/team/{staffId:guid}/duty",
            async (Guid id, Guid staffId, StaffDutyInput r, EventService s) =>
            {
                var e = await s.Find(id);
                var t =
                    await s.Db.EventStaff.SingleOrDefaultAsync(x =>
                        x.Id == staffId && x.EventId == id
                    ) ?? throw new ApiError(404, "Assignment not found.");
                var p = await s.Me();
                if (t.PersonId != p)
                    await s.Manage(e);
                Check(t.Version == r.Version && t.Active, "Assignment changed or inactive.", 409);
                Check(
                    new[] { "Pending", "In Progress", "Completed" }.Contains(r.Status),
                    "Invalid task status."
                );
                t.TaskStatus = r.Status;
                if (r.CheckIn)
                {
                    Check(
                        t.StartsAt <= DateTimeOffset.UtcNow && t.EndsAt > DateTimeOffset.UtcNow,
                        "Shift is not active.",
                        409
                    );
                    t.ShiftCheckinAt ??= DateTimeOffset.UtcNow;
                }
                t.Version++;
                await s.Db.SaveChangesAsync();
                return t;
            }
        );
        api.MapDelete(
            "/events/{id:guid}/team/{staffId:guid}",
            async (Guid id, Guid staffId, EventService s) =>
            {
                var e = await s.Find(id);
                var w = await s.A.Workspace("events.manage");
                await s.Governance.Scope(w, e.OrgId);
                var t =
                    await s.Db.EventStaff.SingleOrDefaultAsync(x =>
                        x.Id == staffId && x.EventId == id
                    ) ?? throw new ApiError(404, "Assignment not found.");
                t.Active = false;
                t.Version++;
                await s.Audit(e, "staff.removed", w.UserId, staffId.ToString());
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapPut(
            "/events/{id:guid}/feedback-form",
            async (Guid id, EventFeedbackForm r, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                Check(
                    !await s.Db.EventFeedbackResponses.AnyAsync(x => x.EventId == id),
                    "Survey is frozen after first response.",
                    409
                );
                EventService.ValidateFields(r.Questions);
                Check(
                    r.Deadline > e.EndsAt && r.Deadline <= e.EndsAt.AddDays(30),
                    "Survey deadline must be after the event and within 30 days."
                );
                var f = await s.Db.EventFeedbackForms.SingleOrDefaultAsync(x => x.EventId == id);
                if (f == null)
                {
                    f = new() { TenantId = e.TenantId, EventId = id };
                    s.Db.EventFeedbackForms.Add(f);
                }
                f.Questions = r.Questions;
                f.Anonymous = r.Anonymous;
                f.Deadline = r.Deadline.ToUniversalTime();
                await s.Db.SaveChangesAsync();
                return f;
            }
        );
        api.MapGet(
            "/events/{id:guid}/feedback-form",
            async (Guid id, EventService s, string? token = null) =>
            {
                var e = await s.Find(id);
                await s.View(e, token);
                return Results.Ok(
                    await s.Db.EventFeedbackForms.SingleOrDefaultAsync(x => x.EventId == id)
                );
            }
        );
        api.MapPost(
            "/events/{id:guid}/feedback/{registrationId:guid}",
            async (Guid id, Guid registrationId, FeedbackInput input, EventService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var e = await s.Find(id);
                var r = await s.Own(e, registrationId, input.Token);
                var f =
                    await s.Db.EventFeedbackForms.SingleOrDefaultAsync(x => x.EventId == id)
                    ?? throw new ApiError(404, "Survey unavailable.");
                Check(
                    e.Lifecycle == "Completed"
                        && f.Deadline > DateTimeOffset.UtcNow
                        && !r.FeedbackSubmitted
                        && await s.Db.EventCheckins.AnyAsync(x =>
                            x.RegistrationId == r.Id && x.SessionId == null && !x.Voided
                        ),
                    "Feedback unavailable, duplicate, or attendance required.",
                    409
                );
                EventService.ValidateAnswers(f.Questions, input.Answers);
                r.FeedbackSubmitted = true;
                r.Version++;
                s.Db.EventFeedbackResponses.Add(
                    new()
                    {
                        TenantId = e.TenantId,
                        EventId = id,
                        RegistrationId = f.Anonymous ? null : r.Id,
                        Answers = input.Answers,
                    }
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { recorded = true, anonymous = f.Anonymous });
            }
        );
        api.MapGet(
            "/events/{id:guid}/feedback",
            async (Guid id, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                var f = await s.Db.EventFeedbackForms.SingleOrDefaultAsync(x => x.EventId == id);
                var count = await s.Db.EventFeedbackResponses.CountAsync(x => x.EventId == id);
                if (count < 5)
                    return Results.Ok(
                        new
                        {
                            count,
                            suppressed = true,
                            detail = "Summaries require at least five responses.",
                        }
                    );
                var ratings = await s
                    .Db.Database.SqlQuery<EventRatingSummary>(
                        $"""
                        SELECT x.key AS "Question",avg((x.value::text)::double precision) AS "Average",count(*)::int AS "Responses"
                        FROM "EventFeedbackResponses" f CROSS JOIN LATERAL jsonb_each(f."Answers"::jsonb) x
                        WHERE f."EventId"={id} AND jsonb_typeof(x.value)='number' GROUP BY x.key
                        """
                    )
                    .ToListAsync();
                return Results.Ok(
                    new
                    {
                        count,
                        suppressed = false,
                        anonymous = f?.Anonymous,
                        ratings,
                        notice = "Anonymous raw responses and individual identities are never exported.",
                    }
                );
            }
        );
        api.MapGet(
            "/events/{id:guid}/feedback/export",
            async (Guid id, EventService s, HttpContext context) =>
            {
                var e = await s.Find(id);
                var w = await s.A.Workspace("events.reports");
                await s.Governance.Scope(w, e.OrgId);
                var f = await s.Db.EventFeedbackForms.SingleOrDefaultAsync(x => x.EventId == id);
                var count = await s.Db.EventFeedbackResponses.CountAsync(x => x.EventId == id);
                Check(
                    f != null && !f.Anonymous && count >= 5,
                    "Individual export is unavailable for anonymous or small-group surveys.",
                    403
                );
                return Results.Stream(
                    async stream =>
                    {
                        await stream.WriteAsync(new byte[] { 91 }, context.RequestAborted);
                        var first = true;
                        await foreach (
                            var row in s
                                .Db.EventFeedbackResponses.AsNoTracking()
                                .Where(x => x.EventId == id)
                                .Select(x => new { x.RegistrationId, x.Answers })
                                .AsAsyncEnumerable()
                                .WithCancellation(context.RequestAborted)
                        )
                        {
                            if (!first)
                                await stream.WriteAsync(new byte[] { 44 }, context.RequestAborted);
                            first = false;
                            await JsonSerializer.SerializeAsync(
                                stream,
                                row,
                                GovernanceService.Json,
                                context.RequestAborted
                            );
                        }
                        await stream.WriteAsync(new byte[] { 93 }, context.RequestAborted);
                    },
                    contentType: "application/json",
                    fileDownloadName: "feedback.json"
                );
            }
        );
        api.MapGet(
            "/events/{id:guid}/analytics",
            async (Guid id, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                return await Analytics(e, s);
            }
        );
        api.MapGet(
            "/events-dashboard",
            async (EventService s, DateTimeOffset? from = null, DateTimeOffset? to = null) =>
            {
                var w = await s.A.Workspace("events.reports");
                var begin = (from ?? DateTimeOffset.UtcNow.AddMonths(-6)).ToUniversalTime();
                var end = (to ?? begin.AddYears(1)).ToUniversalTime();
                Check(begin < end && end <= begin.AddYears(2), "Invalid report range.");
                var orgs = (await s.Governance.Organizations(w)).Select(x => x.Id);
                var events = s.Db.Events.Where(e =>
                    e.TenantId == w.TenantId
                    && orgs.Contains(e.OrgId)
                    && e.StartsAt >= begin
                    && e.StartsAt < end
                );
                var ids = events.Select(x => x.Id);
                var confirmed = s.Db.EventRegistrations.Where(x =>
                    ids.Contains(x.EventId) && x.Status == "Confirmed"
                );
                var attended = s.Db.EventCheckins.Where(x =>
                    ids.Contains(x.EventId) && x.SessionId == null && !x.Voided
                );
                var invitations = s.Db.EventInvitations.Where(x => ids.Contains(x.EventId));
                var count = await invitations.CountAsync();
                var responded = await invitations.CountAsync(x => x.Rsvp != "Awaiting Response");
                var reg = await confirmed.CountAsync();
                var att = await attended.CountAsync();
                return new
                {
                    eventsCreated = await events.CountAsync(),
                    upcoming = await events.CountAsync(x =>
                        x.StartsAt > DateTimeOffset.UtcNow
                        && x.Publication == "Published"
                        && x.Lifecycle != "Cancelled"
                    ),
                    confirmed = reg,
                    attended = att,
                    invitations = count,
                    invitationsDelivered = await invitations.CountAsync(i =>
                        i.SentAt != null && i.Status == "Delivered"
                    ),
                    invitationsSuppressed = await invitations.CountAsync(i =>
                        i.Status == "Suppressed"
                    ),
                    rsvpResponseRate = count == 0 ? 0 : Math.Round(100d * responded / count, 1),
                    attendanceRate = reg == 0 ? 0 : Math.Round(100d * att / reg, 1),
                    denominators = "RSVP: responses / deduplicated invitations. Attendance: distinct active event check-ins / confirmed primary registrations. Companions are seats, not individually checked-in attendees.",
                    byClub = await events
                        .GroupBy(x => x.OrgId)
                        .Select(g => new
                        {
                            orgId = g.Key,
                            name = s.Db.Orgs.Where(o => o.Id == g.Key).Select(o => o.Name).First(),
                            events = g.Count(),
                            confirmed = s.Db.EventRegistrations.Count(r =>
                                r.Status == "Confirmed" && g.Select(x => x.Id).Contains(r.EventId)
                            ),
                            attended = s.Db.EventCheckins.Count(c =>
                                c.SessionId == null
                                && !c.Voided
                                && g.Select(x => x.Id).Contains(c.EventId)
                            ),
                        })
                        .Take(100)
                        .ToListAsync(),
                    byCountry = await confirmed
                        .Where(r => r.MembershipId != null)
                        .Join(
                            s.Db.Memberships,
                            r => r.MembershipId,
                            m => (Guid?)m.Id,
                            (r, m) => new { r.Id, m.ClubId }
                        )
                        .Join(
                            s.Db.Orgs,
                            r => r.ClubId,
                            o => o.Id,
                            (r, o) => new { r.Id, o.Country }
                        )
                        .GroupBy(x => x.Country)
                        .Select(g => new
                        {
                            country = g.Key,
                            registered = g.Count(),
                            attended = g.Count(r =>
                                s.Db.EventCheckins.Any(c =>
                                    c.RegistrationId == r.Id && c.SessionId == null && !c.Voided
                                )
                            ),
                        })
                        .ToListAsync(),
                    byLevel = await events
                        .Join(s.Db.Orgs, e => e.OrgId, o => o.Id, (e, o) => new { e.Id, o.LevelId })
                        .GroupBy(x => x.LevelId)
                        .Select(g => new
                        {
                            levelId = g.Key,
                            name = s
                                .Db.Levels.Where(l => l.Id == g.Key)
                                .Select(l => l.Name)
                                .First(),
                            events = g.Count(),
                        })
                        .ToListAsync(),
                    participationTrend = await confirmed
                        .Join(
                            events,
                            r => r.EventId,
                            e => e.Id,
                            (r, e) =>
                                new
                                {
                                    r.Id,
                                    e.StartsAt.Year,
                                    e.StartsAt.Month,
                                }
                        )
                        .GroupBy(x => new { x.Year, x.Month })
                        .Select(g => new
                        {
                            g.Key.Year,
                            g.Key.Month,
                            registered = g.Count(),
                            attended = g.Count(r =>
                                s.Db.EventCheckins.Any(c =>
                                    c.RegistrationId == r.Id && c.SessionId == null && !c.Voided
                                )
                            ),
                        })
                        .OrderBy(x => x.Year)
                        .ThenBy(x => x.Month)
                        .ToListAsync(),
                    breakdownPolicy = "Country and club use the registration membership snapshot, preferring the owner club. Guest nationalities are not inferred. Trend months use UTC event starts.",
                    byType = await events
                        .GroupBy(x => x.TypeId)
                        .Select(g => new
                        {
                            typeId = g.Key,
                            name = s
                                .Db.EventTypes.Where(t => t.Id == g.Key)
                                .Select(t => t.Name)
                                .First(),
                            count = g.Count(),
                        })
                        .ToListAsync(),
                    byMonth = await events
                        .GroupBy(x => new { x.StartsAt.Year, x.StartsAt.Month })
                        .Select(g => new
                        {
                            g.Key.Year,
                            g.Key.Month,
                            count = g.Count(),
                        })
                        .OrderBy(x => x.Year)
                        .ThenBy(x => x.Month)
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/events/{id:guid}/attendance/export",
            async (Guid id, EventService s) =>
            {
                var e = await s.Find(id);
                var w = await s.A.Workspace("events.reports");
                await s.Governance.Scope(w, e.OrgId);
                var rows = await s
                    .Db.EventCheckins.Where(x => x.EventId == id && !x.Voided)
                    .Join(
                        s.Db.EventRegistrations,
                        c => c.RegistrationId,
                        r => r.Id,
                        (c, r) =>
                            new
                            {
                                r.Name,
                                r.Category,
                                c.SessionId,
                                c.At,
                            }
                    )
                    .Take(100000)
                    .ToListAsync();
                string C(string x)
                {
                    var trimmed = x.TrimStart();
                    if (
                        (x.Length > 0 && "=+-@\t\r\n".Contains(x[0]))
                        || (trimmed.Length > 0 && "=+-@".Contains(trimmed[0]))
                    )
                        x = "'" + x;
                    return "\"" + x.Replace("\"", "\"\"") + "\"";
                }
                var csv =
                    "Participant,Category,Session,CheckedInUtc\r\n"
                    + string.Join(
                        "\r\n",
                        rows.Select(r =>
                            string.Join(
                                ",",
                                C(
                                    (r.Name.Length > 0 && "=+-@".Contains(r.Name[0]) ? "'" : "")
                                        + r.Name
                                ),
                                C(r.Category),
                                C(r.SessionId?.ToString() ?? "Event"),
                                C(r.At.ToString("O"))
                            )
                        )
                    );
                await s.Audit(e, "attendance.exported", w.UserId);
                await s.Db.SaveChangesAsync();
                return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "attendance.csv");
            }
        );
        api.MapGet(
            "/event-filters",
            async (EventService s) =>
            {
                var u = await s.A.User();
                return await s
                    .Db.EventSavedFilters.Where(x => x.UserId == u.Id)
                    .Take(30)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/event-filters",
            async (EventSavedFilter input, EventService s) =>
            {
                var u = await s.A.User();
                Check(
                    input.Name.Length is > 0 and <= 100
                        && input.Filters.Length <= 4000
                        && await s.Db.EventSavedFilters.CountAsync(x => x.UserId == u.Id) < 30,
                    "Invalid filter or limit reached."
                );
                using var json = JsonDocument.Parse(input.Filters);
                var f = new EventSavedFilter
                {
                    UserId = u.Id,
                    Name = input.Name,
                    Filters = input.Filters,
                };
                s.Db.EventSavedFilters.Add(f);
                await s.Db.SaveChangesAsync();
                return f;
            }
        );
        MapEventMedia(api);
    }

    private static async Task<object> Analytics(ClubEvent e, EventService s)
    {
        var regs = s.Db.EventRegistrations.Where(x => x.EventId == e.Id);
        var invitations = s.Db.EventInvitations.Where(x => x.EventId == e.Id);
        var attended = s.Db.EventCheckins.Where(x =>
            x.EventId == e.Id && x.SessionId == null && !x.Voided
        );
        var confirmed = await regs.CountAsync(x => x.Status == "Confirmed");
        var attendance = await attended.CountAsync();
        var invited = await invitations.CountAsync();
        var responded = await invitations.CountAsync(x => x.Rsvp != "Awaiting Response");
        return new
        {
            eventId = e.Id,
            eligibleAudience = await (await s.Audience(e)).CountAsync(),
            invited,
            delivered = await invitations.CountAsync(i =>
                i.SentAt != null && i.Status == "Delivered"
            ),
            suppressed = await invitations.CountAsync(i => i.Status == "Suppressed"),
            responded,
            confirmed,
            waitlisted = await regs.CountAsync(x => x.Status == "Waitlisted"),
            attended = attendance,
            noShows = e.Lifecycle == "Completed" ? Math.Max(0, confirmed - attendance) : (int?)null,
            guests = await regs.CountAsync(x => x.Status == "Confirmed" && x.MembershipId == null),
            members = await regs.CountAsync(x => x.Status == "Confirmed" && x.MembershipId != null),
            seats = await regs.Where(x => x.Status == "Confirmed" || x.Status == "Offered")
                .SumAsync(x => x.Companions + 1),
            rsvpRate = invited == 0 ? 0 : Math.Round(100d * responded / invited, 1),
            attendanceRate = confirmed == 0 ? 0 : Math.Round(100d * attendance / confirmed, 1),
            conversionRate = invited == 0
                ? 0
                : Math.Round(
                    100d
                        * await regs.CountAsync(x =>
                            x.Status == "Confirmed" && x.InvitationId != null
                        )
                        / invited,
                    1
                ),
            denominators = "RSVP: all persisted responses / unique invitations. Conversion: invited confirmed registrations / unique invitations. Attendance: active event check-ins / confirmed primary registrations. No shows only after completion. Cancelled/rejected/waitlisted are excluded from confirmed. Companions reserve capacity but do not count as individually verified attendees.",
            sessions = await s
                .Db.EventSessions.Where(x => x.EventId == e.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    booked = s.Db.EventSessionBookings.Count(b => b.SessionId == x.Id && b.Active),
                    attended = s.Db.EventCheckins.Count(c => c.SessionId == x.Id && !c.Voided),
                })
                .ToListAsync(),
            byClub = await regs.Where(x => x.Status == "Confirmed" && x.MembershipId != null)
                .Join(
                    s.Db.Memberships,
                    r => r.MembershipId,
                    m => (Guid?)m.Id,
                    (r, m) => new { r.Id, m.ClubId }
                )
                .GroupBy(x => x.ClubId)
                .Select(g => new
                {
                    clubId = g.Key,
                    confirmed = g.Count(),
                    attended = g.Count(r =>
                        s.Db.EventCheckins.Any(c =>
                            c.RegistrationId == r.Id && c.SessionId == null && !c.Voided
                        )
                    ),
                })
                .ToListAsync(),
        };
    }
}

public record StaffDutyInput(string Status, bool CheckIn, long Version);

public class EventRatingSummary
{
    public string Question { get; set; } = "";
    public double Average { get; set; }
    public int Responses { get; set; }
}
