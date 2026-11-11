using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
using static Clubs.GovernanceService;

namespace Clubs;

public record FinanceLineInput(
    string Description,
    decimal Quantity,
    decimal Rate,
    decimal Discount = 0,
    decimal TaxPercent = 0,
    string AccountCode = "4000",
    Guid? BeneficiaryEntityId = null
);

public record FinanceComponent(
    string Description,
    decimal Amount,
    string AccountCode = "4000",
    decimal TaxPercent = 0,
    Guid? BeneficiaryEntityId = null
);

public record FinancePosting(
    string Account,
    decimal Debit,
    decimal Credit,
    Guid? PersonId = null,
    Guid? EventId = null,
    Guid? ProjectId = null
);

public record FinanceReview(Guid EntityId, Guid Id, string Hash);

public class FinanceService(
    Access a,
    GovernanceService governance,
    NotificationService notifications
)
{
    public Access A => a;
    public Db Db => a.Db;
    readonly HashSet<(Guid Entity, string Permission)> requestedAccess = [];
    public GovernanceService Governance => governance;
    public static readonly string[] Permissions =
    [
        "finance.view",
        "finance.manage",
        "finance.configure",
        "finance.reconcile",
        "finance.post",
        "finance.approve",
        "finance.self",
    ];
    public static readonly string[] Purposes =
    [
        "ClubDues",
        "AssociationDues",
        "DistrictDues",
        "NationalDues",
        "InternationalDues",
        "JoiningFee",
        "Renewal",
        "Event",
        "Project",
        "Donation",
        "Sponsorship",
        "Other",
    ];
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<Assignment> Workspace() => await A.WorkspaceAny(Permissions);

    public async Task<FinanceEntity> Entity(Guid id, string permission = "finance.view")
    {
        var w = await Workspace();
        var e = await Db.FinanceEntitys.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == w.TenantId
        );
        Check(e != null && e.Active, "Financial entity unavailable.", 404);
        var grants = await A.Grants(w);
        Check(
            grants.Contains(permission)
                || permission == "finance.view"
                    && grants.Any(x => x.StartsWith("finance.") && x != "finance.self"),
            "Financial permission required.",
            403
        );
        var role = await Db
            .FinanceGrants.Where(x =>
                x.EntityId == id && x.TenantId == w.TenantId && x.UserId == w.UserId
            )
            .Select(x => x.Role)
            .SingleOrDefaultAsync();
        var allowed =
            permission == "finance.view"
                ? new[] { "Reader", "Billing", "Treasurer", "Reviewer", "Owner" }
            : permission == "finance.configure" ? new[] { "Owner" }
            : permission == "finance.manage" ? new[] { "Billing", "Treasurer", "Owner" }
            : permission == "finance.approve" ? new[] { "Reviewer", "Treasurer", "Owner" }
            : new[] { "Treasurer", "Owner" };
        Check(
            role != null && allowed.Contains(role),
            "Explicit financial entity authority required; hierarchy alone does not grant access.",
            403
        );
        requestedAccess.Add((id, permission));
        return e;
    }

    public T Bind<T>(T r, FinanceEntity e)
        where T : FinancialRecord
    {
        r.EntityId = e.Id;
        r.TenantId = e.TenantId;
        r.OrgId = e.OrgId;
        return r;
    }

    public async Task Lock(Guid entity)
    {
        var tenant = await Db
            .FinanceEntitys.Where(x => x.Id == entity)
            .Select(x => x.TenantId)
            .SingleAsync();
        await Db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({tenant.ToString()},7))"
        );
        foreach (
            var entry in Db
                .ChangeTracker.Entries()
                .Where(x => x.Entity is FinancialRecord && x.State == EntityState.Unchanged)
                .ToArray()
        )
            await entry.ReloadAsync();
        foreach (var grant in requestedAccess.ToArray())
            await Entity(grant.Entity, grant.Permission);
    }

    public void Audit(FinanceEntity e, Guid user, string action, string detail = "") =>
        Db.FinanceAudits.Add(
            Bind(
                new FinanceAudit
                {
                    ActorId = user,
                    Action = action,
                    Detail = detail,
                },
                e
            )
        );

    public decimal Money(FinanceEntity e, decimal n)
    {
        Check(n >= -999999999999m && n <= 999999999999m, "Amount exceeds supported limit.");
        return decimal.Round(n, e.Decimals, MidpointRounding.AwayFromZero);
    }

    public void Exact(FinanceEntity e, decimal n, bool positive = true)
    {
        Check(n == Money(e, n) && (!positive || n > 0), "Invalid amount or currency precision.");
    }

    public async Task Person(Guid? person, FinanceEntity e)
    {
        if (person != null)
            Check(
                await Db.Memberships.AnyAsync(m =>
                    m.TenantId == e.TenantId
                    && m.PersonId == person
                    && (
                        m.ClubId == e.OrgId
                        || Db.FinanceOrganizations.Any(o =>
                            o.EntityId == e.Id && o.ServedOrgId == m.ClubId
                        )
                    )
                ),
                "Payer is outside authorized entity collection organizations.",
                403
            );
    }

    public async Task Sources(FinanceEntity e, Guid? eventId, Guid? projectId)
    {
        if (eventId != null)
            Check(
                await Db.Events.AnyAsync(x =>
                    x.Id == eventId
                    && x.TenantId == e.TenantId
                    && (
                        x.OrgId == e.OrgId
                        || Db.FinanceOrganizations.Any(o =>
                            o.EntityId == e.Id && o.ServedOrgId == x.OrgId
                        )
                    )
                ),
                "Event is outside this financial entity.",
                403
            );
        if (projectId != null)
            Check(
                await Db.ServiceProjects.AnyAsync(x =>
                    x.Id == projectId
                    && x.TenantId == e.TenantId
                    && (
                        x.OrgId == e.OrgId
                        || Db.FinanceOrganizations.Any(o =>
                            o.EntityId == e.Id && o.ServedOrgId == x.OrgId
                        )
                    )
                ),
                "Project is outside this financial entity.",
                403
            );
    }

    public async Task<FinanceJournal> Post(
        FinanceEntity e,
        DateOnly date,
        string source,
        string narration,
        IEnumerable<FinancePosting> postings,
        Guid actor,
        Guid? reverses = null
    )
    {
        var existing = await Db.FinanceJournals.SingleOrDefaultAsync(x =>
            x.EntityId == e.Id && x.Source == source
        );
        var lines = postings.Where(x => x.Debit != 0 || x.Credit != 0).ToArray();
        if (existing != null)
        {
            var prior = await (
                from l in Db.FinanceJournalLines
                join ac in Db.FinanceAccounts on l.AccountId equals ac.Id
                where l.JournalId == existing.Id
                select new FinancePosting(
                    ac.Code,
                    l.Debit,
                    l.Credit,
                    l.PersonId,
                    l.EventId,
                    l.ProjectId
                )
            ).ToListAsync();
            IEnumerable<FinancePosting> Sort(IEnumerable<FinancePosting> rows) =>
                rows.OrderBy(x => x.Account)
                    .ThenBy(x => x.Debit)
                    .ThenBy(x => x.Credit)
                    .ThenBy(x => x.PersonId)
                    .ThenBy(x => x.EventId)
                    .ThenBy(x => x.ProjectId);
            Check(
                existing.Date == date
                    && existing.Narration == narration
                    && Sort(prior).SequenceEqual(Sort(lines)),
                "Journal source key was used for different posting details.",
                409
            );
            return existing;
        }
        Check(
            lines.Length >= 2
                && lines.Length <= 250
                && lines.Sum(x => x.Debit) == lines.Sum(x => x.Credit)
                && lines.Sum(x => x.Debit) > 0,
            "Journal must have equal positive debits and credits."
        );
        foreach (var l in lines)
        {
            Exact(e, l.Debit, false);
            Exact(e, l.Credit, false);
            Check(
                l.Debit >= 0 && l.Credit >= 0 && (l.Debit == 0 || l.Credit == 0),
                "Each journal line has one positive side."
            );
        }
        var periods = await Db
            .FinancePeriods.Where(x => x.EntityId == e.Id && x.StartsOn <= date && x.EndsOn >= date)
            .ToListAsync();
        Check(
            periods.Count == 1 && !periods[0].Closed,
            "Posting requires one open accounting period.",
            409
        );
        var codes = lines.Select(x => x.Account).Distinct().ToArray();
        var accounts = await Db
            .FinanceAccounts.Where(x => x.EntityId == e.Id && x.Active && codes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code);
        Check(accounts.Count == codes.Length, "Active posting account missing.");
        var j = Bind(
            new FinanceJournal
            {
                Date = date,
                PeriodId = periods[0].Id,
                Source = source,
                Narration = narration,
                CreatorId = actor,
                ReversesId = reverses,
            },
            e
        );
        Db.FinanceJournals.Add(j);
        foreach (var l in lines)
            Db.FinanceJournalLines.Add(
                Bind(
                    new FinanceJournalLine
                    {
                        JournalId = j.Id,
                        AccountId = accounts[l.Account].Id,
                        Debit = l.Debit,
                        Credit = l.Credit,
                        PersonId = l.PersonId,
                        EventId = l.EventId,
                        ProjectId = l.ProjectId,
                    },
                    e
                )
            );
        Audit(e, actor, "journal.post", source);
        return j;
    }

    public async Task<FinanceInvoice> Invoice(Guid id, bool write = false)
    {
        var w = await Workspace();
        var i = await Db.FinanceInvoices.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == w.TenantId
        );
        Check(i != null, "Invoice unavailable.", 404);
        if (
            !write
            && i.PersonId != null
            && await Db.Persons.AnyAsync(x => x.Id == i.PersonId && x.UserId == w.UserId)
        )
            return i;
        await Entity(i.EntityId, write ? "finance.manage" : "finance.view");
        return i;
    }

    public async Task<FinancePayment> Payment(Guid id, bool write = false)
    {
        var w = await Workspace();
        var p = await Db.FinancePayments.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == w.TenantId
        );
        Check(p != null, "Payment unavailable.", 404);
        if (
            !write
            && p.PersonId != null
            && await Db.Persons.AnyAsync(x => x.Id == p.PersonId && x.UserId == w.UserId)
        )
            return p;
        await Entity(p.EntityId, write ? "finance.reconcile" : "finance.view");
        return p;
    }

    public async Task<FinanceInvoice> CreateInvoice(
        FinanceEntity e,
        Guid? person,
        string customer,
        string purpose,
        DateOnly date,
        DateOnly due,
        string key,
        FinanceLineInput[] lines,
        Guid actor,
        Guid? membership = null,
        Guid? plan = null,
        DateOnly? start = null,
        DateOnly? end = null,
        Guid? eventId = null,
        Guid? projectId = null,
        string notes = ""
    )
    {
        Check(
            key.Length is > 0 and <= 150
                && customer.Length is > 0 and <= 200
                && purpose.Length <= 50
                && Purposes.Contains(purpose)
                && lines.Length is > 0 and <= 100
                && due >= date
                && notes.Length <= 4000,
            "Invalid invoice details."
        );
        var old = await Db.FinanceInvoices.SingleOrDefaultAsync(x =>
            x.EntityId == e.Id && x.IdempotencyKey == key
        );
        var requestHash = Hash(
            Pack(
                new
                {
                    person,
                    customer,
                    purpose,
                    date,
                    due,
                    lines,
                    eventId,
                    projectId,
                    notes,
                }
            )
        );
        if (old != null)
        {
            Check(
                old.RequestHash == requestHash,
                "Invoice idempotency key belongs to different details.",
                409
            );
            return old;
        }
        if (
            !(
                purpose == "Event"
                && eventId != null
                && person != null
                && await Db.EventRegistrations.AnyAsync(r =>
                    r.EventId == eventId && r.PersonId == person && r.TenantId == e.TenantId
                )
            )
        )
            await Person(person, e);
        await Sources(e, eventId, projectId);
        var i = Bind(
            new FinanceInvoice
            {
                RequestHash = requestHash,
                PersonId = person,
                Customer = customer,
                Purpose = purpose,
                Date = date,
                DueOn = due,
                Currency = e.Currency,
                IdempotencyKey = key,
                CreatorId = actor,
                MembershipId = membership,
                PlanId = plan,
                PeriodStart = start,
                PeriodEnd = end,
                EventId = eventId,
                ProjectId = projectId,
                Notes = notes,
            },
            e
        );
        Db.FinanceInvoices.Add(i);
        foreach (var l in lines)
        {
            Check(
                l.Description.Length is > 0 and <= 500
                    && l.Quantity > 0
                    && l.Quantity <= 100000
                    && l.Rate >= 0
                    && l.Discount >= 0
                    && l.TaxPercent >= 0
                    && l.TaxPercent <= 100,
                "Invalid invoice line."
            );
            Exact(e, l.Rate, false);
            Exact(e, l.Discount, false);
            Check(
                l.AccountCode.StartsWith("4")
                    && await Db.FinanceAccounts.AnyAsync(x =>
                        x.EntityId == e.Id
                        && x.Code == l.AccountCode
                        && x.Kind == "Income"
                        && x.Active
                    ),
                "Invoice needs an active income account."
            );
            if (l.BeneficiaryEntityId != null && l.BeneficiaryEntityId != e.Id)
            {
                var b = await Entity(l.BeneficiaryEntityId.Value, "finance.configure");
                Check(
                    b.Currency == e.Currency && b.Decimals == e.Decimals,
                    "Inter-entity collection requires matching native currencies and precision."
                );
            }
            var net = Money(e, l.Quantity * l.Rate - l.Discount);
            Check(net >= 0, "Discount exceeds line value.");
            var tax = Money(e, net * l.TaxPercent / 100);
            Db.FinanceInvoiceLines.Add(
                Bind(
                    new FinanceInvoiceLine
                    {
                        InvoiceId = i.Id,
                        Description = l.Description,
                        Quantity = l.Quantity,
                        Rate = l.Rate,
                        Discount = l.Discount,
                        TaxPercent = l.TaxPercent,
                        Net = net,
                        Tax = tax,
                        Total = net + tax,
                        AccountCode = l.AccountCode,
                        BeneficiaryEntityId = l.BeneficiaryEntityId,
                    },
                    e
                )
            );
            i.Subtotal += net;
            i.Tax += tax;
            i.Total += net + tax;
        }
        Check(
            i.Total > 0,
            "Invoice total must be positive; use a recorded waiver for fully exempt dues."
        );
        Audit(e, actor, "invoice.create", i.Id.ToString());
        return i;
    }

    public async Task Issue(FinanceInvoice i, FinanceEntity e, Guid actor)
    {
        if (i.State == "Issued")
            return;
        Check(i.State == "Draft", "Only draft invoices can issue.", 409);
        var lines = await Db.FinanceInvoiceLines.Where(x => x.InvoiceId == i.Id).ToListAsync();
        var posts = new List<FinancePosting>
        {
            new("1100", i.Total, 0, i.PersonId, i.EventId, i.ProjectId),
        };
        foreach (var l in lines)
        {
            var other = l.BeneficiaryEntityId != null && l.BeneficiaryEntityId != e.Id;
            posts.Add(
                new(other ? "2200" : l.AccountCode, 0, l.Net, i.PersonId, i.EventId, i.ProjectId)
            );
            if (l.Tax > 0)
                posts.Add(new("2100", 0, l.Tax, i.PersonId, i.EventId, i.ProjectId));
            if (other && l.Net > 0)
            {
                var target = await Db.FinanceEntitys.SingleAsync(x =>
                    x.Id == l.BeneficiaryEntityId && x.TenantId == e.TenantId
                );
                var entitlement = Db.FinanceEntitlements.Local.FirstOrDefault(x =>
                    x.InvoiceId == i.Id && x.BeneficiaryEntityId == target.Id
                );
                if (entitlement == null)
                    Db.FinanceEntitlements.Add(
                        Bind(
                            new FinanceEntitlement
                            {
                                InvoiceId = i.Id,
                                BeneficiaryEntityId = target.Id,
                                Amount = l.Net,
                            },
                            e
                        )
                    );
                else
                    entitlement.Amount += l.Net;
                await Post(
                    target,
                    i.Date,
                    "entitlement:" + i.Id + ":" + l.Id,
                    "Receivable from collecting entity",
                    [
                        new("1200", l.Net, 0, i.PersonId, i.EventId, i.ProjectId),
                        new(l.AccountCode, 0, l.Net, i.PersonId, i.EventId, i.ProjectId),
                    ],
                    actor
                );
            }
        }
        await Post(e, i.Date, "invoice:" + i.Id, "Invoice issuance", posts, actor);
        e.InvoiceSequence++;
        e.Version++;
        i.Number = e.InvoicePrefix + "-" + e.InvoiceSequence.ToString("D8");
        i.IssuerSnapshot = Pack(
            new
            {
                e.Name,
                e.Address,
                e.Country,
                e.TaxRegistration,
                e.Currency,
            }
        );
        i.State = "Issued";
        i.Version++;
        Audit(e, actor, "invoice.issue", i.Number);
        await Notice(i.PersonId, e, "Invoice " + i.Number + " issued.");
    }

    public async Task Notice(Guid? person, FinanceEntity e, string text)
    {
        if (person == null)
            return;
        var user = await Db
            .Persons.Where(x => x.Id == person)
            .Select(x => x.UserId)
            .SingleOrDefaultAsync();
        if (user == null)
            return;
        await notifications.User(
            user,
            "finance.update",
            text,
            "/app/finance/member",
            e.TenantId,
            e.OrgId
        );
        var senders = await Db
            .Assignments.Where(x =>
                x.TenantId == e.TenantId
                && Db.FinanceGrants.Any(g =>
                    g.EntityId == e.Id
                    && g.UserId == x.UserId
                    && new[] { "Owner", "Treasurer", "Billing" }.Contains(g.Role)
                )
                && Db.RolePermissions.Any(p =>
                    p.RoleId == x.RoleId && p.PermissionId == "finance.manage"
                )
            )
            .ToListAsync();
        foreach (var sender in senders)
            if (await A.Effective(sender))
            {
                Db.Mails.Add(
                    new Mail
                    {
                        TenantId = e.TenantId,
                        OrgId = e.OrgId,
                        UserId = user,
                        Kind = "finance." + e.Id,
                        Channel = "InApp",
                        Purpose = "Transactional",
                        Subject = "Financial update",
                        Body = text,
                        Link = "/app/finance/member",
                        SenderId = sender.UserId,
                        SenderAssignmentId = sender.Id,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                        TokenHash = Hash(Token()),
                    }
                );
                break;
            }
    }

    public async Task Verify(FinancePayment p, FinanceEntity e, Guid actor, bool gateway = false)
    {
        if (p.Status == "Verified")
            return;
        Check(p.Status == "Pending", "Payment cannot be verified.", 409);
        Check(
            gateway
                || p.RecorderId != actor
                    && !await Db.Persons.AnyAsync(x => x.Id == p.PersonId && x.UserId == actor),
            "Payment recording and verification require independent users.",
            403
        );
        await Post(
            e,
            p.Date,
            "payment:" + p.Id,
            "Verified collection (not bank settlement)",
            [new("1020", p.Amount, 0, p.PersonId), new("2300", 0, p.Amount, p.PersonId)],
            actor
        );
        p.Status = "Verified";
        p.VerifierId = actor;
        e.ReceiptSequence++;
        e.Version++;
        p.ReceiptNumber = e.ReceiptPrefix + "-" + e.ReceiptSequence.ToString("D8");
        p.Version++;
        Audit(e, actor, "payment.verify", p.Id.ToString());
        await Notice(
            p.PersonId,
            e,
            "Payment verified. Receipt " + p.ReceiptNumber + " is available."
        );
    }

    public async Task Allocate(
        FinancePayment p,
        FinanceInvoice i,
        decimal amount,
        FinanceEntity e,
        Guid actor,
        string key
    )
    {
        Check(key.Length is > 0 and <= 150, "Allocation key required.");
        var old = await Db.FinanceAllocations.SingleOrDefaultAsync(x =>
            x.EntityId == e.Id && x.Key == key
        );
        if (old != null)
        {
            Check(
                old.PaymentId == p.Id && old.InvoiceId == i.Id && old.Amount == amount,
                "Allocation key mismatch.",
                409
            );
            return;
        }
        Exact(e, amount);
        Check(
            p.EntityId == i.EntityId
                && p.Currency == i.Currency
                && p.PersonId == i.PersonId
                && p.Status == "Verified"
                && i.State == "Issued",
            "Payment and invoice scope, payer or state mismatch.",
            409
        );
        Check(
            amount <= p.Amount - p.Allocated - p.Refunded
                && amount <= i.Total - i.Paid - i.Credited,
            "Allocation exceeds available payment or invoice balance.",
            409
        );
        Check(
            p.PersonId != null || p.Payer == i.Customer,
            "Unlinked payer name must match the billed customer.",
            409
        );
        var allocation = Bind(
            new FinanceAllocation
            {
                Key = key,
                PaymentId = p.Id,
                InvoiceId = i.Id,
                Amount = amount,
            },
            e
        );
        Db.FinanceAllocations.Add(allocation);
        await Post(
            e,
            Today,
            "allocation:" + allocation.Id,
            "Payment allocation",
            [
                new("2300", amount, 0, p.PersonId, i.EventId, i.ProjectId),
                new("1100", 0, amount, p.PersonId, i.EventId, i.ProjectId),
            ],
            actor
        );
        p.Allocated += amount;
        p.Version++;
        i.Paid += amount;
        i.Version++;
        Audit(e, actor, "payment.allocate", i.Number);
    }

    public async Task<FinanceInvoice?> Assess(
        FinanceEntity e,
        DuesPlan plan,
        Membership m,
        DateOnly start,
        DateOnly end,
        Guid actor,
        decimal discount = 0,
        string waiverReason = ""
    )
    {
        Check(
            start < end
                && start >= plan.EffectiveFrom
                && start <= plan.EffectiveTo
                && end == start.AddMonths(plan.Months),
            "Billing period outside fee effective dates."
        );
        Check(
            m.TenantId == e.TenantId
                && m.ClubId == plan.BillingOrgId
                && m.Status == "Active"
                && (plan.CategoryId == null || plan.CategoryId == m.CategoryId),
            "Membership does not match active fee eligibility."
        );
        var key =
            "dues:" + m.Id + ":" + plan.ChargeCode + ":" + (plan.Once ? "once" : start + ":" + end);
        var old = await Db.FinanceInvoices.SingleOrDefaultAsync(x =>
            x.EntityId == e.Id && x.IdempotencyKey == key
        );
        if (old != null)
            return old;
        Check(
            !await Db.FinanceAudits.AnyAsync(x =>
                x.EntityId == e.Id && x.Action == "dues.waiver" && x.Detail.StartsWith(key + "|")
            ),
            "Dues already waived for this assessment.",
            409
        );
        var parts = Unpack<FinanceComponent[]>(plan.Components);
        var joined = DateOnly.FromDateTime(m.JoinedAt.UtcDateTime);
        Check(
            joined < end
                && (m.EndsAt == null || DateOnly.FromDateTime(m.EndsAt.Value.UtcDateTime) > start),
            "Membership is outside the assessment period."
        );
        var factor =
            plan.Prorate && joined > start
                ? (decimal)Math.Max(0, end.DayNumber - joined.DayNumber)
                    / (end.DayNumber - start.DayNumber)
                : 1m;
        Check(
            discount >= 0 && (discount == 0 || waiverReason.Length is > 0 and <= 1000),
            "Concession requires a reason."
        );
        Exact(e, discount, false);
        var amount = parts.Sum(x => Money(e, x.Amount * factor));
        Check(discount <= amount, "Discount exceeds assessment.");
        if (amount == discount)
        {
            Audit(e, actor, "dues.waiver", key + "|" + waiverReason);
            return null;
        }
        var remaining = discount;
        var lines = parts
            .Select(x =>
            {
                var rate = Money(e, x.Amount * factor);
                var reduction = Math.Min(rate, remaining);
                remaining -= reduction;
                return new FinanceLineInput(
                    x.Description,
                    1,
                    rate,
                    reduction,
                    x.TaxPercent,
                    x.AccountCode,
                    x.BeneficiaryEntityId
                );
            })
            .ToArray();
        var name = await Db
            .Persons.Where(x => x.Id == m.PersonId)
            .Select(x => x.DisplayName)
            .SingleAsync();
        var invoice = await CreateInvoice(
            e,
            m.PersonId,
            name,
            plan.Purpose,
            Today,
            Today.AddDays(plan.GraceDays),
            key,
            lines,
            actor,
            m.Id,
            plan.Id,
            start,
            end,
            notes: waiverReason
        );
        await Db.SaveChangesAsync();
        await Issue(invoice, e, actor);
        return invoice;
    }

    public async Task<GovernanceRequest> Submit(
        FinancialRecord record,
        string kind,
        string hash,
        Guid creator,
        Guid? subject = null
    )
    {
        var w = await Workspace();
        var r = await governance.Request(
            w,
            record.OrgId,
            kind,
            kind + " review",
            Pack(new FinanceReview(record.EntityId, record.Id, hash)),
            subject: subject
        );
        await governance.Submit(r, w);
        return r;
    }

    public async Task Apply(GovernanceRequest request, Assignment w)
    {
        var reference = Unpack<FinanceReview>(request.Payload);
        var e = await Entity(reference.EntityId, "finance.approve");
        await Lock(e.Id);
        if (request.Kind == "FinanceExpense")
        {
            var claim = await Db.FinanceExpenses.SingleAsync(x =>
                x.Id == reference.Id && x.EntityId == e.Id
            );
            Check(
                claim.RequestId == request.Id
                    && claim.Status == "Submitted"
                    && Hash(
                        Pack(
                            new
                            {
                                claim.Amount,
                                claim.Lines,
                                claim.Evidence,
                                claim.PersonId,
                                claim.AccountCode,
                                claim.EventId,
                                claim.ProjectId,
                                claim.BudgetId,
                            }
                        )
                    ) == reference.Hash,
                "Expense review snapshot changed.",
                409
            );
            Check(
                claim.CreatorId != w.UserId
                    && !await Db.Persons.AnyAsync(p =>
                        p.Id == claim.PersonId && p.UserId == w.UserId
                    ),
                "Cannot approve own expense.",
                403
            );
            foreach (var docId in Unpack<Guid[]>(claim.Evidence))
            {
                var d = await Db.GovernanceDocuments.SingleAsync(x =>
                    x.Id == docId && x.TenantId == e.TenantId
                );
                await new OperationsService(A, governance, notifications).Document(d);
            }
            if (claim.BudgetId != null)
            {
                var budget = await Db.FinanceBudgets.SingleAsync(x => x.Id == claim.BudgetId);
                var ids = new List<Guid> { budget.Id };
                while (budget.ReplacesId != null && ids.Count < 100)
                {
                    budget = await Db.FinanceBudgets.SingleAsync(x => x.Id == budget.ReplacesId);
                    ids.Add(budget.Id);
                }
                var approved = await Db
                    .FinanceExpenses.Where(x =>
                        x.EntityId == e.Id
                        && x.BudgetId != null
                        && ids.Contains(x.BudgetId.Value)
                        && x.Status == "Approved"
                    )
                    .SumAsync(x => x.Amount);
                var current = await Db.FinanceBudgets.SingleAsync(x => x.Id == claim.BudgetId);
                Check(
                    approved + claim.Amount <= current.Amount,
                    "Expense exceeds the approved budget; revise budget before approval.",
                    409
                );
            }
            await Post(
                e,
                Today,
                "expense:" + claim.Id,
                claim.Description,
                [
                    new(
                        claim.AccountCode,
                        claim.Amount,
                        0,
                        claim.PersonId,
                        claim.EventId,
                        claim.ProjectId
                    ),
                    new("2000", 0, claim.Amount, claim.PersonId, claim.EventId, claim.ProjectId),
                ],
                w.UserId
            );
            claim.Status = "Approved";
            claim.Version++;
        }
        else
        {
            var b = await Db.FinanceBudgets.SingleAsync(x =>
                x.Id == reference.Id && x.EntityId == e.Id
            );
            Check(
                b.RequestId == request.Id
                    && b.CreatorId != w.UserId
                    && b.Status == "Submitted"
                    && Hash(
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
                    ) == reference.Hash,
                "Budget review snapshot changed.",
                409
            );
            b.Status = "Approved";
            b.Version++;
            if (b.ReplacesId != null)
            {
                var old = await Db.FinanceBudgets.SingleAsync(x =>
                    x.Id == b.ReplacesId && x.EntityId == e.Id
                );
                old.Status = "Superseded";
                old.Version++;
            }
        }
        Audit(e, w.UserId, "review.approve", request.Id.ToString());
    }
}
