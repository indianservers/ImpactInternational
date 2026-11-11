using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record ProviderResult(string Id, string Status, decimal? Cost = null, string Currency = "");

public interface ICommunicationProvider
{
    string Name { get; }
    Task<ProviderResult> Send(
        CommunicationAccount account,
        Mail mail,
        CommunicationService s,
        CancellationToken token
    );
}

public class MetaCommunicationProvider(
    IHttpClientFactory factory,
    IConfiguration config,
    IHostEnvironment env,
    CommunicationStorage storage
) : ICommunicationProvider
{
    public string Name => "Meta";
    public string BaseUrl =>
        env.IsDevelopment() && config["Communications:MockBase"] is string mock
            ? mock.TrimEnd('/') + "/meta"
            : "https://graph.facebook.com/"
                + (
                    config["Communications:MetaVersion"]
                    ?? throw new ApiError(409, "Configure a supported Meta Graph API version.")
                );

    public async Task<ProviderResult> Send(
        CommunicationAccount account,
        Mail mail,
        CommunicationService s,
        CancellationToken token
    )
    {
        var parameters = JsonSerializer.Deserialize<string[]>(mail.TemplateParameters) ?? [];
        var template = new Dictionary<string, object>
        {
            { "name", mail.TemplateName },
            { "language", new { code = mail.Language } },
        };
        if (parameters.Length > 0)
            template["components"] = new[]
            {
                new
                {
                    type = "body",
                    parameters = parameters
                        .Select(value => new { type = "text", text = value })
                        .ToArray(),
                },
            };
        object payload =
            mail.TemplateName != ""
                ? new
                {
                    messaging_product = "whatsapp",
                    to = mail.Recipient.TrimStart('+'),
                    type = "template",
                    template,
                }
                : new
                {
                    messaging_product = "whatsapp",
                    to = mail.Recipient.TrimStart('+'),
                    type = "text",
                    text = new { body = mail.Body },
                };
        var file =
            mail.CommunicationId == null
                ? null
                : await s.Db.CommunicationAttachments.FirstOrDefaultAsync(
                    f =>
                        f.MessageId == mail.CommunicationId
                        && f.Status == "Stored"
                        && f.ExpiresAt > DateTimeOffset.UtcNow,
                    token
                );
        if (file != null)
        {
            Check(mail.TemplateName == "", "Media replies require the active service window.", 409);
            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent("whatsapp"), "messaging_product");
            multipart.Add(new StringContent(file.Type), "type");
            var content = new StreamContent(File.OpenRead(storage.PathFor(file.StorageKey)));
            content.Headers.ContentType = new MediaTypeHeaderValue(file.Type);
            multipart.Add(content, "file", file.Name);
            using var upload = new HttpRequestMessage(
                HttpMethod.Post,
                BaseUrl + "/" + Uri.EscapeDataString(account.Reference) + "/media"
            )
            {
                Content = multipart,
            };
            upload.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                s.Unprotect(account.Secret)
            );
            using var uploadResponse = await factory
                .CreateClient("communications")
                .SendAsync(upload, token);
            if (!uploadResponse.IsSuccessStatusCode)
                throw new ProviderFailure((int)uploadResponse.StatusCode, "media-upload");
            using var uploadJson = JsonDocument.Parse(
                await uploadResponse.Content.ReadAsStringAsync(token)
            );
            var media = new Dictionary<string, object>
            {
                { "id", uploadJson.RootElement.GetProperty("id").GetString()! },
                { "caption", mail.Body },
            };
            var kind = file.Type == "application/pdf" ? "document" : "image";
            if (kind == "document")
                media["filename"] = file.Name;
            payload = new Dictionary<string, object>
            {
                { "messaging_product", "whatsapp" },
                { "to", mail.Recipient.TrimStart('+') },
                { "type", kind },
                { kind, media },
            };
        }
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            BaseUrl + "/" + Uri.EscapeDataString(account.Reference) + "/messages"
        )
        {
            Content = JsonContent.Create(payload),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            s.Unprotect(account.Secret)
        );
        using var response = await factory.CreateClient("communications").SendAsync(req, token);
        if (!response.IsSuccessStatusCode)
            throw new ProviderFailure(
                (int)response.StatusCode,
                ((int)response.StatusCode).ToString()
            );
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return new(
            json.RootElement.GetProperty("messages")[0].GetProperty("id").GetString()!,
            "Accepted"
        );
    }
}

public class TwilioCommunicationProvider(
    IHttpClientFactory factory,
    IConfiguration config,
    IHostEnvironment env
) : ICommunicationProvider
{
    public string Name => "Twilio";
    public string BaseUrl =>
        env.IsDevelopment() && config["Communications:MockBase"] is string mock
            ? mock.TrimEnd('/') + "/twilio"
            : "https://api.twilio.com/2010-04-01";

    public async Task<ProviderResult> Send(
        CommunicationAccount account,
        Mail mail,
        CommunicationService s,
        CancellationToken token
    )
    {
        var fields = new Dictionary<string, string>
        {
            { "To", mail.Recipient },
            { "From", account.Sender },
            { "Body", mail.Body },
        };
        if (account.CallbackUrl != "")
            fields["StatusCallback"] = account.CallbackUrl;
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            BaseUrl + "/Accounts/" + Uri.EscapeDataString(account.Reference) + "/Messages.json"
        )
        {
            Content = new FormUrlEncodedContent(fields),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(account.Reference + ":" + s.Unprotect(account.Secret))
            )
        );
        using var response = await factory.CreateClient("communications").SendAsync(req, token);
        if (!response.IsSuccessStatusCode)
            throw new ProviderFailure(
                (int)response.StatusCode,
                ((int)response.StatusCode).ToString()
            );
        using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return new(data.RootElement.GetProperty("sid").GetString()!, "Accepted");
    }
}

public class ProviderFailure(int status, string code) : Exception(code)
{
    public int Status => status;
}

public record AccountInput(
    Guid OrgId,
    string Name,
    string Channel,
    string Provider,
    string Sender,
    string Reference = "",
    string BusinessId = "",
    string Secret = "",
    string WebhookSecret = "",
    string VerifyToken = "",
    string CallbackUrl = "",
    bool Enabled = false,
    bool Inbound = false,
    int PerMinute = 30,
    long Version = 1
);

public record PreferenceInput(
    string Channel,
    bool Transactional = true,
    bool Promotional = false,
    bool Direct = true,
    bool Birthday = false,
    bool QuietEnabled = false,
    int QuietStart = 22,
    int QuietEnd = 8
);

public record PolicyInput(
    bool MemberDirect = true,
    int ApprovalThreshold = 1,
    int RetentionDays = 365
);

public static partial class CommunicationEndpoints
{
    public static void MapCommunications(this RouteGroupBuilder root)
    {
        var api = root.MapGroup("/communications").RequireRateLimiting("events");
        api.AddEndpointFilter(
            async (ctx, next) =>
            {
                foreach (
                    var arg in ctx.Arguments.Where(x => x?.GetType().Name.EndsWith("Input") == true)
                )
                foreach (
                    var p in arg!
                        .GetType()
                        .GetProperties()
                        .Where(p => p.PropertyType == typeof(string))
                )
                    Check(p.GetValue(arg) != null, "Missing field: " + p.Name);
                return await next(ctx);
            }
        );
        MapConversations(api);
        MapBroadcasts(api);
        MapFiles(api);
        MapWebhooks(api);
        api.MapGet(
            "/capabilities",
            async (CommunicationService s, IConfiguration cfg) =>
            {
                var w = await s.A.Workspace("communications.view");
                var grants = await s.A.Grants(w);
                return new
                {
                    grants,
                    channels = new[]
                    {
                        new
                        {
                            name = "InApp",
                            outbound = true,
                            inbound = true,
                            readReceipts = true,
                            configured = true,
                        },
                        new
                        {
                            name = "Email",
                            outbound = true,
                            inbound = false,
                            readReceipts = false,
                            configured = !string.IsNullOrEmpty(cfg["Smtp:Host"]),
                        },
                        new
                        {
                            name = "WhatsApp",
                            outbound = true,
                            inbound = true,
                            readReceipts = true,
                            configured = await s.Db.CommunicationAccounts.AnyAsync(a =>
                                a.TenantId == w.TenantId
                                && a.Channel == "WhatsApp"
                                && a.Enabled
                                && a.Verified
                            ),
                        },
                        new
                        {
                            name = "SMS",
                            outbound = true,
                            inbound = await s.Db.CommunicationAccounts.AnyAsync(a =>
                                a.TenantId == w.TenantId
                                && a.Channel == "SMS"
                                && a.Inbound
                                && a.Enabled
                                && a.Verified
                            ),
                            readReceipts = false,
                            configured = await s.Db.CommunicationAccounts.AnyAsync(a =>
                                a.TenantId == w.TenantId
                                && a.Channel == "SMS"
                                && a.Enabled
                                && a.Verified
                            ),
                        },
                    },
                    ai = new
                    {
                        enabled = false,
                        reason = "No authorized AI provider configured. Private conversations are not sent externally.",
                    },
                    scope = new { w.OrgId, w.TenantId },
                    providerNotice = "External channels require verified credentials and sender configuration. SMTP acceptance does not prove inbox delivery.",
                };
            }
        );
        api.MapGet(
            "/organizations",
            async (CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.view");
                return await s
                    .A.Visible(w)
                    .OrderBy(o => o.Name)
                    .Take(100)
                    .Select(o => new
                    {
                        o.Id,
                        o.Name,
                        o.Code,
                    })
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/options",
            async (CommunicationService s) =>
            {
                var w = await s.A.WorkspaceAny("communications.manage", "communications.inbox");
                return new
                {
                    categories = await s
                        .Db.MembershipCategories.Where(c => c.TenantId == w.TenantId && c.Active)
                        .Select(c => new { c.Id, c.Name })
                        .ToListAsync(),
                    positions = await s
                        .Db.OfficerPositions.Where(p => p.TenantId == w.TenantId && p.Active)
                        .Select(p => new { p.Id, p.Title })
                        .Take(100)
                        .ToListAsync(),
                    committees = await s
                        .Db.Committees.Where(c =>
                            c.TenantId == w.TenantId
                            && s.A.Visible(w).Select(o => o.Id).Contains(c.OrgId)
                        )
                        .Select(c => new { c.Id, c.Name })
                        .Take(100)
                        .ToListAsync(),
                    events = await s
                        .Db.Events.Where(e =>
                            e.TenantId == w.TenantId
                            && s.A.Visible(w).Select(o => o.Id).Contains(e.OrgId)
                        )
                        .OrderByDescending(e => e.StartsAt)
                        .Select(e => new { e.Id, e.Title })
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/notifications",
            async (CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.view");
                var u = await s.A.User();
                return await s
                    .Db.Mails.Where(m =>
                        m.TenantId == w.TenantId
                        && m.UserId == u.Id
                        && m.Channel == "InApp"
                        && m.DeliveryStatus == "Delivered"
                    )
                    .OrderByDescending(m => m.SentAt)
                    .Take(50)
                    .Select(m => new
                    {
                        m.Id,
                        m.Subject,
                        m.Body,
                        at = m.SentAt,
                    })
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/accounts",
            async (CommunicationService s) =>
            {
                var w = await s.A.WorkspaceAny(
                    "communications.manage",
                    "communications.inbox",
                    "communications.configure"
                );
                return await s
                    .Db.CommunicationAccounts.Where(a =>
                        a.TenantId == w.TenantId
                        && s.A.Visible(w).Select(o => o.Id).Contains(a.OrgId)
                    )
                    .OrderBy(a => a.Name)
                    .Select(a => new
                    {
                        a.Id,
                        a.OrgId,
                        a.Name,
                        a.Channel,
                        a.Provider,
                        a.Sender,
                        a.Reference,
                        a.BusinessId,
                        a.CallbackUrl,
                        a.Enabled,
                        a.Verified,
                        a.Inbound,
                        a.PerMinute,
                        a.Version,
                        credentialsStored = a.Secret != "",
                        webhookSecretStored = a.WebhookSecret != "",
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/accounts",
            async (AccountInput r, CommunicationService s) => await SaveAccount(null, r, s)
        );
        api.MapPut(
            "/accounts/{id:guid}",
            async (Guid id, AccountInput r, CommunicationService s) => await SaveAccount(id, r, s)
        );
        api.MapPost(
            "/accounts/{id:guid}/verify",
            async (
                Guid id,
                CommunicationService s,
                IConfiguration cfg,
                IHttpClientFactory http,
                MetaCommunicationProvider meta,
                TwilioCommunicationProvider sms
            ) =>
            {
                var w = await s.A.TenantWorkspace("communications.configure");
                var a = await s.Db.CommunicationAccounts.SingleOrDefaultAsync(a =>
                    a.Id == id && a.TenantId == w.TenantId
                );
                Check(a != null, "Account unavailable.", 404);
                if (a.Provider == "SMTP")
                {
                    Check(
                        cfg["Smtp:Host"] != null
                            && string.Equals(
                                a.Sender,
                                cfg["Smtp:From"],
                                StringComparison.OrdinalIgnoreCase
                            ),
                        "Sender must match the deployment's verified SMTP identity.",
                        409
                    );
                    Check(
                        (cfg.GetSection("Smtp:VerifiedSenders").Get<string[]>() ?? []).Contains(
                            a.Sender,
                            StringComparer.OrdinalIgnoreCase
                        ),
                        "SMTP identity must be explicitly allowlisted by the deployment after provider/domain verification.",
                        409
                    );
                    a.Verified = true;
                }
                else
                {
                    var url =
                        a.Provider == "Meta"
                            ? meta.BaseUrl
                                + "/"
                                + Uri.EscapeDataString(a.Reference)
                                + "?fields=display_phone_number,verified_name"
                            : sms.BaseUrl
                                + "/Accounts/"
                                + Uri.EscapeDataString(a.Reference)
                                + "/IncomingPhoneNumbers.json?PhoneNumber="
                                + Uri.EscapeDataString(a.Sender);
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Authorization =
                        a.Provider == "Meta"
                            ? new AuthenticationHeaderValue("Bearer", s.Unprotect(a.Secret))
                            : new AuthenticationHeaderValue(
                                "Basic",
                                Convert.ToBase64String(
                                    Encoding.UTF8.GetBytes(
                                        a.Reference + ":" + s.Unprotect(a.Secret)
                                    )
                                )
                            );
                    using var response = await http.CreateClient("communications").SendAsync(req);
                    Check(
                        response.IsSuccessStatusCode,
                        "Provider verification failed; credentials were not exposed.",
                        409
                    );
                    using var j = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    Check(
                        a.Provider == "Meta"
                            ? j.RootElement.TryGetProperty("display_phone_number", out var number)
                                && new string(number.GetString()!.Where(char.IsDigit).ToArray())
                                    == new string(a.Sender.Where(char.IsDigit).ToArray())
                            : j
                                .RootElement.GetProperty("incoming_phone_numbers")
                                .EnumerateArray()
                                .Any(n => n.GetProperty("phone_number").GetString() == a.Sender),
                        "Provider did not verify this sender.",
                        409
                    );
                    a.Verified = true;
                }
                a.Version++;
                s.Audit(w, a.OrgId, "account.verified", id);
                await s.Db.SaveChangesAsync();
                return new
                {
                    a.Id,
                    a.Verified,
                    a.Version,
                };
            }
        );
        api.MapPost(
            "/accounts/{id:guid}/sync-templates",
            async (
                Guid id,
                CommunicationService s,
                MetaCommunicationProvider meta,
                IHttpClientFactory http
            ) =>
            {
                var w = await s.A.TenantWorkspace("communications.configure");
                var a = await s.Db.CommunicationAccounts.SingleOrDefaultAsync(a =>
                    a.Id == id && a.TenantId == w.TenantId && a.Provider == "Meta" && a.Verified
                );
                Check(a != null && a.BusinessId != "", "Verified WABA account required.", 409);
                using var req = new HttpRequestMessage(
                    HttpMethod.Get,
                    meta.BaseUrl
                        + "/"
                        + Uri.EscapeDataString(a.BusinessId)
                        + "/message_templates?limit=100"
                );
                req.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    s.Unprotect(a.Secret)
                );
                using var response = await http.CreateClient("communications").SendAsync(req);
                Check(response.IsSuccessStatusCode, "Template synchronization failed.", 409);
                using var j = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var count = 0;
                foreach (var t in j.RootElement.GetProperty("data").EnumerateArray())
                {
                    var name = t.GetProperty("name").GetString()!;
                    var lang = t.GetProperty("language").GetString()!;
                    var existing = await s.Db.CommunicationTemplates.SingleOrDefaultAsync(t =>
                        t.TenantId == w.TenantId
                        && t.AccountId == a.Id
                        && t.OrgId == a.OrgId
                        && t.ProviderName == name
                        && t.Language == lang
                    );
                    var body = t.GetProperty("components")
                        .EnumerateArray()
                        .FirstOrDefault(c => c.GetProperty("type").GetString() == "BODY");
                    if (body.ValueKind == JsonValueKind.Undefined)
                        continue;
                    var text = body.GetProperty("text").GetString()!;
                    // One body parameter is supported; incompatible templates remain unavailable.
                    var compatible = !System.Text.RegularExpressions.Regex.IsMatch(
                        text,
                        @"\{\{([2-9]|\d{2,})\}\}"
                    );
                    compatible =
                        compatible
                        && t.GetProperty("components")
                            .EnumerateArray()
                            .All(c => c.GetProperty("type").GetString() == "BODY")
                        && System
                            .Text.RegularExpressions.Regex.Matches(text, @"\{\{(.*?)\}\}")
                            .All(m => m.Groups[1].Value == "1");
                    if (existing == null)
                    {
                        existing = new()
                        {
                            TenantId = w.TenantId,
                            OrgId = a.OrgId,
                            Name = name,
                            Channel = "WhatsApp",
                            ProviderName = name,
                            AccountId = a.Id,
                            Language = lang,
                        };
                        s.Db.CommunicationTemplates.Add(existing);
                    }
                    else
                        existing.Version++;
                    existing.Body = text.Replace("{{1}}", "{{member_name}}");
                    existing.ProviderStatus = compatible
                        ? t.GetProperty("status").GetString()!
                        : "UNSUPPORTED_PARAMETERS";
                    existing.Active = compatible;
                    s.Revision(existing, existing.Version, "Template", w.UserId);
                    count++;
                }
                await s.Db.SaveChangesAsync();
                return new
                {
                    synchronized = count,
                    note = "First 100 provider templates; only zero/one body parameter text templates are currently dispatchable.",
                };
            }
        );
        api.MapGet(
            "/preferences",
            async (CommunicationService s) =>
            {
                var u = await s.A.User();
                return await s
                    .Db.CommunicationPreferences.Where(p => p.UserId == u.Id)
                    .ToListAsync();
            }
        );
        api.MapPut(
            "/preferences",
            async (PreferenceInput r, CommunicationService s) =>
            {
                var u = await s.A.User();
                Check(
                    CommunicationService.Channels.Contains(r.Channel)
                        && r.QuietStart is >= 0 and <= 23
                        && r.QuietEnd is >= 0 and <= 23,
                    "Invalid preference."
                );
                var p = await s.Db.CommunicationPreferences.SingleOrDefaultAsync(p =>
                    p.UserId == u.Id && p.Channel == r.Channel
                );
                if (p == null)
                {
                    p = new() { UserId = u.Id, Channel = r.Channel };
                    s.Db.CommunicationPreferences.Add(p);
                }
                p.Transactional = r.Transactional;
                p.Promotional = r.Promotional;
                p.Direct = r.Direct;
                p.Birthday = r.Birthday;
                p.QuietEnabled = r.QuietEnabled;
                p.QuietStart = r.QuietStart;
                p.QuietEnd = r.QuietEnd;
                p.UpdatedAt = DateTimeOffset.UtcNow;
                await s.Db.SaveChangesAsync();
                return p;
            }
        );
        api.MapGet(
            "/policy",
            async (CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.configure");
                return await s.Db.CommunicationPolicies.SingleOrDefaultAsync(p =>
                        p.OrgId == w.OrgId
                    ) ?? new() { TenantId = w.TenantId, OrgId = w.OrgId };
            }
        );
        api.MapPut(
            "/policy",
            async (PolicyInput r, CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.configure");
                Check(
                    r.ApprovalThreshold >= 1 && r.RetentionDays is >= 30 and <= 3650,
                    "Invalid policy."
                );
                var p = await s.Db.CommunicationPolicies.SingleOrDefaultAsync(p =>
                    p.OrgId == w.OrgId
                );
                if (p == null)
                {
                    p = new() { TenantId = w.TenantId, OrgId = w.OrgId };
                    s.Db.CommunicationPolicies.Add(p);
                }
                p.MemberDirect = r.MemberDirect;
                p.ApprovalThreshold = r.ApprovalThreshold;
                p.RetentionDays = r.RetentionDays;
                await s.Db.SaveChangesAsync();
                return p;
            }
        );
        api.MapGet(
            "/delivery",
            async (CommunicationService s, Guid? after = null) =>
            {
                var w = await s.A.Workspace("communications.reports");
                var q = s.Db.Mails.Where(m =>
                    m.TenantId == w.TenantId
                    && m.OrgId != null
                    && s.A.Visible(w).Select(o => o.Id).Contains(m.OrgId.Value)
                );
                if (after != null)
                    q = q.Where(m => m.Id.CompareTo(after.Value) > 0);
                return await q.OrderBy(m => m.Id)
                    .Take(51)
                    .Select(m => new
                    {
                        m.Id,
                        m.Channel,
                        m.Kind,
                        m.DeliveryStatus,
                        m.Attempts,
                        m.LastError,
                        m.NextAttemptAt,
                        m.SentAt,
                        m.DeadLetter,
                        m.Cost,
                        m.Currency,
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/delivery/{id:guid}/retry",
            async (Guid id, CommunicationService s) =>
            {
                var w = await s.A.Workspace("communications.manage");
                var m = await s.Db.Mails.SingleOrDefaultAsync(m =>
                    m.Id == id && m.TenantId == w.TenantId && m.OrgId != null
                );
                Check(m != null, "Outbox unavailable.", 404);
                await s.Scope(w, m.OrgId!.Value);
                Check(
                    m.DeadLetter && m.SentAt == null && m.DeliveryStatus != "Uncertain",
                    "Only definitively failed, unsent jobs may be retried. Reconcile uncertain provider acceptance separately.",
                    409
                );
                m.DeadLetter = false;
                m.Attempts = 0;
                m.DeliveryStatus = "Pending";
                m.NextAttemptAt = DateTimeOffset.UtcNow;
                s.Audit(w, m.OrgId.Value, "delivery.retried", id);
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
    }

    private static async Task<object> SaveAccount(Guid? id, AccountInput r, CommunicationService s)
    {
        var w = await s.A.TenantWorkspace("communications.configure");
        await s.Scope(w, r.OrgId);
        Check(
            r.Name.Length is > 1 and <= 120
                && new[] { "SMTP", "Meta", "Twilio" }.Contains(r.Provider)
                && (
                    (r.Provider == "SMTP" && r.Channel == "Email")
                    || (r.Provider == "Meta" && r.Channel == "WhatsApp")
                    || (r.Provider == "Twilio" && r.Channel == "SMS")
                )
                && r.PerMinute is > 0 and <= 1000
                && r.Sender.Length <= 150
                && r.Reference.Length <= 150
                && r.BusinessId.Length <= 150
                && r.Secret.Length <= 5000
                && r.WebhookSecret.Length <= 5000,
            "Invalid provider configuration."
        );
        if (r.Provider != "SMTP")
            Check(
                System.Text.RegularExpressions.Regex.IsMatch(r.Sender, @"^\+[1-9]\d{6,14}$"),
                "Sender must be E.164."
            );
        if (r.CallbackUrl != "")
            Check(
                Uri.TryCreate(r.CallbackUrl, UriKind.Absolute, out var url)
                    && url.Scheme == "https"
                    && url.Query == ""
                    && url.UserInfo == "",
                "Use an absolute HTTPS callback URL without query credentials."
            );
        Check(r.Provider != "SMTP" || !r.Inbound, "SMTP has no inbound reply routing.");
        if (r.Provider == "Meta" && r.Enabled)
            Check(
                !await s.Db.CommunicationAccounts.AnyAsync(a =>
                    a.TenantId == w.TenantId
                    && a.Provider == "Meta"
                    && a.Reference == r.Reference
                    && a.Enabled
                    && a.Id != id
                ),
                "An enabled connection already owns this phone number ID.",
                409
            );
        var a =
            id == null
                ? new CommunicationAccount { TenantId = w.TenantId, OrgId = r.OrgId }
                : await s.Db.CommunicationAccounts.SingleOrDefaultAsync(a =>
                    a.Id == id && a.TenantId == w.TenantId
                ) ?? throw new ApiError(404, "Account unavailable.");
        Check(id == null || a.Version == r.Version, "Account changed.", 409);
        if (id == null)
            s.Db.CommunicationAccounts.Add(a);
        else
            a.Version++;
        a.Name = r.Name;
        a.Channel = r.Channel;
        a.Provider = r.Provider;
        a.Sender = r.Sender;
        a.Reference = r.Reference;
        a.BusinessId = r.BusinessId;
        a.CallbackUrl = r.CallbackUrl;
        a.Enabled = r.Enabled;
        a.Inbound = r.Inbound;
        a.PerMinute = r.PerMinute;
        a.Verified = false;
        if (r.Secret != "")
            a.Secret = s.Protect(r.Secret);
        if (r.WebhookSecret != "")
            a.WebhookSecret = s.Protect(r.WebhookSecret);
        if (r.VerifyToken != "")
            a.VerifyTokenHash = Hash(r.VerifyToken);
        Check(a.Provider == "SMTP" || a.Secret != "", "Provider credential required.");
        s.Audit(w, a.OrgId, "account.saved", a.Id);
        await s.Db.SaveChangesAsync();
        return new
        {
            a.Id,
            a.Name,
            a.Channel,
            a.Provider,
            a.Sender,
            a.Enabled,
            a.Verified,
            a.Version,
            credentialsStored = a.Secret != "",
        };
    }
}
