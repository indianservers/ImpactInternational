using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static partial class FinanceEndpoints
{
    static void MapPayments(RouteGroupBuilder api)
    {
        api.MapGet(
            "/entities/{entity:guid}/payments",
            async (Guid entity, FinanceService s, Guid? after = null) =>
            {
                await s.Entity(entity);
                var q = s.Db.FinancePayments.Where(x => x.EntityId == entity);
                if (after != null)
                    q = q.Where(x => x.Id.CompareTo(after.Value) > 0);
                return await q.OrderBy(x => x.Id).Take(100).ToListAsync();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/payments",
            async (Guid entity, FinancePaymentInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.manage");
                s.Exact(e, i.Amount);
                Check(
                    i.Currency == e.Currency
                        && i.Reference.Length is > 0 and <= 150
                        && i.Key.Length is > 0 and <= 150
                        && i.Payer.Length is > 0 and <= 200
                        && i.Evidence.Length <= 2000
                        && new[] { "BankTransfer", "Cash", "Cheque", "UPI" }.Contains(i.Method)
                        && i.Date <= FinanceService.Today,
                    "Invalid manual payment details."
                );
                await s.Person(i.PersonId, e);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                var old = await s.Db.FinancePayments.SingleOrDefaultAsync(x =>
                    x.EntityId == entity
                    && (x.IdempotencyKey == i.Key || x.Reference == i.Reference)
                );
                if (old != null)
                {
                    Check(
                        old.Amount == i.Amount
                            && old.Currency == i.Currency
                            && old.PersonId == i.PersonId
                            && old.Reference == i.Reference,
                        "Payment reference/key already belongs to different details.",
                        409
                    );
                    return old;
                }
                var p = s.Bind(
                    new FinancePayment
                    {
                        PersonId = i.PersonId,
                        Payer = i.Payer,
                        Reference = i.Reference,
                        Method = i.Method,
                        Currency = i.Currency,
                        Amount = i.Amount,
                        Date = i.Date,
                        IdempotencyKey = i.Key,
                        RecorderId = (await s.Workspace()).UserId,
                        Evidence = i.Evidence,
                    },
                    e
                );
                s.Db.FinancePayments.Add(p);
                s.Audit(e, p.RecorderId, "payment.record", p.Reference);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return p;
            }
        );
        api.MapPost(
            "/payments/{id:guid}/verify",
            async (Guid id, FinanceService s) =>
            {
                var p = await s.Payment(id, true);
                var e = await s.Entity(p.EntityId, "finance.reconcile");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                await s.Verify(p, e, (await s.Workspace()).UserId);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return p;
            }
        );
        api.MapPost(
            "/payments/{id:guid}/allocate",
            async (Guid id, FinanceAllocationInput input, FinanceService s) =>
            {
                var p = await s.Payment(id, true);
                var e = await s.Entity(p.EntityId, "finance.reconcile");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                var i = await s.Db.FinanceInvoices.SingleOrDefaultAsync(x =>
                    x.Id == input.InvoiceId && x.EntityId == e.Id
                );
                Check(i != null, "Invoice unavailable.", 404);
                await s.Allocate(p, i, input.Amount, e, (await s.Workspace()).UserId, input.Key);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return p;
            }
        );
        api.MapPost(
            "/payments/{id:guid}/settle",
            async (Guid id, FinanceSettlementInput input, FinanceService s) =>
            {
                var p = await s.Payment(id, true);
                var e = await s.Entity(p.EntityId, "finance.reconcile");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                Check(
                    p.Status == "Verified"
                        && input.Date <= FinanceService.Today
                        && input.Date >= p.Date
                        && input.Reference.Length is > 0 and <= 150,
                    "Only verified collection may settle with a dated bank reference."
                );
                s.Exact(e, input.Fee, false);
                Check(
                    input.Fee >= 0 && input.Fee < p.Amount - p.Refunded,
                    "Invalid settlement fee."
                );
                if (p.SettlementStatus == "Settled")
                {
                    Check(
                        p.BankAccountId == input.BankAccountId && p.Fee == input.Fee,
                        "Payment already settled differently.",
                        409
                    );
                    return p;
                }
                var bank = await s.Db.FinanceBanks.SingleOrDefaultAsync(x =>
                    x.Id == input.BankAccountId && x.EntityId == e.Id
                );
                Check(bank != null, "Bank account unavailable.", 404);
                await s.Post(
                    e,
                    input.Date,
                    "settlement:" + id,
                    input.Reference,
                    [
                        new(bank.AccountCode, p.Amount - p.Refunded - input.Fee, 0, p.PersonId),
                        new("5300", input.Fee, 0, p.PersonId),
                        new("1020", 0, p.Amount - p.Refunded, p.PersonId),
                    ],
                    (await s.Workspace()).UserId
                );
                p.SettlementStatus = "Settled";
                p.SettledOn = input.Date;
                p.BankAccountId = bank.Id;
                p.Fee = input.Fee;
                p.Version++;
                s.Audit(e, (await s.Workspace()).UserId, "payment.settle", input.Reference);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return p;
            }
        );
        api.MapPost(
            "/payments/{id:guid}/refunds",
            async (Guid id, FinanceRefundInput input, FinanceService s) =>
            {
                var p = await s.Payment(id, true);
                var e = await s.Entity(p.EntityId, "finance.post");
                var w = await s.Workspace();
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                var old = await s.Db.FinanceRefunds.SingleOrDefaultAsync(x =>
                    x.PaymentId == id && x.Reference == input.Reference
                );
                if (old != null)
                {
                    Check(
                        old.Amount == input.Amount && old.InvoiceId == input.InvoiceId,
                        "Refund reference reused.",
                        409
                    );
                    return old;
                }
                s.Exact(e, input.Amount);
                Check(
                    p.Status == "Verified"
                        && input.Amount <= p.Amount - p.Refunded
                        && input.Reference.Length is > 0 and <= 150
                        && input.Reason.Length is > 0 and <= 1000
                        && input.Kind is "Refund" or "Chargeback",
                    "Invalid refund evidence."
                );
                var r = s.Bind(
                    new FinanceRefund
                    {
                        PaymentId = id,
                        InvoiceId = input.InvoiceId,
                        Amount = input.Amount,
                        Reason = input.Reason,
                        Reference = input.Reference,
                        Kind = input.Kind,
                        CreatorId = w.UserId,
                        Date = FinanceService.Today,
                    },
                    e
                );
                s.Db.FinanceRefunds.Add(r);
                var posts = new List<FinancePosting>();
                if (input.InvoiceId != null)
                {
                    var invoice = await s.Db.FinanceInvoices.SingleOrDefaultAsync(x =>
                        x.Id == input.InvoiceId && x.EntityId == e.Id
                    );
                    Check(invoice != null, "Invoice unavailable.", 404);
                    var allocated = await s
                        .Db.FinanceAllocations.Where(x =>
                            x.PaymentId == id && x.InvoiceId == input.InvoiceId
                        )
                        .SumAsync(x => x.Amount);
                    var refunded = await s
                        .Db.FinanceRefunds.Where(x =>
                            x.PaymentId == id && x.InvoiceId == input.InvoiceId
                        )
                        .SumAsync(x => x.Amount);
                    Check(
                        input.Amount <= allocated - refunded && input.Amount <= invoice.Paid,
                        "Refund exceeds payment allocation.",
                        409
                    );
                    invoice.Paid -= input.Amount;
                    invoice.Version++;
                    p.Allocated -= input.Amount;
                    posts.Add(
                        new("1100", input.Amount, 0, p.PersonId, invoice.EventId, invoice.ProjectId)
                    );
                }
                else
                {
                    Check(
                        input.Amount <= p.Amount - p.Allocated - p.Refunded,
                        "Unallocated refund exceeds available balance.",
                        409
                    );
                    posts.Add(new("2300", input.Amount, 0, p.PersonId));
                }
                string cash = "1020";
                if (p.SettlementStatus == "Settled")
                {
                    cash = (
                        await s.Db.FinanceBanks.SingleAsync(x => x.Id == p.BankAccountId)
                    ).AccountCode;
                }
                posts.Add(new(cash, 0, input.Amount, p.PersonId));
                await s.Post(
                    e,
                    FinanceService.Today,
                    "refund:" + r.Id,
                    input.Kind + ": " + input.Reason,
                    posts,
                    w.UserId
                );
                p.Refunded += input.Amount;
                p.Version++;
                if (input.InvoiceId != null)
                {
                    var inv = await s.Db.FinanceInvoices.SingleAsync(x => x.Id == input.InvoiceId);
                    if (inv.RegistrationId != null)
                    {
                        var reg = await s.Db.EventRegistrations.SingleAsync(x =>
                            x.Id == inv.RegistrationId
                        );
                        reg.Status = "Pending Payment";
                        reg.Version++;
                    }
                }
                s.Audit(e, w.UserId, "payment." + input.Kind.ToLowerInvariant(), input.Reference);
                await s.Notice(
                    p.PersonId,
                    e,
                    input.Kind + " recorded against " + p.ReceiptNumber + "."
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapGet(
            "/payments/{id:guid}/receipt",
            async (Guid id, FinanceService s) =>
            {
                var p = await s.Payment(id);
                Check(
                    p.Status == "Verified" && p.ReceiptNumber != "",
                    "Receipt requires verified funds.",
                    409
                );
                var e = await s.Db.FinanceEntitys.SingleAsync(x => x.Id == p.EntityId);
                return Results.File(
                    FinancePdf.Build(
                        "Receipt " + p.ReceiptNumber,
                        [
                            e.Name,
                            p.Payer,
                            p.Date.ToString(),
                            p.Currency + " " + p.Amount.ToString("N"+e.Decimals,System.Globalization.CultureInfo.InvariantCulture),
                            "Method " + p.Method,
                            "Reference " + p.Reference,
                            "Refunded " + p.Refunded.ToString("N"+e.Decimals,System.Globalization.CultureInfo.InvariantCulture),
                            "Settlement " + p.SettlementStatus,
                            "Verification confirms collection; settlement is reported separately.",
                            "Tax treatment is determined by the issuing entity; no tax deductibility is implied.",
                        ]
                    ),
                    "application/pdf",
                    p.ReceiptNumber + ".pdf"
                );
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/bank-accounts",
            async (Guid entity, FinanceService s) =>
            {
                await s.Entity(entity);
                return await s.Db.FinanceBanks.Where(x => x.EntityId == entity).ToListAsync();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/bank-accounts",
            async (
                Guid entity,
                FinanceBankInput i,
                FinanceService s,
                IDataProtectionProvider protection
            ) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                Check(
                    i.Name.Length is > 0 and <= 150
                        && i.AccountNumber.Length is >= 4 and <= 100
                        && i.RoutingDetails.Length <= 1000
                        && await s.Db.FinanceAccounts.AnyAsync(x =>
                            x.EntityId == entity && x.Code == i.AccountCode && x.Kind == "Asset"
                        ),
                    "Invalid bank account."
                );
                Check(
                    i.AccountCode is not "1020" and not "1100" and not "1200",
                    "Choose a cash/bank account, not a control account."
                );
                var r = s.Bind(
                    new FinanceBank
                    {
                        Name = i.Name,
                        MaskedNumber = "•••• " + i.AccountNumber[^4..],
                        ProtectedDetails = protection
                            .CreateProtector("finance.bank")
                            .Protect(i.AccountNumber + "\n" + i.RoutingDetails),
                        AccountCode = i.AccountCode,
                    },
                    e
                );
                s.Db.FinanceBanks.Add(r);
                s.Audit(e, (await s.Workspace()).UserId, "bank.create", i.Name);
                await s.Db.SaveChangesAsync();
                return r;
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/reconciliation/import",
            async (Guid entity, FinanceBankImport i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.reconcile");
                Check(
                    i.Rows.Length is > 0 and <= 500
                        && await s.Db.FinanceBanks.AnyAsync(x =>
                            x.Id == i.BankAccountId && x.EntityId == entity
                        ),
                    "Choose an authorized bank and up to 500 statement rows."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                int imported = 0;
                foreach (var row in i.Rows)
                {
                    s.Exact(e, row.Amount, false);
                    Check(
                        row.Amount != 0
                            && row.Key.Length is > 0 and <= 150
                            && row.Reference.Length is > 0 and <= 150
                            && row.Date <= FinanceService.Today,
                        "Invalid statement row."
                    );
                    if (
                        await s.Db.FinanceBankTransactions.AnyAsync(x =>
                            x.EntityId == entity && x.ImportKey == row.Key
                        )
                    )
                        continue;
                    Check(
                        !s.Db.FinanceBankTransactions.Local.Any(x => x.ImportKey == row.Key),
                        "Repeated statement key in import."
                    );
                    s.Db.FinanceBankTransactions.Add(
                        s.Bind(
                            new FinanceBankTransaction
                            {
                                BankAccountId = i.BankAccountId,
                                Date = row.Date,
                                Amount = row.Amount,
                                Reference = row.Reference,
                                ImportKey = row.Key,
                            },
                            e
                        )
                    );
                    imported++;
                }
                s.Audit(e, (await s.Workspace()).UserId, "bank.import", imported.ToString());
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return new { imported };
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/reconciliation",
            async (Guid entity, FinanceService s, Guid? after = null) =>
            {
                await s.Entity(entity, "finance.reconcile");
                var q = s.Db.FinanceBankTransactions.Where(x => x.EntityId == entity);
                if (after != null)
                    q = q.Where(x => x.Id.CompareTo(after.Value) > 0);
                return await q.OrderBy(x => x.Id).Take(100).ToListAsync();
            }
        );
        api.MapPost(
            "/reconciliation/{id:guid}/match",
            async (Guid id, FinanceMatchInput i, FinanceService s) =>
            {
                var row = await s.Db.FinanceBankTransactions.SingleOrDefaultAsync(x => x.Id == id);
                Check(row != null, "Statement row unavailable.", 404);
                var e = await s.Entity(row.EntityId, "finance.reconcile");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                await s.Db.Entry(row).ReloadAsync();
                if (i.PaymentIds is { Length: > 0 })
                {
                    await MatchClubRemittance(row, i, e, s);
                    await s.Db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return row;
                }
                if (row.Status == "Matched")
                {
                    Check(row.PaymentId == i.PaymentId, "Already matched to another payment.", 409);
                    return row;
                }
                var p = await s.Db.FinancePayments.SingleOrDefaultAsync(x =>
                    x.Id == i.PaymentId && x.EntityId == e.Id
                );
                Check(
                    p != null
                        && p.Status == "Verified"
                        && p.SettlementStatus == "Settled"
                        && p.BankAccountId == row.BankAccountId
                        && row.Amount
                            == (
                                from l in s.Db.FinanceJournalLines
                                join j in s.Db.FinanceJournals on l.JournalId equals j.Id
                                join bank in s.Db.FinanceBanks on p.BankAccountId equals bank.Id
                                join account in s.Db.FinanceAccounts
                                    on l.AccountId equals account.Id
                                where
                                    j.Source == "settlement:" + p.Id
                                    && j.EntityId == e.Id
                                    && account.Code == bank.AccountCode
                                select l.Debit - l.Credit
                            ).Sum()
                        && row.Date >= p.Date,
                    "Statement must match verified, settled amount and bank.",
                    409
                );
                Check(
                    !await s.Db.FinanceBankPaymentMatches.AnyAsync(x=>x.PaymentId==p.Id) && !await s.Db.FinanceBankTransactions.AnyAsync(x =>
                        (x.PaymentId == p.Id || x.MatchedPaymentIds.Contains(p.Id)) && x.Status == "Matched"
                    ),
                    "Payment already matched.",
                    409
                );
                row.PaymentId = p.Id;
                s.Db.FinanceBankPaymentMatches.Add(s.Bind(new FinanceBankPaymentMatch{BankTransactionId=row.Id,PaymentId=p.Id,BankAmount=row.Amount},e));
                row.Status = "Matched";
                row.Version++;
                s.Audit(e, (await s.Workspace()).UserId, "bank.match", p.Reference);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return row;
            }
        );
    }
}
