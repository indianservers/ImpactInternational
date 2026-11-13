using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Theme { get; set; } = "light";
    public string Locale { get; set; } = "en";
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool Verified { get; set; }
    public bool PlatformAdmin { get; set; }
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}

public class Session
{
    public string Id { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid? WorkspaceId { get; set; }
}

public class Tenant
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Currency { get; set; } = "INR";
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string Language { get; set; } = "en";
    public string Color { get; set; } = "#302b78";
}

public class Level
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public Guid? ParentLevelId { get; set; }
    public bool IsClub { get; set; }
}

public class Org
{
    public string Description { get; set; } = "";
    public string ClubType { get; set; } = "Community";
    public string Registration { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string MeetingDay { get; set; } = "";
    public string MeetingSchedule { get; set; } = "";
    public string MeetingVenue { get; set; } = "";
    public int FiscalStartMonth { get; set; } = 1;
    public string PreferredLanguage { get; set; } = "en";
    public string BrandingColor { get; set; } = "#302b78";
    public string OperatingPolicies { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LevelId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string Code { get; set; } = "";
    public string Country { get; set; } = "";
    public string State { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Website { get; set; } = "";
    public string Logo { get; set; } = "";
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string Currency { get; set; } = "INR";
    public DateOnly? CharterDate { get; set; }
    public string Status { get; set; } = "Active";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";

    [JsonIgnore]
    public List<RolePermission> Grants { get; set; } = [];

    [NotMapped]
    public string[] Permissions
    {
        get => Grants.Select(x => x.PermissionId).ToArray();
        set =>
            Grants = value.Distinct().Select(x => new RolePermission { PermissionId = x }).ToList();
    }
}

public class Permission
{
    public string Id { get; set; } = "";
}

public class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoleId { get; set; }
    public string PermissionId { get; set; } = "";
}

public class Assignment
{
    public Guid? AppointmentId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? MembershipId { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public Guid OrgId { get; set; }
}

public class Audit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid? OrgId { get; set; }
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Changes { get; set; } = "";
    public string Outcome { get; set; } = "Success";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class Mail
{
    public Guid? EventId { get; set; }
    public DateTimeOffset? CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Channel { get; set; } = "Email";
    public Guid? CommunicationId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? SenderId { get; set; }
    public Guid? SenderAssignmentId { get; set; }
    public string Purpose { get; set; } = "Transactional";
    public string DeliveryStatus { get; set; } = "Pending";
    public string ProviderId { get; set; } = "";
    public long TemplateVersion { get; set; }
    public string TemplateName { get; set; } = "";
    public string TemplateParameters { get; set; } = "[]";
    public string Language { get; set; } = "en";
    public string LastError { get; set; } = "";
    public decimal? Cost { get; set; }
    public string Currency { get; set; } = "";
    public bool DeadLetter { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid? OrgId { get; set; }
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public Guid? UserId { get; set; }
    public string Kind { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string Link { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Suppressed { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AuthAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Outcome { get; set; } = "Success";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class RegistrationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class SystemSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class TenantSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class OrganizationSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public partial class Db(DbContextOptions<Db> options) : DbContext(options)
{
    public DbSet<AuthAudit> AuthenticationAudits => Set<AuthAudit>();
    public DbSet<RegistrationRequest> RegistrationRequests => Set<RegistrationRequest>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Level> Levels => Set<Level>();
    public DbSet<Org> Orgs => Set<Org>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<Mail> Mails => Set<Mail>();
    public DbSet<LoginChallenge> LoginChallenges => Set<LoginChallenge>();
    public DbSet<DuesCollection> DuesCollections => Set<DuesCollection>();
    public DbSet<DuesFollowup> DuesFollowups => Set<DuesFollowup>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<OrganizationSetting> OrganizationSettings => Set<OrganizationSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        ConfigureMembership(b);
        b.Entity<User>()
            .SplitToTable(
                "UserProfiles",
                t =>
                {
                    t.Property(x => x.Name);
                    t.Property(x => x.Phone);
                    t.Property(x => x.Locale);
                    t.Property(x => x.Theme);
                    t.Property(x => x.TimeZone);
                }
            );
        b.Entity<Tenant>()
            .SplitToTable(
                "Associations",
                t =>
                {
                    t.Property(x => x.Name);
                    t.Property(x => x.Currency);
                    t.Property(x => x.TimeZone);
                    t.Property(x => x.Language);
                    t.Property(x => x.Color);
                }
            );
        b.Entity<SystemSetting>().HasKey(x => x.Key);
        b.Entity<TenantSetting>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        b.Entity<TenantSetting>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<OrganizationSetting>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.Key,
            })
            .IsUnique();
        b.Entity<OrganizationSetting>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OrgId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<AuthAudit>().HasIndex(x => new { x.ActorId, x.At });
        b.Entity<Permission>().HasData(Access.All.Select(x => new Permission { Id = x }));
        b.Entity<Role>()
            .HasMany(x => x.Grants)
            .WithOne()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Role>().Navigation(x => x.Grants).AutoInclude();
        b.Entity<RolePermission>()
            .HasOne<Permission>()
            .WithMany()
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<RolePermission>().HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Session>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Session>().HasIndex(x => new { x.UserId, x.ExpiresAt });
        b.Entity<Org>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Level>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Role>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<Org>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Org>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ParentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Org>()
            .HasOne<Level>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LevelId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Level>()
            .HasOne<Level>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ParentLevelId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Level>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Role>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Assignment>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OrgId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Assignment>()
            .HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RoleId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Assignment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Assignment>()
            .HasIndex(x => new
            {
                x.UserId,
                x.RoleId,
                x.OrgId,
            })
            .IsUnique();
        b.Entity<Org>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.Entity<Org>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.ParentId,
                x.Name,
            });
        b.Entity<Org>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.LevelId,
                x.Status,
            });
        b.Entity<Org>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Level>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.Entity<Role>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.Entity<Audit>().HasIndex(x => new { x.TenantId, x.At });
        b.Entity<Mail>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<LoginChallenge>().HasIndex(x => new { x.IdentifierHash, x.CreatedAt });
        b.Entity<DuesCollection>().HasIndex(x => new { x.TenantId, x.RecorderId, x.RequestKey }).IsUnique();
        b.Entity<DuesCollection>().HasIndex(x => new { x.TenantId, x.ClubId, x.Reference }).IsUnique().HasFilter("\"Status\" IN ('Submitted','Approved')");
        b.Entity<DuesFollowup>().HasIndex(x => new { x.TenantId, x.ActorId, x.RequestKey }).IsUnique();
        b.Entity<DuesFollowup>().HasIndex(x => new { x.InvoiceId, x.CreatedAt });
        b.Entity<DuesFollowup>().HasOne<FinanceInvoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DuesFollowup>().HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DuesCollection>().HasIndex(x => new { x.InvoiceId, x.Status });
        b.Entity<DuesCollection>().HasOne<FinanceInvoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DuesCollection>().HasOne<Org>().WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DuesCollection>().HasOne<User>().WithMany().HasForeignKey(x => x.RecorderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Mail>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Mail>()
            .HasIndex(x => x.NextAttemptAt)
            .HasFilter("\"SentAt\" IS NULL AND NOT \"Used\"");
    }
}
