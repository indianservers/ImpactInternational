using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record ConversationInput(
    Guid OrgId,
    string Title,
    Guid[] UserIds,
    string Kind = "Direct",
    string Channel = "InApp",
    Guid? AccountId = null,
    Guid? CommitteeId = null,
    Guid? EventId = null
);

public record ConversationUpdateInput(
    string Status,
    string Priority,
    string Tags,
    Guid? AssignedId,
    long Version
);

public record ConversationMemberInput(Guid UserId, bool Active = true);

public record MessageInput(
    string Body,
    string ClientKey,
    Guid? ReplyToId = null,
    bool Internal = false,
    string TemplateName = "",
    bool Hold = false
);

public record ReceiptInput(bool Muted = false);

public static partial class CommunicationEndpoints
{
    private static void MapConversations(RouteGroupBuilder api)
    {
        api.MapGet(
            "/people",
            async (CommunicationService s, string search = "") =>
            {
                var w = await s.A.Workspace("communications.view");
                Check(search.Length <= 100, "Search too long.");
                var u = await s.A.User();
                var privileged = (await s.A.Grants(w)).Contains("communications.inbox");
                var clubs = s
                    .Db.Memberships.Where(m =>
                        m.TenantId == w.TenantId
                        && m.Person.UserId == u.Id
                        && m.Status == "Active"
                        && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                    )
                    .Select(m => m.ClubId);
                return await s
                    .Db.Persons.Where(p =>
                        p.UserId != null
                        && p.UserId != u.Id
                        && p.Privacy.AllowContactRequests
                        && s.Db.Memberships.Any(m =>
                            m.PersonId == p.Id
                            && m.TenantId == w.TenantId
                            && m.Status == "Active"
                            && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                            && (
                                privileged
                                    ? s.A.Visible(w).Select(o => o.Id).Contains(m.ClubId)
                                    : clubs.Contains(m.ClubId)
                            )
                        )
                        && EF.Functions.ILike(p.DisplayName, "%" + search + "%")
                    )
                    .OrderBy(p => p.DisplayName)
                    .Take(30)
                    .Select(p => new
                    {
                        p.Id,
                        p.UserId,
                        p.DisplayName,
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/conversations",
            async (ConversationInput r, CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.view");
                await s.Scope(w, r.OrgId);
                var user = await s.A.User();
                var staff = await s.Staff(w.TenantId, r.OrgId, user.Id);
                Check(
                    r.Title.Length is > 1 and <= 160
                        && r.UserIds.Length is > 0 and <= 100
                        && new[] { "Direct", "Group", "Committee", "Event", "Support" }.Contains(
                            r.Kind
                        )
                        && CommunicationService.Channels.Contains(r.Channel),
                    "Invalid conversation."
                );
                var users = r.UserIds.Append(user.Id).Distinct().ToArray();
                Check(users.Length >= 2, "Choose another participant.");
                var policy = await s.Db.CommunicationPolicies.SingleOrDefaultAsync(p =>
                    p.OrgId == r.OrgId
                );
                Check(staff || policy?.MemberDirect != false, "Direct messaging is disabled.", 403);
                if (r.Channel != "InApp")
                {
                    Check(
                        staff && r.AccountId != null && users.Length == 2,
                        "External inbox needs an authorized account and one recipient.",
                        403
                    );
                    var account = await s.Db.CommunicationAccounts.SingleOrDefaultAsync(x =>
                        x.Id == r.AccountId
                        && x.TenantId == w.TenantId
                        && x.Channel == r.Channel
                        && x.OrgId == r.OrgId
                    );
                    Check(account != null && account.Enabled, "Account unavailable.", 409);
                }
                foreach (var uid in users)
                {
                    var member = await s.Db.Memberships.AnyAsync(m =>
                        m.TenantId == w.TenantId
                        && m.Person.UserId == uid
                        && m.Status == "Active"
                        && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
                        && s.A.Tree(w.TenantId, r.OrgId).Select(o => o.Id).Contains(m.ClubId)
                    );
                    var isStaff = await s.Staff(w.TenantId, r.OrgId, uid);
                    Check(
                        member || isStaff,
                        "Participant is outside the permitted organization.",
                        403
                    );
                    if (uid != user.Id)
                    {
                        Check(
                            !await s.Db.CommunicationPreferences.AnyAsync(p =>
                                p.UserId == uid && p.Channel == "InApp" && !p.Direct
                            )
                                && !await s.Db.Persons.AnyAsync(p =>
                                    p.UserId == uid && !p.Privacy.AllowContactRequests
                                ),
                            "Participant does not accept direct messages.",
                            403
                        );
                        if (!staff && !isStaff)
                        {
                            var own = s
                                .Db.Memberships.Where(m =>
                                    m.Person.UserId == user.Id
                                    && m.TenantId == w.TenantId
                                    && m.Status == "Active"
                                )
                                .Select(m => m.ClubId);
                            Check(
                                await s.Db.Memberships.AnyAsync(m =>
                                    m.Person.UserId == uid
                                    && m.TenantId == w.TenantId
                                    && m.Status == "Active"
                                    && own.Contains(m.ClubId)
                                ),
                                "Members must share a club.",
                                403
                            );
                        }
                    }
                    if (r.Kind == "Committee")
                        Check(
                            r.CommitteeId != null
                                && await s.Db.Committees.AnyAsync(c =>
                                    c.Id == r.CommitteeId
                                    && c.TenantId == w.TenantId
                                    && c.OrgId == r.OrgId
                                )
                                && await s.Db.CommitteeMemberships.AnyAsync(c =>
                                    c.CommitteeId == r.CommitteeId
                                    && c.PersonId
                                        == s.Db.Persons.Where(p => p.UserId == uid)
                                            .Select(p => p.Id)
                                            .FirstOrDefault()
                                    && c.Status == "Active"
                                    && c.StartsAt <= DateTimeOffset.UtcNow
                                    && c.EndsAt > DateTimeOffset.UtcNow
                                ),
                            "Only current committee participants may join.",
                            403
                        );
                    if (r.Kind == "Event")
                        Check(
                            r.EventId != null
                                && await s.Db.Events.AnyAsync(e =>
                                    e.Id == r.EventId
                                    && e.TenantId == w.TenantId
                                    && e.OrgId == r.OrgId
                                )
                                && (
                                    isStaff
                                    || await s.Db.EventRegistrations.AnyAsync(e =>
                                        e.EventId == r.EventId
                                        && e.Status == "Confirmed"
                                        && e.PersonId
                                            == s.Db.Persons.Where(p => p.UserId == uid)
                                                .Select(p => p.Id)
                                                .FirstOrDefault()
                                    )
                                ),
                            "Only authorized event participants may join.",
                            403
                        );
                }
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var c = new Conversation
                {
                    TenantId = w.TenantId,
                    OrgId = r.OrgId,
                    OwnerId = user.Id,
                    Title = r.Title,
                    Kind = r.Kind,
                    Channel = r.Channel,
                    AccountId = r.AccountId,
                    CommitteeId = r.CommitteeId,
                    EventId = r.EventId,
                };
                if (c.Channel != "InApp")
                {
                    var recipient = users.First(uid => uid != user.Id);
                    var contact = await s.Contact(
                        w.TenantId,
                        recipient,
                        c.Channel,
                        "Transactional"
                    );
                    Check(contact.Allowed, contact.Reason, 409);
                    c.ExternalThread = contact.Address;
                }
                s.Db.Conversations.Add(c);
                foreach (var uid in users)
                    s.Db.ConversationParticipants.Add(
                        new()
                        {
                            TenantId = w.TenantId,
                            ConversationId = c.Id,
                            UserId = uid,
                            Staff = await s.Staff(w.TenantId, r.OrgId, uid),
                        }
                    );
                s.Audit(w, c.OrgId, "conversation.created", c.Id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return c;
            }
        );
        api.MapGet(
            "/conversations",
            async (
                CommunicationService s,
                Guid? after = null,
                string search = "",
                string channel = "",
                string status = "",
                bool unread = false,
                bool assigned = false,
                bool team = false
            ) =>
            {
                var w = await s.A.Workspace("communications.view");
                var u = await s.A.User();
                Check(search.Length <= 100, "Search too long.");
                var q = s.Db.Conversations.Where(c =>
                    c.TenantId == w.TenantId
                    && s.Db.ConversationParticipants.Any(p =>
                        p.ConversationId == c.Id && p.UserId == u.Id && p.Active
                    )
                );
                // Staff authority is revalidated for every returned record. No hierarchy bypass exists.
                if (channel != "")
                    q = q.Where(c => c.Channel == channel);
                if (status != "")
                    q = q.Where(c => c.Status == status);
                if (assigned)
                    q = q.Where(c => c.AssignedId == u.Id);
                if (team)
                    q = q.Where(c => c.Kind != "Direct");
                if (search != "")
                    q = q.Where(c => EF.Functions.ILike(c.Title, "%" + search + "%"));
                if (unread)
                    q = q.Where(c =>
                        s.Db.CommunicationMessages.Any(m =>
                            m.ConversationId == c.Id
                            && !m.Internal
                            && m.SenderId != u.Id
                            && m.At
                                > s.Db.ConversationParticipants.Where(p =>
                                        p.ConversationId == c.Id && p.UserId == u.Id
                                    )
                                    .Select(p => p.ReadAt)
                                    .First()
                        )
                    );
                if (after != null)
                {
                    var anchor = await q.SingleOrDefaultAsync(c => c.Id == after);
                    Check(anchor != null, "Invalid inbox cursor.");
                    q = q.Where(c =>
                        c.UpdatedAt < anchor.UpdatedAt
                        || (c.UpdatedAt == anchor.UpdatedAt && c.Id.CompareTo(anchor.Id) < 0)
                    );
                }
                var rows = await q.OrderByDescending(c => c.UpdatedAt)
                    .ThenByDescending(c => c.Id)
                    .Take(31)
                    .ToListAsync();
                var ids = rows.Select(c => c.Id).ToArray();
                var participants = await s
                    .Db.ConversationParticipants.Where(p =>
                        ids.Contains(p.ConversationId) && p.UserId == u.Id && p.Active
                    )
                    .ToDictionaryAsync(p => p.ConversationId);
                var staffOrgs = new HashSet<Guid>();
                foreach (
                    var assignment in await s
                        .Db.Assignments.Where(x =>
                            x.UserId == u.Id
                            && x.TenantId == w.TenantId
                            && s.Db.RolePermissions.Any(p =>
                                p.RoleId == x.RoleId && p.PermissionId == "communications.inbox"
                            )
                        )
                        .ToListAsync()
                )
                {
                    if (await s.A.Effective(assignment))
                    {
                        var orgIds = rows.Select(c => c.OrgId).ToArray();
                        foreach (
                            var org in await s
                                .A.Visible(assignment)
                                .Where(o => orgIds.Contains(o.Id) && o.Status == "Active")
                                .Select(o => o.Id)
                                .ToListAsync()
                        )
                            staffOrgs.Add(org);
                    }
                }
                var memberOrgs = await s
                    .Db.Database.SqlQuery<Guid>(
                        $"""
                        WITH RECURSIVE chain AS (
                          SELECT o."Id",o."ParentId",o."TenantId" FROM "Orgs" o JOIN "Memberships" m ON m."ClubId"=o."Id" JOIN "Persons" p ON p."Id"=m."PersonId" WHERE m."TenantId"={w.TenantId} AND p."UserId"={u.Id} AND m."Status"='Active' AND (m."EndsAt" IS NULL OR m."EndsAt">now())
                          UNION SELECT o."Id",o."ParentId",o."TenantId" FROM "Orgs" o JOIN chain c ON c."ParentId"=o."Id" AND c."TenantId"=o."TenantId"
                        ) SELECT DISTINCT "Id" AS "Value" FROM chain
                        """
                    )
                    .ToArrayAsync();
                var orgs = await s
                    .Db.Orgs.Where(o => rows.Select(c => c.OrgId).Contains(o.Id))
                    .ToDictionaryAsync(o => o.Id, o => o.Name);
                var counts = await (
                    from m in s.Db.CommunicationMessages
                    join p in s.Db.ConversationParticipants
                        on m.ConversationId equals (Guid?)p.ConversationId
                    where
                        ids.Contains(p.ConversationId)
                        && p.UserId == u.Id
                        && p.Active
                        && !m.Internal
                        && m.SenderId != u.Id
                        && m.At > p.ReadAt
                    group m by p.ConversationId into g
                    select new { Id = g.Key, Count = g.Count() }
                ).ToDictionaryAsync(x => x.Id, x => x.Count);
                var result = rows.Where(c =>
                        participants.TryGetValue(c.Id, out var participant)
                        && (
                            participant.Staff
                                ? staffOrgs.Contains(c.OrgId)
                                : memberOrgs.Contains(c.OrgId)
                        )
                    )
                    .Select(c => new
                    {
                        conversation = c,
                        unread = counts.GetValueOrDefault(c.Id),
                        participants[c.Id].Muted,
                        organization = orgs[c.OrgId],
                    })
                    .ToList();
                return new
                {
                    items = result.Take(30),
                    next = rows.Count > 30 ? rows[29].Id : (Guid?)null,
                };
            }
        );
        api.MapGet(
            "/conversations/{id:guid}",
            async (Guid id, CommunicationService s) =>
            {
                var c = await s.Conversation(id);
                var p = await s.Participant(c);
                var users = await s
                    .Db.ConversationParticipants.Where(x => x.ConversationId == id && x.Active)
                    .Select(x => new
                    {
                        x.UserId,
                        x.Staff,
                        x.ReadAt,
                        name = s.Db.Users.Where(u => u.Id == x.UserId).Select(u => u.Name).First(),
                    })
                    .ToListAsync();
                return new
                {
                    conversation = c,
                    participants = users,
                    staff = p.Staff,
                    muted = p.Muted,
                };
            }
        );
        api.MapGet(
            "/conversations/{id:guid}/messages",
            async (Guid id, CommunicationService s, Guid? before = null, string search = "") =>
            {
                var c = await s.Conversation(id);
                var p = await s.Participant(c);
                Check(search.Length <= 100, "Search too long.");
                var q = s.Db.CommunicationMessages.Where(m =>
                    m.ConversationId == id && (p.Staff || !m.Internal)
                );
                if (search != "")
                    q = q.Where(m => EF.Functions.ILike(m.Body, "%" + search + "%"));
                if (before != null)
                {
                    var anchor = await q.SingleOrDefaultAsync(m => m.Id == before);
                    Check(anchor != null, "Invalid message cursor.");
                    q = q.Where(m =>
                        m.At < anchor.At || (m.At == anchor.At && m.Id.CompareTo(anchor.Id) < 0)
                    );
                }
                var rows = await q.OrderByDescending(m => m.At)
                    .ThenByDescending(m => m.Id)
                    .Take(51)
                    .Select(m => new
                    {
                        m.Id,
                        m.Body,
                        m.Subject,
                        m.SenderId,
                        m.ReplyToId,
                        m.Internal,
                        m.Direction,
                        m.At,
                        sender = s.Db.Users.Where(u => u.Id == m.SenderId)
                            .Select(u => u.Name)
                            .FirstOrDefault()
                            ?? "Member",
                        delivery = s.Db.Mails.Where(o => o.CommunicationId == m.Id)
                            .Select(o => o.DeliveryStatus)
                            .FirstOrDefault()
                            ?? "Delivered",
                        attachments = s
                            .Db.CommunicationAttachments.Where(f =>
                                f.MessageId == m.Id
                                && f.ExpiresAt > DateTimeOffset.UtcNow
                                && f.Status == "Stored"
                            )
                            .Select(f => new
                            {
                                f.Id,
                                f.Name,
                                f.Size,
                                f.Type,
                            })
                            .ToArray(),
                    })
                    .ToListAsync();
                return new
                {
                    items = rows.Take(50).Reverse(),
                    next = rows.Count > 50 ? rows[49].Id : (Guid?)null,
                };
            }
        );
        api.MapPost(
            "/conversations/{id:guid}/messages",
            async (Guid id, MessageInput r, CommunicationService s) =>
            {
                var c = await s.Conversation(id);
                var p = await s.Participant(c);
                var u = await s.A.User();
                CommunicationService.Content(r.Body);
                Check(
                    r.ClientKey.Length is > 7 and <= 100,
                    "A stable client idempotency key is required."
                );
                Check(!r.Internal || p.Staff, "Internal notes require explicit staff access.", 403);
                Check(c.Status != "Archived", "Reopen this conversation before sending.", 409);
                if (r.ReplyToId != null)
                    Check(
                        await s.Db.CommunicationMessages.AnyAsync(m =>
                            m.Id == r.ReplyToId
                            && m.ConversationId == id
                            && (p.Staff || !m.Internal)
                        ),
                        "Reply target is unavailable."
                    );
                var existing = await s.Db.CommunicationMessages.SingleOrDefaultAsync(m =>
                    m.TenantId == c.TenantId && m.SenderId == u.Id && m.ClientKey == r.ClientKey
                );
                if (existing != null)
                    return Results.Ok(existing);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Conversations\" WHERE \"Id\"={id} FOR NO KEY UPDATE"
                );
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({"communication.sender:" + u.Id},0))"
                );
                Check(
                    await s.Db.CommunicationMessages.CountAsync(m =>
                        m.TenantId == c.TenantId
                        && m.ConversationId != null
                        && m.SenderId == u.Id
                        && m.At > DateTimeOffset.UtcNow.AddMinutes(-1)
                    ) < 60,
                    "Message rate limit reached. Please retry shortly.",
                    429
                );
                var m = new CommunicationMessage
                {
                    TenantId = c.TenantId,
                    OrgId = c.OrgId,
                    ConversationId = id,
                    SenderId = u.Id,
                    Body = r.Body,
                    ReplyToId = r.ReplyToId,
                    Internal = r.Internal,
                    ClientKey = r.ClientKey,
                    Direction = r.Internal ? "Note" : "Outbound",
                };
                s.Db.CommunicationMessages.Add(m);
                if (c.Channel != "InApp" && !r.Internal)
                {
                    Check(p.Staff, "Only assigned staff can send external replies.", 403);
                    var recipient = await s
                        .Db.ConversationParticipants.Where(x =>
                            x.ConversationId == id && !x.Staff && x.Active
                        )
                        .Select(x => (Guid?)x.UserId)
                        .SingleOrDefaultAsync();
                    Check(recipient != null, "No member recipient.");
                    var contact = await s.Contact(
                        c.TenantId,
                        recipient,
                        c.Channel,
                        "Transactional"
                    );
                    Check(contact.Allowed, contact.Reason, 409);
                    var account = await s.Db.CommunicationAccounts.SingleAsync(x =>
                        x.Id == c.AccountId
                    );
                    Check(
                        account.Enabled && account.Verified,
                        "Verify this sending account first.",
                        409
                    );
                    if (c.Channel == "WhatsApp" && r.TemplateName == "")
                        Check(
                            c.LastInboundAt > DateTimeOffset.UtcNow.AddHours(-24),
                            "An approved WhatsApp template is required outside the service window.",
                            409
                        );
                    CommunicationTemplate? chosenTemplate = null;
                    string[] parameters = [];
                    if (r.TemplateName != "")
                    {
                        chosenTemplate = await s.Db.CommunicationTemplates.SingleOrDefaultAsync(t =>
                            t.TenantId == c.TenantId
                            && t.AccountId == c.AccountId
                            && t.ProviderName == r.TemplateName
                            && t.ProviderStatus == "APPROVED"
                            && t.Channel == c.Channel
                            && t.Active
                        );
                        Check(
                            chosenTemplate != null,
                            "Template is not approved for this sending account.",
                            409
                        );
                        var person = await s.Db.Persons.SingleAsync(p => p.UserId == recipient);
                        var membership = await s
                            .Db.Memberships.Where(x =>
                                x.PersonId == person.Id
                                && x.TenantId == c.TenantId
                                && x.Status == "Active"
                            )
                            .OrderBy(x => x.Id)
                            .FirstAsync();
                        var club = await s.Db.Orgs.SingleAsync(o => o.Id == membership.ClubId);
                        m.Body = await s.Render(
                            new()
                            {
                                TenantId = c.TenantId,
                                OrgId = c.OrgId,
                                CreatorId = u.Id,
                                EventId = c.EventId,
                            },
                            new()
                            {
                                Name = person.DisplayName,
                                Club = club.Name,
                                MemberNumber = membership.Number,
                            },
                            chosenTemplate.Body
                        );
                        parameters = chosenTemplate.Body.Contains("{{member_name}}")
                            ? [person.DisplayName]
                            : [];
                    }
                    s.Db.Mails.Add(
                        new()
                        {
                            TenantId = c.TenantId,
                            OrgId = c.OrgId,
                            CommunicationId = m.Id,
                            AccountId = c.AccountId,
                            SenderId = u.Id,
                            SenderAssignmentId = (
                                await s.SendingAssignment(c.TenantId, c.OrgId, u.Id)
                            )?.Id,
                            UserId = recipient,
                            Recipient = contact.Address,
                            Channel = c.Channel,
                            Body = m.Body,
                            Subject = c.Title,
                            DeliveryStatus = r.Hold ? "Draft" : "Pending",
                            Kind = "communication.reply",
                            TokenHash = Hash(Token()),
                            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                            TemplateName = r.TemplateName,
                            TemplateVersion = chosenTemplate?.Version ?? 0,
                            TemplateParameters = JsonSerializer.Serialize(parameters),
                            Language = chosenTemplate?.Language ?? "en",
                        }
                    );
                }
                c.UpdatedAt = m.At;
                if (!r.Internal)
                    c.Preview = m.Body[..Math.Min(160, m.Body.Length)];
                c.Version++;
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(m);
            }
        );
        api.MapPost(
            "/conversations/{id:guid}/read",
            async (Guid id, ReceiptInput r, CommunicationService s) =>
            {
                var c = await s.Conversation(id);
                var p = await s.Participant(c);
                p.ReadAt = DateTimeOffset.UtcNow;
                p.Muted = r.Muted;
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapPut(
            "/conversations/{id:guid}",
            async (Guid id, ConversationUpdateInput r, CommunicationService s) =>
            {
                var c = await s.Conversation(id);
                var p = await s.Participant(c);
                Check(p.Staff, "Only explicit staff participants can manage this inbox.", 403);
                Check(c.Version == r.Version, "Conversation changed.", 409);
                Check(
                    new[]
                    {
                        "Open",
                        "Pending",
                        "Waiting for Member",
                        "Resolved",
                        "Archived",
                    }.Contains(r.Status)
                        && new[] { "Low", "Normal", "High", "Urgent" }.Contains(r.Priority)
                        && r.Tags.Length <= 300,
                    "Invalid conversation state."
                );
                if (r.AssignedId != null)
                    Check(
                        await s.Db.ConversationParticipants.AnyAsync(p =>
                            p.ConversationId == id
                            && p.UserId == r.AssignedId
                            && p.Staff
                            && p.Active
                        ) && await s.Staff(c.TenantId, c.OrgId, r.AssignedId.Value),
                        "Assignee must be an active explicit staff participant.",
                        403
                    );
                c.Status = r.Status;
                c.Priority = r.Priority;
                c.Tags = r.Tags;
                c.AssignedId = r.AssignedId;
                c.Version++;
                c.UpdatedAt = DateTimeOffset.UtcNow;
                await s.Db.SaveChangesAsync();
                s.Audit(
                    await s.A.Workspace("communications.inbox"),
                    c.OrgId,
                    "conversation.updated",
                    id,
                    r.Status
                );
                await s.Db.SaveChangesAsync();
                return c;
            }
        );
        api.MapPost(
            "/conversations/{id:guid}/participants",
            async (Guid id, ConversationMemberInput r, CommunicationService s) =>
            {
                var c = await s.Conversation(id);
                var u = await s.A.User();
                Check(
                    c.OwnerId == u.Id
                        && await s.Staff(c.TenantId, c.OrgId, u.Id)
                        && await s.Staff(c.TenantId, c.OrgId, r.UserId),
                    "Only the owner may explicitly delegate to eligible staff.",
                    403
                );
                var p = await s.Db.ConversationParticipants.SingleOrDefaultAsync(p =>
                    p.ConversationId == id && p.UserId == r.UserId
                );
                if (p == null)
                    s.Db.ConversationParticipants.Add(
                        new()
                        {
                            TenantId = c.TenantId,
                            ConversationId = id,
                            UserId = r.UserId,
                            Staff = true,
                            Active = r.Active,
                        }
                    );
                else
                    p.Active = r.Active;
                c.UpdatedAt = DateTimeOffset.UtcNow;
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapGet(
            "/conversations/{id:guid}/context",
            async (Guid id, CommunicationService s) =>
            {
                var c = await s.Conversation(id);
                var p = await s.Participant(c);
                Check(p.Staff, "CRM context requires explicit staff access.", 403);
                var uid = await s
                    .Db.ConversationParticipants.Where(x =>
                        x.ConversationId == id && !x.Staff && x.Active
                    )
                    .Select(x => (Guid?)x.UserId)
                    .FirstOrDefaultAsync();
                var pid = uid == null ? null : await s.PersonId(uid.Value);
                return new
                {
                    person = await s
                        .Db.Persons.Where(x => x.Id == pid)
                        .Select(x => new { x.Id, x.DisplayName })
                        .SingleOrDefaultAsync(),
                    memberships = await s
                        .Db.Memberships.Where(m =>
                            m.PersonId == pid
                            && m.TenantId == c.TenantId
                            && s.A.Tree(c.TenantId, c.OrgId).Select(o => o.Id).Contains(m.ClubId)
                        )
                        .Select(m => new
                        {
                            m.Status,
                            m.Number,
                            club = s
                                .Db.Orgs.Where(o => o.Id == m.ClubId)
                                .Select(o => o.Name)
                                .First(),
                        })
                        .Take(20)
                        .ToListAsync(),
                    events = await s
                        .Db.EventRegistrations.Where(r =>
                            r.PersonId == pid
                            && r.TenantId == c.TenantId
                            && s.Db.Events.Any(e =>
                                e.Id == r.EventId
                                && s.A.Tree(c.TenantId, c.OrgId).Select(o => o.Id).Contains(e.OrgId)
                            )
                        )
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(5)
                        .Select(r => new
                        {
                            r.Status,
                            title = s
                                .Db.Events.Where(e => e.Id == r.EventId)
                                .Select(e => e.Title)
                                .First(),
                        })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/stream",
            async (HttpContext ctx, CommunicationService s, Guid? conversationId = null) =>
            {
                Check(ctx.WebSockets.IsWebSocketRequest, "WebSocket required.");
                Check(
                    Uri.TryCreate(
                        ctx.Request.Headers.Origin.ToString(),
                        UriKind.Absolute,
                        out var origin
                    )
                        && origin.Authority == ctx.Request.Host.Value,
                    "Cross-origin WebSocket forbidden.",
                    403
                );
                var session = await s.A.Session();
                var w = await s.A.Workspace("communications.view");
                if (conversationId != null)
                    await s.Conversation(conversationId.Value);
                using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
                var last = "";
                while (
                    socket.State == WebSocketState.Open
                    && !ctx.RequestAborted.IsCancellationRequested
                )
                {
                    try
                    {
                        Check((await s.A.Session()).WorkspaceId == w.Id, "Workspace changed.", 403);
                        w = await s.A.Workspace("communications.view");
                        if (conversationId != null)
                            await s.Conversation(conversationId.Value);
                        var stamp =
                            conversationId == null
                                ? await s
                                    .Db.Conversations.Where(c =>
                                        c.TenantId == w.TenantId
                                        && s.Db.ConversationParticipants.Any(p =>
                                            p.ConversationId == c.Id
                                            && p.UserId == session.UserId
                                            && p.Active
                                        )
                                    )
                                    .MaxAsync(c => (DateTimeOffset?)c.UpdatedAt)
                                : await s
                                    .Db.Conversations.Where(c => c.Id == conversationId)
                                    .Select(c => (DateTimeOffset?)c.UpdatedAt)
                                    .SingleAsync();
                        var unread = await s.Db.CommunicationRecipients.CountAsync(r =>
                            r.UserId == session.UserId
                            && r.SeenAt == null
                            && r.TenantId == w.TenantId
                            && r.QueuedCycle > 0
                            && s.Db.CommunicationBroadcasts.Any(b =>
                                b.Id == r.BroadcastId
                                && new[] { "Sending", "Completed", "Partially Completed" }.Contains(
                                    b.Status
                                )
                                && b.SendAt <= DateTimeOffset.UtcNow
                            )
                        );
                        var readStamp =
                            conversationId == null
                                ? (DateTimeOffset?)null
                                : await s
                                    .Db.ConversationParticipants.Where(p =>
                                        p.ConversationId == conversationId && p.Active
                                    )
                                    .MaxAsync(p => (DateTimeOffset?)p.ReadAt);
                        var broadcastVersion = 0L;
                        if ((await s.A.Grants(w)).Contains("communications.manage"))
                            broadcastVersion =
                                await s
                                    .Db.CommunicationBroadcasts.Where(b =>
                                        b.TenantId == w.TenantId
                                        && s.A.Visible(w).Select(o => o.Id).Contains(b.OrgId)
                                    )
                                    .SumAsync(b => (long?)b.Version)
                                ?? 0;
                        var marker =
                            (stamp?.UtcTicks.ToString() ?? "")
                            + ":"
                            + unread
                            + ":"
                            + (readStamp?.UtcTicks.ToString() ?? "")
                            + ":"
                            + broadcastVersion;
                        if (marker != last)
                        {
                            await socket.SendAsync(
                                Encoding.UTF8.GetBytes(
                                    JsonSerializer.Serialize(
                                        new
                                        {
                                            type = "invalidate",
                                            conversationId,
                                            marker,
                                        }
                                    )
                                ),
                                WebSocketMessageType.Text,
                                true,
                                ctx.RequestAborted
                            );
                            last = marker;
                        }
                        await Task.Delay(1000, ctx.RequestAborted);
                        s.Db.ChangeTracker.Clear();
                    }
                    catch (ApiError)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.PolicyViolation,
                            "Access changed",
                            CancellationToken.None
                        );
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (WebSocketException)
                    {
                        break;
                    }
                }
            }
        );
    }
}
