using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static partial class CommunicationEndpoints
{
    private static bool SecureEqual(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b)
        );

    private static void MapWebhooks(RouteGroupBuilder api)
    {
        api.MapGet(
            "/unsubscribe",
            async (HttpRequest req, CommunicationService s) =>
            {
                await UnsubscribeMail(req.Query["token"].ToString(), s);
                var token = System.Net.WebUtility.HtmlEncode(req.Query["token"].ToString());
                return Results.Content(
                    "<!doctype html><html><head><meta name=\"viewport\" content=\"width=device-width\"><title>Communication preferences</title></head><body style=\"font:16px system-ui;max-width:540px;margin:10vh auto;padding:24px\"><h1>Stop optional messages</h1><p>Confirm to stop optional promotional messages on this channel. Operational preferences remain separate.</p><form method=\"post\" action=\"/api/v1/communications/unsubscribe\"><input type=\"hidden\" name=\"token\" value=\""
                        + token
                        + "\"><button>Stop optional messages</button></form></body></html>",
                    "text/html"
                );
            }
        );
        api.MapPost(
            "/unsubscribe",
            async (HttpRequest req, CommunicationService s) =>
            {
                var token = req.Query["token"].ToString();
                if (token == "" && req.HasFormContentType)
                    token = (await req.ReadFormAsync())["token"].ToString();
                var mail = await UnsubscribeMail(token, s);
                Check(mail.UserId != null, "No linked preference profile.", 409);
                var pref = await s.Db.CommunicationPreferences.SingleOrDefaultAsync(p =>
                    p.UserId == mail.UserId && p.Channel == mail.Channel
                );
                if (pref == null)
                {
                    pref = new() { UserId = mail.UserId.Value, Channel = mail.Channel };
                    s.Db.CommunicationPreferences.Add(pref);
                }
                pref.Promotional = false;
                pref.UpdatedAt = DateTimeOffset.UtcNow;
                s.Db.Set<AuthAudit>()
                    .Add(new() { ActorId = mail.UserId, Action = "communications.unsubscribe" });
                await s.Db.SaveChangesAsync();
                return Results.Content(
                    "<!doctype html><html><body style=\"font:16px system-ui;padding:10vh\"><h1>Preference saved</h1><p>Optional promotional messages are now disabled for this channel.</p></body></html>",
                    "text/html"
                );
            }
        );
        api.MapGet(
            "/webhooks/meta/{id:guid}",
            async (Guid id, HttpRequest req, CommunicationService s) =>
            {
                var a = await s.Db.CommunicationAccounts.SingleOrDefaultAsync(x =>
                    x.Id == id && x.Provider == "Meta" && x.Enabled
                );
                Check(
                    a != null
                        && a.VerifyTokenHash != ""
                        && req.Query["hub.mode"] == "subscribe"
                        && SecureEqual(
                            Hash(req.Query["hub.verify_token"].ToString()),
                            a.VerifyTokenHash
                        ),
                    "Webhook verification failed.",
                    403
                );
                return Results.Text(req.Query["hub.challenge"].ToString());
            }
        );
        api.MapPost(
            "/webhooks/meta/{id:guid}",
            async (Guid id, HttpRequest req, CommunicationService s) =>
            {
                var a = await s.Db.CommunicationAccounts.SingleOrDefaultAsync(x =>
                    x.Id == id && x.Provider == "Meta" && x.Enabled && x.Verified
                );
                Check(a != null && a.WebhookSecret != "", "Webhook unavailable.", 404);
                using var reader = new StreamReader(req.Body, Encoding.UTF8);
                var raw = await reader.ReadToEndAsync();
                var expected =
                    "sha256="
                    + Convert
                        .ToHexString(
                            HMACSHA256.HashData(
                                Encoding.UTF8.GetBytes(s.Unprotect(a.WebhookSecret)),
                                Encoding.UTF8.GetBytes(raw)
                            )
                        )
                        .ToLowerInvariant();
                Check(
                    SecureEqual(expected, req.Headers["X-Hub-Signature-256"].ToString()),
                    "Invalid webhook signature.",
                    403
                );
                using var j = JsonDocument.Parse(raw);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"CommunicationAccounts\" WHERE \"Id\"={id} FOR NO KEY UPDATE"
                );
                var rootAccount = a;
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({"communications.meta:" + a.TenantId + Hash(s.Unprotect(a.WebhookSecret))},0))"
                );
                var processed = 0;
                foreach (var entry in j.RootElement.GetProperty("entry").EnumerateArray())
                foreach (var change in entry.GetProperty("changes").EnumerateArray())
                {
                    var value = change.GetProperty("value");
                    a = rootAccount;
                    var phone = value
                        .GetProperty("metadata")
                        .GetProperty("phone_number_id")
                        .GetString();
                    if (phone != a.Reference)
                    {
                        var candidates = await s
                            .Db.CommunicationAccounts.Where(x =>
                                x.TenantId == rootAccount.TenantId
                                && x.Provider == "Meta"
                                && x.Reference == phone
                                && x.Enabled
                                && x.Verified
                                && x.WebhookSecret != ""
                            )
                            .ToListAsync();
                        a = candidates.FirstOrDefault(x =>
                            SecureEqual(
                                s.Unprotect(x.WebhookSecret),
                                s.Unprotect(rootAccount.WebhookSecret)
                            )
                        );
                        if (a == null)
                            continue;
                    }
                    if (value.TryGetProperty("statuses", out var statuses))
                        foreach (var status in statuses.EnumerateArray())
                        {
                            var mid = status.GetProperty("id").GetString()!;
                            var state = status.GetProperty("status").GetString()!;
                            var time = status.GetProperty("timestamp").GetString()!;
                            var key = Hash(mid + ":" + state + ":" + time);
                            if (
                                await s.Db.CommunicationProviderEvents.AnyAsync(e =>
                                    e.AccountId == a.Id && e.EventKey == key
                                )
                            )
                                continue;
                            s.Db.CommunicationProviderEvents.Add(
                                new()
                                {
                                    TenantId = a.TenantId,
                                    AccountId = a.Id,
                                    EventKey = key,
                                    ProviderId = mid,
                                    Status = state,
                                }
                            );
                            await ApplyProviderStatus(a, mid, state, s);
                            await s.Db.SaveChangesAsync();
                            processed++;
                        }
                    if (a.Inbound && value.TryGetProperty("messages", out var messages))
                        foreach (var m in messages.EnumerateArray())
                        {
                            var mid = m.GetProperty("id").GetString()!;
                            var key = Hash("inbound:" + mid);
                            if (
                                await s.Db.CommunicationProviderEvents.AnyAsync(e =>
                                    e.AccountId == a.Id && e.EventKey == key
                                )
                            )
                                continue;
                            var from = "+" + m.GetProperty("from").GetString()!.TrimStart('+');
                            var text = m.TryGetProperty("text", out var t)
                                ? t.GetProperty("body").GetString()!
                                : "[Unsupported inbound media; retrieve in the authorized provider console]";
                            if (
                                m.TryGetProperty("type", out var mediaKind)
                                && mediaKind.GetString() is "image" or "document"
                            )
                            {
                                var media = m.GetProperty(mediaKind.GetString()!);
                                text = media.TryGetProperty("caption", out var caption)
                                    ? caption.GetString()!
                                    : "[WhatsApp " + mediaKind.GetString() + " attachment]";
                            }
                            s.Db.CommunicationProviderEvents.Add(
                                new()
                                {
                                    TenantId = a.TenantId,
                                    AccountId = a.Id,
                                    EventKey = key,
                                    ProviderId = mid,
                                    Status = "Received",
                                }
                            );
                            Check(
                                m.TryGetProperty("timestamp", out var timeElement)
                                    && long.TryParse(timeElement.GetString(), out _),
                                "Inbound timestamp required."
                            );
                            var received = DateTimeOffset.FromUnixTimeSeconds(
                                long.Parse(timeElement.GetString()!)
                            );
                            Check(
                                received <= DateTimeOffset.UtcNow.AddMinutes(5),
                                "Inbound timestamp is in the future."
                            );
                            await Inbound(a, from, text, mid, s, received);
                            if (
                                m.TryGetProperty("type", out var kind)
                                && kind.GetString() is "image" or "document"
                            )
                            {
                                var item = m.GetProperty(kind.GetString()!);
                                var mediaId = item.GetProperty("id").GetString()!;
                                Check(
                                    mediaId.Length <= 200
                                        && System.Text.RegularExpressions.Regex.IsMatch(
                                            mediaId,
                                            @"^[a-zA-Z0-9_.-]+$"
                                        ),
                                    "Invalid media reference."
                                );
                                var message = s
                                    .Db.ChangeTracker.Entries<CommunicationMessage>()
                                    .Select(e => e.Entity)
                                    .Last(x => x.ProviderId == mid);
                                s.Db.CommunicationAttachments.Add(
                                    new()
                                    {
                                        TenantId = a.TenantId,
                                        MessageId = message.Id,
                                        ProviderAccountId = a.Id,
                                        ProviderMediaId = mediaId,
                                        Status = "Provider pending",
                                        Name = item.TryGetProperty("filename", out var fn)
                                            ? Path.GetFileName(fn.GetString()!)
                                            : "WhatsApp image.jpg",
                                        Type = item.TryGetProperty("mime_type", out var mt)
                                            ? mt.GetString()!
                                            : "image/jpeg",
                                    }
                                );
                            }

                            await s.Db.SaveChangesAsync();
                            processed++;
                        }
                }
                await tx.CommitAsync();
                return Results.Ok(new { processed });
            }
        );
        api.MapPost(
            "/webhooks/twilio/{id:guid}",
            async (Guid id, HttpRequest req, CommunicationService s) =>
            {
                var a = await s.Db.CommunicationAccounts.SingleOrDefaultAsync(a =>
                    a.Id == id && a.Provider == "Twilio" && a.Verified && a.Enabled
                );
                Check(a != null && a.CallbackUrl != "", "Webhook unavailable.", 404);
                var form = await req.ReadFormAsync();
                var data =
                    a.CallbackUrl
                    + string.Concat(
                        form.OrderBy(x => x.Key, StringComparer.Ordinal)
                            .SelectMany(x =>
                                x.Value.Order(StringComparer.Ordinal).Select(v => x.Key + v)
                            )
                    );
                var signature = Convert.ToBase64String(
                    HMACSHA1.HashData(
                        Encoding.UTF8.GetBytes(s.Unprotect(a.Secret)),
                        Encoding.UTF8.GetBytes(data)
                    )
                );
                Check(
                    SecureEqual(signature, req.Headers["X-Twilio-Signature"].ToString()),
                    "Invalid webhook signature.",
                    403
                );
                Check(form["AccountSid"] == a.Reference, "Wrong provider account.", 403);
                var mid = form["MessageSid"].ToString();
                var status = form["MessageStatus"].ToString();
                var inbound = form.ContainsKey("Body");
                var key = Hash(mid + ":" + (inbound ? "inbound" : status));
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"CommunicationAccounts\" WHERE \"Id\"={id} FOR NO KEY UPDATE"
                );
                if (
                    !await s.Db.CommunicationProviderEvents.AnyAsync(e =>
                        e.AccountId == id && e.EventKey == key
                    )
                )
                {
                    s.Db.CommunicationProviderEvents.Add(
                        new()
                        {
                            TenantId = a.TenantId,
                            AccountId = id,
                            EventKey = key,
                            ProviderId = mid,
                            Status = inbound ? "Received" : status,
                            Error = form["ErrorCode"].ToString(),
                        }
                    );
                    if (inbound)
                    {
                        Check(
                            a.Inbound && form["To"] == a.Sender,
                            "Inbound SMS not enabled for this number.",
                            409
                        );
                        await Inbound(a, form["From"].ToString(), form["Body"].ToString(), mid, s);
                    }
                    else
                        await ApplyProviderStatus(a, mid, status, s);
                    await s.Db.SaveChangesAsync();
                }
                await tx.CommitAsync();
                return Results.Text("<Response/>", "application/xml");
            }
        );
        // SMTP has no authenticated delivery/bounce feed. Deployment can enable a signed adapter callback.
        api.MapPost(
            "/webhooks/email/{id:guid}",
            async (Guid id, HttpRequest req, CommunicationService s) =>
            {
                var a = await s.Db.CommunicationAccounts.SingleOrDefaultAsync(a =>
                    a.Id == id && a.Channel == "Email" && a.Enabled && a.Verified
                );
                Check(a != null && a.WebhookSecret != "", "Webhook unavailable.", 404);
                var raw = await new StreamReader(req.Body).ReadToEndAsync();
                var stamp = req.Headers["X-Communication-Timestamp"].ToString();
                Check(
                    long.TryParse(stamp, out var seconds)
                        && Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) <= 300,
                    "Webhook timestamp expired.",
                    403
                );
                var signature = Convert.ToHexString(
                    HMACSHA256.HashData(
                        Encoding.UTF8.GetBytes(s.Unprotect(a.WebhookSecret)),
                        Encoding.UTF8.GetBytes(stamp + "." + raw)
                    )
                );
                Check(
                    SecureEqual(signature, req.Headers["X-Communication-Signature"].ToString()),
                    "Invalid webhook signature.",
                    403
                );
                using var j = JsonDocument.Parse(raw);
                var key = j.RootElement.GetProperty("id").GetString()!;
                Check(key.Length is > 0 and <= 200, "Invalid provider event.");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"CommunicationAccounts\" WHERE \"Id\"={id} FOR NO KEY UPDATE"
                );
                if (
                    !await s.Db.CommunicationProviderEvents.AnyAsync(e =>
                        e.AccountId == id && e.EventKey == key
                    )
                )
                {
                    var mid = j.RootElement.GetProperty("messageId").GetString()!;
                    var status = j.RootElement.GetProperty("status").GetString()!;
                    Check(
                        new[] { "delivered", "bounced", "complaint" }.Contains(status),
                        "Invalid email event."
                    );
                    s.Db.CommunicationProviderEvents.Add(
                        new()
                        {
                            TenantId = a.TenantId,
                            AccountId = id,
                            EventKey = key,
                            ProviderId = mid,
                            Status = status,
                        }
                    );
                    await ApplyProviderStatus(a, mid, status, s);
                    await s.Db.SaveChangesAsync();
                }
                await tx.CommitAsync();
                return Results.Ok();
            }
        );
    }

    private static async Task<Mail> UnsubscribeMail(string token, CommunicationService s)
    {
        string data;
        try
        {
            data = s.Unprotect(token);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            throw new ApiError(403, "Unsubscribe link invalid or expired.");
        }
        var parts = data.Split('|');
        Check(
            parts.Length == 2
                && Guid.TryParse(parts[0], out _)
                && long.TryParse(parts[1], out var expiry)
                && expiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            "Unsubscribe link invalid or expired.",
            403
        );
        var id = Guid.Parse(parts[0]);
        var mail = await s.Db.Mails.SingleOrDefaultAsync(m =>
            m.Id == id && m.Purpose == "Promotional" && m.Kind.StartsWith("communication.")
        );
        Check(mail != null, "Optional communication unavailable.", 404);
        return mail;
    }

    private static async Task ApplyProviderStatus(
        CommunicationAccount a,
        string id,
        string status,
        CommunicationService s
    )
    {
        var mail = await s.Db.Mails.SingleOrDefaultAsync(m =>
            m.AccountId == a.Id && m.ProviderId == id
        );
        if (mail == null)
            return;
        var mapped = status switch
        {
            "delivered" => "Delivered",
            "read" => "Read",
            "sent" => "Sent",
            "failed" or "undelivered" => "Failed",
            "bounced" => "Bounced",
            "complaint" => "Complaint",
            _ => "Accepted",
        };
        var rank = new Dictionary<string, int>
        {
            { "Pending", 0 },
            { "Accepted", 1 },
            { "Sent", 2 },
            { "Delivered", 3 },
            { "Read", 4 },
        };
        if (
            mapped is "Failed" or "Bounced" or "Complaint"
            || rank.GetValueOrDefault(mapped) > rank.GetValueOrDefault(mail.DeliveryStatus)
        )
            mail.DeliveryStatus = mapped;
        if (mapped is "Bounced" or "Complaint")
        {
            var hash = Hash(mail.Recipient.ToLowerInvariant());
            if (
                !await s.Db.CommunicationSuppressions.AnyAsync(x =>
                    x.TenantId == a.TenantId && x.Channel == a.Channel && x.AddressHash == hash
                )
            )
                s.Db.CommunicationSuppressions.Add(
                    new()
                    {
                        TenantId = a.TenantId,
                        Channel = a.Channel,
                        AddressHash = hash,
                        Reason = mapped,
                    }
                );
        }
        if (mail.CommunicationId != null)
        {
            var cid = await s
                .Db.CommunicationMessages.Where(m => m.Id == mail.CommunicationId)
                .Select(m => m.ConversationId)
                .SingleAsync();
            if (cid != null)
                await s
                    .Db.Conversations.Where(c => c.Id == cid)
                    .ExecuteUpdateAsync(x =>
                        x.SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow)
                    );
        }
    }

    private static async Task Inbound(
        CommunicationAccount a,
        string address,
        string body,
        string providerId,
        CommunicationService s,
        DateTimeOffset? received = null
    )
    {
        Check(body.Length <= 12000, "Inbound message too large.");
        var hash = Hash(address.ToLowerInvariant());
        if (new[] { "STOP", "UNSUBSCRIBE", "CANCEL" }.Contains(body.Trim().ToUpperInvariant()))
        {
            if (
                !await s.Db.CommunicationSuppressions.AnyAsync(x =>
                    x.TenantId == a.TenantId && x.Channel == a.Channel && x.AddressHash == hash
                )
            )
                s.Db.CommunicationSuppressions.Add(
                    new()
                    {
                        TenantId = a.TenantId,
                        Channel = a.Channel,
                        AddressHash = hash,
                        Reason = "OptOut",
                    }
                );
        }
        var c = await s.Db.Conversations.SingleOrDefaultAsync(c =>
            c.AccountId == a.Id && c.ExternalThread == address
        );
        if (c == null)
        {
            c = new()
            {
                TenantId = a.TenantId,
                OrgId = a.OrgId,
                AccountId = a.Id,
                Channel = a.Channel,
                Kind = "Support",
                Title = "Inbound " + a.Channel + " inquiry",
                ExternalThread = address,
            };
            s.Db.Conversations.Add(c);
            var pid = await s
                .Db.PersonContacts.Where(p =>
                    p.Verified
                    && p.Kind == "Phone"
                    && p.NormalizedValue == address
                    && s.Db.Memberships.Any(m =>
                        m.PersonId == p.PersonId && m.TenantId == a.TenantId
                    )
                )
                .Select(p => (Guid?)p.PersonId)
                .FirstOrDefaultAsync();
            if (pid != null)
            {
                var uid = await s
                    .Db.Persons.Where(p => p.Id == pid)
                    .Select(p => p.UserId)
                    .SingleAsync();
                if (uid != null)
                    s.Db.ConversationParticipants.Add(
                        new()
                        {
                            TenantId = a.TenantId,
                            ConversationId = c.Id,
                            UserId = uid.Value,
                        }
                    );
            }
        }
        if (c.LastInboundAt == null || (received ?? DateTimeOffset.UtcNow) > c.LastInboundAt)
            c.LastInboundAt = received ?? DateTimeOffset.UtcNow;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        c.Preview = body[..Math.Min(body.Length, 160)];
        c.Version++;
        s.Db.CommunicationMessages.Add(
            new()
            {
                TenantId = a.TenantId,
                OrgId = a.OrgId,
                ConversationId = c.Id,
                Body = body,
                Direction = "Inbound",
                ClientKey = "provider:" + providerId,
                ProviderId = providerId,
            }
        );
    }
}
