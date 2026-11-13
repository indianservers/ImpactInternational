using System.Globalization;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static partial class DuesCenter
{
    static void MapMembers(RouteGroupBuilder api)
    {
        api.MapGet(
            "/members",
            async (
                Access a,
                CommunicationService comm,
                Guid? orgId = null,
                Guid? planId = null,
                DateOnly? from = null,
                DateOnly? to = null,
                string search = "",
                string currency = "",
                int page = 1,
                string sort = "pending",
                string format = ""
            ) =>
            {
                var w = await Workspace(a, comm, View);
                Check(
                    search.Length <= 100
                        && currency.Length <= 3
                        && page >= 1
                        && page <= 100000
                        && new[] { "pending", "name", "collected" }.Contains(sort),
                    "Invalid member dues filters."
                );
                var q = Invoices(a.Db, a, w);
                var canSearchContacts = (await a.Grants(w)).Contains("members.contacts.view");
                if (orgId != null)
                {
                    Check(
                        await a.Visible(w).AnyAsync(o => o.Id == orgId),
                        "Organization outside your scope.",
                        404
                    );
                    q = q.Where(i =>
                        a.Db.Memberships.Any(m =>
                            m.Id == i.MembershipId
                            && a.Tree(w.TenantId, orgId.Value).Any(o => o.Id == m.ClubId)
                        )
                    );
                }
                if (planId != null)
                    q = q.Where(i => i.PlanId == planId);
                if (from != null)
                    q = q.Where(i => i.Date >= from);
                if (to != null)
                    q = q.Where(i => i.Date <= to);
                Check(from == null || to == null || from <= to, "Invalid dates.");
                if (currency != "")
                    q = q.Where(i => i.Currency == currency);
                if (search != "")
                    q = q.Where(i =>
                        EF.Functions.ILike(i.Customer, "%" + search + "%")
                        || EF.Functions.ILike(i.Number, "%" + search + "%")
                        || a.Db.Memberships.Any(m =>
                            m.Id == i.MembershipId
                            && EF.Functions.ILike(m.Number, "%" + search + "%")
                        )
                        || canSearchContacts
                            && a.Db.PersonContacts.Any(c =>
                                c.PersonId == i.PersonId
                                && EF.Functions.ILike(c.Value, "%" + search + "%")
                            )
                    );
                var grouped = q.GroupBy(i => new { i.MembershipId, i.Currency })
                    .Select(g => new
                    {
                        id = g.Key.MembershipId,
                        currency = g.Key.Currency,
                        name = g.OrderByDescending(i => i.Date).Select(i => i.Customer).First(),
                        number = a
                            .Db.Memberships.Where(m => m.Id == g.Key.MembershipId)
                            .Select(m => m.Number)
                            .First(),
                        club = a
                            .Db.Memberships.Where(m => m.Id == g.Key.MembershipId)
                            .Select(m => m.Club.Name)
                            .First(),
                        billed = g.Sum(i => i.Total),
                        collected = g.Sum(i => i.Paid),
                        credits = g.Sum(i => i.Credited),
                        pending = g.Sum(i => i.Total - i.Paid - i.Credited),
                        overdue = g.Where(i => i.DueOn < FinanceService.Today)
                            .Sum(i => i.Total - i.Paid - i.Credited),
                        invoices = g.Count(),
                    });
                var total = await grouped.CountAsync();
                var sorted =
                    sort == "name" ? grouped.OrderBy(r => r.name).ThenBy(r => r.id)
                    : sort == "collected"
                        ? grouped.OrderByDescending(r => r.collected).ThenBy(r => r.id)
                    : grouped.OrderByDescending(r => r.pending).ThenBy(r => r.id);
                if (format != "")
                {
                    Check(total <= 20000, "Narrow export to 20,000 member/currency rows.");
                    var rows = await sorted.ToListAsync();
                    return ExportRegister(
                        "impact-member-dues",
                        [
                            "Member",
                            "Membership ID",
                            "Club",
                            "Currency",
                            "Generated",
                            "Collected",
                            "Credits",
                            "Outstanding",
                            "Overdue",
                        ],
                        rows.Select(r =>
                            new[]
                            {
                                r.name,
                                r.number,
                                r.club,
                                r.currency,
                                r.billed.ToString(CultureInfo.InvariantCulture),
                                r.collected.ToString(CultureInfo.InvariantCulture),
                                r.credits.ToString(CultureInfo.InvariantCulture),
                                r.pending.ToString(CultureInfo.InvariantCulture),
                                r.overdue.ToString(CultureInfo.InvariantCulture),
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
        api.MapGet(
            "/members/{id:guid}",
            async (
                Guid id,
                Access a,
                CommunicationService comm,
                int page = 1,
                string format = ""
            ) =>
            {
                var w = await Workspace(a, comm, View);
                Check(page >= 1 && page <= 100000, "Invalid page.");
                var m = await a
                    .Db.Memberships.Where(m =>
                        m.Id == id
                        && m.TenantId == w.TenantId
                        && a.Visible(w).Any(o => o.Id == m.ClubId)
                    )
                    .Select(m => new
                    {
                        m.Id,
                        m.Number,
                        club = m.Club.Name,
                        category = a
                            .Db.MembershipCategories.Where(c => c.Id == m.CategoryId)
                            .Select(c => c.Name)
                            .First(),
                        name = a
                            .Db.Persons.Where(p => p.Id == m.PersonId)
                            .Select(p => p.DisplayName)
                            .First(),
                    })
                    .SingleOrDefaultAsync();
                Check(m != null, "Membership outside your scope.", 404);
                var q = Invoices(a.Db, a, w).Where(i => i.MembershipId == id);
                var total = await q.CountAsync();
                var sorted = q.OrderByDescending(i => i.Date).ThenBy(i => i.Id);
                if (format != "")
                {
                    Check(total <= 20000, "Statement exceeds 20,000 invoices.");
                    var rows = await sorted.ToListAsync();
                    return ExportRegister(
                        "impact-dues-statement",
                        [
                            "Member",
                            "Membership ID",
                            "Invoice",
                            "Date",
                            "Due",
                            "Currency",
                            "Generated",
                            "Paid",
                            "Credits",
                            "Outstanding",
                        ],
                        rows.Select(i =>
                            new[]
                            {
                                m.name,
                                m.Number,
                                i.Number,
                                i.Date.ToString("yyyy-MM-dd"),
                                i.DueOn.ToString("yyyy-MM-dd"),
                                i.Currency,
                                i.Total.ToString(CultureInfo.InvariantCulture),
                                i.Paid.ToString(CultureInfo.InvariantCulture),
                                i.Credited.ToString(CultureInfo.InvariantCulture),
                                i.Outstanding.ToString(CultureInfo.InvariantCulture),
                            }
                        ),
                        format
                    );
                }
                var summary = await q.GroupBy(i => i.Currency)
                    .Select(g => new
                    {
                        currency = g.Key,
                        billed = g.Sum(i => i.Total),
                        collected = g.Sum(i => i.Paid),
                        credits = g.Sum(i => i.Credited),
                        pending = g.Sum(i => i.Total - i.Paid - i.Credited),
                        overdue = g.Where(i => i.DueOn < FinanceService.Today)
                            .Sum(i => i.Total - i.Paid - i.Credited),
                    })
                    .ToListAsync();
                var payments = await (
                    from x in a.Db.FinanceAllocations
                    join p in a.Db.FinancePayments on x.PaymentId equals p.Id
                    join i in q on x.InvoiceId equals i.Id
                    orderby p.Date descending, x.Id
                    select new
                    {
                        p.Id,
                        p.ReceiptNumber,
                        p.Date,
                        p.Method,
                        p.Reference,
                        p.Currency,
                        p.Status,
                        p.SettlementStatus,
                        allocation = x.Amount,
                        invoice = i.Number,
                    }
                )
                    .Take(100)
                    .ToListAsync();
                return Results.Ok(
                    new
                    {
                        member = m,
                        summary,
                        total,
                        page,
                        items = await sorted
                            .Skip((page - 1) * 50)
                            .Take(50)
                            .Select(i => new
                            {
                                i.Id,
                                i.Number,
                                i.Date,
                                i.DueOn,
                                i.Currency,
                                i.Total,
                                i.Paid,
                                i.Credited,
                                outstanding = i.Total - i.Paid - i.Credited,
                            })
                            .ToListAsync(),
                        payments,
                        notice = "Current ledger for this membership. Invoice pages contain 50 records; payment history shows the latest 100 allocation records. Statements include all issued dues, up to the export limit.",
                    }
                );
            }
        );
    }
}
