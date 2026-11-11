using System.Text;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static partial class FinanceEndpoints
{
    static void MapReports(RouteGroupBuilder api)
    {
        api.MapGet(
            "/entities/{entity:guid}/dashboard",
            async (Guid entity, FinanceService s) =>
            {
                var e = await s.Entity(entity);
                var today = FinanceService.Today;
                var month = new DateOnly(today.Year, today.Month, 1);
                var invoices = s.Db.FinanceInvoices.Where(x =>
                    x.EntityId == entity && x.State == "Issued"
                );
                var payments = s.Db.FinancePayments.Where(x =>
                    x.EntityId == entity && x.Status == "Verified"
                );
                var cash = await (
                    from l in s.Db.FinanceJournalLines
                    join ac in s.Db.FinanceAccounts on l.AccountId equals ac.Id
                    where
                        l.EntityId == entity
                        && (
                            ac.Code == "1000"
                            || ac.Code == "1010"
                            || s.Db.FinanceBanks.Any(b =>
                                b.EntityId == entity && b.AccountCode == ac.Code
                            )
                        )
                    select l.Debit - l.Credit
                ).SumAsync();
                return new
                {
                    e.Currency,
                    e.Decimals,
                    outstanding = await invoices.SumAsync(x => x.Total - x.Paid - x.Credited),
                    overdue = await invoices
                        .Where(x => x.DueOn < today)
                        .SumAsync(x => x.Total - x.Paid - x.Credited),
                    todayCollections = await payments
                        .Where(x => x.Date == today)
                        .SumAsync(x => x.Amount - x.Refunded),
                    monthCollections = await payments
                        .Where(x => x.Date >= month)
                        .SumAsync(x => x.Amount - x.Refunded),
                    unsettled = await payments
                        .Where(x => x.SettlementStatus != "Settled")
                        .SumAsync(x => x.Amount - x.Refunded),
                    cashBank = cash,
                    pendingExpenses = await s.Db.FinanceExpenses.CountAsync(x =>
                        x.EntityId == entity && x.Status == "Submitted"
                    ),
                    recentInvoices = await invoices
                        .OrderByDescending(x => x.Date)
                        .ThenByDescending(x => x.Id)
                        .Take(6)
                        .ToListAsync(),
                    recentPayments = await payments
                        .OrderByDescending(x => x.Date)
                        .ThenByDescending(x => x.Id)
                        .Take(6)
                        .ToListAsync(),
                    policy = "Entity books only. Clearing is not bank cash. Budgets are not bank balances. No hierarchy consolidation implied.",
                };
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/journals",
            async (Guid entity, FinanceService s, Guid? after = null) =>
            {
                await s.Entity(entity);
                var q = s.Db.FinanceJournals.Where(x => x.EntityId == entity);
                if (after != null)
                    q = q.Where(x => x.Id.CompareTo(after.Value) > 0);
                return await q.OrderBy(x => x.Id).Take(100).ToListAsync();
            }
        );
        api.MapGet(
            "/journals/{id:guid}",
            async (Guid id, FinanceService s) =>
            {
                var j = await s.Db.FinanceJournals.SingleOrDefaultAsync(x => x.Id == id);
                Check(j != null, "Journal unavailable.", 404);
                await s.Entity(j.EntityId);
                return new
                {
                    journal = j,
                    lines = await (
                        from l in s.Db.FinanceJournalLines
                        join a in s.Db.FinanceAccounts on l.AccountId equals a.Id
                        where l.JournalId == id
                        select new
                        {
                            l.Id,
                            a.Code,
                            a.Name,
                            l.Debit,
                            l.Credit,
                            l.PersonId,
                            l.EventId,
                            l.ProjectId,
                        }
                    ).ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/audit",
            async (Guid entity, FinanceService s, Guid? after = null) =>
            {
                await s.Entity(entity);
                var q = s.Db.FinanceAudits.Where(x => x.EntityId == entity);
                if (after != null)
                    q = q.Where(x => x.Id.CompareTo(after.Value) > 0);
                return await q.OrderBy(x => x.Id).Take(100).ToListAsync();
            }
        );
        api.MapGet("/entities/{entity:guid}/reports/{kind}", Report);
        api.MapGet(
            "/entities/{entity:guid}/budgets/{id:guid}/actual",
            async (Guid entity, Guid id, FinanceService s) =>
            {
                await s.Entity(entity);
                var b = await s.Db.FinanceBudgets.SingleOrDefaultAsync(x =>
                    x.Id == id && x.EntityId == entity
                );
                Check(b != null, "Budget unavailable.", 404);
                var lineage = new List<Guid> { b.Id };
                var parent = b.ReplacesId;
                for (var n = 0; parent != null && n < 100; n++)
                {
                    lineage.Add(parent.Value);
                    parent = await s
                        .Db.FinanceBudgets.Where(x => x.Id == parent && x.EntityId == entity)
                        .Select(x => x.ReplacesId)
                        .SingleAsync();
                }
                var claims = s.Db.FinanceExpenses.Where(x =>
                    x.EntityId == entity && x.BudgetId != null && lineage.Contains(x.BudgetId.Value)
                );
                var committed = await claims
                    .Where(x => x.Status == "Submitted")
                    .SumAsync(x => x.Amount);
                var actual = await claims
                    .Where(x => x.Status == "Approved")
                    .SumAsync(x => x.Amount);
                return new
                {
                    budget = b,
                    committed,
                    actual,
                    available = b.Amount - committed - actual,
                    variance = b.Amount - actual,
                    utilization = b.Amount == 0 ? 0 : decimal.Round(actual / b.Amount * 100, 2),
                    formula = "Actual = approved accrued expenses; commitments = submitted expenses only. Approval consumes commitment. Paid expense is not subtracted a second time.",
                };
            }
        );
    }

    static readonly string[] ReportKinds =
    [
        "dues-register",
        "outstanding",
        "ageing",
        "collections",
        "invoices",
        "receipts",
        "refunds",
        "reconciliation",
        "income-statement",
        "balance-sheet",
        "trial-balance",
        "general-ledger",
        "account-ledger",
        "bank-book",
        "expenses",
        "budget-actual",
        "event",
        "project",
        "donations",
        "inter-entity",
        "audit",
        "member-statement",
    ];

    static async Task<IResult> Report(
        Guid entity,
        string kind,
        FinanceService s,
        DateOnly? from = null,
        DateOnly? to = null,
        string account = "",
        Guid? projectId = null,
        Guid? eventId = null,
        Guid? personId = null,
        string format = "json",
        Guid? after = null
    )
    {
        var e = await s.Entity(entity);
        Check(
            ReportKinds.Contains(kind) && new[] { "json", "csv", "pdf" }.Contains(format),
            "Unknown financial report or format."
        );
        var start = from ?? new DateOnly(FinanceService.Today.Year, 1, 1);
        var end = to ?? FinanceService.Today;
        Check(start <= end && end <= start.AddYears(20), "Invalid report dates.");
        await s.Sources(e, eventId, projectId);
        await s.Person(personId, e);
        var baseLines =
            from l in s.Db.FinanceJournalLines
            join j in s.Db.FinanceJournals on l.JournalId equals j.Id
            join a in s.Db.FinanceAccounts on l.AccountId equals a.Id
            where l.EntityId == entity && j.Date <= end
            select new
            {
                l,
                j,
                a,
            };
        if (projectId != null)
            baseLines = baseLines.Where(x => x.l.ProjectId == projectId);
        if (eventId != null)
            baseLines = baseLines.Where(x => x.l.EventId == eventId);
        if (personId != null)
            baseLines = baseLines.Where(x => x.l.PersonId == personId);
        if (account != "")
            baseLines = baseLines.Where(x => x.a.Code == account);
        var cumulative = await baseLines
            .GroupBy(x => new
            {
                x.a.Id,
                x.a.Code,
                x.a.Name,
                x.a.Kind,
            })
            .Select(g => new
            {
                g.Key.Id,
                g.Key.Code,
                g.Key.Name,
                g.Key.Kind,
                debit = g.Sum(x => x.l.Debit),
                credit = g.Sum(x => x.l.Credit),
                balance = g.Sum(x => x.l.Debit - x.l.Credit),
            })
            .OrderBy(x => x.Code)
            .ToListAsync();
        if (kind == "bank-book")
            baseLines = baseLines.Where(x =>
                x.a.Code == "1000"
                || x.a.Code == "1010"
                || s.Db.FinanceBanks.Any(b => b.EntityId == entity && b.AccountCode == x.a.Code)
            );
        var activity = baseLines.Where(x => x.j.Date >= start);
        var period = await activity
            .GroupBy(x => new
            {
                x.a.Code,
                x.a.Name,
                x.a.Kind,
            })
            .Select(g => new
            {
                g.Key.Code,
                g.Key.Name,
                g.Key.Kind,
                debit = g.Sum(x => x.l.Debit),
                credit = g.Sum(x => x.l.Credit),
                balance = g.Sum(x => x.l.Debit - x.l.Credit),
            })
            .OrderBy(x => x.Code)
            .ToListAsync();
        var income = -period.Where(x => x.Kind == "Income").Sum(x => x.balance);
        var expense = period.Where(x => x.Kind == "Expense").Sum(x => x.balance);
        var assets = cumulative.Where(x => x.Kind == "Asset").Sum(x => x.balance);
        var liabilities = -cumulative.Where(x => x.Kind == "Liability").Sum(x => x.balance);
        var equity = -cumulative.Where(x => x.Kind == "Equity").Sum(x => x.balance);
        var retained = -cumulative.Where(x => x.Kind is "Income" or "Expense").Sum(x => x.balance);
        var invoices = s.Db.FinanceInvoices.Where(x =>
            x.EntityId == entity && x.State == "Issued" && x.Date >= start && x.Date <= end
        );
        if (projectId != null)
            invoices = invoices.Where(x => x.ProjectId == projectId);
        if (eventId != null)
            invoices = invoices.Where(x => x.EventId == eventId);
        if (personId != null)
            invoices = invoices.Where(x => x.PersonId == personId);
        var outstanding = await invoices.SumAsync(x => x.Total - x.Paid - x.Credited);
        var ageing = await invoices
            .Where(x => x.Total > x.Paid + x.Credited)
            .GroupBy(x =>
                x.DueOn >= end ? "Current"
                : x.DueOn >= end.AddDays(-30) ? "1–30"
                : x.DueOn >= end.AddDays(-60) ? "31–60"
                : x.DueOn >= end.AddDays(-90) ? "61–90"
                : "90+"
            )
            .Select(g => new { bucket = g.Key, amount = g.Sum(x => x.Total - x.Paid - x.Credited) })
            .ToListAsync();
        var ledger = activity;
        if (after != null)
            ledger = ledger.Where(x => x.l.Id.CompareTo(after.Value) > 0);
        var entries = await ledger
            .OrderBy(x => x.l.Id)
            .Take(100)
            .Select(x => new
            {
                x.l.Id,
                x.j.Date,
                x.j.Source,
                x.j.Narration,
                x.a.Code,
                x.a.Name,
                x.l.Debit,
                x.l.Credit,
                x.l.PersonId,
                x.l.ProjectId,
                x.l.EventId,
            })
            .ToListAsync();
        object records = kind switch
        {
            "invoices" or "dues-register" or "outstanding" or "member-statement" => await invoices
                .Where(x => kind != "outstanding" || x.Total > x.Paid + x.Credited)
                .OrderBy(x => x.Id)
                .Take(100)
                .ToListAsync(),
            "collections" or "receipts" or "reconciliation" => await s
                .Db.FinancePayments.Where(x =>
                    x.EntityId == entity
                    && x.Date >= start
                    && x.Date <= end
                    && (personId == null || x.PersonId == personId)
                )
                .OrderBy(x => x.Id)
                .Take(100)
                .ToListAsync(),
            "refunds" => await s
                .Db.FinanceRefunds.Where(x =>
                    x.EntityId == entity && x.Date >= start && x.Date <= end
                )
                .OrderBy(x => x.Id)
                .Take(100)
                .ToListAsync(),
            "expenses" => await s
                .Db.FinanceExpenses.Where(x =>
                    x.EntityId == entity
                    && x.Date >= start
                    && x.Date <= end
                    && (projectId == null || x.ProjectId == projectId)
                    && (eventId == null || x.EventId == eventId)
                )
                .OrderBy(x => x.Id)
                .Take(100)
                .ToListAsync(),
            "donations" => await s
                .Db.FinancePledges.Where(x =>
                    x.EntityId == entity
                    && x.Date >= start
                    && x.Date <= end
                    && (projectId == null || x.ProjectId == projectId)
                )
                .OrderBy(x => x.Id)
                .Take(100)
                .ToListAsync(),
            "inter-entity" => await s
                .Db.FinanceEntitlements.Where(x => x.EntityId == entity)
                .OrderBy(x => x.Id)
                .Take(100)
                .ToListAsync(),
            "budget-actual" => await s
                .Db.FinanceBudgets.Where(x =>
                    x.EntityId == entity && (x.Status == "Approved" || x.Status == "Superseded")
                )
                .OrderBy(x => x.Id)
                .Take(100)
                .ToListAsync(),
            "audit" => await s
                .Db.FinanceAudits.Where(x => x.EntityId == entity)
                .OrderBy(x => x.Id)
                .Take(100)
                .ToListAsync(),
            _ => entries,
        };
        var payload = new
        {
            kind,
            entity = e.Name,
            e.Currency,
            from = start,
            to = end,
            scope = "Single financial entity; native currency. Not a formal consolidated statement.",
            income,
            expense,
            surplus = income - expense,
            assets,
            liabilities,
            equity,
            retainedFunds = retained,
            balanceSheetDifference = assets - liabilities - equity - retained,
            trialDifference = cumulative.Sum(x => x.balance),
            outstanding,
            ageing,
            accounts = cumulative,
            periodAccounts = period,
            entries,
            records,
            recordLimit = 100,
            next = entries.Count == 100 ? entries[^1].Id : (Guid?)null,
            notes = "Outstanding and ageing reflect current allocations for invoices selected by invoice date; not historical as-of receivables. Ledger and statement balances derive from immutable postings through report end. Pledges are not cash.",
        };
        if (format == "json")
            return Results.Ok(payload);
        var text = new List<string> { "Code,Account,Kind,Debit,Credit,Balance" };
        string Cell(string v) =>
            "\""
            + ((v.Length > 0 && "=+-@\t\r".Contains(v[0])) ? "'" : "")
            + v.Replace("\"", "\"\"")
            + "\"";
        foreach (var row in cumulative)
            text.Add(
                string.Join(
                    ",",
                    new[]
                    {
                        Cell(row.Code),
                        Cell(row.Name),
                        Cell(row.Kind),
                        row.debit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        row.credit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        row.balance.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    }
                )
            );
        if (format == "csv")
            return Results.File(
                Encoding.UTF8.GetBytes(string.Join("\r\n", text)),
                "text/csv",
                kind + "-accounts.csv"
            );
        return Results.File(
            FinancePdf.Build(
                kind + " / " + e.Currency,
                new[]
                {
                    e.Name,
                    start + " to " + end,
                    "Entity accounts; no formal consolidation.",
                    "Income "
                        + income
                        + " / Expenses "
                        + expense
                        + " / Surplus "
                        + (income - expense),
                    "Assets "
                        + assets
                        + " / Liabilities "
                        + liabilities
                        + " / Equity/funds "
                        + (equity + retained),
                    "Trial difference " + cumulative.Sum(x => x.balance),
                }
                    .Concat(
                        cumulative.Select(x =>
                            x.Code
                            + " "
                            + x.Name
                            + " DR "
                            + x.debit
                            + " CR "
                            + x.credit
                            + " BAL "
                            + x.balance
                        )
                    )
                    .ToArray()
            ),
            "application/pdf",
            kind + "-accounts.pdf"
        );
    }
}
