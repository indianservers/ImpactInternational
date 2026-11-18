using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
namespace Clubs;

public class CommunityPolicy : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public bool RequirePublicApproval { get; set; } = true;
    public long Version { get; set; } = 1;
}
public class ActivityParticipation : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PostId { get; set; }
    public Guid PersonId { get; set; }
    public string Consent { get; set; } = "Pending";
    public bool PublicName { get; set; }
    public bool Verified { get; set; }
    public Guid? VerifiedBy { get; set; }
    public string Evidence { get; set; } = "";
    public string Role { get; set; } = "Participant";
    public decimal Hours { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class ActivityCertificate : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ParticipationId { get; set; }
    public Guid PersonId { get; set; }
    public Guid IssuedBy { get; set; }
    public string Number { get; set; } = "";
    public string NameSnapshot { get; set; } = "";
    public string ActivitySnapshot { get; set; } = "";
    public DateOnly ActivityOn { get; set; }
    public string OrganizationSnapshot { get; set; } = "";
    public string Status { get; set; } = "Issued";
    public string RevocationReason { get; set; } = "";
    [JsonIgnore] public string TokenHash { get; set; } = "";
    [JsonIgnore] public string ProtectedToken { get; set; } = "";
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class CommunityFollow : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Kind { get; set; } = "Organization";
    public string Target { get; set; } = "";
}
public class CommunityDigestPreference : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid AssignmentId { get; set; }
    public bool Enabled { get; set; }
    public DateTimeOffset? LastQueuedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset NextDigestAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
}
public class BusinessOpportunity : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid PersonId { get; set; }
    public Guid OrgId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "";
    public string Subcategory { get; set; } = "";
    public string[] Tags { get; set; } = [];
    public string City { get; set; } = "";
    public DateOnly ClosesOn { get; set; }
    public string Status { get; set; } = "Open";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class OpportunityResponse : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }
    public Guid UserId { get; set; }
    public Guid PersonId { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public partial class Db
{
    public DbSet<CommunityPolicy> CommunityPolicies => Set<CommunityPolicy>();
    public DbSet<ActivityParticipation> ActivityParticipations => Set<ActivityParticipation>();
    public DbSet<ActivityCertificate> ActivityCertificates => Set<ActivityCertificate>();
    public DbSet<CommunityFollow> CommunityFollows => Set<CommunityFollow>();
    public DbSet<CommunityDigestPreference> CommunityDigestPreferences => Set<CommunityDigestPreference>();
    public DbSet<BusinessOpportunity> BusinessOpportunities => Set<BusinessOpportunity>();
    public DbSet<OpportunityResponse> OpportunityResponses => Set<OpportunityResponse>();
    static void ConfigureCommunityGrowth(ModelBuilder b)
    {
        b.Entity<CommunityPolicy>().HasIndex(x => x.TenantId).IsUnique();
        b.Entity<CommunityPolicy>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<ActivityParticipation>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<ActivityParticipation>().HasIndex(x => new { x.PostId, x.PersonId }).IsUnique();
        b.Entity<ActivityParticipation>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<ActivityParticipation>().HasOne<CommunityPost>().WithMany().HasForeignKey(x => new { x.TenantId, x.PostId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ActivityParticipation>().HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ActivityCertificate>().HasIndex(x => x.ParticipationId).IsUnique();
        b.Entity<ActivityCertificate>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<ActivityCertificate>().HasOne<ActivityParticipation>().WithMany().HasForeignKey(x => new { x.TenantId, x.ParticipationId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityFollow>().HasIndex(x => new { x.TenantId, x.UserId, x.Kind, x.Target }).IsUnique();
        b.Entity<CommunityDigestPreference>().HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        b.Entity<CommunityDigestPreference>().HasIndex(x => new { x.Enabled, x.NextDigestAt });
        b.Entity<BusinessOpportunity>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<BusinessOpportunity>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<BusinessOpportunity>().HasIndex(x => new { x.TenantId, x.Status, x.ClosesOn });
        b.Entity<OpportunityResponse>().HasIndex(x => new { x.OpportunityId, x.UserId }).IsUnique();
        b.Entity<OpportunityResponse>().HasOne<BusinessOpportunity>().WithMany().HasForeignKey(x => new { x.TenantId, x.OpportunityId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
