using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using static Clubs.Access;

namespace Clubs;

public static partial class EventEndpoints
{
    private static void MapEventMedia(RouteGroupBuilder api)
    {
        api.MapGet(
            "/events/{id:guid}/documents",
            async (Guid id, EventService s, string? token = null) =>
            {
                var e = await s.Find(id);
                await s.View(e, token);
                return await s
                    .Db.GovernanceDocuments.Where(x =>
                        x.EventId == id
                        && x.Status == "Available"
                        && (x.RetainUntil == null || x.RetainUntil > DateTimeOffset.UtcNow)
                    )
                    .OrderByDescending(x => x.At)
                    .Take(100)
                    .Select(x => new
                    {
                        x.Id,
                        x.Title,
                        x.Kind,
                        x.ContentType,
                        x.FileName,
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
                "/events/{id:guid}/documents",
                async (
                    Guid id,
                    IFormFile file,
                    [FromForm] string title,
                    [FromForm] string kind,
                    EventService s
                ) =>
                {
                    var e = await s.Find(id);
                    await s.Manage(e);
                    Check(
                        file.Length is > 0 and <= 5242880
                            && title.Length is > 0 and <= 200
                            && new[]
                            {
                                "EventBanner",
                                "Agenda",
                                "Speaker",
                                "Venue",
                                "Attachment",
                                "Gallery",
                            }.Contains(kind),
                        "Invalid event file (maximum 5 MB)."
                    );
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    var type = MemberMediaEndpoints.FileType(bytes);
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    Check(
                        type == "application/pdf" ? ext == ".pdf"
                            : type == "image/png" ? ext == ".png"
                            : type == "image/jpeg" && new[] { ".jpg", ".jpeg" }.Contains(ext),
                        "File content and extension must match PDF, PNG or JPEG."
                    );
                    Check(
                        !new[] { "EventBanner", "Gallery" }.Contains(kind)
                            || type.StartsWith("image/"),
                        "An image is required."
                    );
                    var d = new GovernanceDocument
                    {
                        TenantId = e.TenantId,
                        OrgId = e.OrgId,
                        EventId = id,
                        Title = title,
                        Kind = kind,
                        FileName = Path.GetFileName(file.FileName),
                        ContentType = type,
                        Content = bytes,
                        Sha256 = Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(bytes)
                        ),
                        UploaderId = (await s.A.User()).Id,
                        RetainUntil = e.EndsAt.AddYears(1),
                    };
                    s.Db.GovernanceDocuments.Add(d);
                    await s.Db.SaveChangesAsync();
                    return Results.Ok(new { d.Id });
                }
            )
            .DisableAntiforgery();
        api.MapGet(
            "/events/{id:guid}/documents/{documentId:guid}",
            async (
                Guid id,
                Guid documentId,
                EventService s,
                HttpContext context,
                string? token = null
            ) =>
            {
                var e = await s.Find(id);
                await s.View(e, token);
                var query = s.Db.GovernanceDocuments.Where(x =>
                    x.Id == documentId
                    && x.EventId == id
                    && x.Status == "Available"
                    && (x.RetainUntil == null || x.RetainUntil > DateTimeOffset.UtcNow)
                );
                var d = await query
                    .Select(x => new
                    {
                        x.Sha256,
                        x.ContentType,
                        x.FileName,
                        x.At,
                    })
                    .SingleOrDefaultAsync();
                Check(d != null, "Document not found.", 404);
                var tag = "\"" + d!.Sha256 + "\"";
                context.Response.Headers.CacheControl = "private, no-cache";
                context.Response.Headers.ETag = tag;
                if (context.Request.Headers.IfNoneMatch.Any(x => x == tag))
                    return Results.StatusCode(304);
                var bytes = await query.Select(x => x.Content).SingleOrDefaultAsync();
                Check(bytes != null, "Document expired.", 404);
                return Results.File(
                    bytes!,
                    d.ContentType,
                    d.FileName,
                    lastModified: d.At,
                    entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue(tag),
                    enableRangeProcessing: true
                );
            }
        );
        api.MapPost(
            "/events/{id:guid}/passes/{registrationId:guid}/{format}",
            async (
                Guid id,
                Guid registrationId,
                string format,
                EventToken? input,
                EventService s
            ) =>
            {
                var e = await s.Find(id);
                var r = await s.Own(e, registrationId, input?.Token);
                Check(
                    r.Status == "Confirmed"
                        && e.Lifecycle != "Cancelled"
                        && e.EndsAt > DateTimeOffset.UtcNow,
                    "Pass unavailable.",
                    409
                );
                var t = await s.Db.EventTickets.SingleOrDefaultAsync(x =>
                    x.RegistrationId == r.Id && !x.Revoked
                );
                Check(t != null, "Pass must be issued first.", 409);
                var token = s.Unprotect(t!.ProtectedToken);
                using var data = QRCodeGenerator.GenerateQrCode(token, QRCodeGenerator.ECCLevel.Q);
                if (format == "png")
                {
                    using var png = new PngByteQRCode(data);
                    return Results.File(png.GetGraphic(6), "image/png", "event-qr.png");
                }
                Check(format is "pdf" or "badge", "Unsupported pass format.");
                return Results.File(
                    EventPdf.Build(
                        [
                            "IMPACT INTERNATIONAL / EVENT PASS",
                            e.Title,
                            r.Name,
                            e.StartsAt.ToString("u") + " - " + e.EndsAt.ToString("u"),
                            e.TimeZone + " / " + e.Venue,
                            "Registration: " + r.Id,
                            "Category: " + r.Category,
                            "Reserved seats: " + (1 + r.Companions),
                        ],
                        data
                    ),
                    "application/pdf",
                    format == "badge" ? "event-badge.pdf" : "event-pass.pdf"
                );
            }
        );
        api.MapPost(
            "/events/{id:guid}/certificates/{registrationId:guid}",
            async (Guid id, Guid registrationId, EventToken? input, EventService s) =>
            {
                var e = await s.Find(id);
                var r = await s.Own(e, registrationId, input?.Token);
                Check(
                    e.Lifecycle == "Completed"
                        && await s.Db.EventCheckins.AnyAsync(x =>
                            x.EventId == id
                            && x.RegistrationId == r.Id
                            && x.SessionId == null
                            && !x.Voided
                        ),
                    "Certificates require recorded attendance at a completed event.",
                    403
                );
                return Results.File(
                    EventPdf.Build([
                        "IMPACT INTERNATIONAL",
                        "CERTIFICATE OF PARTICIPATION",
                        r.Name,
                        "Participated in",
                        e.Title,
                        e.StartsAt.ToString("yyyy-MM-dd"),
                        "Registration reference: " + r.Id,
                    ]),
                    "application/pdf",
                    "participation-certificate.pdf"
                );
            }
        );
    }
}
