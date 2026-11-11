using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class NotificationService(Db db)
{
    public async Task User(
        Guid? userId,
        string template,
        string detail,
        string link = "",
        Guid? tenantId = null,
        Guid? orgId = null,
        Guid? eventId = null
    )
    {
        if (userId == null)
            return;
        var person = await db
            .Persons.Include(x => x.Privacy)
            .SingleOrDefaultAsync(x => x.UserId == userId);
        var text = await db.NotificationTemplates.SingleOrDefaultAsync(x => x.Id == template);
        db.Mails.Add(
            new()
            {
                TenantId = tenantId,
                EventId = eventId,
                OrgId = orgId,
                UserId = userId,
                Kind = template,
                Subject = text?.Subject ?? "ClubSphere membership update",
                Body = (text?.Body ?? "{detail}").Replace("{detail}", detail),
                Link = link,
                TokenHash = Access.Hash(Access.Token()),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                Suppressed = person?.Privacy.TransactionalEmail == false,
            }
        );
    }

    public async Task Recipient(
        string email,
        string template,
        string detail,
        string link = "",
        Guid? tenantId = null,
        Guid? orgId = null,
        Guid? eventId = null
    )
    {
        var user = await db
            .Users.Where(x => x.Email == email && x.Verified)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync();
        if (user != null)
        {
            await User(user, template, detail, link, tenantId, orgId, eventId);
            return;
        }
        var text = await db.NotificationTemplates.SingleOrDefaultAsync(x => x.Id == template);
        db.Mails.Add(
            new()
            {
                TenantId = tenantId,
                EventId = eventId,
                OrgId = orgId,
                Recipient = email,
                Kind = template,
                Subject = text?.Subject ?? "ClubSphere invitation",
                Body = (text?.Body ?? "{detail}").Replace("{detail}", detail),
                Link = link,
                TokenHash = Access.Hash(Access.Token()),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            }
        );
    }

    public async Task Operation(
        Guid? personId,
        string detail,
        OperationalRecord record,
        Guid senderId,
        Access access
    )
    {
        if (personId == null)
            return;
        var person = await db.Persons.SingleOrDefaultAsync(p => p.Id == personId);
        if (person?.UserId == null)
            return;
        Assignment? sender = null;
        foreach (
            var candidate in await db
                .Assignments.Where(a =>
                    a.UserId == senderId
                    && a.TenantId == record.TenantId
                    && db.RolePermissions.Any(r =>
                        r.RoleId == a.RoleId && r.PermissionId == "operations.manage"
                    )
                )
                .ToListAsync()
        )
            if (
                await access.Effective(candidate)
                && await access.VisibleIds(candidate).ContainsAsync(record.OrgId)
            )
            {
                sender = candidate;
                break;
            }
        if (sender == null)
            return;
        await User(
            person.UserId,
            "operations.update",
            detail,
            "/app/operations",
            record.TenantId,
            record.OrgId
        );
        db.Mails.Add(
            new()
            {
                TenantId = record.TenantId,
                OrgId = record.OrgId,
                UserId = person.UserId,
                Kind = "operations.update",
                Channel = "InApp",
                Purpose = "Transactional",
                Subject = "Club operations update",
                Body = detail,
                Link = "/app/operations",
                SenderId = senderId,
                SenderAssignmentId = sender.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                TokenHash = Access.Hash(Access.Token()),
            }
        );
    }
}
