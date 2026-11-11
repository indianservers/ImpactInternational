using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Clubs;

// Template metadata stays in CertificateTemplates; official identity stays in DigitalCards.
public class IdCardTemplateVersion : OperationalRecord
{
    public Guid TemplateId { get; set; }
    public long Number { get; set; }
    public string Layout { get; set; } = "{}";
    public Guid PublisherId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class IdCardJob : OperationalRecord
{
    public Guid CreatorId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public Guid? RequestId { get; set; }
    public string Status { get; set; } = "Pending Approval";
    public string Format { get; set; } = "Zip";
    public string PrintLayout { get; set; } = "{}";
    public bool Reissue { get; set; }
    public bool ReuseIssued {get;set;}
    public bool CancelRequested { get; set; }
    public int Total { get; set; }
    public int Success { get; set; }
    public int Failed { get; set; }
    public string OutputKey { get; set; } = "";
    public string Error { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
}

public class IdCardJobItem : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid JobId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid? CardId { get; set; }
    public int Ordinal { get; set; }
    public string Status { get; set; } = "Pending";
    public string Error { get; set; } = "";
}

public class IdCardPrintPreset : OperationalRecord
{
    public string Name { get; set; } = "";
    public string Layout { get; set; } = "{}";
}

public class IdCardPolicy : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? DefaultTemplateVersionId { get; set; }
    public bool AllowMemberDownload { get; set; } = true;
    public bool AllowClubDesign { get; set; } = true;
    public int ValidityMonths { get; set; } = 12;
    public long Version { get; set; } = 1;
}

public partial class Db
{
    public DbSet<IdCardTemplateVersion> IdCardTemplateVersions => Set<IdCardTemplateVersion>();
    public DbSet<IdCardJob> IdCardJobs => Set<IdCardJob>();
    public DbSet<IdCardJobItem> IdCardJobItems => Set<IdCardJobItem>();
    public DbSet<IdCardPrintPreset> IdCardPrintPresets => Set<IdCardPrintPreset>();
    public DbSet<IdCardPolicy> IdCardPolicies => Set<IdCardPolicy>();

    private void ConfigureIdCards(ModelBuilder b)
    {
        foreach (
            var t in new[]
            {
                typeof(IdCardTemplateVersion),
                typeof(IdCardJob),
                typeof(IdCardPrintPreset),
            }
        )
        {
            var e = b.Entity(t);
            e.HasBaseType((Type?)null);
            e.HasKey("Id");
            e.HasAlternateKey("TenantId", "Id");
            e.Property("Version").IsConcurrencyToken();
            e.HasOne(typeof(Org))
                .WithMany()
                .HasForeignKey("TenantId", "OrgId")
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex("TenantId", "OrgId", "UpdatedAt");
        }
        b.Entity<IdCardTemplateVersion>()
            .HasOne<CertificateTemplate>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.TemplateId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<IdCardTemplateVersion>().HasIndex(x => new { x.TemplateId, x.Number }).IsUnique();
        b.Entity<IdCardJob>()
            .HasOne<IdCardTemplateVersion>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.TemplateVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<IdCardJob>().HasIndex(x => new { x.Status, x.CreatedAt });
        b.Entity<IdCardJobItem>().HasKey(x => x.Id);
        b.Entity<IdCardJobItem>()
            .HasOne<IdCardJob>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.JobId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<IdCardJobItem>()
            .HasOne<Membership>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<IdCardJobItem>().HasIndex(x => new { x.JobId, x.MembershipId }).IsUnique();
        b.Entity<IdCardJobItem>()
            .HasIndex(x => new
            {
                x.JobId,
                x.Status,
                x.Ordinal,
            });
        b.Entity<IdCardPolicy>().HasKey(x => x.Id);
        b.Entity<IdCardPolicy>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<IdCardPolicy>().HasIndex(x => x.TenantId).IsUnique();
        b.Entity<IdCardPolicy>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<IdCardPolicy>()
            .HasOne<IdCardTemplateVersion>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.DefaultTemplateVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DigitalCard>().HasAlternateKey(c => new { c.TenantId, c.Id });
        b.Entity<IdCardJobItem>()
            .HasOne<DigitalCard>()
            .WithMany()
            .HasForeignKey(i => new { i.TenantId, i.CardId })
            .HasPrincipalKey(c => new { c.TenantId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DigitalCard>()
            .HasOne<IdCardTemplateVersion>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.TemplateVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DigitalCard>()
            .HasIndex(x => x.MembershipId)
            .IsUnique()
            .HasFilter("\"RevokedAt\" IS NULL AND \"State\" IN ('Legacy','Issued')");
        b.Entity<DigitalCard>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.State,
                x.ExpiryAt,
            });
        b.Entity<DigitalCard>().HasIndex(x => x.Serial).IsUnique().HasFilter("\"Serial\"<>''");
    }
}
