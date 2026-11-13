using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record DuesBulkCollection(Guid BatchId, DuesCollectionInput[] Rows);

public record DuesReminderInput(
    Guid[] InvoiceIds,
    string Template =
        "Hello {member}, dues invoice {invoice} has {balance} outstanding. Due date: {dueDate}."
);

public static partial class DuesCenter
{
    static void MapWorkflows(RouteGroupBuilder api)
    {
        api.MapPost(
            "/collections/bulk",
            async (
                DuesBulkCollection input,
                Access a,
                CommunicationService comm,
                FinanceService finance
            ) =>
            {
                var w = await Workspace(a, comm, Collect);
                Check(
                    input.BatchId != Guid.Empty
                        && input.Rows != null
                        && input.Rows.All(r => r != null)
                        && input.Rows.Length is > 0 and <= 100
                        && input.Rows.Select(r => r.RequestKey).Distinct().Count()
                            == input.Rows.Length,
                    "Choose up to 100 distinct collection entries."
                );
                var output = new List<object>();
                int submitted = 0,
                    failed = 0;
                foreach (var row in input.Rows)
                {
                    try
                    {
                        var result = await Submit(row, a, comm, finance);
                        var value = (result as IValueHttpResult)?.Value;
                        if (value is DuesCollection collection)
                        {
                            var entityId = await a
                                .Db.FinanceInvoices.Where(i => i.Id == collection.InvoiceId)
                                .Select(i => i.EntityId)
                                .SingleAsync();
                            var detail = input.BatchId + "|" + collection.Id;
                            if (
                                !await a.Db.FinanceAudits.AnyAsync(x =>
                                    x.EntityId == entityId
                                    && x.Action == "dues.bulk.collection"
                                    && x.Detail == detail
                                )
                            )
                            {
                                finance.Audit(
                                    await a.Db.FinanceEntitys.SingleAsync(e => e.Id == entityId),
                                    w.UserId,
                                    "dues.bulk.collection",
                                    detail
                                );
                                await a.Db.SaveChangesAsync();
                            }
                        }
                        submitted++;
                        output.Add(
                            new
                            {
                                row.InvoiceId,
                                status = "Submitted",
                                collection = value,
                            }
                        );
                    }
                    catch (ApiError error)
                    {
                        a.Db.ChangeTracker.Clear();
                        failed++;
                        output.Add(
                            new
                            {
                                row.InvoiceId,
                                status = "Failed",
                                message = error.Message,
                                code = error.Status,
                            }
                        );
                    }
                }
                return Results.Ok(
                    new
                    {
                        input.BatchId,
                        submitted,
                        failed,
                        items = output,
                        notice = "Each member collection is independently traceable and verified separately. Successful rows remain recorded if another row fails; retry with the same row request keys.",
                    }
                );
            }
        );
        api.MapPost(
            "/reminders",
            async (
                DuesReminderInput input,
                Access a,
                CommunicationService comm,
                FinanceService finance
            ) =>
            {
                var w = await Workspace(a, comm, Collect);
                Check(
                    input.InvoiceIds != null
                        && input.Template != null
                        && input.InvoiceIds.Length is > 0 and <= 200
                        && input.Template.Length is > 0 and <= 1500,
                    "Select up to 200 invoices and a valid reminder template."
                );
                var ids = input.InvoiceIds.Distinct().ToArray();
                var invoices = await Invoices(a.Db, a, w)
                    .Where(i => ids.Contains(i.Id))
                    .OrderBy(i => i.EntityId)
                    .ThenBy(i => i.Id)
                    .ToListAsync();
                Check(invoices.Count == ids.Length, "An invoice is outside your scope.", 404);
                foreach (var i in invoices)
                    await OwnClub(a, w, i);
                await using var tx = await a.Db.Database.BeginTransactionAsync();
                foreach (var entity in invoices.Select(i => i.EntityId).Distinct().Order())
                    await finance.Lock(entity);
                int queued = 0,
                    skipped = 0;
                foreach (var invoice in invoices)
                {
                    await a.Db.Entry(invoice).ReloadAsync();
                    if (
                        invoice.Outstanding <= 0
                        || invoice.RemindedAt > DateTimeOffset.UtcNow.AddDays(-7)
                        || !await a.Db.Persons.AnyAsync(p =>
                            p.Id == invoice.PersonId && p.UserId != null
                        )
                    )
                    {
                        skipped++;
                        continue;
                    }
                    var entity = await a.Db.FinanceEntitys.SingleAsync(e =>
                        e.Id == invoice.EntityId
                    );
                    var message = input
                        .Template.Replace("{member}", invoice.Customer)
                        .Replace("{invoice}", invoice.Number)
                        .Replace("{balance}", invoice.Outstanding + " " + invoice.Currency)
                        .Replace("{dueDate}", invoice.DueOn.ToString("yyyy-MM-dd"));
                    await finance.Notice(invoice.PersonId, entity, message);
                    invoice.RemindedAt = DateTimeOffset.UtcNow;
                    invoice.Version++;
                    finance.Audit(entity, w.UserId, "dues.reminder", invoice.Id.ToString());
                    queued++;
                }
                await a.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        queued,
                        skipped,
                        channel = "Existing finance notification / in-app outbox",
                        notice = "Queued, not a delivery confirmation. Existing seven-day reminder suppression applies. Scheduled reminders continue through the finance worker.",
                    }
                );
            }
        );
        api.MapGet(
            "/renewals",
            async (
                Access a,
                CommunicationService comm,
                Guid? orgId = null,
                int days = 30,
                int page = 1,
                string status = "upcoming"
            ) =>
            {
                var w = await Workspace(a, comm, View);
                Check(
                    new[] { 7, 30, 60, 90 }.Contains(days)
                        && page >= 1
                        && page <= 100000
                        && new[] { "upcoming", "expired", "completed" }.Contains(status),
                    "Invalid renewal filters."
                );
                var q = a.Db.Memberships.Where(m =>
                    m.TenantId == w.TenantId && a.Visible(w).Any(o => o.Id == m.ClubId)
                );
                if (orgId != null)
                {
                    Check(
                        await a.Visible(w).AnyAsync(o => o.Id == orgId),
                        "Organization outside your scope.",
                        404
                    );
                    q = q.Where(m => a.Tree(w.TenantId, orgId.Value).Any(o => o.Id == m.ClubId));
                }
                if (status == "completed")
                    q = q.Where(m =>
                        a.Db.MembershipRenewals.Any(r =>
                            r.MembershipId == m.Id && r.At >= DateTimeOffset.UtcNow.AddDays(-days)
                        )
                    );
                else if (status == "expired")
                    q = q.Where(m => m.EndsAt < DateTimeOffset.UtcNow);
                else
                    q = q.Where(m =>
                        m.EndsAt >= DateTimeOffset.UtcNow
                        && m.EndsAt <= DateTimeOffset.UtcNow.AddDays(days)
                    );
                return Results.Ok(
                    new
                    {
                        total = await q.CountAsync(),
                        page,
                        items = await q.OrderBy(m => m.EndsAt)
                            .ThenBy(m => m.Id)
                            .Skip((page - 1) * 50)
                            .Take(50)
                            .Select(m => new
                            {
                                m.Id,
                                m.Number,
                                name = a
                                    .Db.Persons.Where(p => p.Id == m.PersonId)
                                    .Select(p => p.DisplayName)
                                    .First(),
                                club = m.Club.Name,
                                m.EndsAt,
                                m.Status,
                                pending = a
                                    .Db.FinanceInvoices.Where(i =>
                                        i.MembershipId == m.Id
                                        && i.State == "Issued"
                                        && i.Purpose == "Renewal"
                                    )
                                    .Select(i => new
                                    {
                                        i.Id,
                                        i.Number,
                                        i.Currency,
                                        outstanding = i.Total - i.Paid - i.Credited,
                                    })
                                    .ToList(),
                            })
                            .ToListAsync(),
                    }
                );
            }
        );
    }
}
