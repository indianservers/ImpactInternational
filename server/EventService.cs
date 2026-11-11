using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record EventInput(
    Guid OrgId,
    Guid TypeId,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset RegistrationStartsAt,
    DateTimeOffset RegistrationEndsAt,
    string TimeZone = "UTC",
    string Description = "",
    string VenueType = "Physical",
    string Venue = "",
    string OnlineUrl = "",
    string Contact = "",
    string Visibility = "Restricted",
    int Capacity = 100,
    bool Waitlist = true,
    bool AllowGuests = false,
    int MaxCompanions = 0,
    bool RegistrationApproval = false,
    string Fields = "[]",
    string Language = "en",
    Guid? CommitteeId = null,
    Guid[]? Cohosts = null,
    bool RsvpRequired = true,
    DateTimeOffset? RsvpDeadline = null,
    string WaitlistPolicy = "Allocate",
    int OfferMinutes = 30,
    string CancellationPolicy = "Cancel before registration closes.",
    DateTimeOffset? ReminderAt = null,
    bool Paid = false,
    long Version = 1,
    double? Latitude = null,
    double? Longitude = null
);

public record EventRegistrationInput(
    Guid[]? SessionIds = null,
    int Companions = 0,
    string[]? GuestNames = null,
    string Category = "General",
    string Responses = "{}",
    string Accessibility = "",
    string Dietary = "",
    string Emergency = "",
    string? Token = null
);

public record EventToken(string? Token = null);

public record EventRsvpInput(string Response, string? Token = null);

public record EventCheckinInput(
    string? Token = null,
    Guid? RegistrationId = null,
    Guid? SessionId = null,
    string Device = "Browser",
    string EntryPoint = "Main entrance"
);

public record EventReference(Guid EventId, Guid? RegistrationId = null);

public class EventService(
    Access a,
    GovernanceService governance,
    NotificationService notifications,
    IDataProtectionProvider protection
)
{
    public Access A => a;
    public Db Db => a.Db;
    public GovernanceService Governance => governance;
    private readonly IDataProtector protector = protection.CreateProtector("ClubSphere.events.v1");

    // Explicit projection includes protected delivery fields hidden from HTTP JSON.
    public Task<int> InsertInvitations(IEnumerable<EventInvitation> invitations)
    {
        var json = JsonSerializer.Serialize(
            invitations.Select(i => new
            {
                i.Id,
                i.TenantId,
                i.EventId,
                i.PersonId,
                i.InviterId,
                i.RecipientKey,
                i.GuestName,
                i.Email,
                i.TokenHash,
                i.ProtectedToken,
                i.Context,
                i.Message,
                i.Status,
                i.SendAt,
                i.SentAt,
                i.ExpiresAt,
                i.MailId,
                i.Rsvp,
                i.RespondedAt,
            })
        );
        return Db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "EventInvitations"
            SELECT x.* FROM jsonb_populate_recordset(NULL::"EventInvitations", {json}::jsonb) AS x
            ON CONFLICT ("EventId", "RecipientKey") DO NOTHING
            """
        );
    }

    public string Protect(string x) => protector.Protect(x);

    public string Unprotect(string x) => protector.Unprotect(x);

    public async Task<ClubEvent> Find(Guid id) =>
        await Db.Events.SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiError(404, "Event not found.");

    public async Task<Guid?> Me()
    {
        var u = await A.User();
        return await Db
            .Persons.Where(x => x.UserId == u.Id)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync();
    }

    public async Task<Assignment?> Manage(ClubEvent e, string capability = "Manage")
    {
        var session = await A.Session();
        var w = await Db.Assignments.SingleOrDefaultAsync(x =>
            x.Id == session.WorkspaceId && x.UserId == session.UserId
        );
        if (
            w != null
            && w.TenantId == e.TenantId
            && await A.Effective(w)
            && (await A.Grants(w)).Contains(
                capability == "Checkin" ? "events.checkin" : "events.manage"
            )
            && await A.Visible(w).AnyAsync(x => x.Id == e.OrgId)
        )
            return w;
        var now = DateTimeOffset.UtcNow;
        Check(
            await Db.EventStaff.AnyAsync(x =>
                x.EventId == e.Id
                && x.PersonId
                    == Db.Persons.Where(p => p.UserId == session.UserId)
                        .Select(p => p.Id)
                        .FirstOrDefault()
                && x.Active
                && x.StartsAt <= now
                && x.EndsAt > now
                && (x.Capability == "Manage" || x.Capability == capability)
            ),
            "Not authorized for this event operation.",
            403
        );
        return null;
    }

    public async Task<bool> Eligible(ClubEvent e, Guid person)
    {
        var now = DateTimeOffset.UtcNow;
        var rules = await Db.EventAudiences.Where(x => x.EventId == e.Id).ToListAsync();
        if (e.Visibility == "Public")
            return true;
        if (
            await Db.EventInvitations.AnyAsync(x =>
                x.EventId == e.Id && x.PersonId == person && x.ExpiresAt > now
            )
        )
            return true;
        if (rules.Count == 0)
            return await Db.Memberships.AnyAsync(x =>
                x.TenantId == e.TenantId
                && x.PersonId == person
                && x.ClubId == e.OrgId
                && x.Status == "Active"
                && (x.EndsAt == null || x.EndsAt > now)
            );
        foreach (var rule in rules)
            if (await AudiencePeople(e, rule).AnyAsync(x => x == person))
                return true;
        return false;
    }

    public IQueryable<Guid> EligibleEventIds(Guid person)
    {
        // Only a canonical typed GUID is interpolated; no caller text enters the SQL.
        var sql = $"""
            WITH RECURSIVE affiliations AS (
             SELECT m."Id" membership, m."TenantId",m."ClubId" org,m."CategoryId",m."PersonId" FROM "Memberships" m WHERE m."PersonId"='{person:D}'::uuid AND m."Status"='Active' AND (m."EndsAt" IS NULL OR m."EndsAt">clock_timestamp())
            ), ancestors AS (
             SELECT a.membership,a."TenantId",a.org,a."CategoryId",a."PersonId" FROM affiliations a
             UNION ALL SELECT a.membership,a."TenantId",o."ParentId",a."CategoryId",a."PersonId" FROM ancestors a JOIN "Orgs" o ON o."Id"=a.org AND o."TenantId"=a."TenantId" WHERE o."ParentId" IS NOT NULL
            ) SELECT DISTINCT e."Id" AS "Value" FROM "Events" e WHERE e."Publication"='Published' AND (
             NOT EXISTS(SELECT 1 FROM "EventAudiences" r WHERE r."EventId"=e."Id") AND EXISTS(SELECT 1 FROM affiliations a WHERE a."TenantId"=e."TenantId" AND a.org=e."OrgId")
             OR EXISTS(SELECT 1 FROM "EventAudiences" r JOIN affiliations m ON m."TenantId"=r."TenantId" WHERE r."EventId"=e."Id"
              AND (r."PersonId" IS NULL OR r."PersonId"=m."PersonId") AND (r."CategoryId" IS NULL OR r."CategoryId"=m."CategoryId")
              AND (r."OrgId" IS NULL OR EXISTS(SELECT 1 FROM ancestors a WHERE a.membership=m.membership AND a.org=r."OrgId"))
              AND (r."OrgId" IS NOT NULL OR r."CommitteeId" IS NOT NULL OR r."PersonId" IS NOT NULL OR m.org=e."OrgId")
              AND (r."CommitteeId" IS NULL OR EXISTS(SELECT 1 FROM "CommitteeMemberships" c WHERE c."CommitteeId"=r."CommitteeId" AND c."PersonId"=m."PersonId" AND c."Status"='Active' AND c."StartsAt"<=clock_timestamp() AND c."EndsAt">clock_timestamp()))
              AND (r."Country"='' OR EXISTS(SELECT 1 FROM "Orgs" p WHERE p."Id"=m.org AND p."Country"=r."Country"))
              AND (r."City"='' OR EXISTS(SELECT 1 FROM "Orgs" p WHERE p."Id"=m.org AND p."City"=r."City"))
              AND (r."RoleId" IS NULL OR EXISTS(SELECT 1 FROM "Assignments" a JOIN "Persons" p ON p."UserId"=a."UserId" WHERE p."Id"=m."PersonId" AND a."RoleId"=r."RoleId" AND a."TenantId"=e."TenantId" AND (a."MembershipId" IS NULL OR a."MembershipId"=m.membership) AND (a."AppointmentId" IS NULL OR EXISTS(SELECT 1 FROM "OfficerAppointments" o WHERE o."Id"=a."AppointmentId" AND o."Status"='Active' AND o."StartsAt"<=clock_timestamp() AND o."EffectiveAt"<=clock_timestamp() AND o."EndsAt">clock_timestamp()))))
              AND (r."RegistrationStatus"='' OR EXISTS(SELECT 1 FROM "EventRegistrations" v WHERE v."EventId"=e."Id" AND v."PersonId"=m."PersonId" AND v."Status"=r."RegistrationStatus"))
             ))
            """;
        return Db.Database.SqlQueryRaw<Guid>(sql);
    }

    public async Task View(ClubEvent e, string? token = null)
    {
        if (token != null)
        {
            var i = await Invitation(token);
            Check(i.EventId == e.Id, "Wrong event.", 404);
            return;
        }
        if (
            e.Publication == "Published"
            && e.Lifecycle != "Cancelled"
            && (e.Visibility == "Public" || await Me() is Guid p && await Eligible(e, p))
        )
            return;
        var user = await A.User();
        if (
            await Db.EventStaff.AnyAsync(t =>
                t.EventId == e.Id
                && t.Active
                && t.EndsAt > DateTimeOffset.UtcNow
                && Db.Persons.Any(p => p.Id == t.PersonId && p.UserId == user.Id)
            )
        )
            return;
        await Manage(e);
    }

    public IQueryable<Guid> AudiencePeople(ClubEvent e, EventAudience r)
    {
        var now = DateTimeOffset.UtcNow;
        var q = Db.Memberships.Where(m =>
            m.TenantId == e.TenantId && m.Status == r.Status && (m.EndsAt == null || m.EndsAt > now)
        );
        if (r.OrgId != null)
        {
            var ids = A.Tree(e.TenantId, r.OrgId.Value).Select(o => o.Id);
            q = q.Where(m => ids.Contains(m.ClubId));
        }
        else if (r.CommitteeId == null && r.PersonId == null)
            q = q.Where(m => m.ClubId == e.OrgId);
        if (r.CommitteeId != null)
            q = q.Where(m =>
                Db.CommitteeMemberships.Any(c =>
                    c.CommitteeId == r.CommitteeId
                    && c.PersonId == m.PersonId
                    && c.Status == "Active"
                    && c.StartsAt <= now
                    && c.EndsAt > now
                )
            );
        if (r.PersonId != null)
            q = q.Where(m => m.PersonId == r.PersonId);
        if (r.CategoryId != null)
            q = q.Where(m => m.CategoryId == r.CategoryId);
        if (r.RoleId != null)
            q = q.Where(m =>
                Db.Assignments.Any(x =>
                    x.RoleId == r.RoleId
                    && x.UserId == m.Person.UserId
                    && (x.MembershipId == null || x.MembershipId == m.Id)
                    && (
                        x.AppointmentId == null
                        || GovernanceService.Current(Db).Any(p => p.Id == x.AppointmentId)
                    )
                )
            );
        if (r.City != "")
            q = q.Where(m => Db.Orgs.Any(o => o.Id == m.ClubId && o.City == r.City));
        if (r.Country != "")
            q = q.Where(m => Db.Orgs.Any(o => o.Id == m.ClubId && o.Country == r.Country));
        if (r.RegistrationStatus != "")
            q = q.Where(m =>
                Db.EventRegistrations.Any(x =>
                    x.EventId == e.Id
                    && x.PersonId == m.PersonId
                    && x.Status == r.RegistrationStatus
                )
            );
        return q.Select(m => m.PersonId).Distinct();
    }

    public async Task<IQueryable<Guid>> Audience(ClubEvent e)
    {
        var rules = await Db.EventAudiences.Where(x => x.EventId == e.Id).ToListAsync();
        IQueryable<Guid>? q = null;
        if (rules.Count == 0)
            rules.Add(new() { OrgId = e.OrgId });
        foreach (var r in rules)
            q = q == null ? AudiencePeople(e, r) : q.Union(AudiencePeople(e, r));
        return q!.Distinct();
    }

    public async Task<(Guid? PersonId, string Key)> RecipientIdentity(string email)
    {
        var person = await Db
            .Persons.Where(p =>
                p.UserId != null
                && Db.Users.Any(u => u.Id == p.UserId && u.Email == email && u.Verified)
            )
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync();
        return (person, person != null ? "person:" + person : "guest:" + Hash(email));
    }

    public async Task<EventInvitation> Invitation(string? token)
    {
        Check(token != null && token.Length == 64, "Invalid invitation.", 404);
        var hash = Hash(token!);
        var i = await Db.EventInvitations.SingleOrDefaultAsync(x => x.TokenHash == hash);
        Check(
            i != null && i.ExpiresAt > DateTimeOffset.UtcNow,
            "Invitation invalid or expired.",
            410
        );
        return i!;
    }

    public async Task Lock(Guid id)
    {
        await Db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Events\" WHERE \"Id\"={id} FOR NO KEY UPDATE"
        );
        string actor = "";
        try
        {
            actor = (await A.User()).Id.ToString();
        }
        catch (ApiError ex) when (ex.Status == 401) { }
        await Db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('clubs.actor',{actor},true)"
        );
    }

    public static string Window(ClubEvent e)
    {
        var n = DateTimeOffset.UtcNow;
        return e.Lifecycle is "Cancelled" or "Completed" or "Archived"
            || !e.RegistrationEnabled
            || e.Publication != "Published"
                ? "Closed"
            : n < e.RegistrationStartsAt ? "Upcoming"
            : n <= e.RegistrationEndsAt ? "Open"
            : "Closed";
    }

    public async Task RsvpNotify(Guid person, ClubEvent e, string text)
    {
        var uid = await Db.Persons.Where(p => p.Id == person).Select(p => p.UserId).SingleAsync();
        await notifications.User(
            uid,
            "event.update",
            e.Title + ": " + text,
            "/app/events/" + e.Id,
            e.TenantId,
            e.OrgId,
            e.Id
        );
    }

    public async Task Notify(ClubEvent e, EventRegistration r, string detail)
    {
        var link = "/app/my-events?registration=" + r.Id;
        if (r.PersonId != null)
        {
            var uid = await Db
                .Persons.Where(x => x.Id == r.PersonId)
                .Select(x => x.UserId)
                .SingleAsync();
            await notifications.User(
                uid,
                "event.update",
                e.Title + ": " + detail,
                link,
                e.TenantId,
                e.OrgId,
                e.Id
            );
        }
        else if (r.Email != "")
            await notifications.Recipient(
                r.Email,
                "event.update",
                e.Title + ": " + detail,
                "/event-invitation?token="
                    + Unprotect(
                        (
                            await Db.EventInvitations.SingleAsync(x => x.Id == r.InvitationId)
                        ).ProtectedToken
                    ),
                e.TenantId,
                e.OrgId,
                e.Id
            );
    }

    public async Task<EventTicket> Issue(ClubEvent e, EventRegistration r)
    {
        var current = await Db.EventTickets.SingleOrDefaultAsync(x =>
            x.RegistrationId == r.Id && !x.Revoked
        );
        if (current != null)
            return current;
        var token = Token();
        var t = new EventTicket
        {
            TenantId = e.TenantId,
            EventId = e.Id,
            RegistrationId = r.Id,
            TokenHash = Hash(token),
            ProtectedToken = Protect(token),
        };
        Db.EventTickets.Add(t);
        return t;
    }

    public async Task Confirm(ClubEvent e, EventRegistration r)
    {
        Check(!e.Paid, "Paid booking is disabled.", 409);
        var used =
            await Db
                .EventRegistrations.Where(x =>
                    x.EventId == e.Id
                    && x.Id != r.Id
                    && (x.Status == "Confirmed" || x.Status == "Offered")
                )
                .SumAsync(x => (int?)(1 + x.Companions))
            ?? 0;
        if (used + 1 + r.Companions > e.Capacity)
        {
            Check(e.Waitlist, "Event is at capacity.", 409);
            r.Status = "Waitlisted";
            await Notify(e, r, "You are waitlisted; no seat is reserved.");
        }
        else
        {
            r.Status = "Confirmed";
            r.OfferExpiresAt = null;
            await Issue(e, r);
            await Notify(e, r, "Registration confirmed. Your digital pass is ready.");
        }
        r.Version++;
    }

    public async Task Promote(ClubEvent e)
    {
        if (e.Lifecycle == "Cancelled" || e.EndsAt <= DateTimeOffset.UtcNow || Window(e) != "Open")
            return;
        var used =
            await Db
                .EventRegistrations.Where(x =>
                    x.EventId == e.Id && (x.Status == "Confirmed" || x.Status == "Offered")
                )
                .SumAsync(x => (int?)(1 + x.Companions))
            ?? 0;
        var waiting = await Db
            .EventRegistrations.Where(x => x.EventId == e.Id && x.Status == "Waitlisted")
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Take(100)
            .ToListAsync();
        foreach (var r in waiting)
        {
            if (used + 1 + r.Companions > e.Capacity)
                break;
            used += 1 + r.Companions;
            if (e.WaitlistPolicy == "Offer")
            {
                r.Status = "Offered";
                r.OfferExpiresAt = DateTimeOffset.UtcNow.AddMinutes(e.OfferMinutes);
                await Notify(e, r, "A seat is offered. Accept before " + r.OfferExpiresAt);
            }
            else
            {
                r.Status = "Confirmed";
                await Issue(e, r);
                await Notify(e, r, "A waitlist seat has been allocated. Your pass is ready.");
            }
            r.Version++;
        }
    }

    public async Task<EventRegistration> Own(ClubEvent e, Guid id, string? token = null)
    {
        var r = await Db.EventRegistrations.SingleOrDefaultAsync(x =>
            x.Id == id && x.EventId == e.Id
        );
        Check(r != null, "Registration not found.", 404);
        if (token != null)
        {
            var i = await Invitation(token);
            Check(i.Id == r!.InvitationId && i.EventId == e.Id, "Not your registration.", 403);
        }
        else
        {
            var p = await Me();
            Check(p != null && r!.PersonId == p, "Not your registration.", 403);
        }
        return r!;
    }

    public async Task<EventRegistration> Register(ClubEvent e, EventRegistrationInput input)
    {
        Check(Window(e) == "Open" && e.RegistrationEnabled, "Registration is closed.", 409);
        Check(!e.Paid, "Paid booking is not available.", 409);
        EventInvitation? invite = null;
        Guid? person = null;
        string name,
            email = "",
            key;
        if (input.Token != null)
        {
            invite = await Invitation(input.Token);
            Check(invite.EventId == e.Id, "Wrong event.", 403);
            person = invite.PersonId;
            if (person == null)
            {
                Check(e.AllowGuests, "Guests are not permitted.", 403);
                name = invite.GuestName;
                email = invite.Email;
                key = invite.RecipientKey;
            }
            else
            {
                name = await Db
                    .Persons.Where(x => x.Id == person)
                    .Select(x => x.DisplayName)
                    .SingleAsync();
                key = "person:" + person;
            }
        }
        else
        {
            person = await Me();
            Check(
                person != null && await Eligible(e, person.Value),
                "Not eligible for this event.",
                403
            );
            name = await Db
                .Persons.Where(x => x.Id == person)
                .Select(x => x.DisplayName)
                .SingleAsync();
            key = "person:" + person;
            invite = await Db.EventInvitations.SingleOrDefaultAsync(x =>
                x.EventId == e.Id && x.PersonId == person
            );
        }
        Check(
            input.Companions >= 0
                && input.Companions <= e.MaxCompanions
                && (input.Companions == 0 || e.AllowGuests)
                && (input.GuestNames?.Length ?? 0) == input.Companions,
            "Provide the permitted companion names."
        );
        Check(
            (input.GuestNames ?? []).All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 120),
            "Provide valid companion names."
        );
        Check(
            input.Category.Length <= 80
                && input.Accessibility.Length <= 1000
                && input.Dietary.Length <= 500
                && input.Emergency.Length <= 500,
            "Registration details too long."
        );
        ValidateAnswers(e.Fields, input.Responses);
        var existing = await Db.EventRegistrations.SingleOrDefaultAsync(x =>
            x.EventId == e.Id && x.ParticipantKey == key
        );
        Check(existing == null, "You already have a registration; use its existing actions.", 409);
        var r = new EventRegistration
        {
            TenantId = e.TenantId,
            EventId = e.Id,
            PersonId = person,
            InvitationId = invite?.Id,
            ParticipantKey = key,
            Name = name,
            Email = email,
            Companions = input.Companions,
            GuestNames = JsonSerializer.Serialize(input.GuestNames ?? []),
            Category = person == null && input.Category == "General" ? "Guest" : input.Category,
            Responses = input.Responses,
            Accessibility = input.Accessibility,
            Dietary = input.Dietary,
            Emergency = input.Emergency,
        };
        if (person != null)
            r.MembershipId = await Db
                .Memberships.Where(x =>
                    x.PersonId == person
                    && x.TenantId == e.TenantId
                    && x.Status == "Active"
                    && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)
                )
                .OrderByDescending(x => x.ClubId == e.OrgId)
                .ThenBy(x => x.ClubId)
                .ThenBy(x => x.Id)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();
        if (r.MembershipId == null)
        {
            Check(e.AllowGuests, "This event is restricted to active association members.", 403);
            if (input.Category == "General")
                r.Category = "Guest";
        }
        Db.EventRegistrations.Add(r);
        await Db.SaveChangesAsync();
        if (e.RegistrationApproval)
        {
            r.Status = "Awaiting Approval";
            var actor = new Assignment
            {
                TenantId = e.TenantId,
                OrgId = e.OrgId,
                UserId =
                    person == null
                        ? e.CreatorId
                        : (
                            (
                                await Db
                                    .Persons.Where(x => x.Id == person)
                                    .Select(x => x.UserId)
                                    .SingleAsync()
                            ) ?? e.CreatorId
                        ),
            };
            var request = await governance.Request(
                actor,
                e.OrgId,
                "EventRegistration",
                "Registration: " + e.Title + " / " + name,
                GovernanceService.Pack(new EventReference(e.Id, r.Id)),
                subject: person == null
                    ? null
                    : await Db
                        .Persons.Where(x => x.Id == person)
                        .Select(x => x.UserId)
                        .SingleAsync()
            );
            r.ApprovalRequestId = request.Id;
            var wf = await Db.MembershipWorkflows.SingleAsync(x =>
                x.TenantId == e.TenantId && x.Kind == "Governance" && x.Active
            );
            request.WorkflowId = wf.Id;
            request.CurrentSequence = 1;
            request.Status = "Submitted";
            request.Version++;
            await Notify(e, r, "Registration awaits independent approval; no seat reserved.");
        }
        else
            await Confirm(e, r);
        return r;
    }

    public static void ValidateFields(string fields)
    {
        Check(fields.Length <= 12000, "Form too large.");
        var list = JsonSerializer.Deserialize<List<EventQuestion>>(fields, JsonOptions) ?? [];
        Check(
            list.Count <= 20 && list.Select(x => x.Key).Distinct().Count() == list.Count,
            "Invalid form fields."
        );
        foreach (var f in list)
            Check(
                f.Key != null
                    && f.Label != null
                    && f.Key.Length is > 0 and <= 40
                    && f.Label.Length is > 0 and <= 200
                    && new[]
                    {
                        "Rating",
                        "Single choice",
                        "Multiple choice",
                        "Short text",
                        "Long text",
                    }.Contains(f.Type)
                    && (f.Options?.Length ?? 0) <= 30,
                "Invalid question."
            );
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static void ValidateAnswers(string fields, string answers)
    {
        ValidateFields(fields);
        Check(answers.Length <= 16000, "Responses too large.");
        try
        {
            var f = JsonSerializer.Deserialize<List<EventQuestion>>(fields, JsonOptions) ?? [];
            var a = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(answers) ?? [];
            Check(a.Keys.All(k => f.Any(x => x.Key == k)), "Unknown form response.");
            foreach (var q in f)
            {
                var has =
                    a.TryGetValue(q.Key, out var v)
                    && v.ValueKind != JsonValueKind.Null
                    && v.ToString().Length > 0;
                Check(!q.Required || has, "Answer " + q.Label + ".");
                if (!has)
                    continue;
                if (q.Type == "Rating")
                    Check(v.TryGetInt32(out var n) && n >= 1 && n <= 5, "Rating must be 1–5.");
                else if (q.Type == "Multiple choice")
                    Check(
                        v.ValueKind == JsonValueKind.Array
                            && v.EnumerateArray()
                                .All(x => (q.Options ?? []).Contains(x.GetString() ?? "")),
                        "Invalid choices."
                    );
                else
                {
                    Check(
                        v.ValueKind == JsonValueKind.String
                            && v.ToString().Length <= (q.Type == "Long text" ? 4000 : 500),
                        "Invalid answer."
                    );
                    if (q.Type == "Single choice")
                        Check((q.Options ?? []).Contains(v.GetString() ?? ""), "Invalid choice.");
                }
            }
        }
        catch (JsonException)
        {
            throw new ApiError(400, "Invalid form responses.");
        }
    }

    public void QueueNotice(ClubEvent e, string message, string audience = "Registrations")
    {
        Db.EventNotices.Add(
            new()
            {
                TenantId = e.TenantId,
                EventId = e.Id,
                Message = message,
                Audience = audience,
            }
        );
    }

    public async Task Audit(ClubEvent e, string action, Guid actor, string detail = "")
    {
        Db.Audits.Add(
            new()
            {
                TenantId = e.TenantId,
                OrgId = e.OrgId,
                ActorId = actor,
                Action = "events." + action,
                Resource = e.Id.ToString(),
                Changes = detail,
            }
        );
        await Task.CompletedTask;
    }
}

public record EventQuestion(
    string Key,
    string Label,
    string Type,
    bool Required = false,
    string[]? Options = null
);
