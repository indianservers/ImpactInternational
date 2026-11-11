using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record DocumentMetadataInput(
    string Title,
    string Folder,
    string Tags,
    string Visibility,
    Guid[]? SharedUsers = null,
    DateTimeOffset? RetainUntil = null
);

public static partial class OperationsEndpoints
{
    static void MapDocuments(RouteGroupBuilder api)
    {
        api.MapGet(
            "/documents",
            async (
                OperationsService s,
                string? search = null,
                string? folder = null,
                Guid? operationId = null,
                Guid? after = null
            ) =>
            {
                var w = await s.A.WorkspaceAny("operations.view", "operations.documents");
                var q = s.Db.GovernanceDocuments.Where(d =>
                    d.TenantId == w.TenantId
                    && (
                        s.A.VisibleIds(w).Contains(d.OrgId)
                        || d.SharedUsers.Contains(w.UserId.ToString())
                    )
                    && d.Status != "Expired"
                    && (d.RetainUntil == null || d.RetainUntil > DateTimeOffset.UtcNow)
                    && (
                        d.Visibility != "Private"
                        || d.UploaderId == w.UserId
                        || d.SharedUsers.Contains(w.UserId.ToString())
                    )
                );
                if (search != null)
                {
                    Check(search.Length <= 100, "Search too long.");
                    q = q.Where(d =>
                        EF.Functions.ILike(d.Title, "%" + search + "%")
                        || EF.Functions.ILike(d.Tags, "%" + search + "%")
                    );
                }
                if (folder != null)
                    q = q.Where(d => d.Folder == folder);
                if (operationId != null)
                    q = q.Where(d => d.OperationId == operationId);
                if (after != null)
                    q = q.Where(d => d.Id.CompareTo(after) > 0);
                var docs = await q.OrderBy(d => d.Id).Take(100).ToListAsync();
                var safe = new List<object>();
                foreach (var d in docs)
                {
                    try
                    {
                        if (d.ObjectKey == "" || d.CommitteeId != null || d.OperationId != null)
                            await s.Document(d);
                        safe.Add(
                            new
                            {
                                d.Id,
                                d.OrgId,
                                d.Title,
                                d.Folder,
                                d.Tags,
                                d.Visibility,
                                d.FileName,
                                d.ContentType,
                                d.CurrentVersion,
                                d.Status,
                                d.At,
                                d.RetainUntil,
                                d.OperationId,
                            }
                        );
                    }
                    catch (ApiError e) when (e.Status is 403 or 404) { }
                }
                return safe;
            }
        );
        api.MapPost(
                "/documents",
                async (HttpRequest req, OperationsService s, CommunicationStorage storage) =>
                {
                    var f = await req.ReadFormAsync();
                    Check(Guid.TryParse(f["orgId"], out var org), "Organization required.");
                    var w = await s.A.Workspace("operations.documents");
                    await s.Governance.Scope(w, org);
                    var title = f["title"].ToString();
                    OperationsService.Title(title);
                    var visibility = f["visibility"].ToString();
                    Check(visibility is "Organization" or "Private", "Choose document visibility.");
                    var shared = string.IsNullOrEmpty(f["sharedUsers"])
                        ? Array.Empty<Guid>()
                        : Unpack<Guid[]>(f["sharedUsers"].ToString());
                    await ValidateShared(shared, w, s);
                    Guid? operation = Guid.TryParse(f["operationId"], out var op) ? op : null;
                    if (operation != null)
                    {
                        if (await s.Db.ClubMeetings.AnyAsync(m => m.Id == operation))
                            Check(
                                (await s.Meeting(operation.Value, true)).OrgId == org,
                                "Ownership mismatch."
                            );
                        else
                            Check(
                                (await s.Project(operation.Value, true)).OrgId == org,
                                "Ownership mismatch."
                            );
                    }
                    var file = f.Files.GetFile("file");
                    var data = await ReadObject(file, storage);
                    var d = new GovernanceDocument
                    {
                        TenantId = w.TenantId,
                        OrgId = org,
                        Title = title,
                        Kind = f["kind"].ToString() is "" ? "Miscellaneous" : f["kind"].ToString(),
                        Folder = f["folder"].ToString() is "" ? "General" : f["folder"].ToString(),
                        Visibility = visibility,
                        SharedUsers = Pack(shared),
                        OperationId = operation,
                        UploaderId = w.UserId,
                        ObjectKey = data.Key,
                        FileName = data.Name,
                        ContentType = data.Type,
                        Sha256 = data.Hash,
                        RetainUntil = DateTimeOffset.TryParse(f["retainUntil"], out var expiry)
                            ? expiry.ToUniversalTime()
                            : null,
                    };
                    s.Db.GovernanceDocuments.Add(d);
                    s.Db.DocumentVersions.Add(
                        new()
                        {
                            TenantId = w.TenantId,
                            OrgId = org,
                            DocumentId = d.Id,
                            Number = 1,
                            ObjectKey = data.Key,
                            FileName = data.Name,
                            ContentType = data.Type,
                            Sha256 = data.Hash,
                            Size = data.Size,
                            UploaderId = w.UserId,
                        }
                    );
                    s.Governance.Audit(w, org, "operations.document.uploaded", d.Id);
                    await s.Db.SaveChangesAsync();
                    return new
                    {
                        d.Id,
                        d.Title,
                        d.CurrentVersion,
                    };
                }
            )
            .DisableAntiforgery();
        api.MapGet(
            "/documents/{id:guid}",
            async (Guid id, OperationsService s) =>
            {
                var d = await s.Db.GovernanceDocuments.SingleOrDefaultAsync(x => x.Id == id);
                Check(d != null, "Document unavailable.", 404);
                await s.Document(d);
                return new
                {
                    document = new
                    {
                        d.Id,
                        d.Title,
                        d.Folder,
                        d.Tags,
                        d.Visibility,
                        d.SharedUsers,
                        d.Status,
                        d.CurrentVersion,
                        d.RetainUntil,
                        d.OperationId,
                        legacy = d.ObjectKey == "",
                    },
                    versions = await s
                        .Db.DocumentVersions.Where(v => v.DocumentId == id)
                        .OrderByDescending(v => v.Number)
                        .Select(v => new
                        {
                            v.Id,
                            v.Number,
                            v.FileName,
                            v.ContentType,
                            v.Sha256,
                            v.Size,
                            v.UpdatedAt,
                        })
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
                "/documents/{id:guid}/versions",
                async (
                    Guid id,
                    HttpRequest req,
                    OperationsService s,
                    CommunicationStorage storage
                ) =>
                {
                    var d = await s.Db.GovernanceDocuments.SingleOrDefaultAsync(x => x.Id == id);
                    Check(d != null, "Document unavailable.", 404);
                    await s.Document(d);
                    Check(
                        d.ObjectKey != "",
                        "This original governance record retains its existing workflow. New vault versions and sharing apply to object-backed records.",
                        409
                    );
                    var w = await s.A.Workspace("operations.documents");
                    await s.Governance.Scope(w, d.OrgId);
                    Check(
                        d.Status is "Available" or "Changes Requested",
                        "Document under review.",
                        409
                    );
                    var form = await req.ReadFormAsync();
                    Check(
                        int.TryParse(form["version"], out var version)
                            && version == d.CurrentVersion,
                        "Document version changed.",
                        409
                    );
                    await using var tx = await s.Db.Database.BeginTransactionAsync();
                    await s.Lock(id);
                    await s.Db.Entry(d).ReloadAsync();
                    Check(d.CurrentVersion == version, "Document changed.", 409);
                    var data = await ReadObject(form.Files.GetFile("file"), storage);
                    d.CurrentVersion++;
                    d.ObjectKey = data.Key;
                    d.FileName = data.Name;
                    d.ContentType = data.Type;
                    d.Sha256 = data.Hash;
                    d.Status = "Available";
                    s.Db.DocumentVersions.Add(
                        new()
                        {
                            TenantId = d.TenantId,
                            OrgId = d.OrgId,
                            DocumentId = id,
                            Number = d.CurrentVersion,
                            ObjectKey = data.Key,
                            FileName = data.Name,
                            ContentType = data.Type,
                            Sha256 = data.Hash,
                            Size = data.Size,
                            UploaderId = w.UserId,
                        }
                    );
                    s.Governance.Audit(w, d.OrgId, "operations.document.version", id);
                    await s.Db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return new { d.Id, d.CurrentVersion };
                }
            )
            .DisableAntiforgery();
        api.MapPut(
            "/documents/{id:guid}",
            async (Guid id, DocumentMetadataInput i, OperationsService s) =>
            {
                var d = await s.Db.GovernanceDocuments.SingleOrDefaultAsync(x => x.Id == id);
                Check(d != null, "Document unavailable.", 404);
                await s.Document(d);
                Check(
                    d.ObjectKey != "",
                    "This original governance record retains its existing workflow. New vault versions and sharing apply to object-backed records.",
                    409
                );
                var w = await s.A.Workspace("operations.documents");
                await s.Governance.Scope(w, d.OrgId);
                Check(d.UploaderId == w.UserId, "Only the owner manages document sharing.", 403);
                Check(d.Status != "Review", "Document is under review.", 409);
                OperationsService.Title(i.Title);
                Check(
                    i.Visibility is "Private" or "Organization"
                        && i.Folder.Length <= 100
                        && i.Tags.Length <= 1000,
                    "Invalid metadata."
                );
                await ValidateShared(i.SharedUsers ?? [], w, s);
                d.Title = i.Title;
                d.Folder = i.Folder;
                d.Tags = i.Tags;
                d.Visibility = i.Visibility;
                d.SharedUsers = Pack(i.SharedUsers ?? []);
                d.RetainUntil = i.RetainUntil?.ToUniversalTime();
                s.Governance.Audit(w, d.OrgId, "operations.document.metadata", id);
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapGet(
            "/documents/{id:guid}/download",
            async (
                Guid id,
                int? version,
                OperationsService s,
                CommunicationStorage storage,
                HttpResponse response,
                bool preview = false
            ) =>
            {
                var d = await s.Db.GovernanceDocuments.SingleOrDefaultAsync(x => x.Id == id);
                Check(d != null, "Document unavailable.", 404);
                await s.Document(d);
                Check(
                    d.Status == "Available" || d.UploaderId == (await s.A.User()).Id,
                    "Document awaiting approval.",
                    403
                );
                response.Headers.CacheControl = "private, no-store";
                response.Headers["X-Content-Type-Options"] = "nosniff";
                if (preview)
                {
                    response.Headers["X-Frame-Options"] = "SAMEORIGIN";
                    response.Headers.ContentSecurityPolicy =
                        "default-src 'none'; frame-ancestors 'self'";
                    response.Headers.ContentDisposition = "inline";
                }
                if (d.ObjectKey == "")
                {
                    Check(
                        version == null || version == d.CurrentVersion,
                        "Original version unavailable.",
                        404
                    );
                    return Results.File(d.Content, d.ContentType, preview ? null : d.FileName);
                }
                var v = await s.Db.DocumentVersions.SingleOrDefaultAsync(x =>
                    x.DocumentId == id && x.Number == (version ?? d.CurrentVersion)
                );
                Check(
                    v != null && File.Exists(storage.PathFor(v.ObjectKey)),
                    "Object unavailable.",
                    404
                );
                return Results.File(
                    storage.PathFor(v.ObjectKey),
                    v.ContentType,
                    preview ? null : v.FileName,
                    enableRangeProcessing: true
                );
            }
        );
        api.MapPost(
            "/documents/{id:guid}/submit",
            async (Guid id, OperationsService s) =>
            {
                var d = await s.Db.GovernanceDocuments.SingleOrDefaultAsync(x => x.Id == id);
                Check(d != null, "Document unavailable.", 404);
                await s.Document(d);
                Check(
                    d.ObjectKey != "",
                    "This original governance record retains its existing workflow. New vault versions and sharing apply to object-backed records.",
                    409
                );
                var w = await s.A.Workspace("operations.documents");
                await s.Governance.Scope(w, d.OrgId);
                Check(
                    d.UploaderId == w.UserId && d.Status == "Available",
                    "Owner may submit an available version.",
                    409
                );
                var r = await s.Governance.Request(
                    w,
                    d.OrgId,
                    "OperationDocument",
                    "Document version review",
                    Pack(new OperationReference(id, d.CurrentVersion, d.Sha256))
                );
                d.Status = "Review";
                d.ApprovalRequestId = r.Id;
                await s.Governance.Submit(r, w);
                await s.Db.SaveChangesAsync();
                return r;
            }
        );
    }

    static async Task ValidateShared(Guid[] users, Assignment w, OperationsService s)
    {
        Check(users.Length <= 100, "Maximum 100 shares.");
        Check(
            await s
                .Db.Assignments.Where(a => a.TenantId == w.TenantId && users.Contains(a.UserId))
                .Select(a => a.UserId)
                .Distinct()
                .CountAsync() == users.Distinct().Count(),
            "Shares must belong to this association."
        );
    }

    static async Task<(string Key, string Name, string Type, string Hash, long Size)> ReadObject(
        IFormFile? file,
        CommunicationStorage storage
    )
    {
        Check(
            file != null && file.Length is > 0 and <= 5242880,
            "Upload PDF, PNG or JPEG up to 5MB."
        );
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var bytes = stream.ToArray();
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
        Check(type != "", "File signature and extension must agree.");
        var key = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(storage.Root);
        await File.WriteAllBytesAsync(storage.PathFor(key), bytes);
        return (
            key,
            Path.GetFileName(file.FileName),
            type,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
            bytes.Length
        );
    }
}
