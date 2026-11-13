using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public class DuesCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ClubId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid RecorderId { get; set; }
    public Guid RequestKey { get; set; }
    public Guid? ReviewerId { get; set; }
    public Guid? PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public DateOnly Date { get; set; }
    public string Method { get; set; } = "Cash";
    public string Reference { get; set; } = "";
    public string AccountHolder { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "Submitted";
    public string ReviewNote { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
}
public record DuesCollectionInput(Guid InvoiceId, decimal Amount, DateOnly Date, string Method, string Reference,
    string AccountHolder, string Destination, string Evidence, string Notes, Guid RequestKey);
public record DuesCollectionReview(bool Approve, string Note);

public static class DuesCenter
{
    public const string View = "finance.dues.reports", Collect = "finance.dues.collect";
    public static readonly string[] Purposes = ["ClubDues", "AssociationDues", "DistrictDues", "RegionalDues", "ZonalDues", "NationalDues", "InternationalDues", "JoiningFee", "Renewal"];
    public static readonly string[][] Reports =
    [
        ["overview", "Dues overview"], ["club", "Club-wise dues"], ["zone", "Zone-wise dues"],
        ["district", "District-wise dues"], ["region", "Region-wise dues"], ["national", "National-wise dues"],
        ["member", "Member dues ledger"], ["pending", "All pending dues"], ["overdue", "Overdue dues"],
        ["unpaid", "Unpaid invoices"], ["partial", "Partially paid invoices"], ["paid", "Fully cleared invoices"],
        ["ageing", "Due ageing"], ["purpose", "Dues by purpose"], ["billing-month", "Monthly billing"],
        ["collection-month", "Monthly verified collections"], ["cash", "Cash collection register"],
        ["personal", "Personal-account collection register"], ["verification", "Pending collection verification"],
        ["rejected", "Rejected collections"], ["collector", "Collector performance"],
    ];
    static IQueryable<FinanceInvoice> Invoices(Db db, Access a, Assignment w) => db.FinanceInvoices.Where(i =>
        i.TenantId == w.TenantId && i.State == "Issued" && i.MembershipId != null && Purposes.Contains(i.Purpose)
        && db.Memberships.Any(m => m.Id == i.MembershipId && m.TenantId == w.TenantId && a.Visible(w).Any(o => o.Id == m.ClubId)));
    static async Task<Assignment> Workspace(Access a, CommunicationService comm, string permission)
    {
        var w = await a.Workspace(permission);
        await LeaderCommunications.CheckAssignment(comm, w);
        return w;
    }
    public static void MapDuesCenter(this RouteGroupBuilder root)
    {
        var api = root.MapGroup("/finance/dues-center");
        api.MapGet("/options", async (Access a, CommunicationService comm) =>
        {
            var w = await Workspace(a, comm, View);
            return new { reports = Reports.Select(r => new { id = r[0], name = r[1] }),
                organizations = await a.Visible(w).Where(o => o.Status == "Active").OrderBy(o => o.Name)
                    .Select(o => new { o.Id, o.Name, o.ParentId, type = a.Db.Levels.Where(l => l.Id == o.LevelId).Select(l => l.Name).First() }).ToListAsync(),
                canCollect = (await a.Grants(w)).Contains(Collect), canReview = (await a.Grants(w)).Contains("finance.reconcile") };
        });
        api.MapGet("/invoices", async (Access a, CommunicationService comm, string? search, Guid? clubId, int page = 1) =>
        {
            var w = await Workspace(a, comm, Collect);
            var q = Invoices(a.Db, a, w).Where(i => i.Total > i.Paid + i.Credited);
            if (clubId != null) q = q.Where(i => a.Db.Memberships.Any(m => m.Id == i.MembershipId && m.ClubId == clubId));
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(i => EF.Functions.ILike(i.Customer, "%" + search + "%") || EF.Functions.ILike(i.Number, "%" + search + "%"));
            return new { total = await q.CountAsync(), items = await q.OrderBy(i => i.DueOn).ThenBy(i => i.Id).Skip((Math.Clamp(page, 1, 10000) - 1) * 50).Take(50)
                .Select(i => new { i.Id, i.Number, i.Customer, i.Currency, i.DueOn, outstanding = i.Total - i.Paid - i.Credited,
                    entity = a.Db.FinanceEntitys.Where(e => e.Id == i.EntityId).Select(e => e.Name).First(),
                    club = a.Db.Memberships.Where(m => m.Id == i.MembershipId).Select(m => m.Club.Name).First(),
                    pendingVerification = a.Db.DuesCollections.Where(c => c.InvoiceId == i.Id && c.Status == "Submitted").Sum(c => c.Amount) }).ToListAsync() };
        });
        api.MapPost("/collections", Submit);
        api.MapPost("/collections/{id:guid}/review", Review);
        api.MapGet("/reports/{kind}", Report);
    }
    static async Task<IResult> Submit(DuesCollectionInput input, Access a, CommunicationService comm, FinanceService finance)
    {
        var w = await Workspace(a, comm, Collect); var db = a.Db;
        Check(input.RequestKey != Guid.Empty && input.Amount > 0 && input.Date <= FinanceService.Today
            && new[] { "Cash", "PersonalAccount", "BankTransfer", "UPI" }.Contains(input.Method)
            && !string.IsNullOrWhiteSpace(input.Reference) && input.Reference.Length <= 150
            && (input.AccountHolder?.Length ?? 0) <= 200 && (input.Destination?.Length ?? 0) <= 200
            && (input.Notes?.Length ?? 0) <= 2000 && (input.Evidence?.Length ?? 0) <= 2000, "Enter a valid amount, date, method, and receipt/transaction reference.");
        Check(input.Method != "PersonalAccount" || !string.IsNullOrWhiteSpace(input.AccountHolder) && !string.IsNullOrWhiteSpace(input.Destination),
            "For personal-account payments, enter the account holder and UPI ID or masked account details.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var invoice = await Invoices(db, a, w).SingleOrDefaultAsync(i => i.Id == input.InvoiceId);
        Check(invoice != null, "Invoice is outside your assigned members.", 404);
        await finance.Lock(invoice.EntityId);
        Check((await Workspace(a, comm, Collect)).Id == w.Id, "Your active workspace changed. Try again.", 409);
        var membership = await db.Memberships.SingleAsync(m => m.Id == invoice.MembershipId);
        var actor = await a.User();
        Check(actor.PlatformAdmin || membership.ClubId == w.OrgId, "Record collections only for your assigned club.", 403);
        var prior = await db.DuesCollections.SingleOrDefaultAsync(c => c.TenantId == w.TenantId && c.RecorderId == w.UserId && c.RequestKey == input.RequestKey);
        if (prior != null)
        {
            Check(prior.InvoiceId == input.InvoiceId && prior.Amount == input.Amount && prior.Date == input.Date && prior.Method == input.Method
                && prior.Reference == input.Reference.Trim().ToUpperInvariant() && prior.AccountHolder == (input.AccountHolder ?? "").Trim()
                && prior.Destination == (input.Destination ?? "").Trim() && prior.Notes == (input.Notes ?? "").Trim() && prior.Evidence == (input.Evidence ?? "").Trim(), "Request key already used for different collection details.", 409);
            return Results.Ok(prior);
        }
        var entity = await db.FinanceEntitys.SingleAsync(e => e.Id == invoice.EntityId && e.TenantId == w.TenantId);
        Check(entity.Active && invoice.State == "Issued", "Invoice or financial entity unavailable.", 409);
        finance.Exact(entity, input.Amount);
        var reserved = await db.DuesCollections.Where(c => c.InvoiceId == invoice.Id && c.Status == "Submitted").SumAsync(c => c.Amount);
        Check(input.Amount <= invoice.Outstanding - reserved, "Amount exceeds the dues remaining after pending collections.", 409);
        var reference = input.Reference.Trim().ToUpperInvariant();
        Check(!await db.DuesCollections.AnyAsync(c => c.TenantId == w.TenantId && c.ClubId == membership.ClubId && c.Reference == reference)
            && !await db.FinancePayments.AnyAsync(p => p.EntityId == invoice.EntityId && p.Reference.ToUpper() == reference), "Receipt/transaction reference already recorded.", 409);
        var collection = new DuesCollection { TenantId = w.TenantId, ClubId = membership.ClubId, InvoiceId = invoice.Id,
            RecorderId = w.UserId, RequestKey = input.RequestKey, Amount = input.Amount, Currency = invoice.Currency,
            Date = input.Date, Method = input.Method, Reference = reference, AccountHolder = (input.AccountHolder ?? "").Trim(),
            Destination = (input.Destination ?? "").Trim(), Evidence = (input.Evidence ?? "").Trim(), Notes = (input.Notes ?? "").Trim() };
        db.DuesCollections.Add(collection);
        finance.Audit(entity, w.UserId, "dues.collection.submit", collection.Id + ":" + reference);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(collection);
    }
    static async Task<IResult> Review(Guid id, DuesCollectionReview input, Access a, FinanceService finance)
    {
        var w = await a.Workspace("finance.reconcile"); var db = a.Db;
        var c = await db.DuesCollections.SingleOrDefaultAsync(c => c.Id == id && c.TenantId == w.TenantId);
        Check(c != null, "Collection unavailable.", 404);
        var invoice = await db.FinanceInvoices.SingleAsync(i => i.Id == c.InvoiceId && i.TenantId == w.TenantId);
        var entity = await finance.Entity(invoice.EntityId, "finance.reconcile");
        await using var tx = await db.Database.BeginTransactionAsync(); await finance.Lock(entity.Id); await db.Entry(c).ReloadAsync();
        Check(c.RecorderId != w.UserId, "A different finance reviewer must verify this collection.", 403);
        Check(c.Status == "Submitted", "Collection has already been reviewed.", 409);
        Check(!string.IsNullOrWhiteSpace(input.Note) && input.Note.Length <= 2000, "Enter a verification or rejection note.");
        if (input.Approve)
        {
            Check(invoice.State == "Issued" && c.Amount <= invoice.Outstanding, "The invoice balance changed. Reject this collection and record corrected details.", 409);
            Check(!await db.FinancePayments.AnyAsync(p => p.EntityId == entity.Id && p.Reference.ToUpper() == c.Reference), "Payment reference already exists in the books.", 409);
            var payment = finance.Bind(new FinancePayment { PersonId = invoice.PersonId, Payer = invoice.Customer, Reference = c.Reference,
                Method = c.Method == "PersonalAccount" ? "BankTransfer" : c.Method, Currency = c.Currency, Amount = c.Amount, Date = c.Date,
                RecorderId = c.RecorderId, IdempotencyKey = "dues-collection:" + c.Id,
                Evidence = string.Join(" | ", new[] { c.Method, c.AccountHolder, c.Destination, c.Evidence, c.Notes }.Where(v => v != "")) }, entity);
            db.FinancePayments.Add(payment);
            await finance.Verify(payment, entity, w.UserId);
            await finance.Allocate(payment, invoice, c.Amount, entity, w.UserId, "dues-collection:" + c.Id);
            c.PaymentId = payment.Id;
        }
        c.Status = input.Approve ? "Approved" : "Rejected"; c.ReviewerId = w.UserId; c.ReviewedAt = DateTimeOffset.UtcNow; c.ReviewNote = input.Note.Trim();
        finance.Audit(entity, w.UserId, "dues.collection." + c.Status.ToLowerInvariant(), c.Id.ToString());
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(c);
    }

    static async Task<IResult> Report(string kind, Access a, CommunicationService comm, Guid? orgId, DateOnly? from, DateOnly? to, string? currency,
        string? format, int page = 1)
    {
        var w = await Workspace(a, comm, View); var db = a.Db;
        Check(Reports.Any(r => r[0] == kind), "Unknown dues report.", 404);
        var start = from ?? DateOnly.MinValue; var end = to ?? FinanceService.Today;
        Check(start <= end, "From date must be before the through date.");
        var iq = Invoices(db, a, w);
        if (orgId != null)
        {
            Check(await a.Visible(w).AnyAsync(o => o.Id == orgId), "Organization is outside your scope.", 404);
            iq = iq.Where(i => db.Memberships.Any(m => m.Id == i.MembershipId && a.Tree(w.TenantId, orgId.Value).Any(o => o.Id == m.ClubId)));
        }
        if (!string.IsNullOrWhiteSpace(currency)) iq = iq.Where(i => i.Currency == currency);
        Check(await iq.CountAsync() <= 20000, "Choose a smaller organization or currency; limit one report to 20,000 invoices.");
        var all = await iq.OrderBy(i => i.Date).ThenBy(i => i.Id).AsNoTracking().ToListAsync();
        var ids = all.Select(i => i.Id).ToArray();
        var members = await db.Memberships.Where(m => all.Select(i => i.MembershipId).Contains(m.Id)).Select(m => new { m.Id, m.ClubId, m.Number, m.Club.Name }).ToDictionaryAsync(m => m.Id);
        var nodes = await (from o in db.Orgs join l in db.Levels on o.LevelId equals l.Id where o.TenantId == w.TenantId select new { o.Id, o.ParentId, o.Name, Level = l.Name }).ToDictionaryAsync(o => o.Id);
        string Level(FinanceInvoice i, string level)
        {
            Guid? id = members[i.MembershipId!.Value].ClubId;
            for (int depth = 0; id != null && depth < 16 && nodes.TryGetValue(id.Value, out var n); depth++, id = n.ParentId)
                if (n.Level == level) return n.Name;
            return "Unassigned";
        }
        Guid LevelId(FinanceInvoice i, string level)
        {
            Guid? id = members[i.MembershipId!.Value].ClubId;
            for (int depth = 0; id != null && depth < 16 && nodes.TryGetValue(id.Value, out var n); depth++, id = n.ParentId)
                if (n.Level == level) return n.Id;
            return Guid.Empty;
        }
        var invoices = all.Where(i => i.Date >= start && i.Date <= end).ToList();
        var claims = await db.DuesCollections.Where(c => c.TenantId == w.TenantId && ids.Contains(c.InvoiceId) && c.Date >= start && c.Date <= end).OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).AsNoTracking().ToListAsync();
        var allocated = await (from allocation in db.FinanceAllocations join payment in db.FinancePayments on allocation.PaymentId equals payment.Id
            where ids.Contains(allocation.InvoiceId) && payment.Status == "Verified" && payment.Date >= start && payment.Date <= end
            select new { allocation.InvoiceId, allocation.Amount, payment.Date, payment.Currency }).ToListAsync();
        var invoiceById = all.ToDictionary(i => i.Id);
        var entityIds = all.Select(i => i.EntityId).Distinct().ToArray();
        var entities = await db.FinanceEntitys.Where(e => entityIds.Contains(e.Id) && e.TenantId == w.TenantId).ToDictionaryAsync(e => e.Id, e => e.Name);
        var canReview = (await a.Grants(w)).Contains("finance.reconcile");
        var platformAdmin = (await a.User()).PlatformAdmin;
        var reviewableEntities = await db.FinanceGrants.Where(g => g.TenantId == w.TenantId && g.UserId == w.UserId
            && new[] { "Treasurer", "Owner" }.Contains(g.Role)).Select(g => g.EntityId).ToListAsync();
        var users = await db.Users.Where(u => claims.Select(c => c.RecorderId).Contains(u.Id)).Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name);
        Dictionary<string, object?> InvoiceRow(FinanceInvoice i) => new()
        {
            ["invoiceId"] = i.Id, ["invoice"] = i.Number, ["member"] = i.Customer, ["memberId"] = members[i.MembershipId!.Value].Number,
            ["entity"] = entities.GetValueOrDefault(i.EntityId, "Unknown"),
            ["club"] = Level(i, "Club"), ["region"] = Level(i, "Region"), ["district"] = Level(i, "District"), ["zone"] = Level(i, "Zone"),
            ["currency"] = i.Currency, ["billed"] = i.Total, ["collected"] = i.Paid, ["credits"] = i.Credited, ["pending"] = i.Outstanding,
            ["dueOn"] = i.DueOn, ["status"] = i.PaymentStatus, ["purpose"] = i.Purpose,
            ["pendingVerification"] = claims.Where(c => c.InvoiceId == i.Id && c.Status == "Submitted").Sum(c => c.Amount),
        };
        List<Dictionary<string, object?>> Aggregate(IEnumerable<FinanceInvoice> source, Func<FinanceInvoice, string> label, Func<FinanceInvoice, string>? identity = null) => source
            .GroupBy(i => new { Name = label(i), Identity = identity?.Invoke(i) ?? label(i), i.Currency }).OrderBy(g => g.Key.Name).ThenBy(g => g.Key.Currency)
            .Select(g => new Dictionary<string, object?> { ["group"] = g.Key.Name, ["groupId"] = g.Key.Identity, ["currency"] = g.Key.Currency,
                ["invoices"] = g.Count(), ["billed"] = g.Sum(i => i.Total), ["collected"] = g.Sum(i => i.Paid), ["credits"] = g.Sum(i => i.Credited),
                ["pending"] = g.Sum(i => i.Outstanding), ["overdue"] = g.Where(i => i.DueOn < FinanceService.Today).Sum(i => i.Outstanding) }).ToList();
        var rows = kind switch
        {
            "overview" => Aggregate(invoices, _ => "Impact dues"),
            "club" or "zone" or "district" or "region" or "national" => Aggregate(invoices, i => Level(i, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(kind)), i => LevelId(i, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(kind)).ToString()),
            "purpose" => Aggregate(invoices, i => i.Purpose),
            "billing-month" => Aggregate(invoices, i => i.Date.ToString("yyyy-MM")),
            "ageing" => Aggregate(invoices.Where(i => i.Outstanding > 0), i => i.DueOn >= FinanceService.Today ? "Not yet overdue" : FinanceService.Today.DayNumber - i.DueOn.DayNumber <= 30 ? "1–30 days" : FinanceService.Today.DayNumber - i.DueOn.DayNumber <= 60 ? "31–60 days" : FinanceService.Today.DayNumber - i.DueOn.DayNumber <= 90 ? "61–90 days" : "90+ days"),
            "collection-month" => allocated.GroupBy(p => new { Month = p.Date.ToString("yyyy-MM"), p.Currency }).OrderBy(g => g.Key.Month)
                .Select(g => new Dictionary<string, object?> { ["month"] = g.Key.Month, ["currency"] = g.Key.Currency, ["collections"] = g.Count(), ["collected"] = g.Sum(p => p.Amount) }).ToList(),
            "collector" => claims.GroupBy(c => new { c.RecorderId, c.Currency }).Select(g => new Dictionary<string, object?> {
                ["collector"] = users.GetValueOrDefault(g.Key.RecorderId, "Unknown"), ["collectorId"] = g.Key.RecorderId, ["currency"] = g.Key.Currency,
                ["submitted"] = g.Sum(c => c.Amount), ["approved"] = g.Where(c => c.Status == "Approved").Sum(c => c.Amount),
                ["pendingVerification"] = g.Where(c => c.Status == "Submitted").Sum(c => c.Amount), ["rejected"] = g.Where(c => c.Status == "Rejected").Sum(c => c.Amount) }).ToList(),
            "cash" or "personal" or "verification" or "rejected" => claims.Where(c => kind == "cash" ? c.Method == "Cash" : kind == "personal" ? c.Method == "PersonalAccount" : kind == "verification" ? c.Status == "Submitted" : c.Status == "Rejected")
                .Select(c => new Dictionary<string, object?> { ["collectionId"] = c.Id, ["invoice"] = invoiceById[c.InvoiceId].Number, ["member"] = invoiceById[c.InvoiceId].Customer,
                    ["entity"] = entities.GetValueOrDefault(invoiceById[c.InvoiceId].EntityId, "Unknown"),
                    ["club"] = Level(invoiceById[c.InvoiceId], "Club"), ["region"] = Level(invoiceById[c.InvoiceId], "Region"), ["currency"] = c.Currency, ["amount"] = c.Amount,
                    ["date"] = c.Date, ["method"] = c.Method, ["reference"] = c.Reference, ["accountHolder"] = c.AccountHolder, ["destination"] = c.Destination,
                    ["evidence"] = c.Evidence, ["notes"] = c.Notes, ["status"] = c.Status, ["collector"] = users.GetValueOrDefault(c.RecorderId, "Unknown"),
                    ["reviewNote"] = c.ReviewNote, ["canReview"] = c.Status == "Submitted" && c.RecorderId != w.UserId && canReview
                        && (platformAdmin || reviewableEntities.Contains(invoiceById[c.InvoiceId].EntityId)) }).ToList(),
            _ => invoices.Where(i => kind == "member" || kind == "pending" && i.Outstanding > 0 || kind == "overdue" && i.Outstanding > 0 && i.DueOn < FinanceService.Today
                || kind == "unpaid" && i.Paid == 0 && i.Outstanding > 0 || kind == "partial" && i.Paid > 0 && i.Outstanding > 0 || kind == "paid" && i.Outstanding <= 0).Select(InvoiceRow).ToList(),
        };
        if (format == "csv")
        {
            string Cell(object? value) { var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""; if (s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s; return "\"" + s.Replace("\"", "\"\"") + "\""; }
            var columns = rows.SelectMany(r => r.Keys).Distinct().Where(k => k != "canReview").ToArray();
            var csv = string.Join(",", columns.Select(Cell)) + "\r\n" + string.Join("\r\n", rows.Select(r => string.Join(",", columns.Select(k => Cell(r.GetValueOrDefault(k))))));
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", "impact-dues-" + kind + ".csv");
        }
        Check(string.IsNullOrEmpty(format), "Choose CSV export or the on-screen report.");
        return Results.Ok(new { title = Reports.Single(r => r[0] == kind)[1], kind, from = start, to = end, total = rows.Count,
            page = Math.Clamp(page, 1, 10000), items = rows.Skip((Math.Clamp(page, 1, 10000) - 1) * 50).Take(50),
            summary = Aggregate(invoices, _ => "Total"),
            notice = "Current balances for issued membership-linked invoices dated in the selected range. Collection registers use payment dates; monthly verified collections include allocations to older invoices. Currencies are separate. Submitted collections do not reduce dues until independently verified. Cash/personal collections remain unsettled until bank settlement." });
    }
}
