using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using static Clubs.Access;

namespace Clubs;

public record FirebaseIdentity(string Subject, string Email);
public record GoogleLogin(string IdToken);

// The API owns project configuration. Never accept a project, email or role from the browser.
public sealed class FirebaseGoogleVerifier(IConfiguration config, IHttpClientFactory clients)
{
    const string KeysUrl = "https://www.googleapis.com/service_accounts/v1/jwk/securetoken@system.gserviceaccount.com";
    readonly SemaphoreSlim keyLock = new(1, 1);
    ICollection<SecurityKey>? keys;
    DateTimeOffset fetchedAt;

    public static bool Enabled(IConfiguration config) => !string.IsNullOrWhiteSpace(config["Firebase:ProjectId"])
        && !string.IsNullOrWhiteSpace(config["Firebase:ApiKey"]);

    async Task<ICollection<SecurityKey>> Keys(bool refresh = false)
    {
        await keyLock.WaitAsync();
        try
        {
            if (keys == null || fetchedAt < DateTimeOffset.UtcNow.AddMinutes(-30)
                || refresh && fetchedAt < DateTimeOffset.UtcNow.AddMinutes(-1))
            {
                var json = await clients.CreateClient("firebase-auth").GetStringAsync(KeysUrl);
                keys = new JsonWebKeySet(json).GetSigningKeys();
                fetchedAt = DateTimeOffset.UtcNow;
            }
            return keys;
        }
        finally { keyLock.Release(); }
    }

    public async Task<FirebaseIdentity> Verify(string token)
    {
        if (!Enabled(config)) throw new ApiError(503, "Google sign-in is not configured.");
        if (string.IsNullOrEmpty(token) || token.Length > 16000) throw Invalid();
        try
        {
            var project = config["Firebase:ProjectId"]!;
            var handler = new JsonWebTokenHandler { MaximumTokenSizeInBytes = 16000 };
            var parameters = new TokenValidationParameters
            {
                ValidIssuer = "https://securetoken.google.com/" + project,
                ValidAudience = project,
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                RequireExpirationTime = true, RequireSignedTokens = true,
                ValidateIssuerSigningKey = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30), IssuerSigningKeys = await Keys(),
            };
            var result = await handler.ValidateTokenAsync(token, parameters);
            if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                parameters.IssuerSigningKeys = await Keys(refresh: true);
                result = await handler.ValidateTokenAsync(token, parameters);
            }
            if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt) throw Invalid();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (string.IsNullOrWhiteSpace(jwt.Subject) || jwt.Subject.Length > 128
                || !jwt.TryGetPayloadValue<long>("iat", out var issued) || issued > now + 30
                || !jwt.TryGetPayloadValue<long>("auth_time", out var authenticated) || authenticated > now + 30
                || authenticated < now - 300
                || !jwt.TryGetPayloadValue<bool>("email_verified", out var verified) || !verified
                || !jwt.TryGetPayloadValue<JsonElement>("firebase", out var firebase)
                || !firebase.TryGetProperty("sign_in_provider", out var provider) || provider.GetString() != "google.com"
                || !jwt.TryGetPayloadValue<string>("email", out var email)
                || !System.Net.Mail.MailAddress.TryCreate(email, out _)) throw Invalid();

            // Check the current Firebase account as well, including disabled accounts and revoked sessions.
            using var response = await clients.CreateClient("firebase-auth").PostAsJsonAsync(
                "https://identitytoolkit.googleapis.com/v1/accounts:lookup?key=" + Uri.EscapeDataString(config["Firebase:ApiKey"]!),
                new { idToken = token });
            if (!response.IsSuccessStatusCode) throw Invalid();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var users = document.RootElement.GetProperty("users");
            if (users.GetArrayLength() != 1) throw Invalid();
            var user = users[0];
            if (user.GetProperty("localId").GetString() != jwt.Subject
                || !user.TryGetProperty("emailVerified", out var emailVerified) || !emailVerified.GetBoolean()
                || !user.GetProperty("email").GetString()!.Equals(email, StringComparison.OrdinalIgnoreCase)
                || user.TryGetProperty("disabled", out var disabled) && disabled.GetBoolean()
                || user.TryGetProperty("validSince", out var since)
                    && long.TryParse(since.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var validSince)
                    && authenticated < validSince) throw Invalid();
            return new(jwt.Subject, email.Trim().ToLowerInvariant());
        }
        catch (ApiError) { throw; }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        { throw new ApiError(503, "Google sign-in is temporarily unavailable. Please try again."); }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or SecurityTokenException or FormatException)
        { throw Invalid(); }
    }

    static ApiError Invalid() => new(401, "Unable to verify Google sign-in. Please try again.");
}

public static class GoogleSignInEndpoints
{
    public static void MapGoogleSignIn(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/google", async (GoogleLogin input, FirebaseGoogleVerifier verifier, Db db, Access access, HttpContext context, IHostEnvironment env) =>
        {
            var identity = await verifier.Verify(input.IdToken);
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == identity.Email);
            if (user == null || !user.Verified || user.LockedUntil > DateTimeOffset.UtcNow)
            {
                db.AuthenticationAudits.Add(new() { ActorId = user?.Id, Action = "auth.google", Outcome = "Denied" });
                await db.SaveChangesAsync();
                throw new ApiError(401, "Google sign-in requires an existing, verified Impact account with the same email. Create an account or contact your administrator.");
            }
            return await AppSignIn.Complete(user, db, access, context, env, "auth.google");
        }).RequireRateLimiting("auth");
    }
}
