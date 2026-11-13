using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record FinanceEntityInput(
    Guid OrgId,
    string Name,
    string Country,
    string Currency = "USD",
    int Decimals = 2,
    int FiscalMonth = 1,
    string Address = "",
    string TaxRegistration = "",
    string InvoicePrefix = "INV",
    string ReceiptPrefix = "REC",
    string CollectionAuthority = "Own organization only"
);

public record FinanceGrantInput(Guid UserId, string Role);

public record FinanceOrganizationInput(Guid OrgId, string Authority);

public record FinancePeriodInput(string Name, DateOnly StartsOn, DateOnly EndsOn);

public record FinanceReason(string Reason);

public record FinanceAccountInput(string Code, string Name, string Kind);

public record FinanceInvoiceInput(
    Guid? PersonId,
    string Customer,
    string Purpose,
    DateOnly Date,
    DateOnly DueOn,
    string Key,
    FinanceLineInput[] Lines,
    Guid? EventId = null,
    Guid? ProjectId = null,
    string Notes = "",
    Guid? MembershipId = null
);

public record FinanceCreditInput(decimal Amount, string Reason, string Key);

public record FinancePaymentInput(
    Guid? PersonId,
    string Payer,
    string Reference,
    string Method,
    string Currency,
    decimal Amount,
    DateOnly Date,
    string Key,
    string Evidence = ""
);

public record FinanceAllocationInput(Guid InvoiceId, decimal Amount, string Key);

public record FinanceSettlementInput(
    Guid BankAccountId,
    DateOnly Date,
    decimal Fee,
    string Reference
);

public record FinanceRefundInput(
    Guid? InvoiceId,
    decimal Amount,
    string Reason,
    string Reference,
    string Kind = "Refund"
);

public record FinanceJournalInput(
    DateOnly Date,
    string Key,
    string Narration,
    FinancePosting[] Lines
);

public record FinancePlanInput(
    string Name,
    Guid BillingOrgId,
    Guid? CategoryId,
    string Purpose,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    FinanceComponent[] Components,
    int Months = 12,
    int GraceDays = 30,
    bool Prorate = false,
    bool Once = false,
    bool AutoBill = false,
    DateOnly? NextBillOn = null,
    string ChargeCode = ""
);

public record FinanceAssessmentInput(
    Guid MembershipId,
    DateOnly StartsOn,
    DateOnly EndsOn,
    decimal Discount = 0,
    string Reason = ""
);

public record FinanceBatchInput(DateOnly StartsOn, DateOnly EndsOn);

public record FinanceBankInput(
    string Name,
    string AccountNumber,
    string RoutingDetails,
    string AccountCode = "1010"
);

public record FinanceBankRow(DateOnly Date, decimal Amount, string Reference, string Key);

public record FinanceBankImport(Guid BankAccountId, FinanceBankRow[] Rows);

public record FinanceMatchInput(Guid PaymentId, Guid[]? PaymentIds = null, Guid? ClubId = null);

public record FinanceExpenseInput(
    Guid PersonId,
    string Payee,
    string Description,
    string Category,
    FinanceComponent[] Lines,
    Guid[] Evidence,
    DateOnly Date,
    Guid? ProjectId = null,
    Guid? EventId = null,
    Guid? BudgetId = null,
    string AccountCode = "5000"
);

public record FinanceBudgetInput(
    string Name,
    string Kind,
    DateOnly StartsOn,
    DateOnly EndsOn,
    FinanceComponent[] Lines,
    Guid? ProjectId = null,
    Guid? EventId = null,
    Guid? ReplacesId = null
);

public record FinanceDisbursement(decimal Amount, string Reference, DateOnly Date);

public record FinancePledgeInput(
    Guid? PersonId,
    string Contributor,
    string Kind,
    string Description,
    decimal Amount,
    DateOnly Date,
    Guid? ProjectId = null,
    Guid? EventId = null
);

public record FinanceTransferInput(decimal Amount, string Reference, DateOnly Date);

public record FinanceConversionInput(
    decimal Amount,
    string Currency,
    DateOnly Date,
    int Decimals = 2
);

public record FinanceRateInput(string Currency, decimal Rate, DateOnly Date, string Source);

public static partial class FinanceEndpoints
{
    public static void MapFinance(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/finance")
            .AddEndpointFilter<FinanceInputFilter>()
            .RequireRateLimiting("events");
        api.MapGet(
            "/entities",
            async (FinanceService s) =>
            {
                var w = await s.Workspace();
                var platformAdmin = (await s.A.User()).PlatformAdmin;
                return await s
                    .Db.FinanceEntitys.Where(e =>
                        e.TenantId == w.TenantId
                        && (platformAdmin || s.Db.FinanceGrants.Any(g => g.EntityId == e.Id && g.UserId == w.UserId))
                    )
                    .OrderBy(e => e.Name)
                    .Take(100)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/entities",
            async (FinanceEntityInput i, FinanceService s) =>
            {
                var w = await s.A.Workspace("finance.configure");
                await s.Governance.Scope(w, i.OrgId);
                Check(
                    i.Name.Length is > 0 and <= 200
                        && i.Country.Length is > 0 and <= 80
                        && System.Text.RegularExpressions.Regex.IsMatch(i.Currency, "^[A-Z]{3}$")
                        && i.Decimals is >= 0 and <= 4
                        && i.FiscalMonth is >= 1 and <= 12
                        && i.Address.Length <= 1000
                        && i.CollectionAuthority.Length is > 0 and <= 1000
                        && System.Text.RegularExpressions.Regex.IsMatch(
                            i.InvoicePrefix,
                            "^[A-Z0-9-]{1,12}$"
                        )
                        && System.Text.RegularExpressions.Regex.IsMatch(
                            i.ReceiptPrefix,
                            "^[A-Z0-9-]{1,12}$"
                        ),
                    "Invalid financial entity configuration."
                );
                await using var tx = await s.A.Mutation(w);
                var e = new FinanceEntity
                {
                    TenantId = w.TenantId,
                    OrgId = i.OrgId,
                    Name = i.Name,
                    Country = i.Country,
                    Currency = i.Currency,
                    Decimals = i.Decimals,
                    FiscalMonth = i.FiscalMonth,
                    Address = i.Address,
                    TaxRegistration = i.TaxRegistration,
                    InvoicePrefix = i.InvoicePrefix,
                    ReceiptPrefix = i.ReceiptPrefix,
                    CollectionAuthority = i.CollectionAuthority,
                };
                e.EntityId = e.Id;
                s.Db.FinanceEntitys.Add(e);
                s.Db.FinanceGrants.Add(
                    s.Bind(new FinanceGrant { UserId = w.UserId, Role = "Owner" }, e)
                );
                s.Db.FinanceOrganizations.Add(
                    s.Bind(
                        new FinanceOrganization
                        {
                            ServedOrgId = i.OrgId,
                            Authority = i.CollectionAuthority,
                        },
                        e
                    )
                );
                foreach (
                    var (code, name, kind) in new[]
                    {
                        ("1000", "Cash", "Asset"),
                        ("1010", "Bank", "Asset"),
                        ("1020", "Collection clearing", "Asset"),
                        ("1100", "Accounts receivable", "Asset"),
                        ("1200", "Inter-entity receivable", "Asset"),
                        ("2000", "Accounts payable", "Liability"),
                        ("2100", "Tax payable", "Liability"),
                        ("2200", "Inter-entity payable", "Liability"),
                        ("2300", "Member advances", "Liability"),
                        ("3000", "Accumulated funds", "Equity"),
                        ("4000", "Membership income", "Income"),
                        ("4100", "Event income", "Income"),
                        ("4200", "Donation income", "Income"),
                        ("4300", "Sponsorship income", "Income"),
                        ("4400", "Other income", "Income"),
                        ("5000", "Administration", "Expense"),
                        ("5100", "Event expense", "Expense"),
                        ("5200", "Project expense", "Expense"),
                        ("5300", "Gateway fees", "Expense"),
                    }
                )
                    s.Db.FinanceAccounts.Add(
                        s.Bind(
                            new FinanceAccount
                            {
                                Code = code,
                                Name = name,
                                Kind = kind,
                            },
                            e
                        )
                    );
                s.Audit(e, w.UserId, "entity.create", i.CollectionAuthority);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return e;
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/settings",
            async (Guid entity, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                return new
                {
                    entity = e,
                    organizations = await s
                        .Db.FinanceOrganizations.Where(x => x.EntityId == entity)
                        .ToListAsync(),
                    grants = await s
                        .Db.FinanceGrants.Where(x => x.EntityId == entity)
                        .ToListAsync(),
                    periods = await s
                        .Db.FinancePeriods.Where(x => x.EntityId == entity)
                        .OrderByDescending(x => x.StartsOn)
                        .Take(100)
                        .ToListAsync(),
                    rates = await s
                        .Db.FinanceExchangeRates.Where(x => x.EntityId == entity)
                        .OrderByDescending(x => x.Date)
                        .Take(100)
                        .ToListAsync(),
                };
            }
        );
        api.MapPut(
            "/entities/{entity:guid}/gateway",
            async (Guid entity, FinanceReason i, FinanceService s, IConfiguration config) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                Check(
                    i.Reason == ""
                        || System.Text.RegularExpressions.Regex.IsMatch(
                            i.Reason,
                            "^[A-Za-z0-9_-]{1,64}$"
                        ),
                    "Use a server-configured gateway alias."
                );
                if (i.Reason != "")
                    Check(
                        config[$"Finance:Gateways:{i.Reason}:EntityId"] == entity.ToString(),
                        "Gateway must explicitly bind this financial entity in server configuration."
                    );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                e.GatewayKey = i.Reason;
                e.Version++;
                s.Audit(e, (await s.Workspace()).UserId, "gateway.configure", i.Reason);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/grants",
            async (Guid entity, FinanceGrantInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                Check(
                    new[] { "Owner", "Treasurer", "Billing", "Reviewer", "Reader" }.Contains(i.Role)
                        && await s.Db.Assignments.AnyAsync(x =>
                            x.UserId == i.UserId && x.TenantId == e.TenantId
                        ),
                    "Choose an existing association user and financial role."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                var g = await s.Db.FinanceGrants.SingleOrDefaultAsync(x =>
                    x.EntityId == entity && x.UserId == i.UserId
                );
                if (g == null)
                    s.Db.FinanceGrants.Add(
                        s.Bind(new FinanceGrant { UserId = i.UserId, Role = i.Role }, e)
                    );
                else
                {
                    Check(
                        g.UserId != (await s.Workspace()).UserId,
                        "Use another owner to change your own grant.",
                        409
                    );
                    g.Role = i.Role;
                    g.Version++;
                }
                s.Audit(e, (await s.Workspace()).UserId, "grant.set", i.UserId + ":" + i.Role);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapDelete(
            "/entities/{entity:guid}/grants/{user:guid}",
            async (Guid entity, Guid user, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                Check(
                    user != (await s.Workspace()).UserId,
                    "Cannot revoke your own financial owner grant.",
                    409
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                var g = await s.Db.FinanceGrants.SingleOrDefaultAsync(x =>
                    x.EntityId == entity && x.UserId == user
                );
                if (g != null)
                {
                    s.Db.FinanceGrants.Remove(g);
                    s.Audit(e, (await s.Workspace()).UserId, "grant.revoke", user.ToString());
                    await s.Db.SaveChangesAsync();
                }
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/organizations",
            async (Guid entity, FinanceOrganizationInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                var w = await s.Workspace();
                await s.Governance.Scope(w, i.OrgId);
                Check(
                    i.Authority.Length is > 0 and <= 1000,
                    "Record the explicit collection authority."
                );
                if (
                    !await s.Db.FinanceOrganizations.AnyAsync(x =>
                        x.EntityId == entity && x.ServedOrgId == i.OrgId
                    )
                )
                {
                    s.Db.FinanceOrganizations.Add(
                        s.Bind(
                            new FinanceOrganization
                            {
                                ServedOrgId = i.OrgId,
                                Authority = i.Authority,
                            },
                            e
                        )
                    );
                    s.Audit(e, w.UserId, "organization.authorize", i.OrgId + ":" + i.Authority);
                    await s.Db.SaveChangesAsync();
                }
                return Results.NoContent();
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/options",
            async (Guid entity, FinanceService s, string search = "") =>
            {
                await s.Entity(entity);
                Check(search.Length <= 100, "Search too long.");
                var orgs = s
                    .Db.FinanceOrganizations.Where(x => x.EntityId == entity)
                    .Select(x => x.ServedOrgId);
                return new
                {
                    people = await s
                        .Db.Persons.Where(p =>
                            s.Db.Memberships.Any(m => m.PersonId == p.Id && orgs.Contains(m.ClubId))
                            && (
                                search == ""
                                || EF.Functions.ILike(p.DisplayName, "%" + search + "%")
                            )
                        )
                        .OrderBy(p => p.DisplayName)
                        .Take(50)
                        .Select(p => new { p.Id, name = p.DisplayName })
                        .ToListAsync(),
                    memberships = await s
                        .Db.Memberships.Where(m =>
                            orgs.Contains(m.ClubId)
                            && (
                                search == ""
                                || EF.Functions.ILike(m.Person.DisplayName, "%" + search + "%")
                            )
                        )
                        .OrderBy(m => m.Id)
                        .Take(50)
                        .Select(m => new
                        {
                            m.Id,
                            m.PersonId,
                            m.ClubId,
                            m.CategoryId,
                            name = m.Person.DisplayName,
                            number = m.Number,
                        })
                        .ToListAsync(),
                    organizations = await s
                        .Db.Orgs.Where(o => orgs.Contains(o.Id))
                        .Select(o => new { o.Id, o.Name })
                        .ToListAsync(),
                    categories = await s
                        .Db.MembershipCategories.Where(c =>
                            c.TenantId
                            == (
                                s
                                    .Db.FinanceEntitys.Where(e => e.Id == entity)
                                    .Select(e => e.TenantId)
                                    .First()
                            )
                        )
                        .Select(c => new { c.Id, c.Name })
                        .ToListAsync(),
                    projects = await s
                        .Db.ServiceProjects.Where(p => orgs.Contains(p.OrgId))
                        .OrderBy(p => p.Name)
                        .Take(100)
                        .Select(p => new { p.Id, name = p.Name })
                        .ToListAsync(),
                    events = await s
                        .Db.Events.Where(p => orgs.Contains(p.OrgId))
                        .OrderBy(p => p.Title)
                        .Take(100)
                        .Select(p => new { p.Id, name = p.Title })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/entities/{entity:guid}/accounts",
            async (Guid entity, FinanceService s) =>
            {
                await s.Entity(entity);
                return await s
                    .Db.FinanceAccounts.Where(x => x.EntityId == entity)
                    .OrderBy(x => x.Code)
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/accounts",
            async (Guid entity, FinanceAccountInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                Check(
                    System.Text.RegularExpressions.Regex.IsMatch(i.Code, "^[0-9]{4,10}$")
                        && i.Name.Length is > 0 and <= 150
                        && new[] { "Asset", "Liability", "Equity", "Income", "Expense" }.Contains(
                            i.Kind
                        ),
                    "Invalid account."
                );
                var r = s.Bind(
                    new FinanceAccount
                    {
                        Code = i.Code,
                        Name = i.Name,
                        Kind = i.Kind,
                    },
                    e
                );
                s.Db.FinanceAccounts.Add(r);
                s.Audit(e, (await s.Workspace()).UserId, "account.create", i.Code);
                await s.Db.SaveChangesAsync();
                return r;
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/periods",
            async (Guid entity, FinancePeriodInput i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                Check(
                    i.Name.Length is > 0 and <= 100
                        && i.StartsOn <= i.EndsOn
                        && i.EndsOn <= i.StartsOn.AddYears(1),
                    "Invalid accounting period."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                Check(
                    !await s.Db.FinancePeriods.AnyAsync(x =>
                        x.EntityId == entity && x.StartsOn <= i.EndsOn && x.EndsOn >= i.StartsOn
                    ),
                    "Accounting periods cannot overlap.",
                    409
                );
                var r = s.Bind(
                    new FinancePeriod
                    {
                        Name = i.Name,
                        StartsOn = i.StartsOn,
                        EndsOn = i.EndsOn,
                    },
                    e
                );
                s.Db.FinancePeriods.Add(r);
                s.Audit(e, (await s.Workspace()).UserId, "period.create", i.Name);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        api.MapPost(
            "/entities/{entity:guid}/periods/{id:guid}/{action}",
            async (Guid entity, Guid id, string action, FinanceReason i, FinanceService s) =>
            {
                var e = await s.Entity(entity, "finance.configure");
                Check(
                    action is "close" or "reopen" && i.Reason.Length is > 0 and <= 1000,
                    "Period change needs a reason."
                );
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                await s.Lock(entity);
                var r = await s.Db.FinancePeriods.SingleOrDefaultAsync(x =>
                    x.Id == id && x.EntityId == entity
                );
                Check(r != null, "Period not found.", 404);
                r.Closed = action == "close";
                r.Version++;
                s.Audit(e, (await s.Workspace()).UserId, "period." + action, i.Reason);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return r;
            }
        );
        MapBilling(api);
        MapPayments(api);
        MapPlanning(api);
        MapReports(api);
        MapCommand(api);
        MapGateway(app, api);
    }
}
