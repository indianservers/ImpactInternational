using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clubs;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using var rsa = RSA.Create(2048);
var signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };
var publicKey = rsa.ExportParameters(false);
var jwks = JsonSerializer.Serialize(new { keys = new[] { new { kty = "RSA", kid = "test-key", alg = "RS256", use = "sig", n = Base64UrlEncoder.Encode(publicKey.Modulus), e = Base64UrlEncoder.Encode(publicKey.Exponent) } } });
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Firebase:ProjectId"] = "test-project", ["Firebase:ApiKey"] = "test-api-key" }).Build();
var now = DateTimeOffset.UtcNow;

string Token(string audience = "test-project", string? issuer = null, string provider = "google.com", bool verified = true,
    string subject = "google-user", int authAge = 0, int issuedOffset = 0, bool expired = false, SecurityKey? key = null)
{
    return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = issuer ?? "https://securetoken.google.com/test-project", Audience = audience,
        IssuedAt = now.AddSeconds(issuedOffset).UtcDateTime,
        NotBefore = now.AddHours(-2).UtcDateTime,
        Expires = (expired ? now.AddMinutes(-2) : now.AddHours(1)).UtcDateTime,
        SigningCredentials = new(key ?? signingKey, SecurityAlgorithms.RsaSha256),
        Claims = new Dictionary<string, object> { ["sub"] = subject, ["email"] = "member@example.com", ["email_verified"] = verified,
            ["auth_time"] = now.AddSeconds(-authAge).ToUnixTimeSeconds(), ["firebase"] = new Dictionary<string, object> { ["sign_in_provider"] = provider } },
    });
}
FirebaseGoogleVerifier Verifier(string? account = null) => new(config, new Factory(new Stub(jwks, account ??
    "{\"users\":[{\"localId\":\"google-user\",\"email\":\"member@example.com\",\"emailVerified\":true,\"validSince\":\"0\"}]}")));

var good = await Verifier().Verify(Token());
if (good.Subject != "google-user" || good.Email != "member@example.com") throw new Exception("Identity mismatch");
int passed = 1;
async Task Denied(string name, string token, FirebaseGoogleVerifier? verifier = null)
{
    try { await (verifier ?? Verifier()).Verify(token); throw new Exception(name + " was accepted"); }
    catch (ApiError error) when (error.Status == 401) { passed++; Console.WriteLine("PASS " + name); }
}
await Denied("wrong project", Token(audience: "other-project"));
await Denied("wrong issuer", Token(issuer: "https://attacker.example"));
await Denied("non-Google provider", Token(provider: "password"));
await Denied("unverified email", Token(verified: false));
await Denied("expired token", Token(expired: true));
await Denied("old authentication", Token(authAge: 600));
await Denied("future authentication", Token(authAge: -120));
await Denied("future issue time", Token(issuedOffset: 120));
await Denied("empty subject", Token(subject: ""));
await Denied("malformed token", "not.a.valid-token");
await Denied("oversized token", new string('x', 17000));
using var otherRsa = RSA.Create(2048);
await Denied("forged signature", Token(key: new RsaSecurityKey(otherRsa) { KeyId = "test-key" }));
await Denied("disabled account", Token(), Verifier("{\"users\":[{\"localId\":\"google-user\",\"email\":\"member@example.com\",\"emailVerified\":true,\"disabled\":true}]}"));
await Denied("revoked session", Token(), Verifier("{\"users\":[{\"localId\":\"google-user\",\"email\":\"member@example.com\",\"emailVerified\":true,\"validSince\":\"" + now.AddSeconds(60).ToUnixTimeSeconds() + "\"}]}"));
await Denied("changed account email", Token(), Verifier("{\"users\":[{\"localId\":\"google-user\",\"email\":\"changed@example.com\",\"emailVerified\":true}]}"));
Console.WriteLine($"{passed} Google token checks passed.");

sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
{ public HttpClient CreateClient(string name) => new(handler, disposeHandler: false); }
sealed class Stub(string jwks, string account) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
            request.RequestUri!.AbsolutePath.Contains("/jwk/") ? jwks : account, Encoding.UTF8, "application/json") });
}
