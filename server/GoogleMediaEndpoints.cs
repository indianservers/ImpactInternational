using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public record ActivityMediaInput(string FileName, string MimeType, long Size);
public static class GoogleMediaEndpoints
{
    static async Task<Assignment> Admin(Access a) {
        var w = await a.Workspace("settings.manage");
        Check((await a.User()).PlatformAdmin, "Only Platform Admin can connect or manage Google storage accounts.", 403); return w;
    }
    public static void MapGoogleMedia(this RouteGroupBuilder api)
    {
        MapAccounts(api);
        MapUploads(api);
        api.MapGet("/community/media/{id:guid}/content", StreamMedia);
        api.MapGet("/public/activity-media/{id:guid}/content", StreamMedia);
    }
    static void MapAccounts(RouteGroupBuilder api)
    {
        api.MapGet("/google-media/accounts", async (Access a, GoogleMediaService google) => {
            var w = await Admin(a);
            return new { configured = google.Configured, redirectUri = google.RedirectUri, scopes = GoogleMediaService.Scope,
                accounts = await a.Db.GoogleMediaConnections.Where(c => c.TenantId == w.TenantId).OrderByDescending(c => c.ConnectedAt)
                    .Select(c => new { c.Id, c.Email, c.Connected, c.ActiveForUploads, c.ConnectedAt }).ToListAsync() };
        });
        api.MapPost("/google-media/connect", async (Access a, GoogleMediaService google) => {
            var w = await Admin(a); Check(google.Configured, "Configure the Google OAuth client ID, client secret and redirect URI on the server before connecting.", 409);
            var session = await a.Session(); var state = Token(); var verifier = Token();
            await using var tx = await a.Mutation(w);
            a.Db.GoogleMediaOAuthStates.RemoveRange(await a.Db.GoogleMediaOAuthStates.Where(s => s.UserId == w.UserId && (s.ExpiresAt < DateTimeOffset.UtcNow || s.Consumed)).ToListAsync());
            a.Db.GoogleMediaOAuthStates.Add(new() { TenantId = w.TenantId, UserId = w.UserId, SessionId = session.Id, StateHash = Hash(state), VerifierProtected = google.Protect(verifier), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
            await a.Log(w, "google-media.consent.started", w.TenantId.ToString()); await tx.CommitAsync();
            var challenge = WebEncoders.Base64UrlEncode(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
            var url = QueryHelpers.AddQueryString(google.AuthorizeUrl, new Dictionary<string,string?> {
                ["client_id"] = google.ClientId, ["redirect_uri"] = google.RedirectUri, ["response_type"] = "code", ["scope"] = GoogleMediaService.Scope,
                ["access_type"] = "offline", ["prompt"] = "consent select_account", ["state"] = state, ["code_challenge"] = challenge, ["code_challenge_method"] = "S256"
            });
            return new { url };
        });
        api.MapGet("/google-media/callback", Callback);
        api.MapPost("/google-media/accounts/{id:guid}/activate", async (Guid id, Access a) => {
            var w = await Admin(a); await using var tx = await a.Mutation(w);
            var c = await a.Db.GoogleMediaConnections.SingleOrDefaultAsync(c => c.TenantId == w.TenantId && c.Id == id && c.Connected);
            Check(c != null, "Connected Google account unavailable.", 404);
            await a.Db.GoogleMediaConnections.Where(c => c.TenantId == w.TenantId && c.ActiveForUploads).ExecuteUpdateAsync(s => s.SetProperty(c => c.ActiveForUploads, false));
            c.ActiveForUploads = true; a.Db.Entry(c).Property(x => x.ActiveForUploads).IsModified = true;
            await a.Log(w, "google-media.default.changed", id.ToString()); await tx.CommitAsync(); return Results.NoContent();
        });
        api.MapPost("/google-media/accounts/{id:guid}/disconnect", async (Guid id, Access a, GoogleMediaService google, HttpContext ctx) => {
            var w = await Admin(a); await using var tx = await a.Mutation(w);
            var c = await a.Db.GoogleMediaConnections.SingleOrDefaultAsync(c => c.TenantId == w.TenantId && c.Id == id);
            Check(c != null, "Google account unavailable.", 404);
            await google.Revoke(c, ctx.RequestAborted); c.Connected = false; c.ActiveForUploads = false; c.AccessTokenProtected = ""; c.RefreshTokenProtected = "";
            await a.Log(w, "google-media.disconnected", id.ToString()); await tx.CommitAsync(); return Results.NoContent();
        });
    }
    static void MapUploads(RouteGroupBuilder api)
    {
        api.MapGet("/community/media-storage", async (Access a) => {
            var s = await CommunityEndpoints.GetScope(a);
            return new { available = await a.Db.GoogleMediaConnections.AnyAsync(c => c.TenantId == s.W.TenantId && c.Connected && c.ActiveForUploads), maxImageBytes = GoogleMediaService.MaxImage, maxVideoBytes = GoogleMediaService.MaxVideo, mimeTypes = GoogleMediaService.MimeTypes };
        });
        api.MapPost("/community/posts/{id:guid}/media/start", async (Guid id, ActivityMediaInput input, Access a, GoogleMediaService google, HttpContext ctx) => {
            var s = await CommunityEndpoints.GetScope(a); await using var tx = await a.Mutation(s.W); var post = await CommunityGrowthService.Editable(id, a, s);
            Check(CommunityEndpoints.CanModerate(s, post.OrgId), "Only an authorized unit administrator may add activity media.", 403);
            Check(input.FileName is { Length: > 0 and <= 200 } && input.FileName.All(c => !char.IsControl(c)) && !input.FileName.Contains('/') && !input.FileName.Contains('\\'), "Use a file name of at most 200 characters.");
            Check(GoogleMediaService.MimeTypes.Contains(input.MimeType) && input.Size > 0 && input.Size <= (input.MimeType.StartsWith("video/") ? GoogleMediaService.MaxVideo : GoogleMediaService.MaxImage), "Choose a supported image up to 20 MB or video up to 500 MB.");
            Check(await a.Db.CommunityMedia.CountAsync(m => m.PostId == id && (m.Status == "Ready" || (m.Status == "Uploading" || m.Status == "Failed") && m.CreatedAt > DateTimeOffset.UtcNow.AddDays(-1))) < 20, "An activity can have at most 20 media items, including pending uploads.");
            GoogleMediaConnection? connection;
            if (post.GoogleConnectionId != null) connection = await a.Db.GoogleMediaConnections.SingleOrDefaultAsync(c => c.Id == post.GoogleConnectionId && c.TenantId == s.W.TenantId && c.Connected);
            else connection = await a.Db.GoogleMediaConnections.SingleOrDefaultAsync(c => c.TenantId == s.W.TenantId && c.Connected && c.ActiveForUploads);
            Check(connection != null, "Ask Platform Admin to connect Google Drive before adding media. Existing activity folders require their original account.", 409);
            if (connection.RootFolderId == "") connection.RootFolderId = await google.Folder(connection, "Impact International · Activity media", null, ctx.RequestAborted);
            if (post.DriveFolderId == "") {
                post.DriveFolderId = await google.Folder(connection, post.ActivityOn.ToString("yyyy-MM-dd") + " · " + post.Title + " · " + post.Id.ToString()[..8], connection.RootFolderId, ctx.RequestAborted);
                post.GoogleConnectionId = connection.Id;
            }
            var media = new CommunityMedia { TenantId = s.W.TenantId, PostId = id, ConnectionId = connection.Id, UploadedBy = s.W.UserId, FileName = input.FileName.Trim(), MimeType = input.MimeType, Size = input.Size };
            var uploadUrl = await google.BeginUpload(connection, post, media, ctx.RequestAborted);
            a.Db.CommunityMedia.Add(media); await a.Log(s.W, "community.media.upload.started", media.Id.ToString(), id.ToString()); await tx.CommitAsync();
            ctx.Response.Headers.CacheControl = "no-store";
            return new { media.Id, uploadUrl, chunkSize = 8 * 1024 * 1024 };
        });
        api.MapPost("/community/media/{id:guid}/complete", async (Guid id, Access a, GoogleMediaService google, HttpContext ctx) => {
            var s = await CommunityEndpoints.GetScope(a); await using var tx = await a.Mutation(s.W);
            var m = await a.Db.CommunityMedia.SingleOrDefaultAsync(m => m.Id == id && m.TenantId == s.W.TenantId);
            Check(m != null, "Upload unavailable.", 404); var post = await CommunityGrowthService.Editable(m.PostId, a, s);
            Check(CommunityEndpoints.CanModerate(s, post.OrgId), "Only an authorized unit administrator may finish this upload.", 403);
            if (m.Status == "Ready") return Results.Ok(new { m.Id });
            Check((m.Status == "Uploading" || m.Status == "Failed") && m.CreatedAt > DateTimeOffset.UtcNow.AddDays(-1), "Upload expired. Start again.", 409);
            var c = await a.Db.GoogleMediaConnections.SingleOrDefaultAsync(c => c.Id == m.ConnectionId && c.Connected);
            Check(c != null, "Reconnect the activity's Google account to finish this upload.", 409);
            var file = await google.Json(c, HttpMethod.Get, "drive/v3/files/" + Uri.EscapeDataString(m.DriveFileId) + "?fields=id,size,mimeType,parents,trashed,appProperties", ct: ctx.RequestAborted);
            Check(!file.GetProperty("trashed").GetBoolean() && file.GetProperty("size").GetString() == m.Size.ToString(System.Globalization.CultureInfo.InvariantCulture)
                && file.GetProperty("mimeType").GetString() == m.MimeType && file.GetProperty("parents").EnumerateArray().Any(p => p.GetString() == post.DriveFolderId)
                && file.GetProperty("appProperties").GetProperty("impactMediaId").GetString() == m.Id.ToString(), "Google upload is incomplete or does not match this activity.", 409);
            m.Status = "Ready"; m.CompletedAt = DateTimeOffset.UtcNow;
            await a.Log(s.W, "community.media.upload.completed", id.ToString(), post.Id.ToString()); await tx.CommitAsync(); return Results.Ok(new { m.Id });
        });
        api.MapDelete("/community/media/{id:guid}", async (Guid id, Access a) => {
            var s = await CommunityEndpoints.GetScope(a); await using var tx = await a.Mutation(s.W);
            var m = await a.Db.CommunityMedia.SingleOrDefaultAsync(m => m.Id == id && m.TenantId == s.W.TenantId);
            Check(m != null, "Media unavailable.", 404); var post = await CommunityGrowthService.Editable(m.PostId, a, s);
            Check(CommunityEndpoints.CanModerate(s, post.OrgId), "Only an authorized unit administrator may remove media.", 403);
            // The private administrator-owned Drive copy remains available for recovery.
            m.Status = "Removed"; await a.Log(s.W, "community.media.removed", id.ToString()); await tx.CommitAsync(); return Results.NoContent();
        });
    }
    static async Task StreamMedia(Guid id, HttpContext ctx, Access a, GoogleMediaService google)
    {
        var m = await a.Db.CommunityMedia.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id && m.Status == "Ready");
        Check(m != null, "Media unavailable.", 404);
        var post = await a.Db.CommunityPosts.AsNoTracking().SingleAsync(p => p.Id == m.PostId && p.TenantId == m.TenantId);
        if (ctx.Request.Path.StartsWithSegments("/api/v1/public")) {
            Check(post.Status == "Published" && post.Visibility == "Public" && (a.BoundTenant == null || a.BoundTenant == post.TenantId), "Media unavailable.", 404);
        } else {
            var s = await CommunityEndpoints.GetScope(a);
            if (!await CommunityEndpoints.Posts(a.Db,s).AnyAsync(p => p.Id == post.Id)) await CommunityGrowthService.Editable(post.Id,a,s);
        }
        var c = await a.Db.GoogleMediaConnections.SingleOrDefaultAsync(c => c.Id == m.ConnectionId && c.TenantId == m.TenantId && c.Connected);
        Check(c != null, "This activity's Google storage account needs reconnection.", 503);
        var range = ctx.Request.Headers.Range.ToString();
        Check(range == "" || Regex.IsMatch(range, @"^bytes=\d*-\d*$") && range != "bytes=-", "Use a single valid byte range.", 416);
        using var response = await google.Send(c, HttpMethod.Get, "drive/v3/files/" + Uri.EscapeDataString(m.DriveFileId) + "?alt=media", ct: ctx.RequestAborted, range: range == "" ? null : range);
        ctx.Response.StatusCode = (int)response.StatusCode;
        ctx.Response.ContentType = m.MimeType;
        ctx.Response.Headers.CacheControl = "private, no-store";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";
        ctx.Response.Headers.AcceptRanges = "bytes";
        if (response.Content.Headers.ContentLength is long length) ctx.Response.ContentLength = length;
        if (response.Content.Headers.ContentRange != null) ctx.Response.Headers.ContentRange = response.Content.Headers.ContentRange.ToString();
        await using var stream = await response.Content.ReadAsStreamAsync(ctx.RequestAborted);
        await stream.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
    }
    static async Task<IResult> Callback(HttpContext context, Access a, GoogleMediaService google, IConfiguration config, IWebHostEnvironment env, IHttpClientFactory clients)
    {
        var w = await Admin(a); var session = await a.Session();
        var state = context.Request.Query["state"].ToString(); Check(state.Length is >= 32 and <= 200, "Google consent state is missing or invalid.");
        string verifier;
        await using (var tx = await a.Mutation(w)) {
            var entry = await a.Db.GoogleMediaOAuthStates.SingleOrDefaultAsync(s => s.StateHash == Hash(state) && s.UserId == w.UserId && s.TenantId == w.TenantId && s.SessionId == session.Id && !s.Consumed && s.ExpiresAt > DateTimeOffset.UtcNow);
            Check(entry != null, "Google consent expired or was already used. Start connecting again.", 400);
            entry.Consumed = true; verifier = google.Unprotect(entry.VerifierProtected); entry.VerifierProtected = "";
            await a.Db.SaveChangesAsync(); await tx.CommitAsync();
        }
        if (context.Request.Query.ContainsKey("error")) return Results.Redirect("/app/community/storage?google=cancelled");
        var code = context.Request.Query["code"].ToString(); Check(code.Length is > 0 and <= 4096, "Google authorization code is missing.");
        var test = config["GoogleMedia:TestBase"];
        Check(string.IsNullOrEmpty(test) || env.IsDevelopment() && Uri.TryCreate(test, UriKind.Absolute, out var testUri) && testUri.IsLoopback, "Invalid Google test configuration.", 500);
        using var response = await clients.CreateClient().PostAsync(string.IsNullOrEmpty(test) ? "https://oauth2.googleapis.com/token" : test.TrimEnd('/') + "/token", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["client_id"] = google.ClientId, ["client_secret"] = config["GoogleMedia:ClientSecret"] ?? "", ["redirect_uri"] = google.RedirectUri,
            ["code"] = code, ["code_verifier"] = verifier, ["grant_type"] = "authorization_code"
        }), context.RequestAborted);
        Check(response.IsSuccessStatusCode, "Google authorization could not be completed. Start connecting again.", 502);
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>();
        var scopes = tokens.GetProperty("scope").GetString()?.Split(' ') ?? [];
        Check(scopes.Contains("https://www.googleapis.com/auth/drive.file"), "Drive file access was not granted. Reconnect and approve the requested permissions.", 400);
        var access = tokens.GetProperty("access_token").GetString()!;
        var identity = await google.UserInfo(access, context.RequestAborted);
        Check(identity.TryGetProperty("email_verified", out var verified) && verified.ValueKind == JsonValueKind.True, "A verified Google email is required.", 400);
        var subject = identity.GetProperty("sub").GetString()!; var email = identity.GetProperty("email").GetString()!;
        await using var save = await a.Mutation(w);
        var connection = await a.Db.GoogleMediaConnections.SingleOrDefaultAsync(c => c.TenantId == w.TenantId && c.GoogleSubject == subject);
        if (connection == null) { connection = new() { TenantId = w.TenantId, GoogleSubject = subject }; a.Db.GoogleMediaConnections.Add(connection); }
        Check(tokens.TryGetProperty("refresh_token", out var refresh) || connection.RefreshTokenProtected != "", "Google did not grant offline access. Reconnect with consent.", 400);
        if (refresh.ValueKind == JsonValueKind.String) connection.RefreshTokenProtected = google.Protect(refresh.GetString()!);
        connection.ConnectedBy = w.UserId; connection.Email = email; connection.Connected = true; connection.AccessTokenProtected = google.Protect(access);
        connection.AccessExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.GetProperty("expires_in").GetInt32()); connection.ConnectedAt = DateTimeOffset.UtcNow;
        await a.Db.GoogleMediaConnections.Where(c => c.TenantId == w.TenantId && c.ActiveForUploads).ExecuteUpdateAsync(s => s.SetProperty(c => c.ActiveForUploads, false));
        connection.ActiveForUploads = true; a.Db.Entry(connection).Property(c => c.ActiveForUploads).IsModified = true;
        await a.Log(w, "google-media.connected", connection.Id.ToString()); await save.CommitAsync();
        return Results.Redirect("/app/community/storage?google=connected");
    }
}
