using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record MemberMessageInput(Guid OrgId, string Title, string Body, Guid RequestKey);

public static class LeaderCommunications
{
    public static readonly Dictionary<string, string> PresidentLevels = new()
    {
        ["International President"] = "International", ["National President"] = "National",
        ["Regional President"] = "Region", ["District President"] = "District",
        ["Zonal President"] = "Zone", ["Club President"] = "Club",
    };
    public static readonly string[] SendingRoles = PresidentLevels.Keys.Concat(new[]
    {
        "International Administrator", "National Administrator", "Regional Administrator",
        "District Administrator", "Zone Administrator", "Club Secretary", "Club Manager",
    }).ToArray();

    public static async Task Configure(Db db)
    {
        foreach (var tenant in await db.Tenants.Where(t => t.Name == ImpactSetup.Name).ToListAsync())
        {
            var roles = await db.Roles.Include(r => r.Grants).Where(r => r.TenantId == tenant.Id).ToListAsync();
            foreach (var name in PresidentLevels.Keys)
            {
                if (roles.Any(r => r.Name == name)) continue;
                var role = new Role { TenantId = tenant.Id, Name = name, Permissions = ["organization.view", "members.view", "directory.view"] };
                db.Roles.Add(role); roles.Add(role);
            }
            // Explicit read policy for the predefined president roles; office titles never grant contacts or management.
            foreach (var role in roles.Where(r => PresidentLevels.ContainsKey(r.Name)))
                if (!role.Grants.Any(g => g.PermissionId == "leadership.view"))
                {
                    var grant = new RolePermission { RoleId = role.Id, PermissionId = "leadership.view" };
                    role.Grants.Add(grant); db.RolePermissions.Add(grant);
                }
            foreach (var role in roles.Where(r => SendingRoles.Contains(r.Name)))
                foreach (var permission in CommunicationService.Permissions.Where(p => p != "communications.configure"))
                    if (!role.Grants.Any(g => g.PermissionId == permission))
                    {
                        var grant = new RolePermission { RoleId = role.Id, PermissionId = permission };
                        role.Grants.Add(grant);
                        db.RolePermissions.Add(grant);
                    }
            var levels = await db.Levels.Where(l => l.TenantId == tenant.Id).ToListAsync();
            foreach (var role in roles.Where(r => SendingRoles.Contains(r.Name) || r.Grants.Any(g => g.PermissionId == "finance.view")))
            {
                var permissions = new List<string> { DuesCenter.View, "finance.self" };
                if (new[] { "Club President", "Club Treasurer", "Club Secretary", "Club Manager" }.Contains(role.Name)) permissions.Add(DuesCenter.Collect);
                foreach (var permission in permissions.Where(p => !role.Grants.Any(g => g.PermissionId == p)))
                {
                    var grant = new RolePermission { RoleId = role.Id, PermissionId = permission };
                    role.Grants.Add(grant); db.RolePermissions.Add(grant);
                }
            }
            var positions = await db.OfficerPositions.Where(p => p.TenantId == tenant.Id).ToListAsync();
            foreach (var (roleName, levelName) in PresidentLevels)
            {
                var level = levels.SingleOrDefault(l => l.Name == levelName);
                if (level == null) continue;
                var title = levelName == "Club" ? "President" : roleName;
                var position = positions.SingleOrDefault(p => p.LevelId == level.Id && p.Title == title);
                if (position == null)
                {
                    position = new OfficerPosition { TenantId = tenant.Id, LevelId = level.Id, Title = title,
                        Code = level.Id.ToString("N")[..8].ToUpperInvariant() + "-PRES" };
                    db.OfficerPositions.Add(position);
                }
                position.GrantRoleId ??= roles.Single(r => r.Name == roleName).Id;
            }
        }
        await db.SaveChangesAsync();
    }

    public static async Task CheckAssignment(CommunicationService s, Assignment w)
    {
        if (await s.Db.Users.AnyAsync(u => u.Id == w.UserId && u.PlatformAdmin)) return;
        var role = await s.Db.Roles.Where(r => r.Id == w.RoleId).Select(r => r.Name).SingleAsync();
        if (!PresidentLevels.TryGetValue(role, out var expected)) return;
        var actual = await (from assignment in s.Db.Assignments join org in s.Db.Orgs on assignment.OrgId equals org.Id
            join level in s.Db.Levels on org.LevelId equals level.Id where assignment.Id == w.Id select level.Name).SingleAsync();
        Check(actual == expected, role + " must be assigned to the " + expected + " level.", 403);
    }

    public static IQueryable<Membership> Recipients(CommunicationService s, Assignment w, Guid org) =>
        s.Members(w).Where(m => m.Club.Status == "Active" && m.Status == "Active" && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
            && s.A.Tree(w.TenantId, org).Any(o => o.Id == m.ClubId));

    public static void MapLeaderCommunications(this RouteGroupBuilder root)
    {
        var api = root.MapGroup("/communications");
        api.MapGet("/member-messages", async (CommunicationService s) =>
        {
            var w = await s.A.Workspace("communications.send");
            await s.Scope(w, w.OrgId);
            return await s.Db.CommunicationBroadcasts.Where(b => b.TenantId == w.TenantId && b.Kind == "Announcement"
                    && b.Channel == "InApp" && s.A.Visible(w).Any(o => o.Id == b.OrgId))
                .OrderByDescending(b => b.At).Take(20).Select(b => new
                {
                    b.Id, b.Title, b.Status, b.At,
                    organization = s.Db.Orgs.Where(o => o.Id == b.OrgId).Select(o => o.Name).First(),
                    delivered = s.Db.CommunicationRecipients.Count(r => r.BroadcastId == b.Id && r.Exclusion == ""),
                    excluded = s.Db.CommunicationRecipients.Count(r => r.BroadcastId == b.Id && r.Exclusion != ""),
                }).ToListAsync();
        });
        api.MapGet("/member-scope", async (CommunicationService s, Guid? orgId = null) =>
        {
            var w = await s.A.Workspace("communications.view");
            await CheckAssignment(s, w);
            var id = orgId ?? w.OrgId;
            await s.Scope(w, id);
            var org = await s.Db.Orgs.SingleAsync(o => o.Id == id);
            var type = await s.Db.Levels.Where(l => l.Id == org.LevelId).Select(l => l.Name).SingleAsync();
            var ids = Recipients(s, w, id).Select(m => m.PersonId).Distinct();
            var members = await s.Db.Persons.Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.UserId }).ToListAsync();
            var users = members.Where(p => p.UserId != null).Select(p => p.UserId!.Value).Distinct().ToArray();
            var linked = await s.Db.Users.Where(u => users.Contains(u.Id) && u.Verified).Select(u => u.Id).ToListAsync();
            var optedOut = await s.Db.CommunicationPreferences.Where(p => linked.Contains(p.UserId) && p.Channel == "InApp" && !p.Transactional).Select(p => p.UserId).ToListAsync();
            var suppressed = await s.Db.CommunicationSuppressions.Where(p => p.TenantId == w.TenantId && p.Channel == "InApp").Select(p => p.AddressHash).ToListAsync();
            var reachable = members.Count(p => p.UserId != null && linked.Contains(p.UserId.Value)
                && !optedOut.Contains(p.UserId.Value) && !suppressed.Contains(Hash(p.UserId.Value.ToString().ToLowerInvariant())));
            var canSend = (await s.A.Grants(w)).Contains("communications.send");
            return new { org.Id, org.Name, type, totalMembers = members.Count, linkedMembers = members.Count(p => p.UserId != null),
                reachableMembers = reachable, unlinkedMembers = members.Count(p => p.UserId == null),
                clubs = await s.A.Tree(w.TenantId, id).CountAsync(o => s.Db.Levels.Any(l => l.Id == o.LevelId && l.IsClub)),
                canSend,
                sample = canSend ? await s.Db.Persons.Where(p => ids.Contains(p.Id)).OrderBy(p => p.DisplayName).Take(10).Select(p => new { p.Id, name = p.DisplayName }).ToListAsync() : null };
        });
        api.MapPost("/member-messages", async (MemberMessageInput input, CommunicationService s) =>
        {
            var w = await s.A.Workspace("communications.send");
            await using var tx = await s.A.Mutation(w);
            await CheckAssignment(s, w);
            await s.Scope(w, input.OrgId);
            Check(input.RequestKey != Guid.Empty, "A unique send request is required.");
            Check(!string.IsNullOrWhiteSpace(input.Title) && !string.IsNullOrWhiteSpace(input.Body), "Enter a title and message.");
            Check(input.Title.Trim().Length is >= 2 and <= 200, "Enter a message title.");
            CommunicationService.Content(input.Body, input.Title);
            var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(w.UserId + ":" + input.RequestKey)).AsSpan(0, 16));
            var prior = await s.Db.CommunicationBroadcasts.SingleOrDefaultAsync(b => b.Id == id);
            if (prior != null)
            {
                Check(prior.OrgId == input.OrgId && prior.Title == input.Title.Trim() && prior.Body == input.Body.Trim(), "This send request was already used for another message.", 409);
                var previousRecipients = await s.Db.CommunicationRecipients.Where(r => r.BroadcastId == id).ToListAsync();
                return Results.Ok(new { id, delivered = previousRecipients.Count(r => r.Exclusion == ""), excluded = previousRecipients.Count(r => r.Exclusion != ""), total = previousRecipients.Count });
            }
            var memberships = await Recipients(s, w, input.OrgId).OrderBy(m => m.Id)
                .Select(m => new { m.PersonId, m.Number, Club = m.Club.Name, m.Person.UserId, m.Person.DisplayName }).ToListAsync();
            var recipients = memberships.DistinctBy(m => m.PersonId).ToArray();
            Check(recipients.Length > 0, "No active members belong to this scope.");
            var b = new CommunicationBroadcast { Id = id, TenantId = w.TenantId, OrgId = input.OrgId, CreatorId = w.UserId,
                Kind = "Announcement", Purpose = "Transactional", Channel = "InApp", Title = input.Title.Trim(), Subject = input.Title.Trim(),
                Body = input.Body.Trim(), Rules = GovernanceService.Pack(new AudienceRule(input.OrgId)),
                ContentHash = Hash(input.Title.Trim() + "\n" + input.Body.Trim()), Status = "Completed", CompletedAt = DateTimeOffset.UtcNow };
            s.Db.CommunicationBroadcasts.Add(b);
            var context = await s.Context(b);
            var delivered = 0;
            foreach (var person in recipients)
            {
                var contact = await s.Contact(w.TenantId, person.UserId, "InApp", "Transactional");
                var recipient = new CommunicationRecipient { TenantId = w.TenantId, BroadcastId = b.Id, PersonId = person.PersonId,
                    UserId = person.UserId, Name = person.DisplayName, Club = person.Club, MemberNumber = person.Number,
                    QueuedCycle = 1, Exclusion = contact.Allowed ? "" : contact.Reason };
                s.Db.CommunicationRecipients.Add(recipient);
                if (!contact.Allowed) continue;
                delivered++;
                s.Db.CommunicationMessages.Add(new CommunicationMessage { TenantId = w.TenantId, OrgId = input.OrgId,
                    BroadcastId = b.Id, SenderId = w.UserId, PersonId = person.PersonId, Subject = b.Title,
                    Body = CommunicationService.RenderFrozen(b.Body, recipient, context), ClientKey = input.RequestKey + ":" + person.PersonId });
            }
            Check(delivered > 0, "No members in this scope can receive in-app messages. Link member accounts first.");
            b.Status = delivered == recipients.Length ? "Completed" : "Partially Completed";
            s.Revision(b, b.Version, "Member announcement", w.UserId, context);
            s.Audit(w, input.OrgId, "members.sent", b.Id, $"{delivered} delivered; {recipients.Length - delivered} excluded");
            await s.Db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { id = b.Id, delivered, excluded = recipients.Length - delivered, total = recipients.Length });
        });
    }
}
