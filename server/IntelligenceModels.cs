using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Clubs;

public class TenantBranding : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Logo { get; set; } = "";
    public string Favicon { get; set; } = "";
    public string Accent { get; set; } = "#2563eb";
    public string ClubTerm { get; set; } = "Club";
    public string DistrictTerm { get; set; } = "District";
    public string Welcome { get; set; } = "Connected leadership. Meaningful impact.";
    public string Languages { get; set; } = "en";
    public string DisabledFeatures { get; set; } = "[]";
    public bool RemoteAiEnabled { get; set; }
    public string AiPolicy { get; set; } = "";
    public long Version { get; set; } = 1;
}

public class TenantDomain : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Host { get; set; } = "";
    public string Challenge { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public Guid CreatorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public long Version { get; set; } = 1;
}

public class KnowledgePassage : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public int DocumentVersion { get; set; }
    public int Page { get; set; }
    public string SourceHash { get; set; } = "";

    [JsonIgnore]
    public string Text { get; set; } = "";

    [JsonIgnore]
    public NpgsqlTsVector SearchVector { get; set; } = null!;
    public Guid IndexedBy { get; set; }
    public DateTimeOffset IndexedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class IntelligenceAudit : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = "";
    public string QueryHash { get; set; } = "";
    public string Tool { get; set; } = "";
    public int Results { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public partial class Db
{
    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<KnowledgePassage> KnowledgePassages => Set<KnowledgePassage>();
    public DbSet<IntelligenceAudit> IntelligenceAudits => Set<IntelligenceAudit>();

    private void ConfigureIntelligence(ModelBuilder b)
    {
        b.Entity<TenantBranding>().HasIndex(x => x.TenantId).IsUnique();
        b.Entity<TenantDomain>().HasIndex(x => x.Host).IsUnique();
        foreach (
            var t in new[]
            {
                typeof(TenantBranding),
                typeof(TenantDomain),
                typeof(KnowledgePassage),
                typeof(IntelligenceAudit),
            }
        )
        {
            var e = b.Entity(t);
            e.HasKey("Id");
            e.HasOne(typeof(Tenant))
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict);
        }
        b.Entity<TenantBranding>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<TenantDomain>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<KnowledgePassage>()
            .HasOne<GovernanceDocument>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.DocumentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<KnowledgePassage>()
            .HasIndex(x => new
            {
                x.DocumentId,
                x.DocumentVersion,
                x.Page,
            })
            .IsUnique();
        b.Entity<KnowledgePassage>()
            .Property(x => x.SearchVector)
            .HasComputedColumnSql("to_tsvector('simple', \"Text\")", stored: true);
        b.Entity<KnowledgePassage>().HasIndex(x => x.SearchVector).HasMethod("GIN");
        b.Entity<KnowledgePassage>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.DocumentId,
                x.DocumentVersion,
            });
        b.Entity<IntelligenceAudit>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.At,
            });
    }
}
