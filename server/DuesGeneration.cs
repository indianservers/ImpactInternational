using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record DuesPreviewInput(
    Guid[] PlanIds,
    DateOnly StartsOn,
    Guid? OrgId = null,
    Guid[]? MembershipIds = null,
    Guid[]? Excluded = null,
    int Page = 1
);

public record DuesGenerationRow(
    Guid PlanId,
    Guid MembershipId,
    long PlanVersion,
    decimal Discount = 0,
    string Reason = ""
);

public record DuesGenerationInput(DateOnly StartsOn, DuesGenerationRow[] Rows);

public static partial class DuesCenter
{
    static void MapGeneration(RouteGroupBuilder api)
    {
        api.MapPost(
            "/generation/preview",
            async (
                DuesPreviewInput input,
                Access a,
                CommunicationService comm,
                FinanceService finance
            ) =>
            {
                var w = await Workspace(a, comm, View);
                var db = a.Db;
                Check(
                    input.PlanIds != null
                        && input.StartsOn.Year <= 9000
                        && input.PlanIds.Length is > 0 and <= 50
                        && input.Page >= 1
                        && input.Page <= 10000
                        && (input.MembershipIds?.Length ?? 0) <= 500
                        && (input.Excluded?.Length ?? 0) <= 500,
                    "Select valid plans, members and page."
                );
                if (input.OrgId != null)
                    Check(
                        await a.Visible(w).AnyAsync(o => o.Id == input.OrgId),
                        "Organization outside your scope.",
                        404
                    );
                var result = new List<object>();
                int eligible = 0,
                    duplicates = 0,
                    excluded = 0,
                    offset = (input.Page - 1) * 50;
                decimal pageTotal = 0;
                string? currency = null;
                foreach (var id in input.PlanIds.Distinct().Order())
                {
                    var plan = await db.DuesPlans.SingleOrDefaultAsync(p =>
                        p.Id == id && p.TenantId == w.TenantId
                    );
                    Check(plan != null, "Dues plan unavailable.", 404);
                    var entity = await finance.Entity(plan.EntityId, "finance.manage");
                    Check(
                        currency == null || currency == entity.Currency,
                        "Preview one currency at a time."
                    );
                    currency = entity.Currency;
                    var end = input.StartsOn.AddMonths(plan.Months);
                    Check(
                        input.StartsOn >= plan.EffectiveFrom && input.StartsOn <= plan.EffectiveTo,
                        "Assessment date outside plan effective dates."
                    );
                    var beginUtc = new DateTimeOffset(
                        input.StartsOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                    );
                    var endUtc = new DateTimeOffset(
                        end.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                    );
                    var members = db.Memberships.Where(m =>
                        m.TenantId == w.TenantId
                        && m.ClubId == plan.BillingOrgId
                        && m.Status == "Active"
                        && m.JoinedAt < endUtc
                        && (m.EndsAt == null || m.EndsAt > beginUtc)
                        && (plan.CategoryId == null || m.CategoryId == plan.CategoryId)
                        && a.Visible(w).Any(o => o.Id == m.ClubId)
                    );
                    if (input.OrgId != null)
                        members = members.Where(m =>
                            a.Tree(w.TenantId, input.OrgId.Value).Any(o => o.Id == m.ClubId)
                        );
                    if (input.MembershipIds?.Length > 0)
                        members = members.Where(m => input.MembershipIds.Contains(m.Id));
                    if (input.Excluded?.Length > 0)
                    {
                        excluded += await members.CountAsync(m => input.Excluded.Contains(m.Id));
                        members = members.Where(m => !input.Excluded.Contains(m.Id));
                    }
                    var tail =
                        ":"
                        + plan.ChargeCode
                        + ":"
                        + (plan.Once ? "once" : input.StartsOn + ":" + end);
                    var count = await members.CountAsync();
                    eligible += count;
                    duplicates += await members.CountAsync(m =>
                        db.FinanceInvoices.Any(i =>
                            i.EntityId == entity.Id
                            && i.IdempotencyKey == "dues:" + m.Id.ToString() + tail
                        )
                    );
                    if (offset >= count)
                    {
                        offset -= count;
                        continue;
                    }
                    if (result.Count >= 50)
                        continue;
                    var page = await members
                        .OrderBy(m => m.Id)
                        .Skip(offset)
                        .Take(50 - result.Count)
                        .Select(m => new
                        {
                            m.Id,
                            m.Number,
                            m.JoinedAt,
                            club = m.Club.Name,
                            category = db
                                .MembershipCategories.Where(c => c.Id == m.CategoryId)
                                .Select(c => c.Name)
                                .First(),
                            name = db
                                .Persons.Where(p => p.Id == m.PersonId)
                                .Select(p => p.DisplayName)
                                .First(),
                        })
                        .ToListAsync();
                    offset = 0;
                    var parts = GovernanceService.Unpack<FinanceComponent[]>(plan.Components);
                    foreach (var member in page)
                    {
                        var joined = DateOnly.FromDateTime(member.JoinedAt.UtcDateTime);
                        var factor =
                            plan.Prorate && joined > input.StartsOn
                                ? (decimal)Math.Max(0, end.DayNumber - joined.DayNumber)
                                    / (end.DayNumber - input.StartsOn.DayNumber)
                                : 1m;
                        var amount = parts.Sum(p =>
                        {
                            var net = finance.Money(entity, p.Amount * factor);
                            return net + finance.Money(entity, net * p.TaxPercent / 100);
                        });
                        var key = FinanceService.DuesKey(plan, member.Id, input.StartsOn, end);
                        var existing = await db.FinanceInvoices.AnyAsync(i =>
                            i.EntityId == entity.Id && i.IdempotencyKey == key
                        );
                        var waived = await db.FinanceAudits.AnyAsync(x =>
                            x.EntityId == entity.Id
                            && x.Action == "dues.waiver"
                            && x.Detail.StartsWith(key + "|")
                        );
                        if (!existing && !waived)
                            pageTotal += amount;
                        result.Add(
                            new
                            {
                                membershipId = member.Id,
                                member.Number,
                                member.name,
                                member.club,
                                member.category,
                                planId = plan.Id,
                                plan = plan.Name,
                                planVersion = plan.Version,
                                amount,
                                existing,
                                waived,
                                endsOn = end,
                                dueOn = FinanceService.Today.AddDays(plan.GraceDays),
                            }
                        );
                    }
                }
                return Results.Ok(
                    new
                    {
                        eligible,
                        duplicates,
                        excluded,
                        page = input.Page,
                        pageTotal,
                        currency,
                        items = result,
                        notice = "Preview uses active memberships and configured club/category plans. Amounts include configured tax and proration. Totals apply to this preview page; existing invoices and recorded full exemptions are skipped. Issuing dues never renews membership.",
                    }
                );
            }
        );
        api.MapPost(
            "/generation",
            async (
                DuesGenerationInput input,
                Access a,
                CommunicationService comm,
                FinanceService finance
            ) =>
            {
                var w = await Workspace(a, comm, View);
                Check(
                    input.Rows != null
                        && input.Rows.All(r =>
                            r != null && r.Reason != null && r.Reason.Length <= 1000
                        )
                        && input.StartsOn.Year <= 9000
                        && input.Rows.Length is > 0 and <= 500,
                    "Generate between one and 500 reviewed assessments."
                );
                Check(
                    input.Rows.Select(r => (r.PlanId, r.MembershipId)).Distinct().Count()
                        == input.Rows.Length,
                    "Duplicate preview rows."
                );
                var db = a.Db;
                var plans = await db
                    .DuesPlans.Where(p =>
                        input.Rows.Select(r => r.PlanId).Contains(p.Id) && p.TenantId == w.TenantId
                    )
                    .ToDictionaryAsync(p => p.Id);
                Check(
                    plans.Count == input.Rows.Select(r => r.PlanId).Distinct().Count(),
                    "Plan unavailable.",
                    404
                );
                var entities = new Dictionary<Guid, FinanceEntity>();
                foreach (var id in plans.Values.Select(p => p.EntityId).Distinct().Order())
                    entities[id] = await finance.Entity(id, "finance.manage");
                Check(
                    entities.Values.Select(e => e.Currency).Distinct().Count() == 1,
                    "Generate one currency at a time."
                );
                if (input.Rows.Any(r => r.Discount > 0))
                    foreach (var id in entities.Keys)
                        await finance.Entity(id, "finance.post");
                await using var tx = await db.Database.BeginTransactionAsync();
                foreach (var id in entities.Keys.Order())
                    await finance.Lock(id);
                var output = new List<object>();
                int created = 0,
                    skipped = 0,
                    waived = 0;
                decimal total = 0;
                foreach (var row in input.Rows)
                {
                    var plan = plans[row.PlanId];
                    await db.Entry(plan).ReloadAsync();
                    Check(plan.Version == row.PlanVersion, "A plan changed; preview again.", 409);
                    var m = await db.Memberships.SingleOrDefaultAsync(m =>
                        m.Id == row.MembershipId
                        && m.TenantId == w.TenantId
                        && a.Visible(w).Any(o => o.Id == m.ClubId)
                    );
                    Check(m != null, "Member outside your scope.", 404);
                    var end = input.StartsOn.AddMonths(plan.Months);
                    var key = FinanceService.DuesKey(plan, m.Id, input.StartsOn, end);
                    var old = await db.FinanceInvoices.SingleOrDefaultAsync(i =>
                        i.EntityId == plan.EntityId && i.IdempotencyKey == key
                    );
                    var exemption = await db.FinanceAudits.AnyAsync(x =>
                        x.EntityId == plan.EntityId
                        && x.Action == "dues.waiver"
                        && x.Detail.StartsWith(key + "|")
                    );
                    if (old != null || exemption)
                    {
                        skipped++;
                        output.Add(
                            new
                            {
                                membershipId = m.Id,
                                status = exemption ? "Already exempt" : "Existing invoice",
                                invoiceId = old?.Id,
                            }
                        );
                        continue;
                    }
                    var invoice = await finance.Assess(
                        entities[plan.EntityId],
                        plan,
                        m,
                        input.StartsOn,
                        end,
                        w.UserId,
                        row.Discount,
                        row.Reason
                    );
                    if (invoice == null)
                        waived++;
                    else
                    {
                        created++;
                        total += invoice.Total;
                    }
                    output.Add(
                        new
                        {
                            membershipId = m.Id,
                            status = invoice == null ? "Exempt" : "Generated",
                            invoiceId = invoice?.Id,
                            number = invoice?.Number,
                            amount = invoice?.Total ?? 0,
                        }
                    );
                }
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        created,
                        skipped,
                        waived,
                        total,
                        items = output,
                    }
                );
            }
        );
    }
}
