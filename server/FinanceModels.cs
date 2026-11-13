using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Clubs;

public abstract class FinancialRecord : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid EntityId { get; set; }
    public long Version { get; set; } = 1;
}

public class FinanceEntity : FinancialRecord
{
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public int Decimals { get; set; } = 2;
    public int FiscalMonth { get; set; } = 1;
    public string TaxRegistration { get; set; } = "";
    public string Address { get; set; } = "";
    public string InvoicePrefix { get; set; } = "INV";
    public string ReceiptPrefix { get; set; } = "REC";
    public long InvoiceSequence { get; set; } = 0;
    public long ReceiptSequence { get; set; } = 0;
    public string AccountingMethod { get; set; } = "Accrual";
    public bool Active { get; set; } = true;
    public string GatewayKey { get; set; } = "";
    public string CollectionAuthority { get; set; } = "Own organization only";
}

public class FinanceGrant : FinancialRecord
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Reader";
}

public class FinanceOrganization : FinancialRecord
{
    public Guid ServedOrgId { get; set; }
    public string Authority { get; set; } = "";
}

public class FinanceAccount : FinancialRecord
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Asset";
    public bool Active { get; set; } = true;
}

public class FinancePeriod : FinancialRecord
{
    public string Name { get; set; } = "";
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool Closed { get; set; }
}

public class FinanceJournal : FinancialRecord
{
    public DateOnly Date { get; set; }
    public Guid PeriodId { get; set; }
    public string Source { get; set; } = "";
    public string Narration { get; set; } = "";
    public Guid CreatorId { get; set; }
    public Guid? ReversesId { get; set; }
    public DateTimeOffset PostedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class FinanceJournalLine : FinancialRecord
{
    public Guid JournalId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid? PersonId { get; set; }
    public Guid? EventId { get; set; }
    public Guid? ProjectId { get; set; }
}

public class DuesPlan : FinancialRecord
{
    public string ChargeCode { get; set; } = "";
    public Guid CreatorId { get; set; }
    public string Name { get; set; } = "";
    public Guid? CategoryId { get; set; }
    public Guid BillingOrgId { get; set; }
    public string Purpose { get; set; } = "ClubDues";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveTo { get; set; }
    public int Months { get; set; } = 12;
    public int GraceDays { get; set; } = 30;
    public bool Prorate { get; set; }
    public bool Once { get; set; }
    public bool AutoBill { get; set; }
    public DateOnly NextBillOn { get; set; }
    public string Components { get; set; } = "[]";
}

public class FinanceInvoice : FinancialRecord
{
    public string RequestHash { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal Outstanding => Total - Paid - Credited;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string PaymentStatus =>
        Paid + Credited >= Total ? "Paid"
        : Paid > 0 ? "Partially paid"
        : DueOn < FinanceService.Today ? "Overdue"
        : "Unpaid";

    public string Number { get; set; } = "";
    public Guid? PersonId { get; set; }
    public Guid? MembershipId { get; set; }
    public Guid? PlanId { get; set; }
    public Guid? EventId { get; set; }
    public Guid? RegistrationId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? PledgeId { get; set; }
    public string Customer { get; set; } = "";
    public string CustomerAddress { get; set; } = "";
    public string IssuerSnapshot { get; set; } = "";
    public string Purpose { get; set; } = "ClubDues";
    public DateOnly Date { get; set; }
    public DateOnly DueOn { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public string State { get; set; } = "Draft";
    public string Currency { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Credited { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string Notes { get; set; } = "";
    public Guid CreatorId { get; set; }
    public DateTimeOffset? RemindedAt { get; set; }
}

public class FinanceInvoiceLine : FinancialRecord
{
    public Guid InvoiceId { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public decimal Rate { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal Net { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string AccountCode { get; set; } = "4000";
    public Guid? BeneficiaryEntityId { get; set; }
}

public class FinanceCredit : FinancialRecord
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public string Key { get; set; } = "";
    public Guid CreatorId { get; set; }
    public DateOnly Date { get; set; }
}

public class FinancePayment : FinancialRecord
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal Unallocated => Amount - Allocated - Refunded;

    public Guid? PersonId { get; set; }
    public string Payer { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Method { get; set; } = "BankTransfer";
    public string Status { get; set; } = "Pending";
    public string SettlementStatus { get; set; } = "Unsettled";
    public string Currency { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Allocated { get; set; }
    public decimal Refunded { get; set; }
    public decimal Fee { get; set; }
    public DateOnly Date { get; set; }
    public Guid RecorderId { get; set; }
    public Guid? VerifierId { get; set; }
    public string ReceiptNumber { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public Guid? BankAccountId { get; set; }
    public DateOnly? SettledOn { get; set; }
}

public class FinanceAllocation : FinancialRecord
{
    public string Key { get; set; } = "";
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
}

public class FinanceRefund : FinancialRecord
{
    public Guid PaymentId { get; set; }
    public Guid? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Kind { get; set; } = "Refund";
    public string Reason { get; set; } = "";
    public string Reference { get; set; } = "";
    public Guid CreatorId { get; set; }
    public DateOnly Date { get; set; }
}

public class FinanceIntent : FinancialRecord
{
    public Guid InvoiceId { get; set; }
    public string Provider { get; set; } = "Stripe";
    public string ProviderId { get; set; } = "";
    public string CheckoutUrl { get; set; } = "";
    public string Key { get; set; } = "";
    public string State { get; set; } = "Created";
    public decimal Amount { get; set; }
    public Guid? PaymentId { get; set; }
}

public class FinanceWebhook : FinancialRecord
{
    public string ProviderEventId { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class FinanceBank : FinancialRecord
{
    public string Name { get; set; } = "";
    public string MaskedNumber { get; set; } = "";

    [JsonIgnore]
    public string ProtectedDetails { get; set; } = "";
    public string AccountCode { get; set; } = "1010";
}

public class FinanceBankPaymentMatch : FinancialRecord
{
    public Guid BankTransactionId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid? ClubId { get; set; }
    public decimal BankAmount { get; set; }
}

public class FinanceBankTransaction : FinancialRecord
{
    public Guid[] MatchedPaymentIds { get; set; } = [];
    public Guid? RemittanceClubId { get; set; }
    public Guid BankAccountId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = "";
    public string ImportKey { get; set; } = "";
    public Guid? PaymentId { get; set; }
    public string Status { get; set; } = "Unmatched";
}

public class FinanceExpense : FinancialRecord
{
    public Guid PersonId { get; set; }
    public string Payee { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Administration";
    public string Lines { get; set; } = "[]";
    public string Evidence { get; set; } = "[]";
    public string EvidenceHash { get; set; } = "";
    public Guid? EventId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? BudgetId { get; set; }
    public string AccountCode { get; set; } = "5000";
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public string Status { get; set; } = "Draft";
    public Guid CreatorId { get; set; }
    public Guid? RequestId { get; set; }
    public DateOnly Date { get; set; }
}

public class FinanceReimbursement : FinancialRecord
{
    public Guid ExpenseId { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = "";
    public Guid CreatorId { get; set; }
    public DateOnly Date { get; set; }
}

public class FinanceBudget : FinancialRecord
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Annual";
    public Guid? EventId { get; set; }
    public Guid? ProjectId { get; set; }
    public int Revision { get; set; } = 1;
    public Guid? ReplacesId { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string Lines { get; set; } = "[]";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Draft";
    public Guid CreatorId { get; set; }
    public Guid? RequestId { get; set; }
}

public class FinancePledge : FinancialRecord
{
    public Guid? PersonId { get; set; }
    public string Contributor { get; set; } = "";
    public string Kind { get; set; } = "Donation";
    public string Description { get; set; } = "";
    public Guid? ProjectId { get; set; }
    public Guid? EventId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pledged";
    public Guid? InvoiceId { get; set; }
    public Guid CreatorId { get; set; }
    public DateOnly Date { get; set; }
}

public class FinanceEntitlement : FinancialRecord
{
    public Guid InvoiceId { get; set; }
    public Guid BeneficiaryEntityId { get; set; }
    public decimal Amount { get; set; }
    public decimal Settled { get; set; }
}

public class FinanceTransfer : FinancialRecord
{
    public Guid BeneficiaryEntityId { get; set; }
    public Guid EntitlementId { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = "";
    public string Status { get; set; } = "Recorded";
    public Guid CreatorId { get; set; }
    public DateOnly Date { get; set; }
}

public class FinanceExchangeRate : FinancialRecord
{
    public string Currency { get; set; } = "";
    public decimal Rate { get; set; }
    public DateOnly Date { get; set; }
    public string Source { get; set; } = "";
}

public class FinanceJob : FinancialRecord
{
    public string Kind { get; set; } = "Dues";
    public Guid PlanId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public Guid? AfterMembershipId { get; set; }
    public string Status { get; set; } = "Queued";
    public int Processed { get; set; }
    public int Attempts { get; set; }
    public string Error { get; set; } = "";
    public Guid CreatorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class FinanceAudit : FinancialRecord
{
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class FinanceEventPrice : FinancialRecord
{
    public Guid EventId { get; set; }
    public string Category { get; set; } = "Member";
    public decimal Amount { get; set; }
}

public partial class Db
{
    public DbSet<FinanceEventPrice> FinanceEventPrices => Set<FinanceEventPrice>();
    public DbSet<FinanceEntity> FinanceEntitys => Set<FinanceEntity>();
    public DbSet<FinanceGrant> FinanceGrants => Set<FinanceGrant>();
    public DbSet<FinanceOrganization> FinanceOrganizations => Set<FinanceOrganization>();
    public DbSet<FinanceAccount> FinanceAccounts => Set<FinanceAccount>();
    public DbSet<FinancePeriod> FinancePeriods => Set<FinancePeriod>();
    public DbSet<FinanceJournal> FinanceJournals => Set<FinanceJournal>();
    public DbSet<FinanceJournalLine> FinanceJournalLines => Set<FinanceJournalLine>();
    public DbSet<DuesPlan> DuesPlans => Set<DuesPlan>();
    public DbSet<FinanceInvoice> FinanceInvoices => Set<FinanceInvoice>();
    public DbSet<FinanceInvoiceLine> FinanceInvoiceLines => Set<FinanceInvoiceLine>();
    public DbSet<FinanceCredit> FinanceCredits => Set<FinanceCredit>();
    public DbSet<FinancePayment> FinancePayments => Set<FinancePayment>();
    public DbSet<FinanceAllocation> FinanceAllocations => Set<FinanceAllocation>();
    public DbSet<FinanceRefund> FinanceRefunds => Set<FinanceRefund>();
    public DbSet<FinanceIntent> FinanceIntents => Set<FinanceIntent>();
    public DbSet<FinanceWebhook> FinanceWebhooks => Set<FinanceWebhook>();
    public DbSet<FinanceBank> FinanceBanks => Set<FinanceBank>();
    public DbSet<FinanceBankTransaction> FinanceBankTransactions => Set<FinanceBankTransaction>();
    public DbSet<FinanceBankPaymentMatch> FinanceBankPaymentMatches => Set<FinanceBankPaymentMatch>();
    public DbSet<FinanceExpense> FinanceExpenses => Set<FinanceExpense>();
    public DbSet<FinanceReimbursement> FinanceReimbursements => Set<FinanceReimbursement>();
    public DbSet<FinanceBudget> FinanceBudgets => Set<FinanceBudget>();
    public DbSet<FinancePledge> FinancePledges => Set<FinancePledge>();
    public DbSet<FinanceEntitlement> FinanceEntitlements => Set<FinanceEntitlement>();
    public DbSet<FinanceTransfer> FinanceTransfers => Set<FinanceTransfer>();
    public DbSet<FinanceExchangeRate> FinanceExchangeRates => Set<FinanceExchangeRate>();
    public DbSet<FinanceJob> FinanceJobs => Set<FinanceJob>();
    public DbSet<FinanceAudit> FinanceAudits => Set<FinanceAudit>();

    private void ConfigureFinance(ModelBuilder b)
    {
        b.Entity<FinanceBankPaymentMatch>(e => {
            e.HasBaseType((Type?)null); e.HasKey(x=>x.Id);
            e.Property(x=>x.Version).IsConcurrencyToken();
            e.Property(x=>x.BankAmount).HasPrecision(20,4);
            e.HasIndex(x=>x.PaymentId).IsUnique();
            e.HasIndex(x=>x.BankTransactionId);
            e.HasOne<FinanceBankTransaction>().WithMany().HasForeignKey(x=>new{x.TenantId,x.EntityId,x.BankTransactionId}).HasPrincipalKey(x=>new{x.TenantId,x.EntityId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinancePayment>().WithMany().HasForeignKey(x=>new{x.TenantId,x.EntityId,x.PaymentId}).HasPrincipalKey(x=>new{x.TenantId,x.EntityId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Org>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ClubId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t=>t.HasCheckConstraint("CK_FinanceBankPaymentMatch_Amount","\"BankAmount\" > 0"));
        });
        b.Entity<FinanceInvoiceLine>().HasIndex(x => x.InvoiceId);
        b.Entity<FinanceJournalLine>().HasIndex(x => x.JournalId);
        b.Entity<FinanceAllocation>().HasIndex(x => x.InvoiceId);
        b.Entity<FinanceAllocation>().HasIndex(x => x.PaymentId);
        b.Entity<FinanceRefund>().HasIndex(x => x.PaymentId);
        b.Entity<FinanceRefund>().HasIndex(x => x.InvoiceId);

        b.Entity<FinanceEventPrice>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("numeric(24,8)");
            e.HasIndex(x => new { x.EventId, x.Category }).IsUnique();
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ClubEvent>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EventId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceJob>()
            .HasIndex(x => new
            {
                x.PlanId,
                x.PeriodStart,
                x.PeriodEnd,
            })
            .IsUnique();
        b.Entity<FinanceRefund>().HasIndex(x => new { x.EntityId, x.Reference }).IsUnique();
        b.Entity<FinanceReimbursement>().HasIndex(x => new { x.EntityId, x.Reference }).IsUnique();
        b.Entity<FinanceTransfer>().HasIndex(x => new { x.EntityId, x.Reference }).IsUnique();

        b.Entity<FinanceEntity>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceGrant>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceOrganization>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceAccount>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinancePeriod>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceJournal>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceJournalLine>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<DuesPlan>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceInvoice>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceInvoiceLine>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceCredit>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinancePayment>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceAllocation>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceRefund>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceIntent>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceWebhook>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceBank>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceBankTransaction>(e =>
        {
            e.Property(x => x.MatchedPaymentIds).HasColumnType("uuid[]").HasDefaultValueSql("'{}'::uuid[]");
            e.HasIndex(x => x.MatchedPaymentIds).HasMethod("gin");
            e.HasOne<Org>().WithMany().HasForeignKey(x => new { x.TenantId, x.RemittanceClubId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceExpense>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceReimbursement>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceBudget>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinancePledge>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceEntitlement>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceTransfer>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceExchangeRate>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceJob>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceAudit>(e =>
        {
            e.HasBaseType((Type?)null);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            });
            e.HasIndex(x => new { x.EntityId, x.Id });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Org>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrgId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.EntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinanceAllocation>().HasIndex(x => new { x.EntityId, x.Key }).IsUnique();
        b.Entity<FinanceInvoice>()
            .HasOne<DuesPlan>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.PlanId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceInvoice>()
            .HasOne<FinancePledge>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.PledgeId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinancePayment>()
            .HasOne<FinanceBank>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.BankAccountId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceInvoice>()
            .HasOne<Membership>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceInvoice>()
            .HasOne<ClubEvent>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EventId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceInvoice>()
            .HasOne<EventRegistration>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RegistrationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceInvoice>()
            .HasOne<ServiceProject>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProjectId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DuesPlan>()
            .HasOne<MembershipCategory>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CategoryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DuesPlan>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.BillingOrgId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceExpense>()
            .HasOne<ClubEvent>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EventId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceExpense>()
            .HasOne<ServiceProject>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProjectId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceExpense>()
            .HasOne<GovernanceRequest>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RequestId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceBudget>()
            .HasOne<ClubEvent>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EventId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceBudget>()
            .HasOne<ServiceProject>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProjectId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceBudget>()
            .HasOne<GovernanceRequest>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RequestId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceOrganization>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ServedOrgId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceJournalLine>()
            .HasOne<ClubEvent>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EventId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceJournalLine>()
            .HasOne<ServiceProject>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProjectId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinancePledge>()
            .HasOne<ClubEvent>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EventId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinancePledge>()
            .HasOne<ServiceProject>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProjectId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceInvoice>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinancePayment>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceJournalLine>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceExpense>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinancePledge>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceJournal>().HasIndex(x => x.ReversesId).IsUnique();
        b.Entity<FinanceEntity>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<FinanceJournalLine>()
            .HasOne<FinanceJournal>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.JournalId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceJournalLine>()
            .HasOne<FinanceAccount>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.AccountId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceInvoiceLine>()
            .HasOne<FinanceInvoice>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.InvoiceId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceCredit>()
            .HasOne<FinanceInvoice>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.InvoiceId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceAllocation>()
            .HasOne<FinancePayment>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.PaymentId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceAllocation>()
            .HasOne<FinanceInvoice>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.InvoiceId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceRefund>()
            .HasOne<FinancePayment>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.PaymentId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceRefund>()
            .HasOne<FinanceInvoice>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.InvoiceId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceIntent>()
            .HasOne<FinanceInvoice>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.InvoiceId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceIntent>()
            .HasOne<FinancePayment>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.PaymentId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceBankTransaction>()
            .HasOne<FinanceBank>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.BankAccountId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceBankTransaction>()
            .HasOne<FinancePayment>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.PaymentId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceReimbursement>()
            .HasOne<FinanceExpense>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.ExpenseId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceEntitlement>()
            .HasOne<FinanceInvoice>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.InvoiceId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceTransfer>()
            .HasOne<FinanceEntitlement>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.EntitlementId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceJournal>()
            .HasOne<FinancePeriod>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.PeriodId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceJournal>()
            .HasOne<FinanceJournal>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.ReversesId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceJob>()
            .HasOne<DuesPlan>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.PlanId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceExpense>()
            .HasOne<FinanceBudget>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.BudgetId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceBudget>()
            .HasOne<FinanceBudget>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.ReplacesId,
            })
            .HasPrincipalKey(x => new
            {
                x.TenantId,
                x.EntityId,
                x.Id,
            })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceGrant>().HasIndex(x => new { x.EntityId, x.UserId }).IsUnique();
        b.Entity<FinanceOrganization>().HasIndex(x => new { x.EntityId, x.ServedOrgId }).IsUnique();
        b.Entity<FinanceAccount>().HasIndex(x => new { x.EntityId, x.Code }).IsUnique();
        b.Entity<FinanceInvoice>()
            .HasIndex(x => new { x.EntityId, x.Number })
            .IsUnique()
            .HasFilter("\"Number\" <> ''");
        b.Entity<FinanceInvoice>().HasIndex(x => new { x.EntityId, x.IdempotencyKey }).IsUnique();
        b.Entity<FinancePayment>().HasIndex(x => new { x.EntityId, x.Reference }).IsUnique();
        b.Entity<FinancePayment>().HasIndex(x => new { x.EntityId, x.IdempotencyKey }).IsUnique();
        b.Entity<FinancePayment>()
            .HasIndex(x => new { x.EntityId, x.ReceiptNumber })
            .IsUnique()
            .HasFilter("\"ReceiptNumber\" <> ''");
        b.Entity<FinanceIntent>().HasIndex(x => new { x.EntityId, x.Key }).IsUnique();
        b.Entity<FinanceIntent>()
            .HasIndex(x => new { x.EntityId, x.ProviderId })
            .IsUnique()
            .HasFilter("\"ProviderId\" <> ''");
        b.Entity<FinanceWebhook>().HasIndex(x => new { x.EntityId, x.ProviderEventId }).IsUnique();
        b.Entity<FinanceCredit>().HasIndex(x => new { x.EntityId, x.Key }).IsUnique();
        b.Entity<FinanceBankTransaction>()
            .HasIndex(x => new { x.EntityId, x.ImportKey })
            .IsUnique();
        b.Entity<FinanceEntitlement>()
            .HasIndex(x => new
            {
                x.EntityId,
                x.InvoiceId,
                x.BeneficiaryEntityId,
            })
            .IsUnique();
        b.Entity<FinanceJournal>().HasIndex(x => new { x.EntityId, x.Source }).IsUnique();
        b.Entity<FinanceInvoice>()
            .HasIndex(x => new
            {
                x.EntityId,
                x.PersonId,
                x.Date,
            });
        b.Entity<FinanceJournal>().HasIndex(x => new { x.EntityId, x.Date });
        b.Entity<FinancePayment>().HasIndex(x => new { x.EntityId, x.Date });
        b.Entity<FinanceExpense>()
            .HasIndex(x => new
            {
                x.EntityId,
                x.Status,
                x.Date,
            });
        b.Entity<DuesPlan>()
            .HasIndex(x => new
            {
                x.EntityId,
                x.AutoBill,
                x.NextBillOn,
            });
        b.Entity<FinanceJob>()
            .HasIndex(x => new
            {
                x.EntityId,
                x.Status,
                x.CreatedAt,
            });
        b.Entity<FinanceJournalLine>()
            .HasIndex(x => new
            {
                x.EntityId,
                x.AccountId,
                x.JournalId,
            });
        foreach (
            var entity in b
                .Model.GetEntityTypes()
                .Where(e => typeof(FinancialRecord).IsAssignableFrom(e.ClrType))
        )
        foreach (var p in entity.GetProperties().Where(p => p.ClrType == typeof(decimal)))
            p.SetColumnType("numeric(24,8)");
    }
}
