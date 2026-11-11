using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public class CommunicationStorage(IConfiguration config, IHostEnvironment env)
{
    public string Root =>
        Path.GetFullPath(
            config["Communications:StoragePath"]
                ?? Path.Combine(env.ContentRootPath, ".infra", "communication-objects")
        );

    public string PathFor(string key)
    {
        Check(Guid.TryParseExact(key, "N", out _), "Invalid object key.");
        return Path.Combine(Root, key);
    }
}

public static partial class CommunicationEndpoints
{
    private static void MapFiles(RouteGroupBuilder api)
    {
        api.MapPost(
            "/messages/{id:guid}/attachments",
            async (
                Guid id,
                HttpRequest req,
                CommunicationService s,
                CommunicationStorage storage
            ) =>
            {
                var m = await s.Db.CommunicationMessages.SingleOrDefaultAsync(m => m.Id == id);
                Check(m != null && m.ConversationId != null, "Message unavailable.", 404);
                var c = await s.Conversation(m.ConversationId.Value);
                var p = await s.Participant(c);
                Check(
                    c.Channel is "InApp" or "Email" or "WhatsApp",
                    "This adapter does not support file dispatch.",
                    409
                );
                Check(
                    m.SenderId == (await s.A.User()).Id && (!m.Internal || p.Staff),
                    "Only the sender can attach files to this message.",
                    403
                );
                Check(
                    await s.Db.CommunicationAttachments.CountAsync(f => f.MessageId == id) < 5,
                    "Maximum five attachments."
                );
                if (c.Channel != "InApp")
                    Check(
                        await s.Db.Mails.AnyAsync(o =>
                            o.CommunicationId == id && o.DeliveryStatus == "Draft"
                        ),
                        "Only an unsent draft can receive attachments.",
                        409
                    );
                if (c.Channel == "WhatsApp")
                    Check(
                        !await s.Db.CommunicationAttachments.AnyAsync(f => f.MessageId == id),
                        "WhatsApp supports one media item per message."
                    );
                var form = await req.ReadFormAsync();
                var file = form.Files.GetFile("file");
                Check(
                    file != null && file.Length is > 0 and <= 5242880,
                    "Choose a PDF, PNG or JPEG up to 5 MB."
                );
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer);
                var bytes = buffer.ToArray();
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var type =
                    bytes.AsSpan().StartsWith("%PDF-"u8) && ext == ".pdf" ? "application/pdf"
                    : bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                    && ext == ".png"
                        ? "image/png"
                    : bytes.Length > 3
                    && bytes[0] == 255
                    && bytes[1] == 216
                    && bytes[2] == 255
                    && new[] { ".jpg", ".jpeg" }.Contains(ext)
                        ? "image/jpeg"
                    : "";
                Check(type != "", "File signature/extension mismatch.");
                var key = Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(storage.Root);
                await File.WriteAllBytesAsync(storage.PathFor(key), bytes);
                var a = new CommunicationAttachment
                {
                    TenantId = m.TenantId,
                    MessageId = id,
                    Name = Path.GetFileName(file.FileName)[
                        ..Math.Min(Path.GetFileName(file.FileName).Length, 150)
                    ],
                    Type = type,
                    Size = bytes.Length,
                    StorageKey = key,
                    Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
                };
                s.Db.CommunicationAttachments.Add(a);
                c.UpdatedAt = DateTimeOffset.UtcNow;
                try
                {
                    await s.Db.SaveChangesAsync();
                }
                catch
                {
                    File.Delete(storage.PathFor(key));
                    throw;
                }
                return new
                {
                    a.Id,
                    a.Name,
                    a.Type,
                    a.Size,
                };
            }
        );
        api.MapPost(
            "/messages/{id:guid}/dispatch",
            async (Guid id, CommunicationService s) =>
            {
                var m = await s.Db.CommunicationMessages.SingleOrDefaultAsync(m => m.Id == id);
                Check(m != null && m.ConversationId != null, "Message unavailable.", 404);
                await s.Conversation(m.ConversationId.Value);
                Check(
                    m.SenderId == (await s.A.User()).Id,
                    "Only the sender can dispatch this draft.",
                    403
                );
                var mail = await s.Db.Mails.SingleOrDefaultAsync(o => o.CommunicationId == id);
                Check(
                    mail != null && mail.DeliveryStatus == "Draft",
                    "Message is not an unsent draft.",
                    409
                );
                mail.DeliveryStatus = "Pending";
                mail.NextAttemptAt = DateTimeOffset.UtcNow;
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapGet(
            "/attachments/{id:guid}",
            async (
                Guid id,
                CommunicationService s,
                CommunicationStorage storage,
                HttpResponse response
            ) =>
            {
                var a = await s.Db.CommunicationAttachments.SingleOrDefaultAsync(a =>
                    a.Id == id && a.ExpiresAt > DateTimeOffset.UtcNow && a.Status == "Stored"
                );
                Check(a != null, "Attachment expired or unavailable.", 404);
                var m = await s.Db.CommunicationMessages.SingleAsync(m => m.Id == a.MessageId);
                Check(m.ConversationId != null, "Attachment unavailable.", 404);
                var c = await s.Conversation(m.ConversationId.Value);
                var p = await s.Participant(c);
                Check(p.Staff || !m.Internal, "Private note attachment unavailable.", 404);
                response.Headers.CacheControl = "private, no-store";
                var path = storage.PathFor(a.StorageKey);
                Check(File.Exists(path), "Object is unavailable.", 404);
                return Results.File(path, a.Type, a.Name, enableRangeProcessing: true);
            }
        );
        api.MapGet(
            "/team-inbox",
            async (CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.inbox");
                return await s
                    .Db.Conversations.Where(c =>
                        c.TenantId == w.TenantId
                        && c.Kind == "Support"
                        && c.AccountId != null
                        && c.AssignedId == null
                        && s.A.Visible(w).Select(o => o.Id).Contains(c.OrgId)
                    )
                    .OrderByDescending(c => c.UpdatedAt)
                    .Take(50)
                    .Select(c => new
                    {
                        c.Id,
                        c.Title,
                        c.Channel,
                        c.Status,
                        c.UpdatedAt,
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/team-inbox/{id:guid}/claim",
            async (Guid id, CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.inbox");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var c = await s
                    .Db.Conversations.FromSqlInterpolated(
                        $"SELECT * FROM \"Conversations\" WHERE \"Id\"={id} FOR UPDATE"
                    )
                    .SingleOrDefaultAsync();
                Check(
                    c != null
                        && c.TenantId == w.TenantId
                        && c.Kind == "Support"
                        && c.AssignedId == null,
                    "Shared inquiry is unavailable.",
                    409
                );
                await s.Scope(w, c.OrgId);
                c.OwnerId = w.UserId;
                c.AssignedId = w.UserId;
                c.UpdatedAt = DateTimeOffset.UtcNow;
                c.Version++;
                s.Db.ConversationParticipants.Add(
                    new()
                    {
                        TenantId = c.TenantId,
                        ConversationId = id,
                        UserId = w.UserId,
                        Staff = true,
                    }
                );
                s.Audit(w, c.OrgId, "inbox.claimed", id);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return c;
            }
        );
    }
}
