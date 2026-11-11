using System.Buffers.Binary;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using static Clubs.Access;

namespace Clubs;

public static class MemberMediaEndpoints
{
    public static void MapMemberMedia(this RouteGroupBuilder api)
    {
        api.MapPost(
                "/persons/me/photo",
                async (IFormFile file, MembershipService s) =>
                {
                    var p = await s.OwnPerson();
                    Check(
                        file.Length is > 0 and <= 2097152,
                        "Photo must be a PNG or JPEG smaller than 2 MB."
                    );
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var image = StripPhoto(ms.ToArray());
                    p.Photo = image.Bytes;
                    p.PhotoType = image.Type;
                    p.Version++;
                    p.UpdatedAt = DateTimeOffset.UtcNow;
                    await s.Db.SaveChangesAsync();
                    return Results.Ok(new { p.Version });
                }
            )
            .DisableAntiforgery();
        api.MapPost(
                "/members/{id:guid}/documents",
                async (
                    Guid id,
                    IFormFile file,
                    [Microsoft.AspNetCore.Mvc.FromForm] string? kind,
                    [Microsoft.AspNetCore.Mvc.FromForm] DateTimeOffset? retainUntil,
                    MembershipService s
                ) =>
                {
                    var m = await s.Find(id, "members.documents");
                    var u = await s.Access.User();
                    kind ??= "Supporting";
                    Check(
                        new[] { "Application", "Identity", "Certificate", "Supporting" }.Contains(
                            kind
                        ),
                        "Unsupported document kind."
                    );
                    Check(file.Length is > 0 and <= 5242880, "Document must be smaller than 5 MB.");
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    var type = FileType(bytes);
                    Check(type != "", "Only valid PDF, PNG or JPEG documents are accepted.");
                    Check(
                        retainUntil == null || retainUntil > DateTimeOffset.UtcNow,
                        "Retention date must be in the future."
                    );
                    var own = m.Person.UserId == u.Id;
                    var name = Path.GetFileName(file.FileName);
                    Check(
                        new[] { ".pdf", ".png", ".jpg", ".jpeg" }.Contains(
                            Path.GetExtension(name).ToLowerInvariant()
                        ),
                        "Document extension must match a supported file type."
                    );
                    Check(
                        type == "application/pdf"
                                ? Path.GetExtension(name)
                                    .Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                            : type == "image/png"
                                ? Path.GetExtension(name)
                                    .Equals(".png", StringComparison.OrdinalIgnoreCase)
                            : new[] { ".jpg", ".jpeg" }.Contains(
                                Path.GetExtension(name).ToLowerInvariant()
                            ),
                        "File extension does not match its content."
                    );
                    Check(
                        name.Length is > 0 and <= 200 && !name.Any(char.IsControl),
                        "Invalid file name."
                    );
                    var doc = new MembershipDocument
                    {
                        TenantId = m.TenantId,
                        MembershipId = m.Id,
                        Kind = kind,
                        FileName = name,
                        ContentType = type,
                        Content = bytes,
                        Sha256 = Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(bytes)
                        ),
                        UploaderId = u.Id,
                        Status = own ? "Pending" : "Approved",
                        RetainUntil =
                            retainUntil
                            ?? (kind == "Identity" ? DateTimeOffset.UtcNow.AddDays(90) : null),
                    };
                    s.Db.MembershipDocuments.Add(doc);
                    s.Activity(m, u.Id, "document.uploaded", kind);
                    await s.Db.SaveChangesAsync();
                    return Results.Ok(
                        new
                        {
                            doc.Id,
                            doc.FileName,
                            doc.Status,
                            doc.RetainUntil,
                        }
                    );
                }
            )
            .DisableAntiforgery();
        api.MapGet(
            "/membership/documents/{id:guid}",
            async (Guid id, MembershipService s) =>
            {
                var metadata = await s
                    .Db.MembershipDocuments.Where(x => x.Id == id)
                    .Select(x => new { x.MembershipId, x.RetainUntil })
                    .SingleOrDefaultAsync();
                Check(metadata != null, "Document not found.", 404);
                var m = await s.Find(metadata.MembershipId, "members.documents");
                Check(
                    metadata.RetainUntil == null || metadata.RetainUntil > DateTimeOffset.UtcNow,
                    "Document retention period ended.",
                    410
                );
                var doc = await s.Db.MembershipDocuments.SingleAsync(x => x.Id == id);
                s.Activity(m, (await s.Access.User()).Id, "document.downloaded", id.ToString());
                await s.Db.SaveChangesAsync();
                return Results.File(doc.Content, doc.ContentType, doc.FileName);
            }
        );
        api.MapPost(
            "/membership/documents/{id:guid}/approve",
            async (Guid id, MembershipService s) =>
            {
                await s.Access.Workspace("members.documents");
                var doc = await s.Db.MembershipDocuments.SingleOrDefaultAsync(x => x.Id == id);
                Check(doc != null, "Document not found.", 404);
                var m = await s.Find(doc.MembershipId, "members.documents", false);
                doc.Status = "Approved";
                s.Activity(m, (await s.Access.User()).Id, "document.approved", id.ToString());
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapDelete(
            "/membership/documents/{id:guid}",
            async (Guid id, MembershipService s) =>
            {
                var doc = await s.Db.MembershipDocuments.SingleOrDefaultAsync(x => x.Id == id);
                Check(doc != null, "Document not found.", 404);
                var m = await s.Find(doc.MembershipId, "members.documents");
                s.Db.MembershipDocuments.Remove(doc);
                s.Activity(m, (await s.Access.User()).Id, "document.deleted", id.ToString());
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapGet(
            "/members/{id:guid}/cards",
            async (Guid id, MembershipService s) =>
            {
                await s.Find(id, "members.cards");
                return await s
                    .Db.DigitalCards.Where(x => x.MembershipId == id)
                    .OrderByDescending(x => x.IssuedAt)
                    .Select(x => new
                    {
                        x.Id,
                        x.IssuedAt,
                        x.RevokedAt,
                    })
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/members/{id:guid}/cards/reissue",
            async (Guid id, VersionInput r, MembershipService s) =>
            {
                var m = await s.Find(id, "members.cards");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(id);
                await s.Db.Entry(m).ReloadAsync();
                Check(
                    m.Version == r.Version
                        && m.Status == "Active"
                        && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow),
                    "Only a current active membership can receive a card; refresh if it changed.",
                    409
                );
                await s
                    .Db.DigitalCards.Where(x => x.MembershipId == id && x.RevokedAt == null && (x.State == "Legacy" || x.State == "Issued"))
                    .ExecuteUpdateAsync(u =>
                        u.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow)
                    );
                Check(!await s.Db.DigitalCards.AnyAsync(c=>c.MembershipId==id&&c.TemplateVersionId!=null), "Use the reviewed ID card reissue workflow for designed cards.",409);
                s.Card(m);
                s.Activity(m, (await s.Access.User()).Id, "card.reissued");
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { message = "A new verifiable card has been issued." });
            }
        );
        api.MapPost(
            "/membership/cards/{id:guid}/revoke",
            async (Guid id, MembershipService s) =>
            {
                var card = await s.Db.DigitalCards.SingleOrDefaultAsync(x => x.Id == id);
                Check(card != null, "Card not found.", 404);
                Check(card.TemplateVersionId==null,"Use the ID Card Studio revocation workflow for designed cards.",409);
                var m = await s.Find(card.MembershipId, "members.cards");
                card.RevokedAt = DateTimeOffset.UtcNow;
                s.Activity(m, (await s.Access.User()).Id, "card.revoked", id.ToString());
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapGet(
            "/members/{id:guid}/card",
            async (Guid id, MembershipService s, IConfiguration config) =>
            {
                var m = await s.Find(id, "members.cards");
                var p = await s.Db.Persons.SingleAsync(x => x.Id == m.PersonId);
                var card = await s.Db.DigitalCards.SingleOrDefaultAsync(x =>
                    x.MembershipId == id && x.RevokedAt == null && (x.State == "Legacy" || x.State == "Issued")
                );
                Check(
                    card != null,
                    "No current card. An active member can request reissuance.",
                    404
                );
                if(card.TemplateVersionId!=null)return Results.Redirect("/api/v1/id-cards/issuances/"+card.Id+"/preview");
                var tenant = await s.Db.Tenants.SingleAsync(x => x.Id == m.TenantId);
                var origin = config["PublicOrigin"] ?? "http://127.0.0.1:5174";
                var url = origin.TrimEnd('/') + "/verify?token=" + s.Unprotect(card.ProtectedToken);
                return Results.File(
                    Encoding.UTF8.GetBytes(RenderCard(m, p, tenant, url)),
                    "image/svg+xml",
                    m.Number + ".svg"
                );
            }
        );
        api.MapGet(
            "/membership/invitations/{id:guid}/qr",
            async (Guid id, MembershipService s, IConfiguration config) =>
            {
                var w = await s.Access.WorkspaceAny("members.invite", "members.link");
                var invite = await s.Db.MembershipInvitations.SingleOrDefaultAsync(x =>
                    x.Id == id
                    && x.TenantId == w.TenantId
                    && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
                );
                Check(
                    invite != null && !invite.Revoked && invite.ExpiresAt > DateTimeOffset.UtcNow,
                    "Invitation is unavailable.",
                    404
                );
                var url =
                    (config["PublicOrigin"] ?? "http://127.0.0.1:5174").TrimEnd('/')
                    + "/join?token="
                    + s.Unprotect(invite.ProtectedToken);
                using var data = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                using var code = new PngByteQRCode(data);
                return Results.File(code.GetGraphic(6), "image/png");
            }
        );
        api.MapGet(
                "/cards/verify/{token}",
                async (string token, Db db) =>
                {
                    Check(token.Length == 64, "Invalid verification code.");
                    var hash = Hash(token);
                    var card = await db
                        .DigitalCards.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.TokenHash == hash);
                    if (card == null)
                    {
                        db.DigitalCardVerifications.Add(new() { Valid = false });
                        await db.SaveChangesAsync();
                        return Results.Ok(new { valid = false, message = "Card not recognized." });
                    }
                    var m = await db
                        .Memberships.AsNoTracking()
                        .Include(x => x.Person)
                            .ThenInclude(x => x.Privacy)
                        .Include(x => x.Club)
                        .SingleAsync(x => x.Id == card.MembershipId);
                    var valid =
                        card.RevokedAt == null
                        && (card.State == "Legacy" || card.State == "Issued")
                        && (card.ExpiryAt == null || card.ExpiryAt > DateTimeOffset.UtcNow)
                        && m.Status == "Active"
                        && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow);
                    db.DigitalCardVerifications.Add(new() { CardId = card.Id, Valid = valid });
                    await db.SaveChangesAsync();
                    return Results.Ok(
                        new
                        {
                            valid,
                            name = m.Person.Privacy.CardNameVisible
                                ? m.Person.DisplayName
                                : "Verified member",
                            club = m.Club.Name,
                            status = card.RevokedAt != null ? "Card revoked" : m.Status,
                            expiresAt = m.EndsAt,
                            message = valid
                                ? "Current membership verified."
                                : "This card does not verify a current active membership.",
                        }
                    );
                }
            )
            .RequireRateLimiting("cards");
    }

    public static string FileType(byte[] bytes)
    {
        if (
            bytes.Length > 8
            && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
        )
            return "image/png";
        if (bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255)
            return "image/jpeg";
        if (
            bytes.Length >= 10
            && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-"
            && Encoding
                .ASCII.GetString(
                    bytes,
                    Math.Max(0, bytes.Length - 1024),
                    Math.Min(bytes.Length, 1024)
                )
                .Contains("%%EOF")
        )
            return "application/pdf";
        return "";
    }

    public static (byte[] Bytes, string Type) StripPhoto(byte[] b)
    {
        var type = FileType(b);
        Check(type is "image/png" or "image/jpeg", "A valid PNG or JPEG image is required.");
        using var output = new MemoryStream();
        if (type == "image/png")
        {
            Check(b.Length >= 33, "Invalid PNG.");
            var width = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(16, 4));
            var height = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(20, 4));
            Check(
                width is > 0 and <= 4096 && height is > 0 and <= 4096,
                "Photo dimensions exceed 4096 pixels."
            );
            output.Write(b, 0, 8);
            var pos = 8;
            while (pos + 12 <= b.Length)
            {
                var length = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(pos, 4));
                Check(length >= 0 && length <= b.Length - pos - 12, "Invalid PNG chunk.");
                var name = Encoding.ASCII.GetString(b, pos + 4, 4);
                if (new[] { "IHDR", "PLTE", "IDAT", "IEND", "tRNS", "sRGB" }.Contains(name))
                    output.Write(b, pos, length + 12);
                pos += length + 12;
                if (name == "IEND")
                    return (output.ToArray(), type);
            }
            throw new ApiError(400, "Incomplete PNG.");
        }
        output.Write(b, 0, 2);
        var offset = 2;
        var dimensions = false;
        while (offset + 4 <= b.Length)
        {
            Check(b[offset] == 255, "Invalid JPEG marker.");
            var marker = b[offset + 1];
            var length = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(offset + 2, 2));
            Check(length >= 2 && offset + length + 2 <= b.Length, "Invalid JPEG segment.");
            if (
                new byte[]
                {
                    192,
                    193,
                    194,
                    195,
                    197,
                    198,
                    199,
                    201,
                    202,
                    203,
                    205,
                    206,
                    207,
                }.Contains(marker)
            )
            {
                Check(length >= 7, "Invalid JPEG frame.");
                var height = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(offset + 5, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(offset + 7, 2));
                Check(
                    width is > 0 and <= 4096 && height is > 0 and <= 4096,
                    "Photo dimensions exceed 4096 pixels."
                );
                dimensions = true;
            }
            if (marker == 218)
            {
                Check(dimensions, "Missing JPEG dimensions.");
                var end = b.AsSpan(offset).IndexOf(new byte[] { 255, 217 });
                Check(end >= 0, "Incomplete JPEG.");
                output.Write(b, offset, end + 2);
                return (output.ToArray(), type);
            }
            if (marker is not 225 and not 237 and not 254)
                output.Write(b, offset, length + 2);
            offset += length + 2;
        }
        throw new ApiError(400, "Incomplete JPEG.");
    }

    private static string RenderCard(Membership m, Person p, Tenant tenant, string url)
    {
        string E(string? x) => System.Security.SecurityElement.Escape(x ?? "") ?? "";
        using var data = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        var png = Convert.ToBase64String(qr.GetGraphic(6));
        var photo =
            p.Photo == null
                ? ""
                : $"<image href='data:{E(p.PhotoType)};base64,{Convert.ToBase64String(p.Photo)}' x='40' y='115' width='100' height='110' preserveAspectRatio='xMidYMid slice'/>";
        var color = System.Text.RegularExpressions.Regex.IsMatch(tenant.Color, "^#[0-9a-fA-F]{6}$")
            ? tenant.Color
            : "#2563eb";
        return $"<svg xmlns='http://www.w3.org/2000/svg' width='856' height='540' viewBox='0 0 856 540'><rect width='856' height='540' rx='28' fill='#102b50'/><path d='M0 0H856V84H0Z' fill='{color}'/><circle cx='47' cy='42' r='19' fill='none' stroke='#e3c78b' stroke-width='2'/><text x='82' y='50' fill='white' font-family='Arial' font-size='24'>{E(tenant.Name)}</text><text x='40' y='115' fill='#b7c9e0' font-family='Arial' font-size='13'>DIGITAL MEMBERSHIP IDENTITY</text>{photo}<text x='{(p.Photo == null ? 40 : 160)}' y='170' fill='white' font-family='Arial' font-size='30'>{E(p.DisplayName)}</text><text x='{(p.Photo == null ? 40 : 160)}' y='204' fill='#d9bc7d' font-family='Arial' font-size='20'>{E(m.Number)}</text><text x='40' y='288' fill='#dbe7f7' font-family='Arial' font-size='23'>{E(m.Club.Name)}</text><text x='40' y='325' fill='#9fb6d2' font-family='Arial' font-size='18'>{E(m.Category.Name)} · {E(m.Status)}</text><text x='40' y='374' fill='#b8c9df' font-family='Arial' font-size='16'>Valid through: {E(m.EndsAt?.ToString("dd MMM yyyy") ?? "While membership is active")}</text><image href='data:image/png;base64,{png}' x='650' y='307' width='155' height='155'/><text x='650' y='484' fill='#b8c9df' font-family='Arial' font-size='13'>Scan to verify validity</text><path d='M40 420H600' stroke='#294363'/><text x='40' y='458' fill='#9fb6d2' font-family='Arial' font-size='13'>Issued by {E(m.Club.Name)}</text><text x='40' y='485' fill='#9fb6d2' font-family='Arial' font-size='13'>{E(m.Club.Email)}</text></svg>";
    }
}
