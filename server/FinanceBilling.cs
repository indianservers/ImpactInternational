using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public static partial class FinanceEndpoints
{
    static void MapBilling(RouteGroupBuilder api)
    {
        api.MapGet(
            "/entities/{entity:guid}/invoices",
            async (
                Guid entity,
                FinanceService s,
                string search = "",
                string state = "",
                Guid? after = null
            ) =>
            {
                await s.Entity(entity);
                Check(search.Length <= 100, "Search too long.");
                var q = s.Db.FinanceInvoices.Where(x => x.EntityId == entity);
                if (search != "")
                    q = q.Where(x =>
                        EF.Functions.ILike(x.Number, "%" + search + "%")
                        || EF.Functions.ILike(x.Customer, "%" + search + "%")
                    );
                if (state != "")
                    q = q.Where(x => x.State == state);
                if (after != null)
                    q = q.Where(x => x.Id.CompareTo(after.Value) > 0);
                return await q.OrderBy(x => x.Id).Take(100).ToListAsync();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/invoices",
            async (Guid entity, FinanceInvoiceInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.manage");
                var w = await s.Workspace();
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                var r = await s.CreateInvoice(
                    e,
                    i.PersonId,
                    i.Customer,
                    i.Purpose,
                    i.Date,
                    i.DueOn,
                    i.Key,
                    i.Lines,
                    w.UserId,
                    eventId: i.EventId,
                    projectId: i.ProjectId,
                    notes: i.Notes
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapGet(
            "/invoices/{id:guid}",
            async (Guid id, FinanceService s, FinanceGateway gateway) =>
            {
                var i = await s.Invoice(id);
                var e = await s.Db.FinanceEntitys.SingleAsync(x => x.Id == i.EntityId);
                return new
                {
                    invoice = i,
                    lines = await s
                        .Db.FinanceInvoiceLines.Where(x => x.InvoiceId == id)
                        .ToListAsync(),
                    credits = await s.Db.FinanceCredits.Where(x => x.InvoiceId == id).ToListAsync(),
                    allocations = await s
                        .Db.FinanceAllocations.Where(x => x.InvoiceId == id)
                        .ToListAsync(),
                    paymentAvailable = gateway.Available(e),
                    entityName = e.Name,
                    decimals = e.Decimals,
                };
            }
        );
        api.MapPost(
            "/invoices/{id:guid}/issue",
            async (Guid id, FinanceService s) =>
            {
                var i = await s.Invoice(id, true);
                var e = await s.Entity(i.EntityId, "finance.manage");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                await s.Issue(i, e, (await s.Workspace()).UserId);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return i;
            }
        );
        api.MapPost(
            "/invoices/{id:guid}/cancel",
            async (Guid id, FinanceReason input, FinanceService s) =>
            {
                var i = await s.Invoice(id, true);
                var e = await s.Entity(i.EntityId, "finance.manage");
                Check(
                    i.State == "Draft" && input.Reason.Length is > 0 and <= 1000,
                    "Only a draft may be cancelled; issued history needs a credit note.",
                    409
                );
                i.State = "Cancelled";
                i.Version++;
                s.Audit(
                    e,
                    (await s.Workspace()).UserId,
                    "invoice.cancel",
                    i.Id + ":" + input.Reason
                );
                await s.Db.SaveChangesAsync();
                return i;
            }
        );
        api.MapPost(
            "/invoices/{id:guid}/credits",
            async (Guid id, FinanceCreditInput input, FinanceService s) =>
            {
                var i = await s.Invoice(id, true);
                var e = await s.Entity(i.EntityId, "finance.post");
                var w = await s.Workspace();
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                var prior = await s.Db.FinanceCredits.SingleOrDefaultAsync(x =>
                    x.EntityId == e.Id && x.Key == input.Key
                );
                if (prior != null)
                {
                    Check(
                        prior.InvoiceId == id && prior.Amount == input.Amount,
                        "Credit key was used for another request.",
                        409
                    );
                    return prior;
                }
                s.Exact(e, input.Amount);
                Check(
                    i.State == "Issued"
                        && input.Amount <= i.Total - i.Paid - i.Credited
                        && input.Reason.Length is > 0 and <= 1000
                        && input.Key.Length is > 0 and <= 150,
                    "Invalid credit note."
                );
                var r = s.Bind(
                    new FinanceCredit
                    {
                        InvoiceId = id,
                        Amount = input.Amount,
                        Reason = input.Reason,
                        Key = input.Key,
                        CreatorId = w.UserId,
                        Date = FinanceService.Today,
                    },
                    e
                );
                s.Db.FinanceCredits.Add(r);
                var lines = await s
                    .Db.FinanceInvoiceLines.Where(x => x.InvoiceId == id)
                    .OrderBy(x => x.Id)
                    .ToListAsync();
                var posts = new List<FinancePosting>();
                decimal remaining = input.Amount,
                    priorCredit = i.Credited;
                // Amount-only credits consume frozen positive lines in stable ID order.
                // Cumulative tax rounding makes the final full credit reverse original tax exactly.
                for (int n = 0; n < lines.Count; n++)
                {
                    var line = lines[n];
                    if (line.Total == 0)
                        continue;
                    var consumed = Math.Min(priorCredit, line.Total);
                    priorCredit -= consumed;
                    var piece = Math.Min(remaining, line.Total - consumed);
                    remaining -= piece;
                    if (piece == 0)
                        continue;
                    var tax =
                        s.Money(e, (consumed + piece) * line.Tax / line.Total)
                        - s.Money(e, consumed * line.Tax / line.Total);
                    var net = piece - tax;
                    var other =
                        line.BeneficiaryEntityId != null && line.BeneficiaryEntityId != e.Id;
                    posts.Add(
                        new(
                            other ? "2200" : line.AccountCode,
                            net,
                            0,
                            i.PersonId,
                            i.EventId,
                            i.ProjectId
                        )
                    );
                    if (tax > 0)
                        posts.Add(new("2100", tax, 0, i.PersonId, i.EventId, i.ProjectId));
                    if (other && net > 0)
                    {
                        var ent = await s.Db.FinanceEntitlements.SingleAsync(x =>
                            x.InvoiceId == id && x.BeneficiaryEntityId == line.BeneficiaryEntityId
                        );
                        Check(
                            ent.Amount - ent.Settled >= net,
                            "Cannot credit an entitlement already settled.",
                            409
                        );
                        ent.Amount -= net;
                        ent.Version++;
                        var beneficiary = await s.Entity(
                            line.BeneficiaryEntityId!.Value,
                            "finance.post"
                        );
                        await s.Post(
                            beneficiary,
                            FinanceService.Today,
                            "credit-entitlement:" + r.Id + ":" + line.Id,
                            input.Reason,
                            [new(line.AccountCode, net, 0), new("1200", 0, net)],
                            w.UserId
                        );
                    }
                }
                posts.Add(new("1100", 0, input.Amount, i.PersonId, i.EventId, i.ProjectId));
                await s.Post(
                    e,
                    FinanceService.Today,
                    "credit:" + r.Id,
                    input.Reason,
                    posts,
                    w.UserId
                );
                i.Credited += input.Amount;
                i.Version++;
                s.Audit(e, w.UserId, "invoice.credit", i.Number + ":" + input.Reason);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapGet(
            "/invoices/{id:guid}/pdf",
            async (Guid id, FinanceService s) =>
            {
                var i = await s.Invoice(id);
                var entity = await s.Db.FinanceEntitys.SingleAsync(x => x.Id == i.EntityId);
                string M(decimal value) =>
                    value.ToString(
                        "N" + entity.Decimals,
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                var lines = await s
                    .Db.FinanceInvoiceLines.Where(x => x.InvoiceId == id)
                    .ToListAsync();
                return Results.File(
                    FinancePdf.Build(
                        "Invoice " + (i.Number == "" ? "DRAFT" : i.Number),
                        new[]
                        {
                            i.IssuerSnapshot == ""
                                ? (
                                    await s.Db.FinanceEntitys.SingleAsync(x => x.Id == i.EntityId)
                                ).Name
                                : FinancePdf.Issuer(i.IssuerSnapshot),
                            i.Customer,
                            i.CustomerAddress,
                            "Date "
                                + i.Date.ToString("yyyy-MM-dd")
                                + " / Due "
                                + i.DueOn.ToString("yyyy-MM-dd"),
                            i.Currency + " / " + i.State,
                        }
                            .Concat(
                                lines.Select(l =>
                                    $"{l.Description} | {l.Quantity:0.########} x {M(l.Rate)} - {M(l.Discount)} + tax {M(l.Tax)} = {M(l.Total)}"
                                )
                            )
                            .Concat(
                                new[]
                                {
                                    "Total "
                                        + M(i.Total)
                                        + " / Paid "
                                        + M(i.Paid)
                                        + " / Credits "
                                        + M(i.Credited),
                                    "Outstanding " + M(i.Total - i.Paid - i.Credited),
                                    i.Notes,
                                }
                            )
                            .ToArray()
                    ),
                    "application/pdf",
                    (i.Number == "" ? "draft" : i.Number) + ".pdf"
                );
            }
        );
        api.MapPost(
            "/invoices/{id:guid}/send",
            async (Guid id, FinanceService s) =>
            {
                var i = await s.Invoice(id, true);
                var e = await s.Entity(i.EntityId, "finance.manage");
                Check(i.State == "Issued", "Issue before sending.", 409);
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                Check(
                    i.RemindedAt == null || i.RemindedAt < DateTimeOffset.UtcNow.AddHours(-24),
                    "A notification was already queued within 24 hours.",
                    409
                );
                await s.Notice(
                    i.PersonId,
                    e,
                    "Invoice "
                        + i.Number
                        + " has an outstanding balance of "
                        + (i.Total - i.Paid - i.Credited)
                        + " "
                        + i.Currency
                        + "."
                );
                i.RemindedAt = DateTimeOffset.UtcNow;
                i.Version++;
                s.Audit(e, (await s.Workspace()).UserId, "invoice.notify", i.Number);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/dues-plans",
            async (Guid entity, FinanceService s) =>
            {
                await s.Entity(entity);
                return await s
                    .Db.DuesPlans.Where(x => x.EntityId == entity)
                    .OrderByDescending(x => x.EffectiveFrom)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/dues-plans",
            async (Guid entity, FinancePlanInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                Check(
                    i.Name.Length is > 0 and <= 150
                        && FinanceService.Purposes.Contains(i.Purpose)
                        && i.EffectiveFrom <= i.EffectiveTo
                        && i.Months is >= 1 and <= 120
                        && i.GraceDays is >= 0 and <= 365
                        && i.Components.Length is > 0 and <= 20
                        && await s.Db.FinanceOrganizations.AnyAsync(x =>
                            x.EntityId == entity && x.ServedOrgId == i.BillingOrgId
                        ),
                    "Invalid fee schedule or collection organization."
                );
                if (i.CategoryId != null)
                    Check(
                        await s.Db.MembershipCategories.AnyAsync(x =>
                            x.Id == i.CategoryId && x.TenantId == e.TenantId
                        ),
                        "Category unavailable."
                    );
                foreach (var line in i.Components)
                {
                    s.Exact(e, line.Amount);
                    Check(
                        line.Description.Length is > 0 and <= 500
                            && line.TaxPercent is >= 0 and <= 100,
                        "Invalid fee component."
                    );
                    if (line.BeneficiaryEntityId != null && line.BeneficiaryEntityId != entity)
                        await s.Entity(line.BeneficiaryEntityId.Value, "finance.configure");
                }
                var r = s.Bind(
                    new DuesPlan
                    {
                        ChargeCode =
                            i.ChargeCode == "" ? i.Name.Trim().ToUpperInvariant() : i.ChargeCode,
                        CreatorId = (await s.Workspace()).UserId,
                        Name = i.Name,
                        BillingOrgId = i.BillingOrgId,
                        CategoryId = i.CategoryId,
                        Purpose = i.Purpose,
                        EffectiveFrom = i.EffectiveFrom,
                        EffectiveTo = i.EffectiveTo,
                        Components = Pack(i.Components),
                        Months = i.Months,
                        GraceDays = i.GraceDays,
                        Prorate = i.Prorate,
                        Once = i.Once,
                        AutoBill = i.AutoBill,
                        NextBillOn = i.NextBillOn ?? i.EffectiveFrom,
                    },
                    e
                );
                s.Db.DuesPlans.Add(r);
                s.Audit(e, (await s.Workspace()).UserId, "dues.plan.create", r.Name);
                await s.Db.SaveChangesAsync();
                return r;
            }
        );
        api.MapPost(
            "/dues-plans/{id:guid}/assess",
            async (Guid id, FinanceAssessmentInput i, FinanceService s) =>
            {
                var plan = await s.Db.DuesPlans.SingleOrDefaultAsync(x => x.Id == id);
                Check(plan != null, "Plan unavailable.", 404);
                var e = await s.Entity(plan.EntityId, "finance.manage");
                if (i.Discount > 0)
                    await s.Entity(e.Id, "finance.post");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                var m = await s.Db.Memberships.SingleOrDefaultAsync(x =>
                    x.Id == i.MembershipId && x.TenantId == e.TenantId
                );
                Check(m != null, "Membership unavailable.", 404);
                var r = await s.Assess(
                    e,
                    plan,
                    m,
                    i.StartsOn,
                    i.EndsOn,
                    (await s.Workspace()).UserId,
                    i.Discount,
                    i.Reason
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(new { invoice = r, waived = r == null });
            }
        );
        api.MapPost(
            "/dues-plans/{id:guid}/batches",
            async (Guid id, FinanceBatchInput i, FinanceService s) =>
            {
                var p = await s.Db.DuesPlans.SingleOrDefaultAsync(x => x.Id == id);
                Check(p != null, "Plan unavailable.", 404);
                var e = await s.Entity(p.EntityId, "finance.manage");
                Check(
                    i.StartsOn < i.EndsOn
                        && i.StartsOn >= p.EffectiveFrom
                        && i.StartsOn <= p.EffectiveTo
                        && i.EndsOn <= i.StartsOn.AddYears(2),
                    "Invalid billing period."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                var old = await s.Db.FinanceJobs.SingleOrDefaultAsync(x =>
                    x.PlanId == id && x.PeriodStart == i.StartsOn && x.PeriodEnd == i.EndsOn
                );
                if (old != null)
                    return old;
                var job = s.Bind(
                    new FinanceJob
                    {
                        PlanId = id,
                        PeriodStart = i.StartsOn,
                        PeriodEnd = i.EndsOn,
                        CreatorId = (await s.Workspace()).UserId,
                    },
                    e
                );
                s.Db.FinanceJobs.Add(job);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return job;
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/jobs",
            async (Guid entity, FinanceService s) =>
            {
                await s.Entity(entity);
                return await s
                    .Db.FinanceJobs.Where(x => x.EntityId == entity)
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/journals",
            async (Guid entity, FinanceJournalInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.post");
                Check(
                    i.Key.Length is > 0 and <= 150 && i.Narration.Length is > 0 and <= 1000,
                    "Journal key and narration required."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                foreach (var l in i.Lines)
                {
                    await s.Person(l.PersonId, e);
                    await s.Sources(e, l.EventId, l.ProjectId);
                }
                var r = await s.Post(
                    e,
                    i.Date,
                    "manual:" + i.Key,
                    i.Narration,
                    i.Lines,
                    (await s.Workspace()).UserId
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapPost(
            "/journals/{id:guid}/reverse",
            async (Guid id, FinanceReason input, FinanceService s) =>
            {
                var j = await s.Db.FinanceJournals.SingleOrDefaultAsync(x => x.Id == id);
                Check(j != null, "Journal unavailable.", 404);
                var e = await s.Entity(j.EntityId, "finance.post");
                Check(
                    j.Source.StartsWith("manual:") && input.Reason.Length is > 0 and <= 1000,
                    "Use source credit/refund/reimbursement corrections for system journals.",
                    409
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                var lines = await (
                    from l in s.Db.FinanceJournalLines
                    join ac in s.Db.FinanceAccounts on l.AccountId equals ac.Id
                    where l.JournalId == id
                    select new FinancePosting(
                        ac.Code,
                        l.Credit,
                        l.Debit,
                        l.PersonId,
                        l.EventId,
                        l.ProjectId
                    )
                ).ToListAsync();
                var r = await s.Post(
                    e,
                    FinanceService.Today,
                    "reversal:" + id,
                    input.Reason,
                    lines,
                    (await s.Workspace()).UserId,
                    id
                );
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
    }
}
