using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class BulkImportEndpoints
{
    public record DuesRow(int Row, string Key, string MemberNumber, decimal Amount,
        DateOnly Date, DateOnly DueOn, string Purpose, string Description,
        DateOnly? PeriodStart, DateOnly? PeriodEnd, string Notes);
    public record DuesPreview(Guid Actor, Guid Workspace, Guid Tenant, Guid Entity,
        DateTimeOffset Expires, string FileName, string Hash, DuesRow[] Rows);
    public record CommitDues(string Token);
    public record ReviewedDues(DuesRow Input, Guid? MembershipId, string Member,
        string Status, string Error, Guid? InvoiceId);
    public static readonly string[] DuesFields = ["key", "memberNumber", "amount", "date", "dueOn",
        "purpose", "description", "periodStart", "periodEnd", "notes"];

    public static void MapBulkImportTools(this RouteGroupBuilder api)
    {
        api.MapGet("/membership/dues-imports/entities", async (Access a) =>
        {
            var w = await a.Workspace("members.import");
            Check((await a.Grants(w)).Contains("finance.manage"), "Financial billing permission is required.", 403);
            var platform = (await a.User()).PlatformAdmin;
            return await a.Db.FinanceEntitys.Where(e => e.TenantId == w.TenantId && e.Active
                && (platform || a.Db.FinanceGrants.Any(g => g.EntityId == e.Id && g.UserId == w.UserId
                    && new[] { "Billing", "Treasurer", "Owner" }.Contains(g.Role))))
                .OrderBy(e => e.Name).Select(e => new { e.Id, e.Name, e.Currency }).ToListAsync();
        });
        api.MapGet("/membership/import-template", async (Access a, string kind = "members", string format = "csv", bool sample = false) =>
        {
            await a.Workspace("members.import");
            Check((kind is "members" or "dues") && (format is "csv" or "xlsx"), "Choose members or dues and CSV or Excel.");
            var fields = kind == "members" ? MembershipImport.Fields : DuesFields;
            string[][] rows = !sample ? [] : kind == "members"
                ? [["Sample Member (replace this row)", "sample.member@example.test", "", "Hyderabad", "India", "", "", "2026-11-14", "Business", "Services"]]
                : [["LEGACY-DUES-2026-001", "REPLACE-WITH-MEMBER-NUMBER", "1200.00", "2026-11-14", "2026-12-14", "ClubDues", "Annual membership dues", "2026-01-01", "2026-12-31", "Replace sample values before upload"]];
            if (format == "csv") return Csv(fields, rows, $"impact-{kind}-{(sample ? "sample" : "template")}.csv");
            using var book = new XLWorkbook();
            var sheet = book.AddWorksheet(kind == "members" ? "Members" : "Dues");
            for (int c = 0; c < fields.Length; c++) sheet.Cell(1, c + 1).Value = fields[c];
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < fields.Length; c++) sheet.Cell(r + 2, c + 1).Value = rows[r][c];
            sheet.Range(1, 1, 1, fields.Length).Style.Font.Bold = true;
            sheet.Range(1, 1, 1, fields.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#eee7fa");
            sheet.SheetView.FreezeRows(1);
            sheet.Columns().Width = 23;
            var help = book.AddWorksheet("Instructions");
            var notes = kind == "members" ? new[] {
                "Use the first sheet for data. Delete the sample row before importing real records.",
                "name is required. Map columns after uploading. Empty clubCode/categoryCode use your chosen defaults.",
                "Dates use YYYY-MM-DD. Keep phone numbers as text. Do not enter formulas.",
                "Existing matches and shared contacts require review. Skip duplicates or choose an existing person.",
                "Imports create memberships only. Import dues afterwards using the generated membership numbers."
            } : new[] {
                "Use the first sheet for data. Replace the sample membership number and key before upload.",
                "key, memberNumber, amount, date and dueOn are required. Keep keys stable on every retry.",
                "Export members to obtain memberNumber. Members must be within your scope and served by the selected financial entity.",
                "Amounts use the financial entity currency. Dates use YYYY-MM-DD. dueOn must not precede date.",
                "Optional periodStart and periodEnd must be supplied together. Duplicate periods and repeated keys are checked.",
                "Dues are imported as draft invoices. Review and issue them through Finance. Paid amounts require verified receipts."
            };
            for (int i = 0; i < notes.Length; i++) help.Cell(i + 1, 1).Value = notes[i];
            help.Column(1).Width = 110; help.Column(1).Style.Alignment.WrapText = true;
            help.Rows(1, notes.Length).Height = 40;
            using var stream = new MemoryStream(); book.SaveAs(stream);
            return Results.File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"impact-{kind}-{(sample ? "sample" : "template")}.xlsx");
        });

        api.MapGet("/membership/import-audit", async (Access a) =>
        {
            var w = await a.Workspace("members.import");
            return await a.Db.Audits.Where(x => x.TenantId == w.TenantId && x.OrgId != null && a.VisibleIds(w).Contains(x.OrgId.Value)
                && (x.Action.StartsWith("member.import.") || x.Action.StartsWith("dues.import.")))
                .OrderByDescending(x => x.At).Take(100)
                .Select(x => new { x.Id, x.At, x.Action, x.Resource, x.Changes,
                    actor = a.Db.Users.Where(u => u.Id == x.ActorId).Select(u => u.Name).FirstOrDefault() }).ToListAsync();
        });

        api.MapGet("/membership/imports/{id:guid}/report", async (Guid id, MembershipService s) =>
        {
            var (w, job) = await MembershipImport.Job(id, s);
            Check(job.CreatorId == w.UserId || (await s.Access.Grants(w)).Contains("members.contacts.view"), "Import report is restricted to authorized reviewers.", 403);
            var rows = await s.Db.MemberImportRows.Where(r => r.JobId == id).OrderBy(r => r.RowNumber).ToListAsync();
            var numbers = await s.Db.Memberships.Where(m => m.ImportRowId != null && s.Db.MemberImportRows.Any(r => r.JobId == id && r.Id == m.ImportRowId)).ToDictionaryAsync(m => m.ImportRowId!.Value, m => m.Number);
            await s.Access.Log(w, "member.import.report", id.ToString());
            return Csv(["row", "status", "validation", "existingPersonId", "membershipNumber"], rows.Select(r => new[] {
                r.RowNumber.ToString(), r.Status, r.Error, r.ExistingPersonId?.ToString() ?? "",
                numbers.GetValueOrDefault(r.Id, "")
            }), "member-import-report.csv");
        });

        api.MapPost("/membership/dues-imports/preview", async (IFormFile file,
            [Microsoft.AspNetCore.Mvc.FromForm] Guid entityId, FinanceService s, IDataProtectionProvider protection) =>
        {
            var w = await s.A.Workspace("members.import");
            var entity = await s.Entity(entityId, "finance.manage");
            Check(file.Length is > 0 and <= 1048576, "Use a CSV or Excel file no larger than 1 MB and 500 rows.");
            using var stream = new MemoryStream(); await file.CopyToAsync(stream);
            TabularData table;
            try { table = MembershipImport.Parse(stream.ToArray(), file.FileName, 500); }
            catch (ApiError) { throw; }
            catch { throw new ApiError(400, "The file could not be read as safe CSV or Excel data."); }
            var headers = table.Headers.Select(h => h.Trim().ToLowerInvariant()).ToArray();
            Check(new[] { "key", "membernumber", "amount", "date", "dueon" }.All(headers.Contains), "Required columns: key, memberNumber, amount, date, dueOn.");
            Check(headers.All(h => DuesFields.Any(f => f.Equals(h, StringComparison.OrdinalIgnoreCase))), "Remove unsupported columns. Payments must be recorded through verified receipts.");
            Check(table.Rows.Count > 0, "There are no dues rows to import.");
            var rows = new List<DuesRow>(); var errors = new List<ReviewedDues>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string Get(string field) { var index = Array.IndexOf(headers, field.ToLowerInvariant()); return index < 0 ? "" : table.Rows[i][index].Trim(); }
                try
                {
                    Check(decimal.TryParse(Get("amount"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount), "Use a positive amount without currency symbols or thousands separators.");
                    s.Exact(entity, amount);
                    DateOnly Date(string field) { Check(DateOnly.TryParseExact(Get(field), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d), field + " must use YYYY-MM-DD."); return d; }
                    var date = Date("date"); var due = Date("dueOn");
                    var start = Get("periodStart") == "" ? (DateOnly?)null : Date("periodStart");
                    var end = Get("periodEnd") == "" ? (DateOnly?)null : Date("periodEnd");
                    var purpose = Get("purpose") == "" ? "ClubDues" : Get("purpose");
                    var description = Get("description") == "" ? "Membership dues" : Get("description");
                    Check(Get("key").Length is > 0 and <= 100 && Get("memberNumber").Length is > 0 and <= 100, "Supply a stable key and membership number.");
                    Check(due >= date && date.Year >= 1900 && due.Year <= 2200, "Invalid invoice or due date.");
                    Check(start.HasValue == end.HasValue && (start == null || start <= end), "Supply both period dates in chronological order.");
                    Check((purpose.EndsWith("Dues") || purpose is "JoiningFee" or "Renewal") && FinanceService.Purposes.Contains(purpose), "Choose a supported dues purpose.");
                    Check(description.Length <= 500 && Get("notes").Length <= 2000, "Description or notes are too long.");
                    rows.Add(new(i + 2, Get("key"), Get("memberNumber"), amount, date, due, purpose, description, start, end, Get("notes")));
                }
                catch (ApiError e) { errors.Add(new(new(i + 2, Get("key"), Get("memberNumber"), 0, default, default, "", "", null, null, ""), null, "", "Invalid", e.Message, null)); }
            }
            var reviewed = rows.Count == 0 ? new List<ReviewedDues>() : await Review(rows.ToArray(), s, w, entity);
            reviewed.AddRange(errors); reviewed = reviewed.OrderBy(r => r.Input.Row).ToList();
            var valid = reviewed.All(r => r.Status is "Ready" or "AlreadyImported");
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray()));
            var snapshot = new DuesPreview(w.UserId, w.Id, w.TenantId, entity.Id, DateTimeOffset.UtcNow.AddMinutes(30), Path.GetFileName(file.FileName), hash, rows.ToArray());
            var token = valid ? protection.CreateProtector("Impact.BulkDues.v1").Protect(JsonSerializer.Serialize(snapshot)) : null;
            await s.A.Log(w, "dues.import.preview", entity.Id.ToString(), JsonSerializer.Serialize(new { snapshot.FileName, hash, rows = reviewed.Count, errors = reviewed.Count(r => r.Status == "Invalid") }));
            return Results.Ok(new { token, rows = reviewed, total = reviewed.Count, ready = reviewed.Count(r => r.Status == "Ready"), duplicates = reviewed.Count(r => r.Status == "AlreadyImported"), errors = reviewed.Count(r => r.Status == "Invalid"), currency = entity.Currency });
        }).DisableAntiforgery();

        api.MapPost("/membership/dues-imports/commit", async (CommitDues input, FinanceService s, IDataProtectionProvider protection) =>
        {
            var w = await s.A.Workspace("members.import");
            Check(input.Token is { Length: > 0 and <= 2000000 }, "A reviewed import token is required.");
            DuesPreview snapshot;
            try { snapshot = JsonSerializer.Deserialize<DuesPreview>(protection.CreateProtector("Impact.BulkDues.v1").Unprotect(input.Token))!; }
            catch { throw new ApiError(400, "The preview is invalid. Upload the file again."); }
            Check(snapshot != null && snapshot.Actor == w.UserId && snapshot.Workspace == w.Id && snapshot.Tenant == w.TenantId, "This preview belongs to another workspace.", 403);
            Check(snapshot.Expires > DateTimeOffset.UtcNow, "The preview expired. Upload the file again.", 409);
            var entity = await s.Entity(snapshot.Entity, "finance.manage");
            await using var tx = await s.A.Mutation(w);
            await s.Lock(entity.Id);
            var rows = await Review(snapshot.Rows, s, w, entity);
            Check(rows.All(r => r.Status is "Ready" or "AlreadyImported"), "Records changed after preview. Upload again and review the latest errors.", 409);
            var created = 0; var reused = 0; var results = new List<object>();
            foreach (var row in rows)
            {
                var r = row.Input;
                if (row.Status == "AlreadyImported") { reused++; results.Add(new { row = r.Row, invoiceId = row.InvoiceId, status = "AlreadyImported" }); continue; }
                var invoice = await s.CreateInvoice(entity, await s.Db.Memberships.Where(m => m.Id == row.MembershipId).Select(m => m.PersonId).SingleAsync(), row.Member,
                    r.Purpose, r.Date, r.DueOn, "bulk-dues:" + r.Key,
                    [new(r.Description, 1, r.Amount)], w.UserId, membership: row.MembershipId, start: r.PeriodStart, end: r.PeriodEnd, notes: r.Notes);
                created++;
                s.Audit(entity, w.UserId, "dues.import.row", JsonSerializer.Serialize(new { r.Row, r.Key, membershipId = row.MembershipId, invoiceId = invoice.Id, snapshot.Hash }));
                results.Add(new { row = r.Row, invoiceId = (Guid?)invoice.Id, status = "Created" });
            }
            await s.A.Log(w, "dues.import.completed", entity.Id.ToString(), JsonSerializer.Serialize(new { snapshot.FileName, snapshot.Hash, created, reused, rows = results }));
            await tx.CommitAsync();
            return Results.Ok(new { created, reused, state = "Draft", rows = results });
        });
    }

    static async Task<List<ReviewedDues>> Review(DuesRow[] rows, FinanceService s, Assignment w, FinanceEntity e)
    {
        var result = new List<ReviewedDues>();
        Check(rows.Length is > 0 and <= 500, "Import 1–500 dues rows at a time.");
        var numbers = rows.Select(r => r.MemberNumber).Distinct().ToArray();
        var members = await s.Db.Memberships.Include(m => m.Person).Where(m => m.TenantId == w.TenantId && numbers.Contains(m.Number)
            && s.A.VisibleIds(w).Contains(m.ClubId) && (m.ClubId == e.OrgId || s.Db.FinanceOrganizations.Any(o => o.EntityId == e.Id && o.ServedOrgId == m.ClubId))).ToListAsync();
        var ids = members.Select(m => m.Id).ToArray();
        var keys = rows.Select(r => "bulk-dues:" + r.Key).ToArray();
        var old = await s.Db.FinanceInvoices.Where(i => i.EntityId == e.Id && (keys.Contains(i.IdempotencyKey) || ids.Contains(i.MembershipId ?? Guid.Empty))).ToListAsync();
        Check(await s.Db.FinanceAccounts.AnyAsync(a => a.EntityId == e.Id && a.Code == "4000" && a.Active && a.Kind == "Income"), "Configure an active membership income account (4000) before importing dues.");
        var seenKeys = new HashSet<string>(); var seenCharges = new HashSet<string>();
        foreach (var r in rows)
        {
            var member = members.SingleOrDefault(m => m.Number == r.MemberNumber);
            string status = "Ready", error = ""; Guid? invoiceId = null;
            if (!seenKeys.Add(r.Key)) { status = "Invalid"; error = "Repeated key in this file. Remove the duplicate row."; }
            else if (member == null) { status = "Invalid"; error = "Membership number is unknown, outside your scope, or not served by this financial entity."; }
            else
            {
                var fingerprint = JsonSerializer.Serialize(new { member.Id, r.Purpose, Date = r.PeriodStart == null ? (DateOnly?)r.Date : null, r.PeriodStart, r.PeriodEnd });
                var previous = old.SingleOrDefault(i => i.IdempotencyKey == "bulk-dues:" + r.Key);
                var requestHash = Hash(GovernanceService.Pack(new { person = (Guid?)member.PersonId, customer = member.Person.DisplayName, purpose = r.Purpose, date = r.Date, due = r.DueOn,
                    lines = new FinanceLineInput[] { new(r.Description, 1, r.Amount) }, eventId = (Guid?)null, projectId = (Guid?)null, notes = r.Notes }));
                if (!seenCharges.Add(fingerprint)) { status = "Invalid"; error = "Repeated member/dues period in this file. Consolidate charges into one row."; }
                else if (previous != null)
                {
                    if (previous.RequestHash == requestHash && previous.MembershipId == member.Id && previous.PeriodStart == r.PeriodStart && previous.PeriodEnd == r.PeriodEnd && previous.State != "Cancelled") { status = "AlreadyImported"; invoiceId = previous.Id; }
                    else { status = "Invalid"; error = "This key already belongs to different or voided dues. Review the existing invoice."; }
                }
                else if (old.Any(i => i.MembershipId == member.Id && i.Purpose == r.Purpose && i.State != "Cancelled" && (r.PeriodStart != null ? i.PeriodStart == r.PeriodStart && i.PeriodEnd == r.PeriodEnd : i.Date == r.Date)))
                { status = "Invalid"; error = "Dues already exist for this member and period/date under another key. Review Finance before importing."; }
            }
            result.Add(new(r, member?.Id, member?.Person.DisplayName ?? "", status, error, invoiceId));
        }
        return result;
    }

    public static IResult Csv(string[] fields, IEnumerable<string[]> rows, string name)
    {
        string Quote(string v) => "\"" + MembershipImport.Safe(v).Replace("\"", "\"\"") + "\"";
        var csv = string.Join(',', fields.Select(Quote)) + "\r\n" + string.Join("\r\n", rows.Select(r => string.Join(',', r.Select(Quote))));
        return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", name);
    }
}
