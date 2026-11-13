using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record IntelligenceBrandingInput(
    string DisplayName,
    string Accent,
    string ClubTerm = "Club",
    string DistrictTerm = "District",
    string Logo = "",
    string Favicon = "",
    string Welcome = "Connected leadership. Meaningful impact.",
    string Languages = "en",
    string[]? DisabledFeatures = null,
    bool RemoteAiEnabled = false,
    string AiPolicy = "",
    long Version = 1
);

public record IntelligenceDomainInput(string Host);

public class TenantExperience(
    Access a,
    IConfiguration config,
    IHttpClientFactory clients,
    IHostEnvironment environment
)
{
    public static readonly string[] Features =
    [
        "assistant",
        "knowledge",
        "workflows",
        "events",
        "operations",
        "finance",
    ];
    public static readonly Dictionary<string, string[]> PermissionPrefixes = new()
    {
        ["assistant"] = ["intelligence.assistant"],
        ["knowledge"] = ["intelligence.knowledge"],
        ["workflows"] = ["intelligence.automations"],
        ["events"] = ["events."],
        ["operations"] = ["operations."],
        ["finance"] = ["finance."],
    };

    public static string Normalize(string value)
    {
        Check(
            value.Length <= 253
                && !value.Contains(':')
                && !value.Contains('/')
                && !value.Contains('*'),
            "Enter a bare public hostname."
        );
        string host;
        try
        {
            host = new IdnMapping().GetAscii(value.Trim().TrimEnd('.')).ToLowerInvariant();
        }
        catch
        {
            throw new ApiError(400, "Invalid international hostname.");
        }
        Check(
            host.Contains('.')
                && !IPAddress.TryParse(host, out _)
                && !host.EndsWith(".localhost")
                && !host.EndsWith(".local")
                && !host.EndsWith(".internal")
                && Regex.IsMatch(
                    host,
                    @"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$"
                ),
            "Invalid public hostname."
        );
        return host;
    }

    public static bool Asset(string text) =>
        text == ""
        || (text.StartsWith("/assets/") || text.StartsWith("/brand/")) && !text.Contains("..")
        || Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.Scheme == "https"
            && uri.UserInfo == "";

    public async Task<object> Public(Guid? tenantId)
    {
        if (tenantId == null)
            return new
            {
                displayName = ImpactSetup.Name,
                accent = ImpactSetup.Color,
                clubTerm = "Club",
                districtTerm = "District",
                welcome = "Connected leadership. Meaningful impact.",
                logo = "/brand/impact-logo.webp",
                favicon = "/brand/impact-logo.webp",
                languages = "en",
                customDomain = false,
            };
        var t = await a.Db.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId);
        Check(t != null, "Association unavailable.", 404);
        var b = await a.Db.TenantBrandings.SingleOrDefaultAsync(b => b.TenantId == tenantId);
        return new
        {
            displayName = ImpactSetup.Name,
            accent = b?.Accent ?? t.Color,
            clubTerm = b?.ClubTerm ?? "Club",
            districtTerm = b?.DistrictTerm ?? "District",
            welcome = b?.Welcome ?? "Connected leadership. Meaningful impact.",
            logo = "/brand/impact-logo.webp",
            favicon = "/brand/impact-logo.webp",
            languages = b?.Languages ?? t.Language,
            customDomain = true,
        };
    }

    public async Task<TenantBranding> Save(IntelligenceBrandingInput i)
    {
        Check(i.DisplayName == ImpactSetup.Name, "The organization name is Impact International.");
        var w = await a.TenantWorkspace("settings.manage");
        var b = await a.Db.TenantBrandings.SingleOrDefaultAsync(x => x.TenantId == w.TenantId);
        Check(
            i.DisplayName.Length is > 0 and <= 120
                && Regex.IsMatch(i.Accent, "^#[A-Fa-f0-9]{6}$")
                && i.ClubTerm.Length is > 0 and <= 40
                && i.DistrictTerm.Length is > 0 and <= 40
                && i.Welcome.Length <= 250
                && i.Logo.Length <= 1000
                && i.Favicon.Length <= 1000
                && Asset(i.Logo)
                && Asset(i.Favicon)
                && i.Languages.Length <= 100
                && (i.DisabledFeatures ?? []).All(Features.Contains)
                && (!i.RemoteAiEnabled || i.AiPolicy.Length is >= 20 and <= 2000),
            "Invalid branding, feature or AI policy configuration."
        );
        Check(
            !Regex.IsMatch(i.DisplayName + i.ClubTerm + i.DistrictTerm + i.Welcome, "[<>]"),
            "Branding fields must be plain text."
        );
        if (b == null)
        {
            b = new() { TenantId = w.TenantId };
            a.Db.TenantBrandings.Add(b);
        }
        else
            Check(b.Version == i.Version, "Branding changed. Refresh.", 409);
        b.DisplayName = i.DisplayName;
        b.Accent = i.Accent;
        b.ClubTerm = i.ClubTerm;
        b.DistrictTerm = i.DistrictTerm;
        b.Logo = i.Logo;
        b.Favicon = i.Favicon;
        b.Welcome = i.Welcome;
        b.Languages = i.Languages;
        b.DisabledFeatures = GovernanceService.Pack(i.DisabledFeatures ?? []);
        b.RemoteAiEnabled = i.RemoteAiEnabled;
        b.AiPolicy = i.AiPolicy;
        b.Version++;
        await a.Log(
            w,
            "tenant.branding",
            "tenant",
            "Branding/features/policy updated; secrets and policy text omitted from audit."
        );
        return b;
    }

    public async Task<object> Verify(Guid id)
    {
        var w = await a.TenantWorkspace("settings.manage");
        var d = await a.Db.TenantDomains.SingleOrDefaultAsync(d =>
            d.Id == id && d.TenantId == w.TenantId
        );
        Check(d != null, "Domain unavailable.", 404);
        Check(d.Status != "Active", "Active domain ownership is already recorded.", 409);
        Check(
            d.LastCheckedAt == null || d.LastCheckedAt < DateTimeOffset.UtcNow.AddSeconds(-30),
            "DNS checks are limited to once every 30 seconds.",
            429
        );
        var endpoint = "https://dns.google/resolve";
        if (config["Domains:TestDnsEndpoint"] is string test)
        {
            Check(
                environment.IsDevelopment()
                    && Uri.TryCreate(test, UriKind.Absolute, out var uri)
                    && uri.IsLoopback,
                "DNS test endpoints are Development loopback only.",
                403
            );
            endpoint = test;
        }
        using var response = await clients
            .CreateClient("domain-dns")
            .GetAsync(
                endpoint
                    + "?name="
                    + Uri.EscapeDataString("_clubs-verification." + d.Host)
                    + "&type=TXT"
            );
        Check(
            response.IsSuccessStatusCode,
            "DNS verifier unavailable; ownership is unchanged.",
            502
        );
        var json = await response.Content.ReadAsStringAsync();
        Check(json.Length <= 64000, "DNS response exceeded limit.", 502);
        using var doc = JsonDocument.Parse(json);
        var verified =
            doc.RootElement.TryGetProperty("Answer", out var answers)
            && answers
                .EnumerateArray()
                .Any(a =>
                    a.GetProperty("type").GetInt32() == 16
                    && a.TryGetProperty("data", out var data)
                    && data.GetString()?.Replace("\"", "") == "clubs-verification=" + d.Challenge
                );
        d.LastCheckedAt = DateTimeOffset.UtcNow;
        if (verified)
        {
            d.Status = "Verified";
            d.VerifiedAt = DateTimeOffset.UtcNow;
        }
        d.Version++;
        await a.Log(
            w,
            "tenant.domain.verify",
            d.Id.ToString(),
            verified
                ? "DNS ownership verified; TLS activation remains separate"
                : "DNS challenge not found"
        );
        return new
        {
            domain = d,
            verified,
            activation = "Ownership verification does not provision TLS, DNS routing or ingress. A platform operator must activate a pre-provisioned host.",
        };
    }

    public bool Provisioned(string host) =>
        config
            .GetSection("Domains:ProvisionedHosts")
            .Get<string[]>()
            ?.Contains(host, StringComparer.OrdinalIgnoreCase) == true;
}

public static class TenantHostBinding
{
    public static async Task Bind(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }
        var db = context.RequestServices.GetRequiredService<Db>();
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var host = context.Request.Host.Host.ToLowerInvariant();
        var domain = await db
            .TenantDomains.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Host == host);
        if (domain != null)
        {
            var configured = config.GetSection("Domains:ProvisionedHosts").Get<string[]>() ?? [];
            if (
                domain.Status != "Active"
                || domain.VerifiedAt == null
                || !configured.Contains(host, StringComparer.OrdinalIgnoreCase)
            )
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsJsonAsync(
                    new { detail = "Custom domain is not activated on this deployment." }
                );
                return;
            }
            context.Items["tenant-domain"] = domain.TenantId;
        }
        await next(context);
    }
}
