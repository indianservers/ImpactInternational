using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static partial class FinanceEndpoints
{
    // A reconciliation group changes only the bank match. Member payers, receipts,
    // allocations and their settlement journals remain separate and immutable.
    static async Task MatchClubRemittance(
        FinanceBankTransaction row,
        FinanceMatchInput input,
        FinanceEntity entity,
        FinanceService s
    )
    {
        var ids = input.PaymentIds!;
        var w = await s.Workspace();
        Check(
            input.PaymentId == Guid.Empty
                && ids.Length is > 0 and <= 200
                && ids.Distinct().Count() == ids.Length
                && input.ClubId != null,
            "Select a club and up to 200 distinct member receipts."
        );
        var club = await s.Db.Orgs.SingleOrDefaultAsync(o =>
            o.Id == input.ClubId
            && o.TenantId == entity.TenantId
            && s.A.Visible(w).Any(v => v.Id == o.Id)
            && s.Db.Levels.Any(l => l.Id == o.LevelId && l.IsClub)
        );
        Check(club != null, "Club is outside your scope.", 404);
        if (row.Status == "Matched")
        {
            Check(
                row.RemittanceClubId == club.Id
                    && row.MatchedPaymentIds.Order().SequenceEqual(ids.Order()),
                "Statement already matched differently.",
                409
            );
            return;
        }
        Check(
            row.Status == "Unmatched" && row.Amount > 0,
            "Only unmatched incoming bank deposits can be grouped.",
            409
        );
        var payments = await s
            .Db.FinancePayments.Where(p =>
                ids.Contains(p.Id) && p.EntityId == entity.Id && p.TenantId == entity.TenantId
            )
            .ToListAsync();
        Check(
            payments.Count == ids.Length,
            "A member receipt is outside this financial book.",
            404
        );
        Check(
            payments.All(p =>
                p.Status == "Verified"
                && p.SettlementStatus == "Settled"
                && p.BankAccountId == row.BankAccountId
                && p.Date <= row.Date
                && p.SettledOn <= row.Date
            ),
            "Each member receipt must be verified and settled to this bank before the statement date.",
            409
        );
        Check(
            !await s.Db.FinanceBankPaymentMatches.AnyAsync(m => ids.Contains(m.PaymentId))
                && !await s.Db.FinanceBankTransactions.AnyAsync(b =>
                    b.EntityId == entity.Id
                    && b.Status == "Matched"
                    && (
                        ids.Contains(b.PaymentId ?? Guid.Empty)
                        || b.MatchedPaymentIds.Any(id => ids.Contains(id))
                    )
                ),
            "A member receipt is already bank-matched.",
            409
        );
        foreach (var payment in payments)
        {
            var allocations =
                from a in s.Db.FinanceAllocations
                join i in s.Db.FinanceInvoices on a.InvoiceId equals i.Id
                where a.PaymentId == payment.Id
                select i;
            Check(
                await allocations.AnyAsync()
                    && !await allocations.AnyAsync(i =>
                        i.MembershipId == null
                        || !s.Db.Memberships.Any(m => m.Id == i.MembershipId && m.ClubId == club.Id)
                        || !DuesCenter.Purposes.Contains(i.Purpose)
                    ),
                "Every allocation must belong to a dues invoice for this club.",
                409
            );
        }
        var sources = ids.Select(id => "settlement:" + id).ToArray();
        var bank = await s.Db.FinanceBanks.SingleAsync(b =>
            b.Id == row.BankAccountId && b.EntityId == entity.Id
        );
        var postedByPayment = await (
            from l in s.Db.FinanceJournalLines
            join j in s.Db.FinanceJournals on l.JournalId equals j.Id
            join account in s.Db.FinanceAccounts on l.AccountId equals account.Id
            where
                j.EntityId == entity.Id
                && sources.Contains(j.Source)
                && account.Code == bank.AccountCode
            select new { j.Source, amount = l.Debit - l.Credit }
        )
            .GroupBy(x => x.Source)
            .Select(g => new { g.Key, amount = g.Sum(x => x.amount) })
            .ToDictionaryAsync(g => g.Key, g => g.amount);
        var posted = postedByPayment.Values.Sum();
        Check(
            posted == row.Amount,
            "Combined posted member settlements do not equal the bank deposit.",
            409
        );
        Check(
            ids.All(id => postedByPayment.GetValueOrDefault("settlement:" + id) > 0),
            "A receipt has no positive posted settlement.",
            409
        );
        foreach (var paymentId in ids)
            s.Db.FinanceBankPaymentMatches.Add(
                s.Bind(
                    new FinanceBankPaymentMatch
                    {
                        BankTransactionId = row.Id,
                        PaymentId = paymentId,
                        ClubId = club.Id,
                        BankAmount = postedByPayment["settlement:" + paymentId],
                    },
                    entity
                )
            );
        row.PaymentId = null;
        row.MatchedPaymentIds = ids.Order().ToArray();
        row.RemittanceClubId = club.Id;
        row.Status = "Matched";
        row.Version++;
        s.Audit(
            entity,
            w.UserId,
            "bank.club.match",
            row.Id + "|" + club.Id + "|" + string.Join(',', row.MatchedPaymentIds)
        );
    }
}

public static partial class DuesCenter
{
    static void MapClubRemittances(RouteGroupBuilder api)
    {
        api.MapGet(
            "/bank-remittances",
            async (
                Access a,
                CommunicationService comm,
                FinanceService finance,
                Guid entityId,
                Guid clubId
            ) =>
            {
                var w = await Workspace(a, comm, View);
                var entity = await finance.Entity(entityId, "finance.reconcile");
                Check(
                    await a.Visible(w)
                        .AnyAsync(o =>
                            o.Id == clubId && a.Db.Levels.Any(l => l.Id == o.LevelId && l.IsClub)
                        ),
                    "Club is outside your scope.",
                    404
                );
                var scope = Invoices(a.Db, a, w)
                    .Where(i =>
                        i.EntityId == entityId
                        && a.Db.Memberships.Any(m => m.Id == i.MembershipId && m.ClubId == clubId)
                    );
                var payments = a.Db.FinancePayments.Where(p =>
                    p.EntityId == entityId
                    && p.Status == "Verified"
                    && a.Db.FinanceAllocations.Any(x =>
                        x.PaymentId == p.Id && scope.Any(i => i.Id == x.InvoiceId)
                    )
                    && !a.Db.FinanceAllocations.Any(x =>
                        x.PaymentId == p.Id && !scope.Any(i => i.Id == x.InvoiceId)
                    )
                    && !a.Db.FinanceBankPaymentMatches.Any(m => m.PaymentId == p.Id)
                    && !a.Db.FinanceBankTransactions.Any(b =>
                        b.Status == "Matched"
                        && (b.PaymentId == p.Id || b.MatchedPaymentIds.Contains(p.Id))
                    )
                );
                return Results.Ok(
                    new
                    {
                        entity = entity.Name,
                        entity.Currency,
                        entity.Decimals,
                        banks = await a
                            .Db.FinanceBanks.Where(b => b.EntityId == entityId)
                            .Select(b => new
                            {
                                b.Id,
                                b.Name,
                                b.MaskedNumber,
                            })
                            .ToListAsync(),
                        statements = await a
                            .Db.FinanceBankTransactions.Where(b =>
                                b.EntityId == entityId && b.Status == "Unmatched" && b.Amount > 0
                            )
                            .OrderByDescending(b => b.Date)
                            .Take(200)
                            .Select(b => new
                            {
                                b.Id,
                                b.BankAccountId,
                                b.Date,
                                b.Amount,
                                b.Reference,
                            })
                            .ToListAsync(),
                        payments = await payments
                            .OrderByDescending(p => p.Date)
                            .ThenBy(p => p.Id)
                            .Take(200)
                            .Select(p => new
                            {
                                p.Id,
                                p.Payer,
                                p.Reference,
                                p.ReceiptNumber,
                                p.Amount,
                                p.Refunded,
                                p.Fee,
                                p.Date,
                                p.SettlementStatus,
                                p.BankAccountId,
                                postedBankAmount = (
                                    from l in a.Db.FinanceJournalLines
                                    join j in a.Db.FinanceJournals on l.JournalId equals j.Id
                                    join account in a.Db.FinanceAccounts
                                        on l.AccountId equals account.Id
                                    where
                                        j.EntityId == entityId
                                        && j.Source == "settlement:" + p.Id
                                        && a.Db.FinanceBanks.Any(b =>
                                            b.Id == p.BankAccountId && b.AccountCode == account.Code
                                        )
                                    select (decimal?)(l.Debit - l.Credit)
                                ).Sum() ?? 0,
                            })
                            .ToListAsync(),
                        matched = await a
                            .Db.FinanceBankTransactions.Where(b =>
                                b.EntityId == entityId
                                && b.RemittanceClubId == clubId
                                && b.Status == "Matched"
                            )
                            .OrderByDescending(b => b.Date)
                            .Take(50)
                            .Select(b => new
                            {
                                b.Id,
                                b.Date,
                                b.Amount,
                                b.Reference,
                                b.MatchedPaymentIds,
                                receipts = (
                                    from m in a.Db.FinanceBankPaymentMatches
                                    join p in a.Db.FinancePayments on m.PaymentId equals p.Id
                                    where m.BankTransactionId == b.Id
                                    select new
                                    {
                                        p.Id,
                                        p.Payer,
                                        p.ReceiptNumber,
                                        p.Reference,
                                        p.Amount,
                                        m.BankAmount,
                                    }
                                ).ToList(),
                            })
                            .ToListAsync(),
                        notice = "Member receipts and invoice allocations stay separate. The combined club remittance matches one existing bank statement row to their posted settlement amounts. Matching creates no payment or collection and does not change dues balances.",
                    }
                );
            }
        );
    }
}
