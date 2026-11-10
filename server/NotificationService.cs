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
        Guid? orgId = null
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
        Guid? orgId = null
    )
    {
        var user = await db
            .Users.Where(x => x.Email == email && x.Verified)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync();
        if (user != null)
        {
            await User(user, template, detail, link, tenantId, orgId);
            return;
        }
        var text = await db.NotificationTemplates.SingleOrDefaultAsync(x => x.Id == template);
        db.Mails.Add(
            new()
            {
                TenantId = tenantId,
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
}
