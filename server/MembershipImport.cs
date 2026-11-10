using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using static Clubs.Access;

namespace Clubs;

public record TabularData(string[] Headers, List<string[]> Rows);

public record ImportMapping(Dictionary<string, string> Fields);

public record ImportResolution(
    Guid RowId,
    bool CreateSeparate = false,
    Guid? ExistingPersonId = null
);

public record ResolveImport(ImportResolution[] Rows);

public record ExportInput(
    string Format = "csv",
    string[]? Columns = null,
    Guid[]? SelectedIds = null,
    string? Search = null,
    string? Statuses = null,
    Guid? ClubId = null,
    Guid? CategoryId = null,
    string? City = null,
    string? Profession = null,
    string? Industry = null,
    string? Expertise = null,
    string? Office = null
);

public static class MembershipImport
{
    public static readonly string[] Fields =
    [
        "name",
        "email",
        "phone",
        "city",
        "country",
        "clubCode",
        "categoryCode",
        "joinedAt",
        "profession",
        "industry",
    ];

    public static TabularData Parse(
        byte[] payload,
        string fileName,
        int maxRows = 10000,
        bool preview = false
    )
    {
        Check(payload.Length is > 0 and <= 10485760, "Import files must be 10 MB or smaller.");
        var rows = new List<string[]>();
        string[] headers;
        if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = new MemoryStream(payload);
            using var parser = new TextFieldParser(stream, Encoding.UTF8, true)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true,
            };
            parser.SetDelimiters(",");
            headers = parser.ReadFields() ?? [];
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields() ?? [];
                if (fields.All(string.IsNullOrWhiteSpace))
                    continue;
                Check(
                    fields.Length <= headers.Length,
                    "A CSV row has more columns than its header."
                );
                rows.Add(
                    fields.Concat(Enumerable.Repeat("", headers.Length - fields.Length)).ToArray()
                );
                if (rows.Count > maxRows)
                {
                    Check(preview, "Limit each import job to 10,000 rows.");
                    rows.RemoveAt(rows.Count - 1);
                    break;
                }
            }
        }
        else
        {
            Check(
                fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase),
                "Only CSV and .xlsx workbooks are supported."
            );
            using (var zip = new ZipArchive(new MemoryStream(payload), ZipArchiveMode.Read))
            {
                Check(
                    zip.Entries.Count <= 2000
                        && zip.Entries.Sum(x => x.Length) <= 52428800
                        && !zip.Entries.Any(x =>
                            x.FullName.EndsWith(
                                "vbaProject.bin",
                                StringComparison.OrdinalIgnoreCase
                            )
                        ),
                    "The workbook is too large or contains macros."
                );
            }
            using var workbook = new XLWorkbook(new MemoryStream(payload));
            var sheet = workbook.Worksheets.First();
            var used = sheet.RangeUsed();
            Check(used != null, "Workbook is empty.");
            Check(used.ColumnCount() <= 30, "Limit imports to 30 columns.");
            headers = Enumerable
                .Range(1, used.ColumnCount())
                .Select(i => Cell(sheet.Cell(used.FirstRow().RowNumber(), i)))
                .ToArray();
            foreach (var row in used.RowsUsed().Skip(1))
            {
                rows.Add(
                    Enumerable.Range(1, headers.Length).Select(i => Cell(row.Cell(i))).ToArray()
                );
                if (rows.Count > maxRows)
                {
                    Check(preview, "Limit each import job to 10,000 rows.");
                    rows.RemoveAt(rows.Count - 1);
                    break;
                }
            }
        }
        headers = headers.Select(x => x.Trim()).ToArray();
        Check(
            headers.Length is > 0 and <= 30
                && headers.All(x => x.Length is > 0 and <= 100)
                && headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() == headers.Length,
            "Use 1–30 unique, nonempty column headings."
        );
        Check(
            rows.All(r => r.All(v => v.Length <= 2000)),
            "Cell values cannot exceed 2,000 characters."
        );
        return new(headers, rows);
    }

    private static string Cell(IXLCell cell)
    {
        Check(!cell.HasFormula, "Formula cells are not accepted. Upload values only.");
        return cell.DataType == XLDataType.DateTime
            ? cell.GetDateTime().ToString("yyyy-MM-dd")
            : cell.GetString();
    }

    public static Dictionary<string, string> Values(
        string[] headers,
        string[] row,
        Dictionary<string, string> mapping
    ) =>
        mapping.ToDictionary(
            k => k.Key,
            k =>
            {
                var index = Array.IndexOf(headers, k.Value);
                return index >= 0 && index < row.Length ? row[index].Trim() : "";
            }
        );

    public static PersonInput Person(Dictionary<string, string> v) =>
        new(
            v.GetValueOrDefault("name", ""),
            v.GetValueOrDefault("name", ""),
            Profession: v.GetValueOrDefault("profession", ""),
            Industry: v.GetValueOrDefault("industry", ""),
            Contacts: new[]
            {
                new ContactInput("Email", "Personal", v.GetValueOrDefault("email", "")),
                new ContactInput("Phone", "Personal", v.GetValueOrDefault("phone", "")),
            }
                .Where(x => x.Value != "")
                .ToArray(),
            Addresses:
            [
                new(
                    v.GetValueOrDefault("country", ""),
                    "",
                    v.GetValueOrDefault("city", ""),
                    "",
                    ""
                ),
            ]
        );

    public static void MapMemberImports(this RouteGroupBuilder api)
    {
        api.MapPost(
                "/membership/imports",
                async (
                    IFormFile file,
                    [Microsoft.AspNetCore.Mvc.FromForm] Guid clubId,
                    [Microsoft.AspNetCore.Mvc.FromForm] Guid categoryId,
                    [Microsoft.AspNetCore.Mvc.FromForm] string requestKey,
                    MembershipService s
                ) =>
                {
                    var w = await s.Access.Workspace("members.import");
                    Check(
                        requestKey.Length is >= 8 and <= 100,
                        "An import idempotency key is required."
                    );
                    Check(
                        await s.Access.Visible(w).AnyAsync(x => x.Id == clubId),
                        "Club outside scope.",
                        403
                    );
                    await s.Club(clubId, w.TenantId);
                    await s.Category(categoryId, w.TenantId);
                    Check(file.Length is > 0 and <= 10485760, "File must be 10 MB or smaller.");
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var payload = ms.ToArray();
                    var hash = Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(payload)
                    );
                    var previous = await s.Db.MemberImportJobs.SingleOrDefaultAsync(x =>
                        x.TenantId == w.TenantId
                        && x.CreatorId == w.UserId
                        && x.RequestKey == requestKey
                    );
                    if (previous != null)
                    {
                        Check(
                            previous.Hash == hash
                                && previous.ClubId == clubId
                                && previous.CategoryId == categoryId,
                            "This idempotency key belongs to another import.",
                            409
                        );
                        return Results.Ok(new { previous.Id, previous.Status });
                    }
                    TabularData preview;
                    try
                    {
                        preview = Parse(payload, file.FileName, 50, true);
                    }
                    catch (ApiError)
                    {
                        throw;
                    }
                    catch
                    {
                        throw new ApiError(
                            400,
                            "The file could not be read as safe CSV or Excel data."
                        );
                    }
                    var job = new MemberImportJob
                    {
                        TenantId = w.TenantId,
                        ScopeAssignmentId = w.Id,
                        CreatorId = w.UserId,
                        ClubId = clubId,
                        CategoryId = categoryId,
                        FileName = Path.GetFileName(file.FileName),
                        Payload = payload,
                        Hash = hash,
                        RequestKey = requestKey,
                        Headers = JsonSerializer.Serialize(preview.Headers),
                    };
                    s.Db.MemberImportJobs.Add(job);
                    await s.Access.Log(w, "member.import.upload", job.Id.ToString());
                    return Results.Ok(
                        new
                        {
                            job.Id,
                            job.Status,
                            preview.Headers,
                            preview.Rows,
                        }
                    );
                }
            )
            .DisableAntiforgery();
        api.MapGet(
            "/membership/imports",
            async (MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.import");
                return await s
                    .Db.MemberImportJobs.Where(x =>
                        x.TenantId == w.TenantId && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
                    )
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new
                    {
                        x.Id,
                        x.FileName,
                        x.Status,
                        x.TotalRows,
                        x.ProcessedRows,
                        x.ImportedRows,
                        x.Error,
                        x.CreatedAt,
                        x.CompletedAt,
                    })
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/membership/imports/{id:guid}",
            async (Guid id, MembershipService s, int page = 1) =>
            {
                var (w, job) = await Job(id, s);
                var grants = await s.Access.Grants(w);
                Check(
                    job.CreatorId == w.UserId || grants.Contains("members.contacts.view"),
                    "Only the uploader or a contact-authorized officer can inspect imported personal data.",
                    403
                );
                return new
                {
                    job.Id,
                    job.FileName,
                    job.Status,
                    job.Headers,
                    job.Mapping,
                    job.TotalRows,
                    job.ProcessedRows,
                    job.ImportedRows,
                    job.Error,
                    job.CreatedAt,
                    job.CompletedAt,
                    totalErrors = await s.Db.MemberImportRows.CountAsync(x =>
                        x.JobId == id && x.Status == "Invalid"
                    ),
                    totalReview = await s.Db.MemberImportRows.CountAsync(x =>
                        x.JobId == id && x.Status == "NeedsReview"
                    ),
                    rows = await s
                        .Db.MemberImportRows.Where(x => x.JobId == id)
                        .OrderBy(x => x.RowNumber)
                        .Skip((Math.Max(1, page) - 1) * 50)
                        .Take(50)
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/membership/imports/{id:guid}/mapping",
            async (Guid id, ImportMapping r, MembershipService s) =>
            {
                var (w, job) = await Job(id, s);
                Check(
                    job.Status == "Uploaded",
                    "Mapping cannot change after validation starts.",
                    409
                );
                var headers = JsonSerializer.Deserialize<string[]>(job.Headers)!;
                Check(
                    r.Fields.ContainsKey("name")
                        && r.Fields.Keys.All(Fields.Contains)
                        && r.Fields.Values.All(headers.Contains),
                    "Map a name column and supported existing headings."
                );
                job.Mapping = JsonSerializer.Serialize(r.Fields);
                job.Status = "Validating";
                await s.Db.SaveChangesAsync();
                return Results.Accepted(value: new { job.Id, job.Status });
            }
        );
        api.MapPost(
            "/membership/imports/{id:guid}/resolve",
            async (Guid id, ResolveImport r, MembershipService s) =>
            {
                var (w, job) = await Job(id, s);
                Check(
                    job.Status == "NeedsReview",
                    "This import is not awaiting duplicate review.",
                    409
                );
                Check(r.Rows.Length is > 0 and <= 200, "Resolve 1–200 rows per request.");
                foreach (var decision in r.Rows)
                {
                    var row = await s.Db.MemberImportRows.SingleOrDefaultAsync(x =>
                        x.Id == decision.RowId && x.JobId == id
                    );
                    Check(
                        row != null && row.Status == "NeedsReview",
                        "Choose a row awaiting review."
                    );
                    Check(
                        decision.CreateSeparate ^ (decision.ExistingPersonId != null),
                        "Explicitly choose a separate person or one existing authorized person."
                    );
                    if (decision.ExistingPersonId != null)
                        Check(
                            await s.Scoped(w)
                                .AnyAsync(x => x.PersonId == decision.ExistingPersonId),
                            "Existing person outside scope.",
                            403
                        );
                    row.CreateSeparate = decision.CreateSeparate;
                    row.ExistingPersonId = decision.ExistingPersonId;
                    row.Status = "Valid";
                    row.Error = "";
                }
                await s.Db.SaveChangesAsync();
                if (
                    !await s.Db.MemberImportRows.AnyAsync(x => x.JobId == id && x.Status != "Valid")
                )
                    job.Status = "Ready";
                await s.Access.Log(w, "member.import.duplicates.reviewed", id.ToString());
                return Results.Ok(new { job.Id, job.Status });
            }
        );
        api.MapPost(
            "/membership/imports/{id:guid}/commit",
            async (Guid id, MembershipService s) =>
            {
                var (w, job) = await Job(id, s);
                if (job.Status == "Completed")
                    return Results.Ok(
                        new
                        {
                            job.Id,
                            job.Status,
                            job.ImportedRows,
                        }
                    );
                Check(
                    job.Status == "Ready"
                        && !await s.Db.MemberImportRows.AnyAsync(x =>
                            x.JobId == id && x.Status != "Valid"
                        ),
                    "Every row must be valid and duplicate-reviewed before import.",
                    409
                );
                job.Status = "Queued";
                await s.Access.Log(w, "member.import.commit.request", id.ToString());
                return Results.Accepted(value: new { job.Id, job.Status });
            }
        );
        api.MapPost(
            "/membership/imports/{id:guid}/cancel",
            async (Guid id, MembershipService s) =>
            {
                var (w, job) = await Job(id, s);
                Check(
                    new[]
                    {
                        "Uploaded",
                        "Validating",
                        "NeedsReview",
                        "Ready",
                        "Queued",
                        "Failed",
                    }.Contains(job.Status),
                    "The final atomic commit has started, or this job is already complete.",
                    409
                );
                job.Status = "Cancelled";
                job.Payload = [];
                await s.Access.Log(w, "member.import.cancelled", id.ToString());
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/membership/imports/{id:guid}/retry",
            async (Guid id, MembershipService s) =>
            {
                var (w, job) = await Job(id, s);
                Check(
                    job.Status == "Failed" && job.Payload.Length > 0 && job.ImportedRows == 0,
                    "Only a failed, uncommitted import with retained input can be retried.",
                    409
                );
                await s.Db.MemberImportRows.Where(x => x.JobId == id).ExecuteDeleteAsync();
                job.Status = "Validating";
                if (job.CreatorId == w.UserId)
                    job.ScopeAssignmentId = w.Id;
                job.ProcessedRows = 0;
                job.Error = "";
                job.LeaseId = null;
                job.LeaseUntil = null;
                await s.Access.Log(w, "member.import.retry", id.ToString());
                return Results.Accepted(value: new { job.Id, job.Status });
            }
        );
        api.MapPost("/membership/export", Export);
    }

    public static async Task<(Assignment, MemberImportJob)> Job(Guid id, MembershipService s)
    {
        var w = await s.Access.Workspace("members.import");
        var job = await s.Db.MemberImportJobs.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == w.TenantId && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
        );
        Check(job != null, "Import outside authorized scope.", 404);
        return (w, job);
    }

    private static async Task<IResult> Export(ExportInput r, MembershipService s)
    {
        var w = await s.Access.Workspace("members.export");
        Check(r.Format is "csv" or "xlsx", "Choose CSV or Excel export.");
        var allowed = new[]
        {
            "number",
            "name",
            "club",
            "category",
            "status",
            "city",
            "country",
            "joinedAt",
            "endsAt",
            "email",
            "phone",
            "profession",
            "industry",
        };
        var columns = (r.Columns ?? allowed.Take(9).ToArray()).Distinct().ToArray();
        Check(
            columns.Length > 0 && columns.All(allowed.Contains),
            "Choose permitted export columns."
        );
        Check(
            r.SelectedIds == null || r.SelectedIds.Length <= 10000,
            "Limit one export to 10,000 selected memberships."
        );
        var q = MembershipEndpoints.Filter(
            s,
            s.Scoped(w),
            r.Search,
            r.Statuses,
            r.ClubId,
            r.CategoryId,
            r.City,
            r.Profession,
            r.Industry,
            r.Expertise,
            r.Office
        );
        if (r.SelectedIds != null)
            q = q.Where(x => r.SelectedIds.Contains(x.Id));
        Check(await q.CountAsync() <= 10000, "Narrow the filters to 10,000 memberships or fewer.");
        var grants = await s.Access.Grants(w);
        var rows = await s.Rows(
                q.OrderBy(x => x.Number),
                true,
                grants.Contains("members.contacts.view"),
                true,
                w.UserId
            )
            .ToListAsync();
        var data = rows.Select(x => new Dictionary<string, string>
            {
                { "number", x.Number },
                { "name", x.Name },
                { "club", x.Club },
                { "category", x.Category },
                { "status", x.Status },
                { "city", x.City },
                { "country", x.Country },
                { "joinedAt", x.JoinedAt.ToString("yyyy-MM-dd") },
                { "endsAt", x.EndsAt?.ToString("yyyy-MM-dd") ?? "" },
                { "email", x.Email ?? "" },
                { "phone", x.Phone ?? "" },
                { "profession", x.Profession ?? "" },
                { "industry", x.Industry ?? "" },
            })
            .ToList();
        await s.Access.Log(w, "member.export", rows.Count.ToString(), string.Join(',', columns));
        if (r.Format == "csv")
        {
            string Quote(string value) => "\"" + Safe(value).Replace("\"", "\"\"") + "\"";
            var text =
                string.Join(',', columns.Select(Quote))
                + "\r\n"
                + string.Join(
                    "\r\n",
                    data.Select(row => string.Join(',', columns.Select(c => Quote(row[c]))))
                );
            return Results.File(
                Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(),
                "text/csv",
                "members.csv"
            );
        }
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Members");
        for (int i = 0; i < columns.Length; i++)
            sheet.Cell(1, i + 1).Value = columns[i];
        for (int row = 0; row < data.Count; row++)
        for (int col = 0; col < columns.Length; col++)
            sheet.Cell(row + 2, col + 1).Value = Safe(data[row][columns[col]]);
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(1, data.Count + 1), columns.Length).SetAutoFilter();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Results.File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "members.xlsx"
        );
    }

    public static string Safe(string text) =>
        text.TrimStart().StartsWith('=')
        || text.TrimStart().StartsWith('+')
        || text.TrimStart().StartsWith('-')
        || text.TrimStart().StartsWith('@')
        || text.StartsWith('\t')
        || text.StartsWith('\r')
            ? "'" + text
            : text;
}
