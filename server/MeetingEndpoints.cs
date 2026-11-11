using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record MeetingInput(
    Guid OrgId,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Type = "Regular club meeting",
    string TimeZone = "UTC",
    string Location = "",
    string OnlineUrl = "",
    string Visibility = "Members",
    Guid? ChairId = null,
    Guid? SecretaryId = null,
    Guid? CommitteeId = null,
    Guid? EventId = null,
    Guid? SessionId = null,
    int QuorumMinimum = 1,
    decimal QuorumPercent = 0,
    Guid[]? VotingCategories = null,
    Guid[]? RequiredPeople = null,
    Guid[]? OptionalPeople = null,
    long Version = 1,
    int Recurrences = 0,
    int IntervalDays = 7,
    string? LocalStartsAt = null,
    string? LocalEndsAt = null
);

public record AgendaItem(
    Guid Id,
    string Title,
    string Description = "",
    Guid? PresenterId = null,
    int Duration = 10,
    string Category = "Discussion",
    string Outcome = "",
    string Status = "Pending"
);

public record AgendaInput(AgendaItem[] Items, long Version);

public record TextInput(string Body, long Version = 1);

public record StateInput(string Status, long Version = 1, string Reason = "");

public record AttendanceInput(
    Guid PersonId,
    string Status,
    string Mode = "InPerson",
    string Reason = "",
    string Rsvp = "Pending"
);

public static partial class OperationsEndpoints
{
    public static void MapOperations(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("").AddEndpointFilter<OperationsInputFilter>();
        MapMeetings(group);
        MapTasks(group);
        MapProjects(group);
        MapVolunteers(group);
        MapDocuments(group);
        MapCertificates(group);
        MapIntelligence(group);
    }

    static void MapMeetings(RouteGroupBuilder api)
    {
        api.MapGet(
            "/operations/agenda-templates/{orgId:guid}",
            async (Guid orgId, OperationsService s) =>
            {
                var w = await s.Scope(orgId);
                return await s
                    .Db.OrganizationSettings.Where(x =>
                        x.TenantId == w.TenantId
                        && x.OrgId == orgId
                        && x.Key.StartsWith("operations.agenda.")
                    )
                    .Select(x => new
                    {
                        x.Id,
                        name = x.Key.Substring(18),
                        body = x.Value,
                    })
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPut(
            "/operations/agenda-templates/{orgId:guid}/{name}",
            async (Guid orgId, string name, AgendaInput i, OperationsService s) =>
            {
                var w = await s.Scope(orgId, true);
                Check(
                    name.Length is > 0 and <= 80 && i.Items.Length is > 0 and <= 100,
                    "Invalid template name or item count."
                );
                foreach (var item in i.Items)
                {
                    OperationsService.Title(item.Title);
                    Check(
                        item.Duration is >= 0 and <= 1440 && item.Description.Length <= 5000,
                        "Invalid template item."
                    );
                }
                var key = "operations.agenda." + name;
                var row = await s.Db.OrganizationSettings.SingleOrDefaultAsync(x =>
                    x.TenantId == w.TenantId && x.OrgId == orgId && x.Key == key
                );
                if (row == null)
                {
                    row = new()
                    {
                        TenantId = w.TenantId,
                        OrgId = orgId,
                        Key = key,
                    };
                    s.Db.OrganizationSettings.Add(row);
                }
                s.Governance.Audit(w, orgId, "operations.agenda.template", row.Id, row.Value);
                row.Value = Pack(i.Items);
                await s.Db.SaveChangesAsync();
                return new { row.Id, name };
            }
        );
        api.MapGet(
            "/meetings",
            async (
                OperationsService s,
                DateTimeOffset from,
                DateTimeOffset to,
                string? search = null,
                string? type = null,
                bool mine = false
            ) =>
            {
                from = from.ToUniversalTime();
                to = to.ToUniversalTime();
                var w = await s.Workspace();
                Check(
                    to > from && to - from <= TimeSpan.FromDays(370),
                    "Select a date range up to one year."
                );
                var user = w.UserId;
                var person = await s
                    .Db.Persons.Where(p => p.UserId == user)
                    .Select(p => (Guid?)p.Id)
                    .SingleOrDefaultAsync();
                var pid = person?.ToString() ?? "not-a-person";

                var q = (await s.Meetings(w))
                    .AsNoTracking()
                    .Where(m => m.StartsAt < to && m.EndsAt >= from);
                if (mine)
                    q = q.Where(m =>
                        m.OrganizerId == user
                        || m.ChairId == person
                        || m.SecretaryId == person
                        || m.RequiredPeople.Contains(pid)
                        || m.OptionalPeople.Contains(pid)
                    );
                if (search != null)
                {
                    Check(search.Length <= 100, "Search too long.");
                    q = q.Where(m => EF.Functions.ILike(m.Title, "%" + search + "%"));
                }
                if (type != null)
                    q = q.Where(m => m.Type == type);

                return await q.OrderBy(m => m.StartsAt)
                    .Take(250)
                    .Select(m => new
                    {
                        m.Id,
                        m.Title,
                        m.Type,
                        m.OrgId,
                        m.StartsAt,
                        m.EndsAt,
                        m.Status,
                        m.Visibility,
                        m.TimeZone,
                        m.Location,
                        m.CommitteeId,
                    })
                    .ToListAsync();
            }
        );

        api.MapPost(
            "/meetings",
            async (MeetingInput i, OperationsService s, EventService events) =>
            {
                i = ResolveMeetingTimes(i);
                var w = await s.Scope(i.OrgId, true);
                await ValidateMeeting(i, s, w, events);
                Check(
                    i.Recurrences is >= 0 and <= 52 && i.IntervalDays is >= 1 and <= 366,
                    "Maximum 52 recurrence instances."
                );
                var series = i.Recurrences > 0 ? Guid.NewGuid() : (Guid?)null;
                ClubMeeting? first = null;

                for (int n = 0; n <= i.Recurrences; n++)
                {
                    var m = NewMeeting(i, w);
                    m.SeriesId = series;

                    var zone = TimeZoneInfo.FindSystemTimeZoneById(i.TimeZone);
                    var local = TimeZoneInfo
                        .ConvertTime(i.StartsAt, zone)
                        .DateTime.AddDays(n * i.IntervalDays);
                    while (zone.IsInvalidTime(local))
                        local = local.AddMinutes(1);
                    var offset = zone.IsAmbiguousTime(local)
                        ? zone.GetAmbiguousTimeOffsets(local).Min()
                        : zone.GetUtcOffset(local);
                    m.StartsAt = new DateTimeOffset(local, offset).ToUniversalTime();
                    m.EndsAt = m.StartsAt + (i.EndsAt - i.StartsAt);
                    s.Db.ClubMeetings.Add(m);
                    first ??= m;
                    s.Audit(w, m, "meeting.created");
                }
                await s.Db.SaveChangesAsync();
                return first;
            }
        );

        api.MapGet(
            "/meetings/{id:guid}",
            async (Guid id, OperationsService s) => await s.Meeting(id)
        );

        api.MapPut(
            "/meetings/{id:guid}",
            async (Guid id, MeetingInput i, OperationsService s, EventService events) =>
            {
                i = ResolveMeetingTimes(i);
                var m = await s.Meeting(id, true);
                var w = await s.Workspace(true);
                Check(
                    m.OrgId == i.OrgId
                        && m.Version == i.Version
                        && m.AgendaStatus != "Review"
                        && m.Status is not "Completed" and not "Archived",
                    "Meeting changed or locked.",
                    409
                );
                await ValidateMeeting(i, s, w, events);
                s.Revision(m, "Meeting", Pack(m), w.UserId);
                var replacement = NewMeeting(i, w);
                replacement.Id = m.Id;
                replacement.Status = m.Status;
                replacement.OrganizerId = m.OrganizerId;
                replacement.Agenda = m.Agenda;
                replacement.AgendaStatus = m.AgendaStatus;
                replacement.AgendaRequestId = m.AgendaRequestId;
                replacement.SeriesId = m.SeriesId;
                replacement.Version = m.Version + 1;
                s.Db.Entry(m).CurrentValues.SetValues(replacement);

                await s.Db.SaveChangesAsync();
                return m;
            }
        );

        api.MapPost(
            "/meetings/{id:guid}/status",
            async (Guid id, StateInput i, OperationsService s) =>
            {
                var m = await s.Meeting(id, true);
                var w = await s.Workspace(true);
                Check(m.Version == i.Version, "Meeting changed.", 409);
                var transitions = new Dictionary<string, string[]>
                {
                    { "Draft", ["Scheduled", "Cancelled"] },
                    { "Scheduled", ["In Progress", "Postponed", "Cancelled"] },
                    { "Postponed", ["Scheduled", "Cancelled"] },
                    { "In Progress", ["Completed", "Cancelled"] },
                    { "Completed", ["Archived"] },
                };
                Check(
                    transitions.TryGetValue(m.Status, out var next) && next.Contains(i.Status),
                    "Invalid meeting transition.",
                    409
                );
                s.Revision(m, "Meeting", Pack(m), w.UserId, m.Status);
                m.Status = i.Status;
                m.Version++;
                if (new[] { "Scheduled", "Cancelled", "Postponed" }.Contains(m.Status))
                    foreach (
                        var person in Unpack<Guid[]>(m.RequiredPeople)
                            .Concat(Unpack<Guid[]>(m.OptionalPeople))
                            .Distinct()
                    )
                        await s.Notice(person, "Meeting " + m.Status + ": " + m.Title, m);
                s.Audit(w, m, "meeting.status", i.Reason);
                await s.Db.SaveChangesAsync();
                return m;
            }
        );

        api.MapPut(
            "/meetings/{id:guid}/agenda",
            async (Guid id, AgendaInput i, OperationsService s) =>
            {
                var m = await s.Meeting(id, true);
                var w = await s.Workspace(true);
                Check(
                    m.Version == i.Version && m.AgendaStatus != "Review" && m.Status != "Archived",
                    "Agenda changed or under review.",
                    409
                );
                Check(
                    i.Items.Length <= 100
                        && i.Items.Select(x => x.Id).Distinct().Count() == i.Items.Length,
                    "Invalid agenda."
                );
                foreach (var item in i.Items)
                {
                    OperationsService.Title(item.Title);
                    Check(
                        item.Duration is >= 0 and <= 1440 && item.Description.Length <= 5000,
                        "Invalid agenda item."
                    );
                    if (item.PresenterId != null)
                        await s.Person(item.PresenterId.Value, w.TenantId);
                }
                s.Revision(m, "Agenda", m.Agenda, w.UserId, m.AgendaStatus);
                m.Agenda = Pack(i.Items);
                m.AgendaStatus = "Draft";
                m.Version++;
                s.Audit(w, m, "agenda.updated");
                await s.Db.SaveChangesAsync();
                return m;
            }
        );

        api.MapPost(
            "/meetings/{id:guid}/agenda/submit",
            async (Guid id, OperationsService s) =>
            {
                var m = await s.Meeting(id, true);
                var w = await s.Workspace(true);
                Check(
                    m.AgendaStatus == "Draft" && Unpack<AgendaItem[]>(m.Agenda).Length > 0,
                    "Prepare a draft agenda.",
                    409
                );
                m.AgendaStatus = "Review";
                var r = await s.Submit(m, "OperationAgenda", m.Agenda, w);
                m.AgendaRequestId = r.Id;
                await s.Db.SaveChangesAsync();
                return r;
            }
        );

        api.MapGet(
            "/meetings/{id:guid}/minutes",
            async (Guid id, OperationsService s) =>
            {
                await s.Meeting(id);
                return await s
                    .Db.MeetingMinutes.Where(x => x.MeetingId == id)
                    .OrderByDescending(x => x.Revision)
                    .Take(100)
                    .ToListAsync();
            }
        );

        api.MapPost(
            "/meetings/{id:guid}/minutes",
            async (Guid id, TextInput i, OperationsService s) =>
            {
                var m = await s.Meeting(id, true);
                var w = await s.Workspace(true);
                Check(
                    m.Status is "Completed" or "Archived",
                    "Complete the meeting before minutes.",
                    409
                );
                Check(
                    i.Body.Length is > 0 and <= 50000,
                    "Minutes require text, up to 50,000 characters."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                var v = new MeetingMinutes
                {
                    TenantId = m.TenantId,
                    OrgId = m.OrgId,
                    MeetingId = id,
                    Body = i.Body,
                    PreparedBy = w.UserId,
                    Revision =
                        1
                        + (
                            await s
                                .Db.MeetingMinutes.Where(x => x.MeetingId == id)
                                .MaxAsync(x => (int?)x.Revision)
                            ?? 0
                        ),
                };
                s.Db.MeetingMinutes.Add(v);
                s.Revision(v, "Minutes", v.Body, w.UserId);
                s.Audit(w, v, "minutes.drafted");
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return v;
            }
        );

        api.MapPost(
            "/meetings/{id:guid}/minutes/{version:guid}/submit",
            async (Guid id, Guid version, OperationsService s) =>
            {
                await s.Meeting(id, true);
                var w = await s.Workspace(true);
                var v = await s.Db.MeetingMinutes.SingleOrDefaultAsync(x =>
                    x.Id == version && x.MeetingId == id
                );
                Check(
                    v != null && v.Status == "Draft" && v.PreparedBy == w.UserId,
                    "Minutes are not an editable draft.",
                    409
                );
                v.Status = "Review";
                var r = await s.Submit(v, "OperationMinutes", v.Body, w);
                v.RequestId = r.Id;
                await s.Db.SaveChangesAsync();
                return r;
            }
        );

        api.MapGet(
            "/meetings/{id:guid}/minutes/{version:guid}/pdf",
            async (Guid id, Guid version, OperationsService s) =>
            {
                var m = await s.Meeting(id);
                var v = await s.Db.MeetingMinutes.SingleOrDefaultAsync(x =>
                    x.Id == version && x.MeetingId == id && x.Status == "Published"
                );
                Check(v != null, "Approved minutes unavailable.", 404);
                return Results.File(
                    EventPdf.Build(
                        new[]
                        {
                            m.Title,
                            "Minutes · revision " + v.Revision,
                            m.StartsAt.ToString("u"),
                        }
                            .Concat(v.Body.Split('\n'))
                            .ToArray()
                    ),
                    "application/pdf",
                    "minutes.pdf"
                );
            }
        );

        api.MapGet(
            "/meetings/{id:guid}/attendance",
            async (Guid id, OperationsService s, EventService events) =>
            {
                var m = await s.Meeting(id);
                var roster = true;
                if (m.EventId != null)
                {
                    var source = await events.Find(m.EventId.Value);
                    await events.View(source);
                    try
                    {
                        await events.Manage(source, "Checkin");
                    }
                    catch (ApiError error) when (error.Status == 403)
                    {
                        roster = false;
                    }
                }
                return await MeetingAttendanceResult(m, s, roster);
            }
        );

        api.MapPost(
            "/meetings/{id:guid}/attendance",
            async (Guid id, AttendanceInput i, OperationsService s) =>
            {
                var m = await s.Meeting(id, true);
                var w = await s.Workspace(true);
                Check(
                    m.EventId == null,
                    "Use the linked event check-in and corrections; meeting attendance is projected from it.",
                    409
                );
                await s.Person(i.PersonId, w.TenantId);
                Check(
                    new[]
                    {
                        "Expected",
                        "Present",
                        "Absent",
                        "Excused",
                        "Late",
                        "Online",
                        "Guest",
                    }.Contains(i.Status)
                        && i.Reason.Length <= 1000,
                    "Invalid attendance."
                );
                var row = await s.Db.MeetingAttendances.SingleOrDefaultAsync(x =>
                    x.MeetingId == id && x.PersonId == i.PersonId
                );
                if (row == null)
                {
                    row = new()
                    {
                        TenantId = m.TenantId,
                        OrgId = m.OrgId,
                        MeetingId = id,
                        PersonId = i.PersonId,
                    };
                    s.Db.MeetingAttendances.Add(row);
                }
                else
                    Check(i.Reason.Length >= 5, "Correction requires a reason.");
                s.Revision(row, "Attendance", Pack(row), w.UserId);
                row.Status = i.Status;
                row.Mode = i.Mode;
                row.Reason = i.Reason;
                row.ActorId = w.UserId;
                row.Version++;
                s.Audit(w, row, "attendance.recorded");
                await s.Db.SaveChangesAsync();
                return row;
            }
        );

        api.MapPost(
            "/meetings/{id:guid}/rsvp",
            async (Guid id, StateInput i, OperationsService s) =>
            {
                var m = await s.Meeting(id);
                var w = await s.Workspace();
                Check(
                    new[] { "Accepted", "Declined", "Tentative" }.Contains(i.Status),
                    "Invalid RSVP."
                );
                var p = await s.Db.Persons.SingleAsync(p => p.UserId == w.UserId);
                Check(
                    Unpack<Guid[]>(m.RequiredPeople).Contains(p.Id)
                        || Unpack<Guid[]>(m.OptionalPeople).Contains(p.Id),
                    "RSVP invitation required.",
                    403
                );
                var r = await s.Db.MeetingAttendances.SingleOrDefaultAsync(x =>
                    x.MeetingId == id && x.PersonId == p.Id
                );
                if (r == null)
                {
                    r = new()
                    {
                        TenantId = m.TenantId,
                        OrgId = m.OrgId,
                        MeetingId = id,
                        PersonId = p.Id,
                    };
                    s.Db.MeetingAttendances.Add(r);
                }
                r.Rsvp = i.Status;
                await s.Db.SaveChangesAsync();
                return r;
            }
        );

        api.MapGet(
            "/meetings/{id:guid}/calendar",
            async (Guid id, OperationsService s) =>
            {
                var m = await s.Meeting(id);
                string Clean(string t) =>
                    t.Replace("\\", "\\\\")
                        .Replace("\n", "\\n")
                        .Replace("\r", "")
                        .Replace(",", "\\,")
                        .Replace(";", "\\;");
                var text =
                    $"BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//ClubSphere//Meetings//EN\r\nBEGIN:VEVENT\r\nUID:{m.Id}@clubsphere\r\nDTSTAMP:{DateTimeOffset.UtcNow:yyyyMMdd'T'HHmmss'Z'}\r\nDTSTART:{m.StartsAt.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}\r\nDTEND:{m.EndsAt.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}\r\nSUMMARY:{Clean(m.Title)}\r\nLOCATION:{Clean(m.Location)}\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
                return Results.File(
                    System.Text.Encoding.UTF8.GetBytes(text),
                    "text/calendar",
                    "meeting.ics"
                );
            }
        );
    }

    static MeetingInput ResolveMeetingTimes(MeetingInput input)
    {
        Check(
            (input.LocalStartsAt == null) == (input.LocalEndsAt == null),
            "Provide both local start and end."
        );
        if (input.LocalStartsAt == null)
            return input;
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZone);
        }
        catch
        {
            throw new ApiError(400, "Invalid time zone.");
        }
        DateTimeOffset Convert(string text)
        {
            Check(
                DateTime.TryParseExact(
                    text,
                    "yyyy-MM-dd'T'HH:mm",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var local
                ),
                "Invalid local meeting time."
            );
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            Check(
                !zone.IsInvalidTime(local),
                "That local time does not exist during the daylight-saving transition. Choose a valid time."
            );
            var offset = zone.IsAmbiguousTime(local)
                ? zone.GetAmbiguousTimeOffsets(local).Min()
                : zone.GetUtcOffset(local);
            return new DateTimeOffset(local, offset).ToUniversalTime();
        }
        return input with
        {
            StartsAt = Convert(input.LocalStartsAt),
            EndsAt = Convert(input.LocalEndsAt!),
        };
    }

    static ClubMeeting NewMeeting(MeetingInput i, Assignment w) =>
        new()
        {
            TenantId = w.TenantId,
            OrgId = i.OrgId,
            Title = i.Title,
            OrganizerId = w.UserId,
            ChairId = i.ChairId,
            SecretaryId = i.SecretaryId,
            CommitteeId = i.CommitteeId,
            StartsAt = i.StartsAt.ToUniversalTime(),
            EndsAt = i.EndsAt.ToUniversalTime(),
            TimeZone = i.TimeZone,
            Type = i.Type,
            Location = i.Location,
            OnlineUrl = i.OnlineUrl,
            Visibility = i.Visibility,
            EventId = i.EventId,
            SessionId = i.SessionId,
            QuorumMinimum = i.QuorumMinimum,
            QuorumPercent = i.QuorumPercent,
            VotingCategories = Pack(i.VotingCategories ?? []),
            RequiredPeople = Pack(i.RequiredPeople ?? []),
            OptionalPeople = Pack(i.OptionalPeople ?? []),
        };

    static async Task ValidateMeeting(
        MeetingInput i,
        OperationsService s,
        Assignment w,
        EventService events
    )
    {
        OperationsService.Title(i.Title);
        OperationsService.Dates(i.StartsAt, i.EndsAt);
        Check(
            i.EndsAt - i.StartsAt <= TimeSpan.FromDays(7),
            "Meeting duration exceeds seven days."
        );
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(i.TimeZone);
        }
        catch
        {
            throw new ApiError(400, "Invalid time zone.");
        }
        Check(
            i.Visibility is "Members" or "Confidential"
                && i.QuorumMinimum >= 0
                && i.QuorumPercent is >= 0 and <= 100,
            "Invalid visibility or quorum."
        );
        Check(
            i.OnlineUrl == ""
                || Uri.TryCreate(i.OnlineUrl, UriKind.Absolute, out var uri)
                    && uri.Scheme == "https",
            "Online link must use HTTPS."
        );
        foreach (
            var p in (i.RequiredPeople ?? [])
                .Concat(i.OptionalPeople ?? [])
                .Concat(
                    new[] { i.ChairId, i.SecretaryId }.Where(x => x != null).Select(x => x!.Value)
                )
                .Distinct()
        )
        {
            await s.Person(p, w.TenantId);
        }
        if (i.CommitteeId != null)
        {
            var c = await s.Governance.CommitteeAccess(i.CommitteeId.Value);
            Check(c.OrgId == i.OrgId, "Committee must own this meeting.");
        }
        foreach (var category in i.VotingCategories ?? [])
            Check(
                await s.Db.MembershipCategories.AnyAsync(c =>
                    c.Id == category && c.TenantId == w.TenantId
                ),
                "Voting category unavailable."
            );
        if (i.EventId != null)
        {
            Check(
                await s.Db.Events.AnyAsync(e =>
                    e.Id == i.EventId && e.TenantId == w.TenantId && e.OrgId == i.OrgId
                ),
                "Event must share meeting ownership."
            );
            await events.Manage(await events.Find(i.EventId.Value));
        }
        if (i.SessionId != null)
            Check(
                await s.Db.EventSessions.AnyAsync(x =>
                    x.Id == i.SessionId && x.EventId == i.EventId
                ),
                "Session must belong to linked event."
            );
    }

    static async Task<object> MeetingAttendanceResult(
        ClubMeeting m,
        OperationsService s,
        bool roster = true
    )
    {
        IQueryable<Guid> present;
        int physicallyPresent;
        object rows;
        if (m.EventId != null)
        {
            var checkins = s.Db.EventCheckins.Where(x =>
                x.EventId == m.EventId
                && !x.Voided
                && (m.SessionId == null ? x.SessionId == null : x.SessionId == m.SessionId)
            );
            physicallyPresent = await checkins
                .Select(x => x.RegistrationId)
                .Distinct()
                .CountAsync();
            present = checkins
                .Join(
                    s.Db.EventRegistrations,
                    c => c.RegistrationId,
                    r => r.Id,
                    (c, r) => r.PersonId
                )
                .Where(p => p != null)
                .Select(p => p!.Value)
                .Distinct();
            rows = !roster
                ? Array.Empty<object>()
                : await checkins
                    .OrderBy(x => x.At)
                    .Take(1000)
                    .Select(x => new
                    {
                        x.Id,
                        x.RegistrationId,
                        x.At,
                        x.SessionId,
                        personName = s
                            .Db.EventRegistrations.Where(r => r.Id == x.RegistrationId)
                            .Select(r => r.Name)
                            .First(),
                    })
                    .ToListAsync();
        }
        else
        {
            var attendance = s.Db.MeetingAttendances.Where(x => x.MeetingId == m.Id);
            present = attendance
                .Where(x => new[] { "Present", "Late", "Online" }.Contains(x.Status))
                .Select(x => x.PersonId)
                .Distinct();
            physicallyPresent = await attendance.CountAsync(x =>
                new[] { "Present", "Late", "Online", "Guest" }.Contains(x.Status)
            );
            rows = await s.WithPeople(await attendance.OrderBy(x => x.Id).Take(1000).ToListAsync());
        }

        var cats = Unpack<Guid[]>(m.VotingCategories);
        var eligible = s.Db.Memberships.Where(x =>
            x.TenantId == m.TenantId
            && x.ClubId == m.OrgId
            && x.Status == "Active"
            && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)
            && (cats.Length == 0 || cats.Contains(x.CategoryId))
        );
        var denominator = await eligible.Select(x => x.PersonId).Distinct().CountAsync();
        var votingPresent = await eligible
            .Where(x => present.Contains(x.PersonId))
            .Select(x => x.PersonId)
            .Distinct()
            .CountAsync();
        var required = Math.Max(
            m.QuorumMinimum,
            (int)Math.Ceiling(denominator * m.QuorumPercent / 100)
        );
        return new
        {
            rows,
            present = physicallyPresent,
            eligible = denominator,
            votingPresent,
            required,
            met = votingPresent >= required,
            source = m.EventId == null ? "Meeting" : "Event",
            categories = cats,
        };
    }
}
