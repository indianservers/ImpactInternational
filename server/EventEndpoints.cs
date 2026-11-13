using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record EventUpdateInput(
    Guid EventId,
    string Title,
    string Description,
    string Venue,
    string OnlineUrl,
    long Version
);

public record EventStatusInput(string Action, long Version, string Reason = "");

public record EventAudienceInput(EventAudience[] Rules);

public record InvitationBatchInput(
    DateTimeOffset? SendAt = null,
    string Message = "You are invited.",
    EventGuestInput[]? Guests = null
);

public record EventGuestInput(string Name, string Email, bool Consent);

public record FeedbackInput(string Answers, string? Token = null);

public record EventStaffInput(
    Guid PersonId,
    string Role,
    string Capability,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Responsibilities = "",
    string Tasks = "[]",
    string Notes = ""
);

public static partial class EventEndpoints
{
    public static void MapEvents(this RouteGroupBuilder api)
    {
        api = api.MapGroup("")
            .AddEndpointFilter(
                async (context, next) =>
                {
                    var nullability = new System.Reflection.NullabilityInfoContext();
                    foreach (
                        var arg in context.Arguments.Where(x =>
                            x != null && x.GetType().Namespace == "Clubs" && x is not EventService
                        )
                    )
                    {
                        foreach (
                            var prop in arg!
                                .GetType()
                                .GetProperties()
                                .Where(p => p.PropertyType == typeof(string))
                        )
                            if (
                                nullability.Create(prop).ReadState
                                == System.Reflection.NullabilityState.NotNull
                            )
                                Check(
                                    prop.GetValue(arg) != null,
                                    "Required text field is missing: " + prop.Name
                                );
                    }
                    return await next(context);
                }
            );
        api.MapGet(
            "/public-events/{id:guid}",
            async (Guid id, EventService s) =>
            {
                var e = await s.Find(id);
                Check(
                    e.Visibility == "Public"
                        && e.Publication == "Published"
                        && e.Lifecycle != "Archived",
                    "Public event not found.",
                    404
                );
                return new
                {
                    e.Id,
                    e.Title,
                    e.Description,
                    e.StartsAt,
                    e.EndsAt,
                    e.TimeZone,
                    e.VenueType,
                    e.Venue,
                    e.AllowGuests,
                    e.Lifecycle,
                    registrationWindow = EventService.Window(e),
                };
            }
        );

        api.MapGet(
            "/event-organizations",
            async (EventService s, string search = "", Guid? id = null, int page = 1) =>
            {
                var w = await s.A.WorkspaceAny(
                    "events.view",
                    "events.manage",
                    "events.reports",
                    "events.checkin"
                );
                var q = s.A.Visible(w);
                if (!(await s.A.Grants(w)).Contains("events.manage"))
                {
                    var tenant = w.TenantId;
                    var root = w.OrgId;
                    var sql =
                        $"""WITH RECURSIVE parents AS(SELECT "Id","ParentId","TenantId" FROM "Orgs" WHERE "Id"='{root:D}'::uuid AND "TenantId"='{tenant:D}'::uuid UNION ALL SELECT o."Id",o."ParentId",o."TenantId" FROM "Orgs" o JOIN parents p ON p."ParentId"=o."Id" AND p."TenantId"=o."TenantId") SELECT "Id" AS "Value" FROM parents""";
                    var ancestors = s.Db.Database.SqlQueryRaw<Guid>(sql);
                    q = s.Db.Orgs.Where(x =>
                        x.TenantId == w.TenantId
                        && (
                            ancestors.Contains(x.Id)
                            || s.A.Visible(w).Select(o => o.Id).Contains(x.Id)
                        )
                    );
                }
                if (id != null)
                    q = q.Where(x => x.Id == id);
                if (search != "")
                    q = q.Where(x =>
                        EF.Functions.ILike(x.Name, "%" + search + "%")
                        || EF.Functions.ILike(x.Code, "%" + search + "%")
                    );
                return new
                {
                    total = await q.CountAsync(),
                    items = await q.OrderBy(x => x.Name)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.Name,
                            x.Code,
                            x.LevelId,
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/event-options",
            async (EventService s, Guid? orgId = null) =>
            {
                var w = await s.A.WorkspaceAny("events.view", "events.manage", "events.configure");
                var org = orgId ?? w.OrgId;
                await s.Governance.Scope(w, org);
                return new
                {
                    categories = await s
                        .Db.MembershipCategories.Where(x => x.TenantId == w.TenantId)
                        .Select(x => new { x.Id, x.Name })
                        .ToListAsync(),
                    roles = await s
                        .Db.Roles.Where(x => x.TenantId == w.TenantId)
                        .Select(x => new { x.Id, x.Name })
                        .ToListAsync(),
                    committees = await s
                        .Db.Committees.Where(x =>
                            x.TenantId == w.TenantId && x.OrgId == org && x.Status == "Active"
                        )
                        .Select(x => new { x.Id, x.Name })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/event-people",
            async (EventService s, Guid orgId, string search = "") =>
            {
                var w = await s.A.Workspace("events.manage");
                await s.Governance.Scope(w, orgId);
                var scope = s.A.Tree(w.TenantId, orgId).Select(o => o.Id);
                var people = (await s.Governance.Members(w))
                    .Where(m =>
                        scope.Contains(m.ClubId)
                        && m.Status == "Active"
                        && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                    )
                    .Select(m => m.PersonId)
                    .Distinct();
                var q = s.Db.Persons.Where(p => people.Contains(p.Id));
                if (search != "")
                    q = q.Where(p => EF.Functions.ILike(p.DisplayName, "%" + search + "%"));
                return new
                {
                    items = await q.OrderBy(p => p.DisplayName)
                        .Take(20)
                        .Select(p => new
                        {
                            p.Id,
                            p.DisplayName,
                            hasAccount = p.UserId != null,
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/event-types",
            async (EventService s) =>
            {
                var w = await s.A.WorkspaceAny("events.view", "events.manage", "events.configure");
                return await s
                    .Db.EventTypes.Where(x => x.TenantId == w.TenantId && x.Active)
                    .OrderBy(x => x.Name)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/event-types",
            async (EventType input, EventService s) =>
            {
                var w = await s.A.TenantWorkspace("events.configure");
                Check(
                    input.Name.Trim().Length is > 1 and <= 100 && input.DefaultCapacity > 0,
                    "Invalid event type."
                );
                var t = new EventType
                {
                    TenantId = w.TenantId,
                    Name = input.Name.Trim(),
                    DefaultCapacity = input.DefaultCapacity,
                };
                s.Db.EventTypes.Add(t);
                await s.Db.SaveChangesAsync();
                return t;
            }
        );
        api.MapGet("/events", List);
        api.MapGet("/my/events", List);
        api.MapPost("/events", async (EventInput r, EventService s) => await Save(null, r, s));
        api.MapPut(
            "/events/{id:guid}",
            async (Guid id, EventInput r, EventService s) => await Save(id, r, s)
        );
        api.MapGet(
            "/events/{id:guid}",
            async (Guid id, EventService s, string? token = null) =>
            {
                var e = await s.Find(id);
                await s.View(e, token);
                bool manage = false,
                    configureTeam = false,
                    registration = false,
                    checkin = false;
                try
                {
                    configureTeam = (await s.Manage(e)) != null;
                    manage = true;
                    registration = true;
                    checkin = true;
                }
                catch (ApiError)
                {
                    try
                    {
                        await s.Manage(e, "Checkin");
                        checkin = true;
                    }
                    catch (ApiError) { }
                }
                if (!registration)
                {
                    try
                    {
                        await s.Manage(e, "Registration");
                        registration = true;
                    }
                    catch (ApiError) { }
                }
                var person = token == null ? await s.Me() : null;
                return new
                {
                    eventInfo = e,
                    registrationWindow = EventService.Window(e),
                    myRsvp = person == null
                        ? null
                        : await s
                            .Db.EventInvitations.Where(i => i.EventId == id && i.PersonId == person)
                            .Select(i => i.Rsvp)
                            .FirstOrDefaultAsync(),
                    canManage = manage,
                    canCheckin = checkin,
                    canManageRegistrations = registration,
                    canConfigureTeam = configureTeam,
                    organizer = await s
                        .Db.Orgs.Where(x => x.Id == e.OrgId)
                        .Select(x => x.Name)
                        .SingleAsync(),
                    cohosts = await s
                        .Db.EventCohosts.Where(x => x.EventId == id)
                        .Join(s.Db.Orgs, x => x.OrgId, o => o.Id, (x, o) => new { o.Id, o.Name })
                        .ToListAsync(),
                    sessions = await s
                        .Db.EventSessions.Where(x => x.EventId == id)
                        .OrderBy(x => x.StartsAt)
                        .ToListAsync(),
                    myRegistration = person == null
                        ? null
                        : await s.Db.EventRegistrations.SingleOrDefaultAsync(x =>
                            x.EventId == id && x.PersonId == person
                        ),
                };
            }
        );
        api.MapPost(
            "/events/{id:guid}/status",
            async (Guid id, EventStatusInput input, EventService s) =>
            {
                var e = await s.Find(id);
                var w = await s.Manage(e);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                await s.Db.Entry(e).ReloadAsync();
                Check(e.Version == input.Version, "Event changed.", 409);
                var user = await s.A.User();
                if (input.Action == "Submit")
                {
                    Check(
                        e.EndsAt > DateTimeOffset.UtcNow,
                        "An ended event cannot be submitted for publication.",
                        409
                    );

                    Check(
                        w != null
                            && new[] { "Draft", "Rejected" }.Contains(e.Approval)
                            && e.Publication == "Draft",
                        "Event is not editable.",
                        409
                    );
                    var req = await s.Governance.Request(
                        w!,
                        e.OrgId,
                        "EventPublication",
                        "Publish event: " + e.Title,
                        GovernanceService.Pack(new EventReference(id))
                    );
                    e.ApprovalRequestId = req.Id;
                    await s.Governance.Submit(req, w!);
                    e.Approval = "Pending Approval";
                }
                else if (input.Action == "Withdraw proposal")
                {
                    Check(
                        w != null && e.Publication == "Draft" && e.ApprovalRequestId != null,
                        "No editable proposal.",
                        409
                    );
                    var r = await s.Db.GovernanceRequests.SingleAsync(x =>
                        x.Id == e.ApprovalRequestId
                    );
                    Check(
                        r.CreatorId == user.Id
                            && new[]
                            {
                                "Submitted",
                                "Under Review",
                                "Changes Requested",
                                "Rejected",
                            }.Contains(r.Status),
                        "Only the requester can replace this proposal.",
                        403
                    );
                    if (r.Status != "Rejected")
                    {
                        r.Status = "Withdrawn";
                        r.Version++;
                    }
                    e.Approval = "Draft";
                    e.ApprovalRequestId = null;
                }
                else if (input.Action == "Publish")
                {
                    Check(
                        e.Approval == "Approved"
                            && e.Publication == "Draft"
                            && e.EndsAt > DateTimeOffset.UtcNow,
                        "Approval is required before publishing.",
                        409
                    );
                    e.Publication = "Published";
                    e.PublishedAt = DateTimeOffset.UtcNow;
                }
                else if (input.Action == "Cancel")
                {
                    Check(
                        e.Lifecycle != "Archived"
                            && e.Lifecycle != "Completed"
                            && input.Reason.Trim().Length >= 5,
                        "Give a cancellation reason."
                    );
                    e.Lifecycle = "Cancelled";
                    e.RegistrationEnabled = false;
                    await s.Db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "GovernanceRequests" SET "Status"='Cancelled',"Version"="Version"+1 WHERE "TenantId"={e.TenantId} AND "Kind" LIKE 'Event%' AND "Status" IN ('Draft','Submitted','Under Review','Changes Requested') AND "Payload"::jsonb->>'eventId'={id.ToString()}
                        """
                    );

                    await s
                        .Db.EventTickets.Where(x => x.EventId == id)
                        .ExecuteUpdateAsync(x => x.SetProperty(t => t.Revoked, true));
                    await s
                        .Db.EventRegistrations.Where(x =>
                            x.EventId == id && x.Status != "Cancelled"
                        )
                        .ExecuteUpdateAsync(x =>
                            x.SetProperty(r => r.Status, "Cancelled")
                                .SetProperty(r => r.Version, r => r.Version + 1)
                        );
                    s.QueueNotice(e, "Event cancelled: " + input.Reason);
                    s.QueueNotice(e, "Event cancelled: " + input.Reason, "UnregisteredInvitations");
                    await s
                        .Db.EventSessionBookings.Where(x => x.EventId == id)
                        .ExecuteUpdateAsync(x => x.SetProperty(t => t.Active, false));
                }
                else if (input.Action == "Close registration")
                    e.RegistrationEnabled = false;
                else if (input.Action == "Open registration")
                {
                    Check(
                        e.Publication == "Published"
                            && new[] { "Scheduled", "In Progress" }.Contains(e.Lifecycle)
                            && e.RegistrationEndsAt > DateTimeOffset.UtcNow,
                        "Cannot reopen registration.",
                        409
                    );
                    e.RegistrationEnabled = true;
                }
                else if (input.Action == "Complete")
                {
                    Check(
                        e.Publication == "Published"
                            && e.StartsAt <= DateTimeOffset.UtcNow
                            && e.Lifecycle != "Cancelled",
                        "Event cannot be completed.",
                        409
                    );
                    e.Lifecycle = "Completed";
                    e.RegistrationEnabled = false;
                }
                else if (input.Action == "Archive")
                {
                    Check(
                        new[] { "Completed", "Cancelled" }.Contains(e.Lifecycle),
                        "Complete or cancel first.",
                        409
                    );
                    e.Lifecycle = "Archived";
                    e.RegistrationEnabled = false;
                }
                else
                    throw new ApiError(400, "Unknown event action.");
                e.Version++;
                await s.Audit(e, input.Action, user.Id, input.Reason);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return e;
            }
        );
        api.MapPost(
            "/events/{id:guid}/amend",
            async (Guid id, EventUpdateInput input, EventService s) =>
            {
                var e = await s.Find(id);
                var w = await s.A.Workspace("events.manage");
                await s.Governance.Scope(w, e.OrgId);
                Check(
                    input.EventId == id
                        && e.Publication == "Published"
                        && e.Version == input.Version,
                    "Event changed or is not published.",
                    409
                );
                Check(
                    input.Title.Trim().Length is > 2 and <= 200
                        && input.Description.Length <= 12000
                        && (e.VenueType == "Online" || input.Venue.Trim().Length > 2),
                    "Invalid event amendment."
                );
                if (e.VenueType != "Physical")
                    Check(
                        Uri.TryCreate(input.OnlineUrl, UriKind.Absolute, out var url)
                            && url.Scheme == "https",
                        "A valid HTTPS meeting URL is required."
                    );
                await using var tx = await s.A.Mutation(w);
                var r = await s.Governance.Request(
                    w,
                    e.OrgId,
                    "EventUpdate",
                    "Amend event: " + e.Title,
                    GovernanceService.Pack(input)
                );
                await s.Governance.Submit(r, w);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { request = r });
            }
        );
        api.MapPost(
            "/events/{id:guid}/duplicate",
            async (Guid id, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                var w = await s.A.Workspace("events.manage");
                Check(await s.A.Visible(w).AnyAsync(x => x.Id == e.OrgId), "Outside scope.", 403);
                var copy = JsonSerializer.Deserialize<ClubEvent>(JsonSerializer.Serialize(e))!;
                copy.Id = Guid.NewGuid();
                copy.Title += " (copy)";
                copy.StartsAt = e.StartsAt.AddDays(7);
                copy.EndsAt = e.EndsAt.AddDays(7);
                copy.RegistrationStartsAt = DateTimeOffset.UtcNow;
                copy.RegistrationEndsAt = copy.StartsAt;
                copy.Approval = "Draft";
                copy.Publication = "Draft";
                copy.PublishedAt = null;
                copy.ApprovalRequestId = null;
                copy.Lifecycle = "Scheduled";
                copy.ReminderAt = null;
                copy.ReminderSent = false;
                copy.CompletionSent = false;
                copy.Version = 1;
                copy.CreatorId = w.UserId;
                copy.CreatedAt = DateTimeOffset.UtcNow;
                s.Db.Events.Add(copy);
                foreach (
                    var x in await s
                        .Db.EventSessions.AsNoTracking()
                        .Where(x => x.EventId == id)
                        .ToListAsync()
                )
                {
                    x.Id = Guid.NewGuid();
                    x.EventId = copy.Id;
                    x.StartsAt = x.StartsAt.AddDays(7);
                    x.EndsAt = x.EndsAt.AddDays(7);
                    s.Db.EventSessions.Add(x);
                }
                foreach (
                    var x in await s
                        .Db.EventAudiences.AsNoTracking()
                        .Where(x => x.EventId == id)
                        .ToListAsync()
                )
                {
                    x.Id = Guid.NewGuid();
                    x.EventId = copy.Id;
                    s.Db.EventAudiences.Add(x);
                }
                await s.Db.SaveChangesAsync();
                return copy;
            }
        );
        api.MapGet(
            "/events/{id:guid}/calendar.ics",
            async (Guid id, EventService s, string? token = null) =>
            {
                var e = await s.Find(id);
                await s.View(e, token);
                string Esc(string v) =>
                    v.Replace("\\", "\\\\")
                        .Replace("\r", "")
                        .Replace("\n", "\\n")
                        .Replace(",", "\\,")
                        .Replace(";", "\\;");
                return Results.File(
                    System.Text.Encoding.UTF8.GetBytes(
                        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//ClubSphere//Events//EN\r\nBEGIN:VEVENT\r\nUID:"
                            + e.Id
                            + "@clubsphere\r\nDTSTAMP:"
                            + DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'")
                            + "\r\nDTSTART:"
                            + e.StartsAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'")
                            + "\r\nDTEND:"
                            + e.EndsAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'")
                            + "\r\nSUMMARY:"
                            + Esc(e.Title)
                            + "\r\nLOCATION:"
                            + Esc(e.Venue)
                            + "\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n"
                    ),
                    "text/calendar",
                    "event.ics"
                );
            }
        );
        api.MapPost(
            "/events/{id:guid}/sessions",
            async (Guid id, EventSession r, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                await s.Db.Entry(e).ReloadAsync();
                await s.Manage(e);
                Check(
                    e.Publication == "Draft" && e.Approval == "Draft",
                    "Sessions are frozen after submission.",
                    409
                );
                Check(
                    r.StartsAt >= e.StartsAt
                        && r.EndsAt <= e.EndsAt
                        && r.StartsAt < r.EndsAt
                        && r.Capacity > 0
                        && r.Title.Length is > 1 and <= 200,
                    "Invalid session."
                );
                Check(
                    !await s.Db.EventSessions.AnyAsync(x =>
                        x.EventId == id
                        && x.Status == "Active"
                        && x.StartsAt < r.EndsAt
                        && x.EndsAt > r.StartsAt
                        && (
                            (r.Room != "" && x.Room == r.Room)
                            || (r.Speaker != "" && x.Speaker == r.Speaker)
                        )
                    ),
                    "Room or speaker scheduling conflict.",
                    409
                );
                var v = new EventSession
                {
                    TenantId = e.TenantId,
                    EventId = id,
                    Title = r.Title,
                    Description = r.Description,
                    Speaker = r.Speaker,
                    Room = r.Room,
                    StartsAt = r.StartsAt.ToUniversalTime(),
                    EndsAt = r.EndsAt.ToUniversalTime(),
                    Capacity = r.Capacity,
                    RegistrationRequired = r.RegistrationRequired,
                    AttendanceRequired = r.AttendanceRequired,
                    MembersOnly = r.MembersOnly,
                };
                s.Db.EventSessions.Add(v);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return v;
            }
        );
        api.MapDelete(
            "/events/{id:guid}/sessions/{sessionId:guid}",
            async (Guid id, Guid sessionId, EventService s) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                await s.Db.Entry(e).ReloadAsync();
                await s.Manage(e);
                Check(e.Approval == "Draft", "Sessions are frozen.", 409);
                var v =
                    await s.Db.EventSessions.SingleOrDefaultAsync(x =>
                        x.Id == sessionId && x.EventId == id
                    ) ?? throw new ApiError(404, "Session not found.");
                v.Status = "Cancelled";
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapPut(
            "/events/{id:guid}/audience",
            async (Guid id, EventAudienceInput input, EventService s) =>
            {
                var e = await s.Find(id);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                await s.Db.Entry(e).ReloadAsync();
                var w = await s.A.Workspace("events.manage");
                await s.Governance.Scope(w, e.OrgId);
                Check(
                    e.Approval == "Draft" && input.Rules.Length <= 30,
                    "Audience is frozen after submission.",
                    409
                );
                foreach (var r in input.Rules)
                {
                    Check(r.Status == "Active", "Only active membership audiences are supported.");
                    if (r.OrgId != null)
                        await s.Governance.Scope(w, r.OrgId.Value);
                    if (r.CommitteeId != null)
                    {
                        var c =
                            await s.Db.Committees.SingleOrDefaultAsync(x =>
                                x.Id == r.CommitteeId && x.TenantId == e.TenantId
                            ) ?? throw new ApiError(404, "Committee not found.");
                        await s.Governance.Scope(w, c.OrgId);
                    }
                    if (r.PersonId != null)
                        Check(
                            await (await s.Governance.Members(w)).AnyAsync(x =>
                                x.PersonId == r.PersonId
                            ),
                            "Person outside scope.",
                            403
                        );
                    if (r.RoleId != null)
                        Check(
                            await s.Db.Roles.AnyAsync(x =>
                                x.Id == r.RoleId && x.TenantId == e.TenantId
                            ),
                            "Invalid role."
                        );
                    if (r.CategoryId != null)
                        Check(
                            await s.Db.MembershipCategories.AnyAsync(x =>
                                x.Id == r.CategoryId && x.TenantId == e.TenantId
                            ),
                            "Invalid category."
                        );
                }
                await s.Db.EventAudiences.Where(x => x.EventId == id).ExecuteDeleteAsync();
                foreach (var r in input.Rules)
                    s.Db.EventAudiences.Add(
                        new()
                        {
                            TenantId = e.TenantId,
                            EventId = id,
                            OrgId = r.OrgId,
                            CommitteeId = r.CommitteeId,
                            PersonId = r.PersonId,
                            RoleId = r.RoleId,
                            CategoryId = r.CategoryId,
                            City = r.City,
                            Country = r.Country,
                            Status = r.Status,
                            RegistrationStatus = r.RegistrationStatus,
                        }
                    );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { saved = true });
            }
        );
        api.MapGet(
            "/events/{id:guid}/audience",
            async (Guid id, EventService s, int page = 1) =>
            {
                var e = await s.Find(id);
                await s.Manage(e);
                var people = await s.Audience(e);
                return new
                {
                    total = await people.CountAsync(),
                    rules = await s.Db.EventAudiences.Where(x => x.EventId == id).ToListAsync(),
                    items = await s
                        .Db.Persons.Where(x => people.Contains(x.Id))
                        .OrderBy(x => x.DisplayName)
                        .ThenBy(x => x.Id)
                        .Skip((Math.Clamp(page, 1, 10000) - 1) * 20)
                        .Take(20)
                        .Select(x => new
                        {
                            x.Id,
                            x.DisplayName,
                            reason = "Matches approved event audience",
                        })
                        .ToListAsync(),
                };
            }
        );
        MapInvitations(api);
        MapRegistration(api);
        MapOperations(api);
    }

    private static async Task<object> List(
        EventService s,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string search = "",
        Guid? typeId = null,
        Guid? orgId = null,
        string status = "",
        string venueType = "",
        string location = "",
        string registrationStatus = "",
        Guid? after = null,
        bool mine = false,
        bool manage = false,
        int size = 30
    )
    {
        var user = await s.A.User();
        var person = await s.Me();
        var start = (from ?? DateTimeOffset.UtcNow.AddMonths(-1)).ToUniversalTime();
        var end = (to ?? start.AddMonths(3)).ToUniversalTime();
        Check(start < end && end <= start.AddYears(1), "Select a range no longer than one year.");
        var q = s.Db.Events.Where(e => e.StartsAt < end && e.EndsAt > start);
        var session = await s.A.Session();
        var w = await s.Db.Assignments.SingleOrDefaultAsync(x =>
            x.Id == session.WorkspaceId && x.UserId == user.Id
        );
        IQueryable<Guid> scoped = s.Db.Orgs.Where(x => false).Select(x => x.Id);
        if (
            w != null
            && await s.A.Effective(w)
            && (await s.A.Grants(w)).Any(x => x is "events.manage" or "events.reports")
        )
            scoped = s.A.Visible(w).Select(o => o.Id);
        var eligible =
            person == null
                ? s.Db.Events.Where(x => false).Select(x => x.Id)
                : s.EligibleEventIds(person.Value);
        // Discovery is scoped to selected association; public events across associations remain discoverable.
        q = q.Where(e =>
            (w != null && e.TenantId == w.TenantId && scoped.Contains(e.OrgId))
            || (
                person != null
                && s.Db.EventStaff.Any(t =>
                    t.EventId == e.Id
                    && t.PersonId == person
                    && t.Active
                    && t.StartsAt <= DateTimeOffset.UtcNow
                    && t.EndsAt > DateTimeOffset.UtcNow
                )
            )
            || (
                e.Publication == "Published"
                && e.Lifecycle != "Archived"
                && (
                    e.Visibility == "Public"
                    || eligible.Contains(e.Id)
                    // Null person IDs belong to guest rows, not to every account
                    // that has not created a person profile yet.
                    || (
                        person != null
                        && (
                            s.Db.EventInvitations.Any(i =>
                                i.EventId == e.Id
                                && i.PersonId == person
                                && i.ExpiresAt > DateTimeOffset.UtcNow
                            )
                            || s.Db.EventRegistrations.Any(r =>
                                r.EventId == e.Id && r.PersonId == person
                            )
                            || s.Db.EventAudiences.Any(r =>
                                r.EventId == e.Id && r.PersonId == person
                            )
                        )
                    )
                )
            )
        );
        // Published events whose audience is an organization/committee are checked in the bounded range below.
        if (manage)
        {
            Check(
                w != null
                    && (await s.A.Grants(w)).Any(x => x is "events.manage" or "events.reports"),
                "Choose an event administration workspace.",
                403
            );
            q = q.Where(e => e.TenantId == w!.TenantId && scoped.Contains(e.OrgId));
        }
        if (mine)
            q = q.Where(e =>
                person != null
                && s.Db.EventRegistrations.Any(r =>
                    r.EventId == e.Id && r.PersonId == person && r.Status != "Cancelled"
                )
            );
        if (registrationStatus != "") q = q.Where(e => person != null && s.Db.EventRegistrations.Any(r => r.EventId == e.Id && r.PersonId == person && r.Status == registrationStatus));
        if (typeId != null)
            q = q.Where(x => x.TypeId == typeId);
        if (orgId != null)
        {
            var target = s
                .Db.Orgs.Where(x => x.Id == orgId)
                .Select(x => x.TenantId)
                .FirstOrDefault();
            var ids = s.A.Tree(target, orgId.Value).Select(x => x.Id);
            q = q.Where(x => ids.Contains(x.OrgId));
        }
        if (search != "")
            q = q.Where(x => EF.Functions.ILike(x.Title, "%" + search + "%"));
        if (status != "")
            q = q.Where(x =>
                x.Lifecycle == status || x.Publication == status || x.Approval == status
            );
        if (venueType != "")
            q = q.Where(x => x.VenueType == venueType);
        if (location != "")
            q = q.Where(x => EF.Functions.ILike(x.Venue, "%" + location + "%"));
        var matchingIds = q.Select(e => e.Id);
        var confirmedRecords = s.Db.EventRegistrations.Where(r => matchingIds.Contains(r.EventId) && r.Status == "Confirmed");
        var confirmedCount = await confirmedRecords.CountAsync();
        var attendedCount = await s.Db.EventCheckins.Where(c => matchingIds.Contains(c.EventId) && c.SessionId == null && !c.Voided).Select(c => c.RegistrationId).Distinct().CountAsync();
        var summary = new {
            upcoming = await q.CountAsync(e => e.StartsAt > DateTimeOffset.UtcNow && e.Publication == "Published" && e.Lifecycle != "Cancelled"),
            openInvitations = await s.Db.EventInvitations.CountAsync(i => matchingIds.Contains(i.EventId) && i.Rsvp == "Awaiting Response" && i.ExpiresAt > DateTimeOffset.UtcNow && i.PersonId == person && person != null),
            confirmed = confirmedCount,
            attendanceRate = confirmedCount == 0 ? (double?)null : Math.Round(100d * attendedCount / confirmedCount, 1),
            hosts = await q.Select(e => e.OrgId).Distinct().CountAsync(),
            programs = await q.CountAsync()
        };
        if (after != null)
        {
            var cursor = await q.Where(x => x.Id == after)
                .Select(x => new { x.StartsAt, x.Id })
                .SingleOrDefaultAsync();
            Check(cursor != null, "Invalid cursor.");
            q = q.Where(x =>
                x.StartsAt > cursor!.StartsAt
                || (x.StartsAt == cursor.StartsAt && x.Id.CompareTo(cursor.Id) > 0)
            );
        }
        var items = await q.OrderBy(x => x.StartsAt)
            .ThenBy(x => x.Id)
            .Take(Math.Clamp(size, 1, 100) + 1)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.OrgId,
                x.TypeId,
                x.StartsAt,
                x.EndsAt,
                x.TimeZone,
                x.VenueType,
                x.Venue,
                x.Approval,
                x.Publication,
                x.Lifecycle,
                x.Capacity,
                x.Visibility,
                x.RegistrationEndsAt,
                registrationWindow = EventService.Window(x),
                organizer = s.Db.Orgs.Where(o => o.Id == x.OrgId).Select(o => o.Name).First(),
                category = s.Db.EventTypes.Where(t => t.Id == x.TypeId).Select(t => t.Name).First(),
                registered = s.Db.EventRegistrations.Count(r => r.EventId == x.Id && r.Status == "Confirmed"),
                myStatus = s.Db.EventRegistrations.Where(r => r.EventId == x.Id && r.PersonId == person && person != null).Select(r => r.Status).FirstOrDefault(),
                coverId = s.Db.GovernanceDocuments.Where(d => d.TenantId == x.TenantId && d.EventId == x.Id && d.Kind == "EventBanner" && d.Status == "Available" && (d.RetainUntil == null || d.RetainUntil > DateTimeOffset.UtcNow)).Select(d => (Guid?)d.Id).FirstOrDefault(),
            })
            .ToListAsync();
        var more = items.Count > Math.Clamp(size, 1, 100);
        if (more)
            items.RemoveAt(items.Count - 1);
        return new
        {
            items,
            summary,
            next = more ? items.LastOrDefault()?.Id : (Guid?)null,
            from = start,
            to = end,
        };
    }

    private static async Task<ClubEvent> Save(Guid? id, EventInput r, EventService s)
    {
        var w = await s.A.Workspace("events.manage");
        await s.Governance.Scope(w, r.OrgId);
        Check(
            r.Title.Trim().Length is > 2 and <= 200
                && r.Description.Length <= 12000
                && r.Venue.Length <= 500
                && r.Contact.Length <= 300
                && r.OnlineUrl.Length <= 2000
                && r.Language.Length <= 20
                && r.StartsAt < r.EndsAt
                && r.EndsAt <= r.StartsAt.AddMonths(3)
                && r.RegistrationStartsAt < r.RegistrationEndsAt
                && r.RegistrationEndsAt <= r.EndsAt
                && r.Capacity is > 0 and <= 100000
                && r.MaxCompanions is >= 0 and <= 10
                && new[] { "Allocate", "Offer" }.Contains(r.WaitlistPolicy)
                && r.OfferMinutes is >= 1 and <= 1440,
            "Invalid event dates or configuration."
        );
        Check(!r.Paid, "Paid booking is disabled until payments are integrated.", 409);
        Check(
            new[] { "Physical", "Online", "Hybrid" }.Contains(r.VenueType)
                && new[] { "Public", "Restricted" }.Contains(r.Visibility),
            "Invalid venue or visibility."
        );
        Check(r.VenueType == "Online" || r.Venue.Trim().Length > 2, "Physical venue is required.");
        Check(
            r.VenueType == "Physical"
                || (
                    Uri.TryCreate(r.OnlineUrl, UriKind.Absolute, out var url)
                    && url.Scheme == "https"
                ),
            "A valid HTTPS meeting URL is required."
        );
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(r.TimeZone);
        }
        catch
        {
            throw new ApiError(400, "Unknown time zone.");
        }
        EventService.ValidateFields(r.Fields);
        Check(
            await s.Db.EventTypes.AnyAsync(x =>
                x.Id == r.TypeId && x.TenantId == w.TenantId && x.Active
            ),
            "Invalid event type."
        );
        if (r.CommitteeId != null)
            Check(
                await s.Db.Committees.AnyAsync(x =>
                    x.Id == r.CommitteeId
                    && x.TenantId == w.TenantId
                    && x.OrgId == r.OrgId
                    && x.Status == "Active"
                ),
                "Committee is not active in event owner."
            );
        Check((r.Cohosts?.Length ?? 0) <= 20, "At most twenty co-hosts are permitted.");
        foreach (var host in r.Cohosts ?? [])
            await s.Governance.Scope(w, host);
        await using var tx = await s.Db.Database.BeginTransactionAsync();
        if (id != null)
            await s.Lock(id.Value);
        var e =
            id == null
                ? new ClubEvent
                {
                    TenantId = w.TenantId,
                    OrgId = r.OrgId,
                    CreatorId = w.UserId,
                }
                : await s.Find(id.Value);
        Check(e.TenantId == w.TenantId && e.OrgId == r.OrgId, "Owner is immutable.", 403);
        Check(
            id == null
                || (e.Version == r.Version && e.Approval == "Draft" && e.Publication == "Draft"),
            "Only draft events can be edited. Withdraw a submitted proposal first.",
            409
        );
        if (id != null)
            Check(
                !await s.Db.EventSessions.AnyAsync(v =>
                    v.EventId == id
                    && v.Status == "Active"
                    && (v.StartsAt < r.StartsAt || v.EndsAt > r.EndsAt)
                ),
                "Adjust draft sessions before changing the event range.",
                409
            );
        e.Title = r.Title.Trim();
        e.TypeId = r.TypeId;
        e.Description = r.Description;
        e.StartsAt = r.StartsAt.ToUniversalTime();
        e.EndsAt = r.EndsAt.ToUniversalTime();
        e.RegistrationStartsAt = r.RegistrationStartsAt.ToUniversalTime();
        e.RegistrationEndsAt = r.RegistrationEndsAt.ToUniversalTime();
        Check(
            (r.RsvpDeadline ?? r.RegistrationEndsAt) <= r.EndsAt,
            "RSVP deadline must not follow the event end."
        );
        e.RsvpDeadline = (r.RsvpDeadline ?? r.RegistrationEndsAt).ToUniversalTime();
        e.TimeZone = r.TimeZone;
        e.VenueType = r.VenueType;
        e.Venue = r.Venue;
        Check(
            (r.Latitude == null && r.Longitude == null)
                || (r.Latitude is >= -90 and <= 90 && r.Longitude is >= -180 and <= 180),
            "Provide valid paired map coordinates."
        );
        e.Latitude = r.Latitude;
        e.Longitude = r.Longitude;
        e.OnlineUrl = r.OnlineUrl;
        e.Contact = r.Contact;
        e.Visibility = r.Visibility;
        e.Capacity = r.Capacity;
        e.Waitlist = r.Waitlist;
        e.AllowGuests = r.AllowGuests;
        e.MaxCompanions = r.MaxCompanions;
        e.RegistrationApproval = r.RegistrationApproval;
        e.Fields = r.Fields;
        e.Language = r.Language;
        e.CommitteeId = r.CommitteeId;
        e.RsvpRequired = r.RsvpRequired;
        e.WaitlistPolicy = r.WaitlistPolicy;
        e.OfferMinutes = r.OfferMinutes;
        e.CancellationPolicy = r.CancellationPolicy;
        e.ReminderAt = r.ReminderAt?.ToUniversalTime();
        e.Version++;
        if (id == null)
            s.Db.Events.Add(e);
        else
            await s.Db.EventCohosts.Where(x => x.EventId == e.Id).ExecuteDeleteAsync();
        foreach (var host in (r.Cohosts ?? []).Distinct())
            s.Db.EventCohosts.Add(
                new()
                {
                    TenantId = w.TenantId,
                    EventId = e.Id,
                    OrgId = host,
                }
            );
        await s.Audit(e, id == null ? "created" : "edited", w.UserId);
        await s.Db.SaveChangesAsync();
        await s.A.Workspace("events.manage");
        await tx.CommitAsync();
        return e;
    }
}
