using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

// Files go directly from the browser to a single-use Drive upload session. Only metadata is stored here.
public class GoogleMediaService(HttpClient http, Db db, IDataProtectionProvider protection, IConfiguration config, IWebHostEnvironment environment)
{
    public const string Scope = "openid email https://www.googleapis.com/auth/drive.file";
    public const long MaxImage = 20L * 1024 * 1024, MaxVideo = 500L * 1024 * 1024;
    public static readonly string[] MimeTypes = ["image/jpeg", "image/png", "image/webp", "image/gif", "video/mp4", "video/webm", "video/quicktime"];
    readonly IDataProtector protector = protection.CreateProtector("Impact.GoogleMedia.Tokens.v1");
    public string Protect(string value) => protector.Protect(value);
    public string Unprotect(string value) => protector.Unprotect(value);
    public string ClientId => config["GoogleMedia:ClientId"] ?? "";
    string ClientSecret => config["GoogleMedia:ClientSecret"] ?? "";
    public string RedirectUri => !string.IsNullOrWhiteSpace(config["GoogleMedia:RedirectUri"]) ? config["GoogleMedia:RedirectUri"]! : (config["PublicOrigin"]?.TrimEnd('/') + "/api/v1/google-media/callback");
    public bool Configured => ClientId.Length > 0 && ClientSecret.Length > 0 && Uri.TryCreate(RedirectUri, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || environment.IsDevelopment() && uri.IsLoopback);
    string TestBase {
        get {
            var url = config["GoogleMedia:TestBase"];
            if (string.IsNullOrEmpty(url)) return "";
            Check(environment.IsDevelopment() && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsLoopback, "Google test endpoints are restricted to local development.");
            return url.TrimEnd('/');
        }
    }
    public string AuthorizeUrl => TestBase == "" ? "https://accounts.google.com/o/oauth2/v2/auth" : TestBase + "/authorize";
    string TokenUrl => TestBase == "" ? "https://oauth2.googleapis.com/token" : TestBase + "/token";
    string RevokeUrl => TestBase == "" ? "https://oauth2.googleapis.com/revoke" : TestBase + "/revoke";
    string ApiUrl(string path) => TestBase == "" ? "https://www.googleapis.com/" + path : TestBase + "/" + path;
    public static string Challenge(string verifier) => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    public async Task<JsonElement> Token(Dictionary<string, string> fields, CancellationToken ct = default)
    {
        Check(Configured, "Configure Google OAuth credentials before connecting an account.", 503);
        fields["client_id"] = ClientId; fields["client_secret"] = ClientSecret;
        using var response = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(fields), ct);
        Check(response.IsSuccessStatusCode, "Google authorization failed. Reconnect the account and try again.", 502);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
    }
    public async Task<string> AccessToken(GoogleMediaConnection connection, CancellationToken ct = default)
    {
        Check(connection.Connected && connection.RefreshTokenProtected != "", "The Google account is disconnected. Ask a platform administrator to reconnect it.", 503);
        if (connection.AccessExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2) && connection.AccessTokenProtected != "") return Unprotect(connection.AccessTokenProtected);
        var result = await Token(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = Unprotect(connection.RefreshTokenProtected) }, ct);
        var token = result.GetProperty("access_token").GetString()!;
        connection.AccessTokenProtected = Protect(token); connection.AccessExpiresAt = DateTimeOffset.UtcNow.AddSeconds(result.GetProperty("expires_in").GetInt32());
        if (result.TryGetProperty("refresh_token", out var refresh)) connection.RefreshTokenProtected = Protect(refresh.GetString()!);
        await db.SaveChangesAsync(ct); return token;
    }
    public async Task<JsonElement> UserInfo(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiUrl("oauth2/v3/userinfo"));
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await http.SendAsync(request, ct);
        Check(response.IsSuccessStatusCode, "Google account identity could not be verified.", 502);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
    }
    public async Task<HttpResponseMessage> Send(GoogleMediaConnection c, HttpMethod method, string path, HttpContent? content = null, CancellationToken ct = default, string? range = null)
    {
        using var request = new HttpRequestMessage(method, ApiUrl(path)) { Content = content };
        request.Headers.Authorization = new("Bearer", await AccessToken(c, ct));
        if (range != null) request.Headers.TryAddWithoutValidation("Range", range);
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) { response.Dispose(); throw new ApiError(502, "Google Drive could not complete this request. Check the account connection, storage quota and file availability, then retry."); }
        return response;
    }
    public async Task<JsonElement> Json(GoogleMediaConnection c, HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        using var response = await Send(c, method, path, body == null ? null : JsonContent.Create(body), ct);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
    }
    public async Task<string> Folder(GoogleMediaConnection c, string name, string? parent, CancellationToken ct)
    {
        var metadata = new Dictionary<string, object> { ["name"] = name, ["mimeType"] = "application/vnd.google-apps.folder" };
        if (parent != null) metadata["parents"] = new[] { parent };
        var result = await Json(c, HttpMethod.Post, "drive/v3/files?fields=id", metadata, ct);
        return result.GetProperty("id").GetString()!;
    }
    public async Task<string> BeginUpload(GoogleMediaConnection c, CommunityPost post, CommunityMedia media, CancellationToken ct)
    {
        var ids = await Json(c, HttpMethod.Get, "drive/v3/files/generateIds?count=1&space=drive", ct: ct);
        media.DriveFileId = ids.GetProperty("ids")[0].GetString()!;
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl("upload/drive/v3/files?uploadType=resumable&fields=id")) {
            Content = JsonContent.Create(new { id = media.DriveFileId, name = media.FileName, mimeType = media.MimeType, parents = new[] { post.DriveFolderId }, appProperties = new { impactMediaId = media.Id.ToString() } })
        };
        request.Headers.Authorization = new("Bearer", await AccessToken(c, ct));
        request.Headers.Add("X-Upload-Content-Type", media.MimeType); request.Headers.Add("X-Upload-Content-Length", media.Size.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var response = await http.SendAsync(request, ct);
        Check(response.IsSuccessStatusCode && response.Headers.Location != null, "Google Drive could not start an upload. Check available Google storage and retry.", 502);
        var location = response.Headers.Location!;
        Check(location.IsAbsoluteUri && (TestBase != "" && location.GetLeftPart(UriPartial.Authority) == new Uri(TestBase).GetLeftPart(UriPartial.Authority) || location.Scheme == "https" && location.Host == "www.googleapis.com"), "Google returned an unexpected upload address.", 502);
        return location.ToString();
    }
    public async Task Revoke(GoogleMediaConnection c, CancellationToken ct)
    {
        if (!c.Connected) return;
        using var response = await http.PostAsync(RevokeUrl, new FormUrlEncodedContent(new Dictionary<string,string> { ["token"] = Unprotect(c.RefreshTokenProtected) }), ct);
        Check(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest, "Google could not revoke access. Retry disconnecting the account.", 502);
    }
    public async Task ImportExisting(GoogleMediaConnection c,CommunityPost post,CommunityMedia media,byte[] bytes,CancellationToken ct)
    {
        var url=await BeginUpload(c,post,media,ct);
        using var content=new ByteArrayContent(bytes);content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue(media.MimeType);
        content.Headers.ContentRange=new System.Net.Http.Headers.ContentRangeHeaderValue(0,bytes.Length-1,bytes.Length);
        using var response=await http.PutAsync(url,content,ct);
        Check(response.IsSuccessStatusCode,"Event gallery import was interrupted by Google. Retry importing the gallery.",502);
        media.Status="Ready";media.CompletedAt=DateTimeOffset.UtcNow;
    }
}
