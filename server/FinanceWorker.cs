using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class FinanceWorker(IServiceScopeFactory scopes, ILogger<FinanceWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            var active = false;
            try
            {
                active = await Run(stop);
            }
            catch (Exception ex) when (!stop.IsCancellationRequested)
            {
                log.LogWarning(
                    "Finance background pass failed ({Type}); durable work retained",
                    ex.GetType().Name
                );
            }
            try
            {
                await Task.Delay(
                    active ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(15),
                    stop
                );
            }
            catch (OperationCanceledException) { }
        }
    }

    async Task<bool> Run(CancellationToken stop)
    {
        using var scope = scopes.CreateScope();
        var s = scope.ServiceProvider.GetRequiredService<FinanceService>();
        var db = s.Db;
        var recurring = await db
            .DuesPlans.Where(x =>
                x.AutoBill && x.NextBillOn <= FinanceService.Today && x.NextBillOn <= x.EffectiveTo
            )
            .OrderBy(x => x.NextBillOn)
            .Take(5)
            .ToListAsync(stop);
        foreach (var plan in recurring)
        {
            await using var tx = await db.Database.BeginTransactionAsync(stop);
            await s.Lock(plan.EntityId);
            if (plan.NextBillOn > FinanceService.Today || plan.NextBillOn > plan.EffectiveTo)
            {
                await tx.CommitAsync(stop);
                continue;
            }
            if (!await Authorized(s, plan.EntityId, plan.CreatorId))
            {
                plan.AutoBill = false;
                plan.Version++;
                await db.SaveChangesAsync(stop);
                await tx.CommitAsync(stop);
                continue;
            }
            var e = await db.FinanceEntitys.SingleAsync(x => x.Id == plan.EntityId, stop);
            if (
                !await db.FinanceJobs.AnyAsync(
                    x => x.PlanId == plan.Id && x.PeriodStart == plan.NextBillOn,
                    stop
                )
            )
                db.FinanceJobs.Add(
                    s.Bind(
                        new FinanceJob
                        {
                            PlanId = plan.Id,
                            PeriodStart = plan.NextBillOn,
                            PeriodEnd = plan.NextBillOn.AddMonths(plan.Months),
                            CreatorId = plan.CreatorId,
                        },
                        e
                    )
                );
            plan.NextBillOn = plan.NextBillOn.AddMonths(plan.Months);
            plan.AutoBill = !plan.Once;
            plan.Version++;
            await db.SaveChangesAsync(stop);
            await tx.CommitAsync(stop);
        }
        var ids = await db
            .FinanceJobs.Where(x => x.Status == "Queued" || x.Status == "Running")
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .Take(5)
            .ToListAsync(stop);
        foreach (var id in ids)
        {
            using var workScope = scopes.CreateScope();
            var fs = workScope.ServiceProvider.GetRequiredService<FinanceService>();
            try
            {
                await Process(fs, id, stop);
            }
            catch (Exception ex) when (!stop.IsCancellationRequested)
            {
                fs.Db.ChangeTracker.Clear();
                var job = await fs.Db.FinanceJobs.SingleAsync(x => x.Id == id, stop);
                job.Status = "Failed";
                job.Error = ex is ApiError api
                    ? api.Message
                    : "Batch failed; review configuration and retry.";
                job.Attempts++;
                job.Version++;
                await fs.Db.SaveChangesAsync(stop);
                log.LogWarning("Finance billing job {Id} stopped for review", id);
            }
        }
        var invoices = await db
            .FinanceInvoices.Where(x =>
                x.State == "Issued"
                && x.Total > x.Paid + x.Credited
                && x.DueOn <= FinanceService.Today.AddDays(7)
                && (x.RemindedAt == null || x.RemindedAt < DateTimeOffset.UtcNow.AddDays(-7))
            )
            .OrderBy(x => x.DueOn)
            .Select(x => x.Id)
            .Take(50)
            .ToListAsync(stop);
        foreach (var id in invoices)
        {
            await using var tx = await db.Database.BeginTransactionAsync(stop);
            var i = await db.FinanceInvoices.SingleAsync(x => x.Id == id, stop);
            await s.Lock(i.EntityId);
            if (i.RemindedAt > DateTimeOffset.UtcNow.AddDays(-7))
            {
                await tx.CommitAsync(stop);
                continue;
            }
            var e = await db.FinanceEntitys.SingleAsync(x => x.Id == i.EntityId, stop);
            if (await Authorized(s, e.Id, i.CreatorId))
                await s.Notice(
                    i.PersonId,
                    e,
                    "Dues reminder: "
                        + i.Number
                        + " / "
                        + (i.Total - i.Paid - i.Credited)
                        + " "
                        + i.Currency
                        + " outstanding."
                );
            i.RemindedAt = DateTimeOffset.UtcNow;
            i.Version++;
            await db.SaveChangesAsync(stop);
            await tx.CommitAsync(stop);
        }
        return ids.Count > 0;
    }

    public static async Task<bool> Authorized(FinanceService s, Guid entity, Guid user)
    {
        var e = await s.Db.FinanceEntitys.SingleAsync(x => x.Id == entity);
        if (
            !e.Active
            || !await s.Db.FinanceGrants.AnyAsync(x =>
                x.EntityId == entity
                && x.UserId == user
                && new[] { "Owner", "Treasurer", "Billing" }.Contains(x.Role)
            )
        )
            return false;
        foreach (
            var a in await s
                .Db.Assignments.Where(x =>
                    x.TenantId == e.TenantId
                    && x.UserId == user
                    && s.Db.RolePermissions.Any(p =>
                        p.RoleId == x.RoleId && p.PermissionId == "finance.manage"
                    )
                )
                .ToListAsync()
        )
            if (await s.A.Effective(a))
                return true;
        return false;
    }

    static async Task Process(FinanceService s, Guid id, CancellationToken stop)
    {
        var db = s.Db;
        var job = await db.FinanceJobs.SingleAsync(x => x.Id == id, stop);
        await using var tx = await db.Database.BeginTransactionAsync(stop);
        await s.Lock(job.EntityId);
        if (job.Status is not "Queued" and not "Running")
        {
            await tx.CommitAsync(stop);
            return;
        }
        if (!await Authorized(s, job.EntityId, job.CreatorId))
            throw new ApiError(
                403,
                "Billing creator no longer has effective collection authority."
            );
        var plan = await db.DuesPlans.SingleAsync(x => x.Id == job.PlanId, stop);
        var e = await db.FinanceEntitys.SingleAsync(x => x.Id == job.EntityId, stop);
        var q = db.Memberships.Where(m =>
            m.TenantId == e.TenantId
            && m.ClubId == plan.BillingOrgId
            && m.Status == "Active"
            && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)
            && (plan.CategoryId == null || m.CategoryId == plan.CategoryId)
        );
        if (job.AfterMembershipId != null)
            q = q.Where(m => m.Id.CompareTo(job.AfterMembershipId.Value) > 0);
        var members = await q.OrderBy(x => x.Id).Take(50).ToListAsync(stop);
        foreach (var m in members)
        {
            await s.Assess(e, plan, m, job.PeriodStart, job.PeriodEnd, job.CreatorId);
            job.AfterMembershipId = m.Id;
            job.Processed++;
        }
        job.Status = members.Count < 50 ? "Completed" : "Running";
        job.Version++;
        await db.SaveChangesAsync(stop);
        await tx.CommitAsync(stop);
    }
}
