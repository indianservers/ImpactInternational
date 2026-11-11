using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record FinanceEventPriceInput(Guid EventId, string Category, decimal Amount);

public static partial class FinanceEndpoints
{
    public static void MapFinanceMembers(this WebApplication app)
    {
        app.MapGet(
            "/api/v1/my/finance",
            async (FinanceService s) =>
            {
                var w = await s.Workspace();
                var people = s.Db.Persons.Where(p => p.UserId == w.UserId).Select(p => p.Id);
                var invoices = s.Db.FinanceInvoices.Where(x =>
                    x.TenantId == w.TenantId
                    && x.PersonId != null
                    && people.Contains(x.PersonId.Value)
                    && x.State == "Issued"
                );
                var payments = s.Db.FinancePayments.Where(x =>
                    x.TenantId == w.TenantId
                    && x.PersonId != null
                    && people.Contains(x.PersonId.Value)
                );
                return new
                {
                    balances = await invoices
                        .GroupBy(x => x.Currency)
                        .Select(g => new
                        {
                            currency = g.Key,
                            outstanding = g.Sum(x => x.Total - x.Paid - x.Credited),
                            due = g.Where(x => x.Total > x.Paid + x.Credited)
                                .Min(x => (DateOnly?)x.DueOn),
                        })
                        .ToListAsync(),
                    invoices = await invoices
                        .OrderByDescending(x => x.Date)
                        .ThenByDescending(x => x.Id)
                        .Take(100)
                        .ToListAsync(),
                    payments = await payments
                        .OrderByDescending(x => x.Date)
                        .ThenByDescending(x => x.Id)
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );
        app.MapGet(
            "/api/v1/finance/entities/{entity:guid}/event-prices",
            async (Guid entity, FinanceService s) =>
            {
                await s.Entity(entity);
                return await s
                    .Db.FinanceEventPrices.Where(x => x.EntityId == entity)
                    .Take(100)
                    .ToListAsync();
            }
        );
        app.MapPost(
            "/api/v1/finance/entities/{entity:guid}/event-prices",
            async (Guid entity, FinanceEventPriceInput i, FinanceService s, EventService events) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                await s.Sources(e, i.EventId, null);
                var ev = await events.Find(i.EventId);
                await events.Manage(ev, "Settings");
                s.Exact(e, i.Amount);
                Check(i.Category is "Member" or "Guest", "Price category must be Member or Guest.");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                await events.Lock(ev.Id);
                Check(
                    !await s.Db.EventRegistrations.AnyAsync(x => x.EventId == ev.Id),
                    "Configure pricing before any registrations.",
                    409
                );
                Check(
                    !ev.RegistrationApproval && ev.MaxCompanions == 0,
                    "Paid events currently require direct registration without companions.",
                    409
                );
                var price = await s.Db.FinanceEventPrices.SingleOrDefaultAsync(x =>
                    x.EventId == ev.Id && x.Category == i.Category
                );
                if (price == null)
                    s.Db.FinanceEventPrices.Add(
                        s.Bind(
                            new FinanceEventPrice
                            {
                                EventId = ev.Id,
                                Category = i.Category,
                                Amount = i.Amount,
                            },
                            e
                        )
                    );
                else
                {
                    Check(
                        price.EntityId == entity,
                        "Event is already assigned to another collector.",
                        409
                    );
                    price.Amount = i.Amount;
                    price.Version++;
                }
                ev.Paid = true;
                ev.Version++;
                s.Audit(e, (await s.Workspace()).UserId, "event.price", ev.Title);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        app.MapPost(
            "/api/v1/finance/registrations/{id:guid}/invoice",
            async (Guid id, FinanceService s, EventService events) =>
            {
                var registration = await s.Db.EventRegistrations.SingleOrDefaultAsync(x =>
                    x.Id == id
                );
                Check(registration != null, "Registration unavailable.", 404);
                var ev = await events.Find(registration.EventId);
                await events.Own(ev, id);
                Check(
                    ev.Paid
                        && registration.PersonId != null
                        && registration.Status == "Pending Payment",
                    "A signed-in member's pending paid registration is required.",
                    409
                );
                var price = await s.Db.FinanceEventPrices.SingleOrDefaultAsync(x =>
                    x.EventId == ev.Id && x.Category == registration.Category
                );
                Check(price != null, "This registration category has no configured price.", 409);
                var e = await s.Db.FinanceEntitys.SingleAsync(x =>
                    x.Id == price.EntityId && x.Active
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                var invoice = await s.CreateInvoice(
                    e,
                    registration.PersonId,
                    registration.Name,
                    "Event",
                    FinanceService.Today,
                    FinanceService.Today.AddDays(7),
                    "registration:" + id,
                    [
                        new(
                            ev.Title + " / " + registration.Category,
                            1,
                            price.Amount,
                            AccountCode: "4100"
                        ),
                    ],
                    (await s.Workspace()).UserId,
                    eventId: ev.Id
                );
                invoice.RegistrationId = id;
                await s.Db.SaveChangesAsync();
                await s.Issue(invoice, e, invoice.CreatorId);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return invoice;
            }
        );
        app.MapPost(
            "/api/v1/finance/registrations/{id:guid}/confirm",
            async (Guid id, FinanceService s, EventService events) =>
            {
                var r = await s.Db.EventRegistrations.SingleOrDefaultAsync(x => x.Id == id);
                Check(r != null, "Registration unavailable.", 404);
                var i = await s.Db.FinanceInvoices.SingleOrDefaultAsync(x =>
                    x.RegistrationId == id
                );
                Check(i != null, "Invoice unavailable.", 404);
                var e = await s.Entity(i.EntityId, "finance.reconcile");
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(e.Id);
                await events.Lock(r.EventId);
                Check(r.Status == "Pending Payment", "Registration is not awaiting payment.", 409);
                var ev = await s.Db.Events.SingleAsync(x => x.Id == r.EventId);
                await events.Confirm(ev, r);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        app.MapPost(
            "/api/v1/finance/jobs/{id:guid}/retry",
            async (Guid id, FinanceService s) =>
            {
                var j = await s.Db.FinanceJobs.SingleOrDefaultAsync(x => x.Id == id);
                Check(j != null, "Job unavailable.", 404);
                await s.Entity(j.EntityId, "finance.manage");
                Check(j.Status == "Failed", "Only stopped jobs may retry.", 409);
                j.Status = "Queued";
                j.Error = "";
                j.CreatorId = (await s.Workspace()).UserId;
                j.Version++;
                await s.Db.SaveChangesAsync();
                return j;
            }
        );
    }
}
