using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
namespace Clubs;

public class IciRegionCoverage : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid RegionId { get; set; }
}
public class IciPrivateProfile
{
    public Guid UserId { get; set; }
    public string TrainingCareer { get; set; } = "";
    public string LeadershipCareer { get; set; } = "";
    [JsonIgnore] public string ProtectedBankDetails { get; set; } = "";
    public long Version { get; set; } = 1;
}
public class IciRequest : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid CreatorId { get; set; }
    public string CreatorRole { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? EventId { get; set; }
    public Guid? TargetOrgId { get; set; }
    public Guid? RelatedRequestId { get; set; }
    public string Portfolio { get; set; } = "";
    public decimal Amount { get; set; }
    public int MemberCount { get; set; }
    public int VolunteerCount { get; set; }
    public decimal Hours { get; set; }
    public string Status { get; set; } = "Draft";
    public int Stage { get; set; }
    public int Revision { get; set; } = 1;
    public string ReviewRoute { get; set; } = "[]";
    [JsonIgnore] public string Payload { get; set; } = "{}";
    public string PaymentReference { get; set; } = "";
    public Guid? PaymentVerifierId { get; set; }
    public Guid? ResultId { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class IciDecision : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RequestId { get; set; }
    public Guid ActorId { get; set; }
    public string ActorRole { get; set; } = "";
    public int Stage { get; set; }
    public int Revision { get; set; }
    public string Decision { get; set; } = "";
    public string Comment { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
public class IciSubscriptionMember : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid RequestId { get; set; }
    public Guid PersonId { get; set; }
    public Guid MembershipId { get; set; }
    public string PeriodKey { get; set; } = "";
}
public record IciRequestInput(string Kind, Guid OrgId, string Title, string Description = "", Guid? EventId = null,
    Guid? TargetOrgId = null, Guid? RelatedRequestId = null, string Portfolio = "", decimal Amount = 0,
    int MemberCount = 0, int VolunteerCount = 0, decimal Hours = 0, string Reference = "", Guid[]? MemberIds = null,
    string Code = "", string Country = "India", string City = "", string Email = "", string Role = "",
    Guid? OldAssignmentId = null, string Slot = "", Guid[]? RegionIds = null, long Version = 1);
public record IciActionInput(string Action, long Version, string Comment = "");
public record IciAssignmentInput(string Email, Guid RoleId, Guid OrgId, string Portfolio = "", string Slot = "", Guid[]? RegionIds = null);
public record IciScopeInput(string Portfolio, string Slot, Guid[] RegionIds);
public record IciProfileInput(string TrainingCareer = "", string LeadershipCareer = "", string BankName = "",
    string AccountHolder = "", string AccountNumber = "", string RoutingCode = "", bool ReplaceBankDetails = false, long Version = 1);
public record IciBankDetails(string BankName, string AccountHolder, string AccountNumber, string RoutingCode);

public partial class Db
{
    public DbSet<IciRequest> IciRequests => Set<IciRequest>();
    public DbSet<IciDecision> IciDecisions => Set<IciDecision>();
    public DbSet<IciRegionCoverage> IciRegionCoverages => Set<IciRegionCoverage>();
    public DbSet<IciPrivateProfile> IciPrivateProfiles => Set<IciPrivateProfile>();
    public DbSet<IciSubscriptionMember> IciSubscriptionMembers => Set<IciSubscriptionMember>();
    void ConfigureIci(ModelBuilder b)
    {
        b.Entity<IciPrivateProfile>().HasKey(p => p.UserId);
        b.Entity<IciPrivateProfile>().Property(p => p.Version).IsConcurrencyToken();
        b.Entity<IciPrivateProfile>().HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<IciRegionCoverage>().HasIndex(c => new { c.AssignmentId, c.RegionId }).IsUnique();
        b.Entity<IciRegionCoverage>().HasOne<Assignment>().WithMany().HasForeignKey(c => c.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<IciRegionCoverage>().HasOne<Org>().WithMany().HasForeignKey(c => new { c.TenantId, c.RegionId }).HasPrincipalKey(o => new { o.TenantId, o.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<IciRequest>().Property(r => r.Version).IsConcurrencyToken();
        b.Entity<IciRequest>().Property(r => r.Amount).HasPrecision(18, 2);
        b.Entity<IciRequest>().HasIndex(r => new { r.TenantId, r.OrgId, r.Kind, r.Status });
        b.Entity<IciRequest>().HasOne<Org>().WithMany().HasForeignKey(r => new { r.TenantId, r.OrgId }).HasPrincipalKey(o => new { o.TenantId, o.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<IciDecision>().HasIndex(d => new { d.RequestId, d.Revision, d.Stage, d.ActorId }).IsUnique();
        b.Entity<IciDecision>().HasOne<IciRequest>().WithMany().HasForeignKey(d => new { d.TenantId, d.RequestId }).HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<IciSubscriptionMember>().HasIndex(m => new { m.TenantId, m.OrgId, m.PersonId, m.PeriodKey }).IsUnique();
        b.Entity<IciSubscriptionMember>().HasOne<IciRequest>().WithMany().HasForeignKey(m => new { m.TenantId, m.RequestId }).HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
