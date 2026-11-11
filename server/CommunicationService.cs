using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record AudienceRule(
    Guid? OrgId = null,
    Guid? CategoryId = null,
    string Status = "Active",
    string Country = "",
    string Language = "",
    DateTimeOffset? JoinedAfter = null,
    DateTimeOffset? JoinedBefore = null,
    string Renewal = "",
    Guid? PositionId = null,
    Guid? CommitteeId = null,
    Guid? EventId = null,
    string Rsvp = "",
    bool? Attended = null,
    AudienceRule[]? All = null,
    AudienceRule[]? Any = null
);

public record BroadcastReference(Guid Id, long Version, string Hash);

public class CommunicationService(
    Access a,
    GovernanceService governance,
    IDataProtectionProvider keys
)
{
    public Access A => a;
    public Db Db => a.Db;
    public GovernanceService Governance => governance;
    public static readonly string[] Channels = ["InApp", "Email", "WhatsApp", "SMS"];
    public static readonly string[] Permissions =
    [
        "communications.view",
        "communications.send",
        "communications.manage",
        "communications.inbox",
        "communications.reports",
        "communications.configure",
    ];
    private readonly IDataProtector protector = keys.CreateProtector(
        "ClubSphere.communication.accounts.v1"
    );

    public string Protect(string s) => protector.Protect(s);

    public string Unprotect(string s) => protector.Unprotect(s);

    public static readonly string[] Variables =
    [
        "member_name",
        "club_name",
        "member_id",
        "organization_name",
        "event_name",
        "event_date",
        "event_location",
        "rsvp_link",
        "officer_name",
    ];

    public static void Content(string body, string subject = "")
    {
        Check(
            body.Length is > 0 and <= 12000 && subject.Length <= 200,
            "Message length is invalid."
        );
        Check(
            !Regex.IsMatch(body + subject, "<[^>]+>"),
            "Use plain text. HTML and scripts are not accepted."
        );
        foreach (Match m in Regex.Matches(body + subject, @"\{\{(.*?)\}\}"))
            Check(
                Variables.Contains(m.Groups[1].Value),
                "Unknown template variable: " + m.Groups[1].Value
            );
    }

    public void Revision(
        ITenantRecord resource,
        long version,
        string kind,
        Guid actor,
        Dictionary<string, string>? context = null
    )
    {
        var node = JsonSerializer.SerializeToNode(resource, resource.GetType())!;
        if (context != null)
            node["ResolvedContext"] = JsonSerializer.SerializeToNode(context);
        Db.CommunicationRevisions.Add(
            new()
            {
                TenantId = resource.TenantId,
                ResourceId = resource.Id,
                Version = version,
                Kind = kind,
                ActorId = actor,
                Snapshot = node.ToJsonString(),
            }
        );
    }

    public async Task<Dictionary<string, string>> Context(CommunicationBroadcast b)
    {
        var org = await Db.Orgs.SingleAsync(o => o.Id == b.OrgId);
        var officer = await Db
            .Users.Where(u => u.Id == b.CreatorId)
            .Select(u => u.Name)
            .SingleAsync();
        var ev = b.EventId == null ? null : await Db.Events.SingleAsync(e => e.Id == b.EventId);
        return new()
        {
            { "organization_name", org.Name },
            { "officer_name", officer },
            { "event_name", ev?.Title ?? "" },
            {
                "event_date",
                ev == null
                    ? ""
                    : TimeZoneInfo
                        .ConvertTime(ev.StartsAt, TimeZoneInfo.FindSystemTimeZoneById(ev.TimeZone))
                        .ToString("yyyy-MM-dd HH:mm")
                        + " "
                        + ev.TimeZone
            },
            { "event_location", ev?.Venue ?? "" },
            { "rsvp_link", ev == null ? "" : "/app/events/" + ev.Id },
        };
    }

    public static string RenderFrozen(
        string text,
        CommunicationRecipient r,
        Dictionary<string, string> context
    )
    {
        var result = text.Replace("{{member_name}}", r.Name)
            .Replace("{{club_name}}", r.Club)
            .Replace("{{member_id}}", r.MemberNumber);
        foreach (var pair in context)
            result = result.Replace("{{" + pair.Key + "}}", pair.Value);
        return result;
    }

    public void Audit(Assignment w, Guid org, string action, Guid id, string detail = "") =>
        Db.Audits.Add(
            new()
            {
                TenantId = w.TenantId,
                OrgId = org,
                ActorId = w.UserId,
                Action = "communications." + action,
                Resource = id.ToString(),
                Changes = detail,
            }
        );

    public static Assignment Within(Assignment w, Guid org) =>
        new()
        {
            Id = w.Id,
            TenantId = w.TenantId,
            OrgId = org,
            RoleId = w.RoleId,
            UserId = w.UserId,
            MembershipId = w.MembershipId,
            AppointmentId = w.AppointmentId,
        };

    public async Task Scope(Assignment w, Guid org) =>
        Check(
            await A.Visible(w).AnyAsync(x => x.Id == org && x.Status == "Active"),
            "Organization outside authorized scope.",
            404
        );

    public async Task<Guid?> PersonId(Guid user) =>
        await Db
            .Persons.Where(p => p.UserId == user)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync();

    public async Task<bool> TenantMember(Guid tenant, Guid user) =>
        await Db.Memberships.AnyAsync(m =>
            m.TenantId == tenant
            && m.Person.UserId == user
            && m.Status == "Active"
            && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
        );

    public async Task<ConversationParticipant> Participant(Conversation c)
    {
        var u = await A.User();
        var workspace = await A.Workspace("communications.view");
        Check(workspace.TenantId == c.TenantId, "Conversation is outside this association.", 404);
        var p = await Db.ConversationParticipants.SingleOrDefaultAsync(x =>
            x.ConversationId == c.Id && x.UserId == u.Id && x.Active
        );
        Check(p != null, "Conversation is private.", 404);
        if (p.Staff)
            Check(
                await Staff(c.TenantId, c.OrgId, u.Id),
                "Conversation staff access expired.",
                403
            );
        else
            Check(
                await Db.Memberships.AnyAsync(m =>
                    m.TenantId == c.TenantId
                    && m.Person.UserId == u.Id
                    && m.Status == "Active"
                    && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                    && A.Tree(c.TenantId, c.OrgId).Select(o => o.Id).Contains(m.ClubId)
                ),
                "Conversation membership expired.",
                403
            );
        if (!p.Staff && c.Kind == "Committee")
            Check(
                await Db.CommitteeMemberships.AnyAsync(m =>
                    m.CommitteeId == c.CommitteeId
                    && m.PersonId
                        == Db.Persons.Where(x => x.UserId == u.Id)
                            .Select(x => x.Id)
                            .FirstOrDefault()
                    && m.Status == "Active"
                    && m.StartsAt <= DateTimeOffset.UtcNow
                    && m.EndsAt > DateTimeOffset.UtcNow
                ),
                "Committee participation expired.",
                403
            );
        if (!p.Staff && c.Kind == "Event")
            Check(
                await Db.EventRegistrations.AnyAsync(r =>
                    r.EventId == c.EventId
                    && r.Status == "Confirmed"
                    && r.PersonId
                        == Db.Persons.Where(x => x.UserId == u.Id)
                            .Select(x => x.Id)
                            .FirstOrDefault()
                ),
                "Event participation ended.",
                403
            );
        return p;
    }

    public async Task<bool> Staff(Guid tenant, Guid org, Guid user)
    {
        var assignments = await Db
            .Assignments.Where(x =>
                x.UserId == user
                && x.TenantId == tenant
                && Db.RolePermissions.Any(p =>
                    p.RoleId == x.RoleId && p.PermissionId == "communications.inbox"
                )
            )
            .ToListAsync();
        foreach (var w in assignments)
            if (
                await A.Effective(w)
                && await A.Visible(w).AnyAsync(o => o.Id == org && o.Status == "Active")
            )
                return true;
        return false;
    }

    public async Task<Conversation> Conversation(Guid id)
    {
        var c = await Db.Conversations.SingleOrDefaultAsync(x => x.Id == id);
        Check(c != null, "Conversation not found.", 404);
        await Participant(c);
        return c;
    }

    public IQueryable<Membership> Members(Assignment w) =>
        Db.Memberships.Where(m =>
            m.TenantId == w.TenantId && A.Visible(w).Select(o => o.Id).Contains(m.ClubId)
        );

    public async Task<IQueryable<Guid>> Audience(Assignment w, AudienceRule rule, int depth = 0)
    {
        Check(
            depth <= 4 && (rule.All?.Length ?? 0) <= 8 && (rule.Any?.Length ?? 0) <= 8,
            "Audience expression too complex."
        );
        var org = rule.OrgId ?? w.OrgId;
        await Scope(w, org);
        Check(
            new[] { "Active", "Expired", "Suspended", "Resigned", "All" }.Contains(rule.Status),
            "Invalid membership status."
        );
        var m = Db.Memberships.Where(x =>
            x.TenantId == w.TenantId && A.Tree(w.TenantId, org).Select(o => o.Id).Contains(x.ClubId)
        );
        if (rule.Status != "All")
            m = m.Where(x => x.Status == rule.Status);
        if (rule.Status == "Active")
            m = m.Where(x => x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow);
        if (rule.CategoryId != null)
            m = m.Where(x => x.CategoryId == rule.CategoryId);
        if (rule.Country != "")
            m = m.Where(x => Db.Orgs.Any(o => o.Id == x.ClubId && o.Country == rule.Country));
        if (rule.Language != "")
            m = m.Where(x =>
                Db.Users.Any(u => u.Id == x.Person.UserId && u.Locale == rule.Language)
            );
        if (rule.JoinedAfter != null)
            m = m.Where(x => x.JoinedAt >= rule.JoinedAfter);
        if (rule.JoinedBefore != null)
            m = m.Where(x => x.JoinedAt < rule.JoinedBefore);
        if (rule.Renewal == "Due")
            m = m.Where(x =>
                x.EndsAt > DateTimeOffset.UtcNow && x.EndsAt < DateTimeOffset.UtcNow.AddDays(30)
            );
        if (rule.PositionId != null)
            m = m.Where(x =>
                GovernanceService
                    .Current(Db)
                    .Any(o =>
                        o.PersonId == x.PersonId
                        && o.TenantId == w.TenantId
                        && o.PositionId == rule.PositionId
                    )
            );
        if (rule.CommitteeId != null)
        {
            Check(
                await Db.Committees.AnyAsync(c =>
                    c.Id == rule.CommitteeId
                    && c.TenantId == w.TenantId
                    && A.Visible(w).Select(o => o.Id).Contains(c.OrgId)
                ),
                "Committee outside scope.",
                404
            );
            m = m.Where(x =>
                Db.CommitteeMemberships.Any(c =>
                    c.CommitteeId == rule.CommitteeId
                    && c.PersonId == x.PersonId
                    && c.Status == "Active"
                    && c.StartsAt <= DateTimeOffset.UtcNow
                    && c.EndsAt > DateTimeOffset.UtcNow
                )
            );
        }
        if (rule.EventId != null)
        {
            Check(
                await Db.Events.AnyAsync(e =>
                    e.Id == rule.EventId
                    && e.TenantId == w.TenantId
                    && A.Visible(w).Select(o => o.Id).Contains(e.OrgId)
                ),
                "Event outside scope.",
                404
            );
            if (rule.Rsvp != "")
                m = m.Where(x =>
                    Db.EventInvitations.Any(i =>
                        i.EventId == rule.EventId && i.PersonId == x.PersonId && i.Rsvp == rule.Rsvp
                    )
                );
            if (rule.Attended != null)
                m = m.Where(x =>
                    Db.EventRegistrations.Any(r =>
                        r.EventId == rule.EventId
                        && r.PersonId == x.PersonId
                        && Db.EventCheckins.Any(c =>
                            c.RegistrationId == r.Id && c.SessionId == null && !c.Voided
                        )
                    ) == rule.Attended
                );
        }
        IQueryable<Guid> ids = m.Select(x => x.PersonId).Distinct();
        foreach (var child in rule.All ?? [])
            ids = ids.Intersect(await Audience(w, child, depth + 1));
        if (rule.Any?.Length > 0)
        {
            IQueryable<Guid>? union = null;
            foreach (var child in rule.Any)
            {
                var q = await Audience(w, child, depth + 1);
                union = union == null ? q : union.Union(q);
            }
            ids = ids.Intersect(union!);
        }
        return ids;
    }

    public static AudienceRule Parse(string json)
    {
        try
        {
            Check(json.Length <= 16000, "Audience is too large.");
            var rule =
                JsonSerializer.Deserialize<AudienceRule>(json, GovernanceService.Json) ?? new();
            var count = 0;
            void Validate(AudienceRule node, int depth)
            {
                Check(++count <= 32 && depth <= 4, "Audience expression too complex.");
                foreach (var child in (node.All ?? []).Concat(node.Any ?? []))
                    Validate(child, depth + 1);
            }
            Validate(rule, 0);
            return rule;
        }
        catch (JsonException)
        {
            throw new ApiError(400, "Invalid audience rules.");
        }
    }

    public async Task<CommunicationBroadcast> Broadcast(
        Guid id,
        string permission = "communications.manage"
    )
    {
        var w = await A.Workspace(permission);
        var b = await Db.CommunicationBroadcasts.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == w.TenantId
        );
        Check(b != null, "Broadcast not found.", 404);
        await Scope(w, b.OrgId);
        return b;
    }

    public async Task Freeze(CommunicationBroadcast b, Assignment w)
    {
        var ids = await Audience(Within(w, b.OrgId), Parse(b.Rules));
        Guid? cursor = null;
        while (true)
        {
            var q = Db.Persons.Where(p => ids.Contains(p.Id));
            if (cursor != null)
                q = q.Where(p => p.Id.CompareTo(cursor.Value) > 0);
            var page = await q.OrderBy(p => p.Id)
                .Take(500)
                .Select(p => new
                {
                    p.Id,
                    p.UserId,
                    p.DisplayName,
                })
                .ToListAsync();
            if (page.Count == 0)
                break;
            var pids = page.Select(p => p.Id).ToArray();
            var affiliations = await Members(w)
                .Where(m => pids.Contains(m.PersonId))
                .OrderBy(m => m.ClubId)
                .ThenBy(m => m.Id)
                .Select(m => new
                {
                    m.PersonId,
                    m.Number,
                    Club = Db.Orgs.Where(o => o.Id == m.ClubId).Select(o => o.Name).First(),
                })
                .ToListAsync();
            var json = JsonSerializer.Serialize(
                page.Select(p =>
                {
                    var m = affiliations.First(x => x.PersonId == p.Id);
                    return new
                    {
                        Id = Guid.NewGuid(),
                        TenantId = b.TenantId,
                        BroadcastId = b.Id,
                        PersonId = p.Id,
                        UserId = p.UserId,
                        Name = p.DisplayName,
                        Club = m.Club,
                        MemberNumber = m.Number,
                        QueuedCycle = 0,
                        Exclusion = "",
                        SeenAt = (DateTimeOffset?)null,
                        AcknowledgedAt = (DateTimeOffset?)null,
                    };
                })
            );
            await Db.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO "CommunicationRecipients" SELECT x.* FROM jsonb_populate_recordset(NULL::"CommunicationRecipients",{json}::jsonb)x ON CONFLICT("BroadcastId","PersonId") DO NOTHING"""
            );
            cursor = page[^1].Id;
        }
    }

    public async Task<string> Render(
        CommunicationBroadcast b,
        CommunicationRecipient r,
        string text
    )
    {
        var org = await Db.Orgs.SingleAsync(o => o.Id == b.OrgId);
        ClubEvent? ev =
            b.EventId == null ? null : await Db.Events.SingleAsync(e => e.Id == b.EventId);
        Check(
            ev != null
                || !Regex.IsMatch(
                    text,
                    @"\{\{(event_name|event_date|event_location|rsvp_link)\}\}"
                ),
            "This template requires an event."
        );
        return text.Replace("{{member_name}}", r.Name)
            .Replace("{{club_name}}", r.Club)
            .Replace("{{member_id}}", r.MemberNumber)
            .Replace("{{organization_name}}", org.Name)
            .Replace(
                "{{officer_name}}",
                await Db.Users.Where(u => u.Id == b.CreatorId).Select(u => u.Name).SingleAsync()
            )
            .Replace("{{event_name}}", ev?.Title ?? "")
            .Replace(
                "{{event_date}}",
                ev == null
                    ? ""
                    : TimeZoneInfo
                        .ConvertTime(ev.StartsAt, TimeZoneInfo.FindSystemTimeZoneById(ev.TimeZone))
                        .ToString("yyyy-MM-dd HH:mm")
                        + " "
                        + ev.TimeZone
            )
            .Replace("{{event_location}}", ev?.Venue ?? "")
            .Replace("{{rsvp_link}}", ev == null ? "" : "/app/events/" + ev.Id);
    }

    public async Task<(bool Allowed, string Address, string Reason)> Contact(
        Guid tenant,
        Guid? user,
        string channel,
        string purpose
    )
    {
        if (user == null)
            return (false, "", "No linked account");
        var u = await Db.Users.SingleAsync(x => x.Id == user);
        var p = await Db
            .Persons.Include(x => x.Privacy)
            .SingleOrDefaultAsync(x => x.UserId == user);
        var pref = await Db.CommunicationPreferences.SingleOrDefaultAsync(x =>
            x.UserId == user && x.Channel == channel
        );
        var promotional = purpose == "Promotional";
        var allowed =
            channel == "InApp" ? (pref?.Transactional ?? true)
            : channel == "Email"
                ? (
                    u.Verified
                    && (pref?.Transactional ?? true)
                    && p?.Privacy.TransactionalEmail != false
                )
            : (pref?.Transactional == true);
        if (promotional)
            allowed =
                allowed
                && (pref?.Promotional ?? (channel == "Email" && p?.Privacy.Marketing == true));
        var address =
            channel == "Email" ? u.Email
            : channel == "InApp" ? u.Id.ToString()
            : await Db
                .PersonContacts.Where(c =>
                    c.PersonId == (p == null ? Guid.Empty : p.Id) && c.Kind == "Phone" && c.Verified
                )
                .Select(c => c.NormalizedValue)
                .FirstOrDefaultAsync()
                ?? "";
        if (channel is "SMS" or "WhatsApp")
            allowed = allowed && Regex.IsMatch(address, @"^\+[1-9]\d{6,14}$");
        if (
            await Db.CommunicationSuppressions.AnyAsync(s =>
                s.TenantId == tenant
                && s.Channel == channel
                && s.AddressHash == Hash(address.ToLowerInvariant())
            )
        )
            return (false, address, "Suppressed");
        return (allowed, address, allowed ? "" : "Consent or contact unavailable");
    }

    public async Task<bool> Sender(
        Guid? assignment,
        Guid org,
        string permission = "communications.manage"
    )
    {
        if (assignment == null)
            return false;
        var w = await Db.Assignments.SingleOrDefaultAsync(x => x.Id == assignment);
        return w != null
            && await A.Effective(w)
            && (await A.Grants(w)).Contains(permission)
            && await A.Visible(w).AnyAsync(o => o.Id == org && o.Status == "Active");
    }

    public async Task<Assignment?> SendingAssignment(Guid tenant, Guid org, Guid user)
    {
        foreach (
            var w in await Db
                .Assignments.Where(x => x.TenantId == tenant && x.UserId == user)
                .ToListAsync()
        )
            if (
                await A.Effective(w)
                && (await A.Grants(w)).Contains("communications.manage")
                && await A.Visible(w).AnyAsync(o => o.Id == org)
            )
                return w;
        return null;
    }

    public static DateTimeOffset NextCycle(DateTimeOffset instant, string zone, int days)
    {
        var z = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var local = TimeZoneInfo.ConvertTime(instant, z).DateTime.AddDays(days);
        while (z.IsInvalidTime(local))
            local = local.AddMinutes(1);
        return new DateTimeOffset(
            local,
            z.IsAmbiguousTime(local)
                ? z.GetAmbiguousTimeOffsets(local).Min()
                : z.GetUtcOffset(local)
        ).ToUniversalTime();
    }

    public static DateTimeOffset QuietUntil(
        CommunicationPreference pref,
        string zone,
        DateTimeOffset instant
    )
    {
        if (!pref.QuietEnabled || pref.QuietStart == pref.QuietEnd)
            return instant;
        var z = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var local = TimeZoneInfo.ConvertTime(instant, z);
        bool quiet =
            pref.QuietStart < pref.QuietEnd
                ? local.Hour >= pref.QuietStart && local.Hour < pref.QuietEnd
                : local.Hour >= pref.QuietStart || local.Hour < pref.QuietEnd;
        if (!quiet)
            return instant;
        var end = local.Date.AddHours(pref.QuietEnd);
        if (end <= local.DateTime)
            end = end.AddDays(1);
        while (z.IsInvalidTime(end))
            end = end.AddMinutes(1);
        var offset = z.IsAmbiguousTime(end)
            ? z.GetAmbiguousTimeOffsets(end).Min()
            : z.GetUtcOffset(end);
        return new DateTimeOffset(end, offset).ToUniversalTime();
    }
}
