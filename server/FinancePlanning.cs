using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public static partial class FinanceEndpoints
{
    static void MapPlanning(RouteGroupBuilder api)
    {
        api.MapPost(
            "/entities/{entity:guid}/convert",
            async (Guid entity, FinanceConversionInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity);
                s.Exact(e, i.Amount, false);
                Check(i.Decimals is >= 0 and <= 4, "Unsupported reporting precision.");
                var rate = await s
                    .Db.FinanceExchangeRates.Where(x =>
                        x.EntityId == entity && x.Currency == i.Currency && x.Date <= i.Date
                    )
                    .OrderByDescending(x => x.Date)
                    .ThenByDescending(x => x.Id)
                    .FirstOrDefaultAsync();
                Check(
                    rate != null,
                    "No historical reporting rate is configured for this date.",
                    404
                );
                return new
                {
                    nativeCurrency = e.Currency,
                    nativeAmount = i.Amount,
                    reportingCurrency = i.Currency,
                    reportingAmount = decimal.Round(
                        i.Amount * rate.Rate,
                        i.Decimals,
                        MidpointRounding.AwayFromZero
                    ),
                    rate.Id,
                    rate.Date,
                    rate.Rate,
                    rate.Source,
                    policy = "Reporting translation only; original ledger amounts remain unchanged.",
                };
            }
        );

        api.MapGet(
            "/entities/{entity:guid}/reviews",
            async (Guid entity, FinanceService s) =>
            {
                await s.Entity(entity);
                var requests = s.Db.GovernanceRequests.Where(r =>
                    s.Db.FinanceExpenses.Any(e => e.EntityId == entity && e.RequestId == r.Id)
                    || s.Db.FinanceBudgets.Any(b => b.EntityId == entity && b.RequestId == r.Id)
                );
                return new
                {
                    requests = await requests
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(100)
                        .ToListAsync(),
                    expenses = await s
                        .Db.FinanceExpenses.Where(x =>
                            x.EntityId == entity && x.Status == "Submitted"
                        )
                        .Take(100)
                        .ToListAsync(),
                    budgets = await s
                        .Db.FinanceBudgets.Where(x =>
                            x.EntityId == entity && x.Status == "Submitted"
                        )
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );

        api.MapGet(
            "/entities/{entity:guid}/expenses",
            async (Guid entity, FinanceService s, Guid? after = null) =>
            {
                await s.Entity(entity);
                var q = s.Db.FinanceExpenses.Where(x => x.EntityId == entity);
                if (after != null)
                    q = q.Where(x => x.Id.CompareTo(after.Value) > 0);
                return await q.OrderBy(x => x.Id).Take(100).ToListAsync();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/expenses",
            async (Guid entity, FinanceExpenseInput i, FinanceService s, OperationsService ops) =>
            {
                var e = await s.Entity(entity, "finance.manage");
                await using var creationTx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                await s.Person(i.PersonId, e);
                await s.Sources(e, i.EventId, i.ProjectId);
                Check(
                    i.Description.Length is > 0 and <= 1000
                        && i.Payee.Length is > 0 and <= 200
                        && i.Lines.Length is > 0 and <= 50
                        && i.Evidence.Length is > 0 and <= 10
                        && i.Date <= FinanceService.Today
                        && await s.Db.FinanceAccounts.AnyAsync(x =>
                            x.EntityId == entity && x.Code == i.AccountCode && x.Kind == "Expense"
                        ),
                    "Expense needs valid lines, evidence and an expense account."
                );
                decimal total = 0;
                foreach (var l in i.Lines)
                {
                    s.Exact(e, l.Amount);
                    Check(
                        l.Description.Length is > 0 and <= 500,
                        "Expense line description required."
                    );
                    total += l.Amount;
                }
                foreach (var doc in i.Evidence)
                {
                    var d = await s.Db.GovernanceDocuments.SingleOrDefaultAsync(x =>
                        x.Id == doc && x.TenantId == e.TenantId
                    );
                    Check(d != null, "Evidence unavailable.", 404);
                    await ops.Document(d);
                    Check(
                        d.TenantId == e.TenantId,
                        "Evidence belongs to another association.",
                        403
                    );
                }
                if (i.BudgetId != null)
                    Check(
                        await s.Db.FinanceBudgets.AnyAsync(x =>
                            x.Id == i.BudgetId
                            && x.EntityId == entity
                            && x.Status == "Approved"
                            && x.StartsOn <= i.Date
                            && x.EndsOn >= i.Date
                            && x.ProjectId == i.ProjectId
                            && x.EventId == i.EventId
                        ),
                        "Choose an approved budget for the same period and source."
                    );
                foreach (var docId in i.Evidence)
                {
                    var docText = docId.ToString();
                    Check(
                        !await s.Db.FinanceExpenses.AnyAsync(x =>
                            x.EntityId == entity
                            && x.Status != "Rejected"
                            && x.Evidence.Contains(docText)
                        ),
                        "Receipt evidence is already claimed.",
                        409
                    );
                }
                var evidence = Pack(i.Evidence.OrderBy(x => x));
                var hash = Hash(evidence);
                Check(
                    !await s.Db.FinanceExpenses.AnyAsync(x =>
                        x.EntityId == entity && x.Status != "Rejected" && x.EvidenceHash == hash
                    ),
                    "This receipt evidence is already used by another claim.",
                    409
                );
                var r = s.Bind(
                    new FinanceExpense
                    {
                        PersonId = i.PersonId,
                        Payee = i.Payee,
                        Description = i.Description,
                        Category = i.Category,
                        Lines = Pack(i.Lines),
                        Evidence = evidence,
                        EvidenceHash = hash,
                        Amount = total,
                        Date = i.Date,
                        EventId = i.EventId,
                        ProjectId = i.ProjectId,
                        BudgetId = i.BudgetId,
                        AccountCode = i.AccountCode,
                        CreatorId = (await s.Workspace()).UserId,
                    },
                    e
                );
                s.Db.FinanceExpenses.Add(r);
                s.Audit(e, r.CreatorId, "expense.create", r.Id.ToString());
                await s.Db.SaveChangesAsync();
                await creationTx.CommitAsync();
                return r;
            }
        );
        api.MapPost(
            "/expenses/{id:guid}/submit",
            async (Guid id, FinanceService s) =>
            {
                var c = await s.Db.FinanceExpenses.SingleOrDefaultAsync(x => x.Id == id);
                Check(c != null, "Claim unavailable.", 404);
                var e = await s.Entity(c.EntityId, "finance.manage");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                Check(c.Status == "Draft", "Expense already submitted.", 409);
                var subject = await s
                    .Db.Persons.Where(x => x.Id == c.PersonId)
                    .Select(x => x.UserId)
                    .SingleOrDefaultAsync();
                var request = await s.Submit(
                    c,
                    "FinanceExpense",
                    Hash(
                        Pack(
                            new
                            {
                                c.Amount,
                                c.Lines,
                                c.Evidence,
                                c.PersonId,
                                c.AccountCode,
                                c.EventId,
                                c.ProjectId,
                                c.BudgetId,
                            }
                        )
                    ),
                    c.CreatorId,
                    subject
                );
                c.RequestId = request.Id;
                c.Status = "Submitted";
                c.Version++;
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return new { claim = c, request };
            }
        );
        api.MapPost(
            "/expenses/{id:guid}/reimburse",
            async (Guid id, FinanceDisbursement i, FinanceService s) =>
            {
                var c = await s.Db.FinanceExpenses.SingleOrDefaultAsync(x => x.Id == id);
                Check(c != null, "Claim unavailable.", 404);
                var e = await s.Entity(c.EntityId, "finance.reconcile");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                var old = await s.Db.FinanceReimbursements.SingleOrDefaultAsync(x =>
                    x.EntityId == e.Id && x.Reference == i.Reference
                );
                if (old != null)
                {
                    Check(
                        old.ExpenseId == id && old.Amount == i.Amount,
                        "Reference belongs to another reimbursement.",
                        409
                    );
                    return old;
                }
                s.Exact(e, i.Amount);
                Check(
                    c.Status == "Approved"
                        && i.Amount <= c.Amount - c.Paid
                        && i.Reference.Length is > 0 and <= 150
                        && i.Date <= FinanceService.Today,
                    "Approved unpaid expense and bank reference required."
                );
                var actor = (await s.Workspace()).UserId;
                Check(
                    c.CreatorId != actor
                        && !await s.Db.Persons.AnyAsync(x =>
                            x.Id == c.PersonId && x.UserId == actor
                        ),
                    "Cannot disburse your own reimbursement.",
                    403
                );
                var r = s.Bind(
                    new FinanceReimbursement
                    {
                        ExpenseId = id,
                        Amount = i.Amount,
                        Reference = i.Reference,
                        Date = i.Date,
                        CreatorId = actor,
                    },
                    e
                );
                s.Db.FinanceReimbursements.Add(r);
                await s.Post(
                    e,
                    i.Date,
                    "reimbursement:" + r.Id,
                    i.Reference,
                    [
                        new("2000", i.Amount, 0, c.PersonId, c.EventId, c.ProjectId),
                        new("1010", 0, i.Amount, c.PersonId, c.EventId, c.ProjectId),
                    ],
                    actor
                );
                c.Paid += i.Amount;
                c.Version++;
                s.Audit(e, actor, "expense.reimburse", i.Reference);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/budgets",
            async (Guid entity, FinanceService s) =>
            {
                await s.Entity(entity);
                return await s
                    .Db.FinanceBudgets.Where(x => x.EntityId == entity)
                    .OrderByDescending(x => x.StartsOn)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/budgets",
            async (Guid entity, FinanceBudgetInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.manage");
                await s.Sources(e, i.EventId, i.ProjectId);
                Check(
                    i.Name.Length is > 0 and <= 200
                        && new[]
                        {
                            "Annual",
                            "District",
                            "National",
                            "Committee",
                            "Event",
                            "Project",
                        }.Contains(i.Kind)
                        && i.StartsOn <= i.EndsOn
                        && i.Lines.Length is > 0 and <= 100,
                    "Invalid budget."
                );
                decimal amount = 0;
                foreach (var line in i.Lines)
                {
                    s.Exact(e, line.Amount);
                    Check(
                        line.Description.Length is > 0 and <= 500,
                        "Budget line description required."
                    );
                    amount += line.Amount;
                }
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                FinanceBudget? old = null;
                if (i.ReplacesId != null)
                {
                    old = await s.Db.FinanceBudgets.SingleOrDefaultAsync(x =>
                        x.Id == i.ReplacesId && x.EntityId == entity
                    );
                    Check(
                        old != null
                            && old.Status == "Approved"
                            && old.ProjectId == i.ProjectId
                            && old.EventId == i.EventId,
                        "Revision must reference approved budget for the same source.",
                        409
                    );
                    Check(
                        !await s.Db.FinanceBudgets.AnyAsync(x =>
                            x.ReplacesId == old.Id && x.Status != "Rejected"
                        ),
                        "A revision already exists.",
                        409
                    );
                }
                var r = s.Bind(
                    new FinanceBudget
                    {
                        Name = i.Name,
                        Kind = i.Kind,
                        StartsOn = i.StartsOn,
                        EndsOn = i.EndsOn,
                        Lines = Pack(i.Lines),
                        Amount = amount,
                        EventId = i.EventId,
                        ProjectId = i.ProjectId,
                        ReplacesId = i.ReplacesId,
                        Revision = old == null ? 1 : old.Revision + 1,
                        CreatorId = (await s.Workspace()).UserId,
                    },
                    e
                );
                s.Db.FinanceBudgets.Add(r);
                s.Audit(e, r.CreatorId, "budget.create", r.Id.ToString());
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapPost(
            "/budgets/{id:guid}/submit",
            async (Guid id, FinanceService s) =>
            {
                var b = await s.Db.FinanceBudgets.SingleOrDefaultAsync(x => x.Id == id);
                Check(b != null, "Budget unavailable.", 404);
                var e = await s.Entity(b.EntityId, "finance.manage");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                Check(b.Status == "Draft", "Budget already submitted.", 409);
                var r = await s.Submit(
                    b,
                    "FinanceBudget",
                    Hash(
                        Pack(
                            new
                            {
                                b.Amount,
                                b.Lines,
                                b.StartsOn,
                                b.EndsOn,
                                b.ProjectId,
                                b.EventId,
                                b.ReplacesId,
                            }
                        )
                    ),
                    b.CreatorId
                );
                b.RequestId = r.Id;
                b.Status = "Submitted";
                b.Version++;
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return new { budget = b, request = r };
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/pledges",
            async (Guid entity, FinanceService s) =>
            {
                await s.Entity(entity);
                return await s
                    .Db.FinancePledges.Where(x => x.EntityId == entity)
                    .OrderByDescending(x => x.Date)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/pledges",
            async (Guid entity, FinancePledgeInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.manage");
                s.Exact(e, i.Amount);
                await s.Person(i.PersonId, e);
                await s.Sources(e, i.EventId, i.ProjectId);
                Check(
                    i.Kind is "Donation" or "Sponsorship"
                        && i.Contributor.Length is > 0 and <= 200
                        && i.Description.Length <= 1000,
                    "Invalid funding pledge."
                );
                var r = s.Bind(
                    new FinancePledge
                    {
                        PersonId = i.PersonId,
                        Contributor = i.Contributor,
                        Kind = i.Kind,
                        Amount = i.Amount,
                        Description = i.Description,
                        Date = i.Date,
                        ProjectId = i.ProjectId,
                        EventId = i.EventId,
                        CreatorId = (await s.Workspace()).UserId,
                    },
                    e
                );
                s.Db.FinancePledges.Add(r);
                s.Audit(e, r.CreatorId, "pledge.create", r.Id.ToString());
                await s.Db.SaveChangesAsync();
                return r;
            }
        );
        api.MapPost(
            "/pledges/{id:guid}/invoice",
            async (Guid id, FinanceService s) =>
            {
                var p = await s.Db.FinancePledges.SingleOrDefaultAsync(x => x.Id == id);
                Check(p != null, "Pledge unavailable.", 404);
                var e = await s.Entity(p.EntityId, "finance.manage");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                var i = await s.CreateInvoice(
                    e,
                    p.PersonId,
                    p.Contributor,
                    p.Kind,
                    FinanceService.Today,
                    FinanceService.Today.AddDays(30),
                    "pledge:" + id,
                    [
                        new(
                            p.Description == "" ? p.Kind : p.Description,
                            1,
                            p.Amount,
                            AccountCode: p.Kind == "Donation" ? "4200" : "4300"
                        ),
                    ],
                    (await s.Workspace()).UserId,
                    eventId: p.EventId,
                    projectId: p.ProjectId
                );
                i.PledgeId = p.Id;
                await s.Db.SaveChangesAsync();
                await s.Issue(i, e, (await s.Workspace()).UserId);
                p.InvoiceId = i.Id;
                p.Status = "Receivable";
                p.Version++;
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return i;
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/inter-entity",
            async (Guid entity, FinanceService s) =>
            {
                await s.Entity(entity);
                return new
                {
                    entitlements = await s
                        .Db.FinanceEntitlements.Where(x => x.EntityId == entity)
                        .OrderBy(x => x.Id)
                        .Take(100)
                        .ToListAsync(),
                    transfers = await s
                        .Db.FinanceTransfers.Where(x => x.EntityId == entity)
                        .OrderByDescending(x => x.Date)
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/inter-entity/{id:guid}/settle",
            async (Guid id, FinanceTransferInput i, FinanceService s) =>
            {
                var ent = await s.Db.FinanceEntitlements.SingleOrDefaultAsync(x => x.Id == id);
                Check(ent != null, "Entitlement unavailable.", 404);
                var e = await s.Entity(ent.EntityId, "finance.reconcile");
                var beneficiary = await s.Entity(ent.BeneficiaryEntityId, "finance.reconcile");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                var old = await s.Db.FinanceTransfers.SingleOrDefaultAsync(x =>
                    x.EntityId == e.Id && x.Reference == i.Reference
                );
                if (old != null)
                {
                    Check(
                        old.EntitlementId == id && old.Amount == i.Amount,
                        "Settlement reference reused.",
                        409
                    );
                    return old;
                }
                s.Exact(e, i.Amount);
                var invoice = await s.Db.FinanceInvoices.SingleAsync(x => x.Id == ent.InvoiceId);
                Check(
                    i.Amount <= ent.Amount - ent.Settled
                        && invoice.Paid >= invoice.Total - invoice.Credited
                        && i.Reference.Length is > 0 and <= 150
                        && i.Date <= FinanceService.Today,
                    "Only fully collected entitlements can be settled with bank evidence."
                );
                var r = s.Bind(
                    new FinanceTransfer
                    {
                        EntitlementId = id,
                        BeneficiaryEntityId = beneficiary.Id,
                        Amount = i.Amount,
                        Reference = i.Reference,
                        Date = i.Date,
                        CreatorId = (await s.Workspace()).UserId,
                        Status = "Reconciled",
                    },
                    e
                );
                s.Db.FinanceTransfers.Add(r);
                await s.Post(
                    e,
                    i.Date,
                    "transfer:" + r.Id,
                    i.Reference,
                    [new("2200", i.Amount, 0), new("1010", 0, i.Amount)],
                    r.CreatorId
                );
                await s.Post(
                    beneficiary,
                    i.Date,
                    "transfer-received:" + r.Id,
                    i.Reference,
                    [new("1010", i.Amount, 0), new("1200", 0, i.Amount)],
                    r.CreatorId
                );
                ent.Settled += i.Amount;
                ent.Version++;
                s.Audit(e, r.CreatorId, "interentity.settlement.record", i.Reference);
                s.Audit(beneficiary, r.CreatorId, "interentity.settlement.receive", i.Reference);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/exchange-rates",
            async (Guid entity, FinanceRateInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                Check(
                    System.Text.RegularExpressions.Regex.IsMatch(i.Currency, "^[A-Z]{3}$")
                        && i.Rate > 0
                        && i.Rate <= 1000000
                        && i.Source.Length is > 0 and <= 200,
                    "Invalid historical reporting rate."
                );
                var r = s.Bind(
                    new FinanceExchangeRate
                    {
                        Currency = i.Currency,
                        Rate = i.Rate,
                        Date = i.Date,
                        Source = i.Source,
                    },
                    e
                );
                s.Db.FinanceExchangeRates.Add(r);
                s.Audit(
                    e,
                    (await s.Workspace()).UserId,
                    "rate.record",
                    i.Currency + ":" + i.Source
                );
                await s.Db.SaveChangesAsync();
                return r;
            }
        );
    }
}
