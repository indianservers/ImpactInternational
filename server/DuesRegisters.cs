using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public class DuesCollectionLine
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid? PaymentId { get; set; }
    public string Invoice { get; set; } = "";
    public string Member { get; set; } = "";
    public string Receipt { get; set; } = "";
    public string Currency { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Source { get; set; } = "";
    public string Method { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Allocated { get; set; }
    public decimal Unallocated { get; set; }
    public string Status { get; set; } = "";
}

public static partial class DuesCenter
{
    static IResult ExportRegister(
        string title,
        string[] headers,
        IEnumerable<string[]> values,
        string format
    )
    {
        if (format == "xlsx")
            return Results.File(
                FinanceWorkbook.Build(headers, values),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                title + ".xlsx"
            );
        if (format == "pdf")
            return Results.File(
                FinancePdf.Build(
                    title,
                    new[] { string.Join(" | ", headers) }
                        .Concat(values.Select(v => string.Join(" | ", v)))
                        .ToArray()
                ),
                "application/pdf",
                title + ".pdf"
            );
        Check(format == "csv", "Invalid export format.");
        string Cell(string s)
        {
            var trimmed = s.TrimStart();
            if (
                s.Length > 0 && "=+-@\t\r\n".Contains(s[0])
                || trimmed.Length > 0 && "=+-@".Contains(trimmed[0])
            )
                s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return Results.File(
            Encoding
                .UTF8.GetPreamble()
                .Concat(
                    Encoding.UTF8.GetBytes(
                        string.Join(
                            "\r\n",
                            new[] { headers }
                                .Concat(values)
                                .Select(r => string.Join(",", r.Select(Cell)))
                        )
                    )
                )
                .ToArray(),
            "text/csv",
            title + ".csv"
        );
    }

    static void MapRegisters(RouteGroupBuilder api)
    {
        api.MapGet(
            "/collections",
            async (
                Access a,
                CommunicationService comm,
                Guid? orgId = null,
                DateOnly? from = null,
                DateOnly? to = null,
                string source = "",
                string search = "",
                int page = 1,
                string format = ""
            ) =>
            {
                var w = await Workspace(a, comm, View);
                Check(
                    page >= 1
                        && page <= 100000
                        && search.Length <= 100
                        && new[]
                        {
                            "",
                            "Gateway",
                            "Manual",
                            "Unallocated",
                            "Settled",
                            "Failed",
                            "Refunded",
                        }.Contains(source),
                    "Invalid collection filters."
                );
                var iq = Invoices(a.Db, a, w);
                if (orgId != null)
                {
                    Check(
                        await a.Visible(w).AnyAsync(o => o.Id == orgId),
                        "Organization outside your scope.",
                        404
                    );
                    iq = iq.Where(i =>
                        a.Db.Memberships.Any(m =>
                            m.Id == i.MembershipId
                            && a.Tree(w.TenantId, orgId.Value).Any(o => o.Id == m.ClubId)
                        )
                    );
                }
                var verified =
                    from x in a.Db.FinanceAllocations
                    join p in a.Db.FinancePayments on x.PaymentId equals p.Id
                    join i in iq on x.InvoiceId equals i.Id
                    select new DuesCollectionLine
                    {
                        Id = x.Id,
                        InvoiceId = i.Id,
                        PaymentId = p.Id,
                        Invoice = i.Number,
                        Member = i.Customer,
                        Receipt = p.ReceiptNumber,
                        Currency = p.Currency,
                        Date = p.Date,
                        Source = a.Db.FinanceIntents.Any(t => t.PaymentId == p.Id)
                            ? "Gateway"
                            : "Manual",
                        Method = p.Method,
                        Reference = p.Reference,
                        Amount = x.Amount,
                        Allocated = x.Amount,
                        Unallocated = p.Amount - p.Allocated - p.Refunded,
                        Status = p.Refunded > 0 ? "Refunded" : p.SettlementStatus,
                    };
                var manual =
                    from c in a.Db.DuesCollections
                    join i in iq on c.InvoiceId equals i.Id
                    where c.Status != "Approved"
                    select new DuesCollectionLine
                    {
                        Id = c.Id,
                        InvoiceId = i.Id,
                        PaymentId = null,
                        Invoice = i.Number,
                        Member = i.Customer,
                        Receipt = c.Reference,
                        Currency = c.Currency,
                        Date = c.Date,
                        Source = "Manual",
                        Method = c.Method,
                        Reference = c.Reference,
                        Amount = c.Amount,
                        Allocated = 0,
                        Unallocated = 0,
                        Status = c.Status,
                    };
                var rows = verified.Concat(manual);
                if (source == "Failed")
                    rows =
                        from t in a.Db.FinanceIntents
                        join i in iq on t.InvoiceId equals i.Id
                        where t.State == "Failed"
                        select new DuesCollectionLine
                        {
                            Id = t.Id,
                            InvoiceId = i.Id,
                            Invoice = i.Number,
                            Member = i.Customer,
                            Receipt = t.ProviderId,
                            Currency = i.Currency,
                            Date = i.Date,
                            Source = "Gateway",
                            Method = t.Provider,
                            Reference = t.ProviderId,
                            Amount = t.Amount,
                            Allocated = 0,
                            Unallocated = 0,
                            Status = t.State,
                        };
                else if (source is "Gateway" or "Manual")
                    rows = rows.Where(r => r.Source == source);
                else if (source == "Unallocated")
                    rows = rows.Where(r => r.Unallocated > 0);
                else if (source != "")
                    rows = rows.Where(r => r.Status == source);
                if (from != null)
                    rows = rows.Where(r => r.Date >= from);
                if (to != null)
                    rows = rows.Where(r => r.Date <= to);
                Check(from == null || to == null || from <= to, "Invalid collection dates.");
                if (search != "")
                    rows = rows.Where(r =>
                        EF.Functions.ILike(r.Member, "%" + search + "%")
                        || EF.Functions.ILike(r.Invoice, "%" + search + "%")
                        || EF.Functions.ILike(r.Reference, "%" + search + "%")
                    );
                var total = await rows.CountAsync();
                var sorted = rows.OrderByDescending(r => r.Date).ThenBy(r => r.Id);
                if (format != "")
                {
                    Check(total <= 20000, "Narrow the export to 20,000 rows.");
                    var data = await sorted.ToListAsync();
                    return ExportRegister(
                        "impact-dues-collections",
                        [
                            "Date",
                            "Member",
                            "Invoice",
                            "Receipt",
                            "Currency",
                            "Source",
                            "Method",
                            "Amount",
                            "Allocated to invoice",
                            "Unallocated payment",
                            "Status",
                            "Reference",
                        ],
                        data.Select(r =>
                            new[]
                            {
                                r.Date.ToString("yyyy-MM-dd"),
                                r.Member,
                                r.Invoice,
                                r.Receipt,
                                r.Currency,
                                r.Source,
                                r.Method,
                                r.Amount.ToString(CultureInfo.InvariantCulture),
                                r.Allocated.ToString(CultureInfo.InvariantCulture),
                                r.Unallocated.ToString(CultureInfo.InvariantCulture),
                                r.Status,
                                r.Reference,
                            }
                        ),
                        format
                    );
                }
                return Results.Ok(
                    new
                    {
                        total,
                        page,
                        items = await sorted.Skip((page - 1) * 50).Take(50).ToListAsync(),
                        notice = "Verified rows represent allocations to individual dues invoices, not duplicate receipts. Approved manual entries appear only through their linked payment allocation. Submitted/failed entries are not collected funds. Unallocated balances are payment-level context and must not be summed across allocations.",
                    }
                );
            }
        );
        api.MapGet(
            "/adjustments",
            async (
                Access a,
                CommunicationService comm,
                Guid? orgId = null,
                int page = 1,
                string format = "",
                DateOnly? from = null,
                DateOnly? to = null,
                string currency = "",
                string search = ""
            ) =>
            {
                var w = await Workspace(a, comm, View);
                Check(
                    page >= 1
                        && page <= 100000
                        && currency.Length <= 3
                        && search.Length <= 100
                        && (from == null || to == null || from <= to),
                    "Invalid filters."
                );
                var iq = Invoices(a.Db, a, w);
                if (orgId != null)
                {
                    Check(
                        await a.Visible(w).AnyAsync(o => o.Id == orgId),
                        "Organization outside your scope.",
                        404
                    );
                    iq = iq.Where(i =>
                        a.Db.Memberships.Any(m =>
                            m.Id == i.MembershipId
                            && a.Tree(w.TenantId, orgId.Value).Any(o => o.Id == m.ClubId)
                        )
                    );
                }
                var q =
                    from c in a.Db.FinanceCredits
                    join i in iq on c.InvoiceId equals i.Id
                    select new
                    {
                        c.Id,
                        c.InvoiceId,
                        invoice = i.Number,
                        member = i.Customer,
                        i.Currency,
                        c.Amount,
                        c.Reason,
                        c.Date,
                        actor = a
                            .Db.Users.Where(u => u.Id == c.CreatorId)
                            .Select(u => u.Name)
                            .First(),
                    };
                if (from != null)
                    q = q.Where(c => c.Date >= from);
                if (to != null)
                    q = q.Where(c => c.Date <= to);
                if (currency != "")
                    q = q.Where(c => c.Currency == currency);
                if (search != "")
                    q = q.Where(c =>
                        EF.Functions.ILike(c.member, "%" + search + "%")
                        || EF.Functions.ILike(c.invoice, "%" + search + "%")
                        || EF.Functions.ILike(c.Reason, "%" + search + "%")
                    );
                var total = await q.CountAsync();
                var sorted = q.OrderByDescending(c => c.Date).ThenBy(c => c.Id);
                if (format != "")
                {
                    Check(total <= 20000, "Narrow the export to 20,000 records.");
                    var data = await sorted.ToListAsync();
                    return ExportRegister(
                        "impact-dues-adjustments",
                        [
                            "Date",
                            "Member",
                            "Invoice",
                            "Currency",
                            "Amount",
                            "Reason",
                            "Recorded by",
                        ],
                        data.Select(r =>
                            new[]
                            {
                                r.Date.ToString("yyyy-MM-dd"),
                                r.member,
                                r.invoice,
                                r.Currency,
                                r.Amount.ToString(CultureInfo.InvariantCulture),
                                r.Reason,
                                r.actor,
                            }
                        ),
                        format
                    );
                }
                return Results.Ok(
                    new
                    {
                        total,
                        page,
                        items = await sorted.Skip((page - 1) * 50).Take(50).ToListAsync(),
                    }
                );
            }
        );
    }
}
