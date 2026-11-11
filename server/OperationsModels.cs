using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Clubs;

public interface IOperationPerson
{
    Guid PersonId { get; set; }
    string PersonName { get; set; }
}

public abstract class OperationalRecord : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ClubMeeting : OperationalRecord
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string LocalStartsAt =>
        TimeZoneInfo
            .ConvertTime(StartsAt, TimeZoneInfo.FindSystemTimeZoneById(TimeZone))
            .ToString("yyyy-MM-dd'T'HH:mm");

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string LocalEndsAt =>
        TimeZoneInfo
            .ConvertTime(EndsAt, TimeZoneInfo.FindSystemTimeZoneById(TimeZone))
            .ToString("yyyy-MM-dd'T'HH:mm");

    public string Title { get; set; } = "";
    public string Type { get; set; } = "Regular club meeting";
    public Guid OrganizerId { get; set; }
    public Guid? ChairId { get; set; }
    public Guid? SecretaryId { get; set; }
    public Guid? CommitteeId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string Location { get; set; } = "";
    public string OnlineUrl { get; set; } = "";
    public string Visibility { get; set; } = "Members";
    public string Status { get; set; } = "Draft";
    public Guid? EventId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? SeriesId { get; set; }
    public int QuorumMinimum { get; set; } = 1;
    public decimal QuorumPercent { get; set; }
    public string VotingCategories { get; set; } = "[]";
    public string Agenda { get; set; } = "[]";
    public string AgendaStatus { get; set; } = "Draft";
    public string RequiredPeople { get; set; } = "[]";
    public string OptionalPeople { get; set; } = "[]";
    public Guid? AgendaRequestId { get; set; }
}

public class MeetingAttendance : OperationalRecord, IOperationPerson
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string PersonName { get; set; } = "";

    public Guid MeetingId { get; set; }
    public Guid PersonId { get; set; }
    public string Status { get; set; } = "Expected";
    public string Rsvp { get; set; } = "Pending";
    public string Mode { get; set; } = "InPerson";
    public string Reason { get; set; } = "";
    public Guid ActorId { get; set; }
}

public class MeetingMinutes : OperationalRecord
{
    public Guid MeetingId { get; set; }
    public string Body { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public Guid PreparedBy { get; set; }
    public Guid? RequestId { get; set; }
    public int Revision { get; set; } = 1;
}

public class OperationTask : OperationalRecord
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourceType { get; set; } = "Administrative";
    public Guid? SourceId { get; set; }
    public Guid? PersonId { get; set; }
    public Guid? CommitteeId { get; set; }
    public string Priority { get; set; } = "Normal";
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public string Status { get; set; } = "To Do";
    public string Blocker { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string Dependencies { get; set; } = "[]";
    public Guid CreatorId { get; set; }
    public DateTimeOffset? RemindedAt { get; set; }
}

public class OperationComment : OperationalRecord
{
    public Guid TaskId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = "";
}

public class ServiceProject : OperationalRecord
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Community";
    public string Description { get; set; } = "";
    public string Objectives { get; set; } = "";
    public Guid ManagerId { get; set; }
    public Guid? CommitteeId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Location { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public int TargetBeneficiaries { get; set; }
    public int PlannedVolunteers { get; set; }
    public decimal PlannedHours { get; set; }
    public decimal EstimatedBudget { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid? RequestId { get; set; }
}

public class ProjectOrganization : OperationalRecord
{
    public Guid ProjectId { get; set; }
    public string Contribution { get; set; } = "";
    public decimal EstimatedContribution { get; set; }
}

public class ProjectActivity : OperationalRecord
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Status { get; set; } = "Planned";
}

public class VolunteerProfile : OperationalRecord, IOperationPerson
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string PersonName { get; set; } = "";

    public Guid PersonId { get; set; }
    public string Skills { get; set; } = "";
    public string Interests { get; set; } = "";
    public string Availability { get; set; } = "";
    public string Training { get; set; } = "";
    public string Language { get; set; } = "en";
}

public class VolunteerShift : OperationalRecord
{
    public Guid ProjectId { get; set; }
    public Guid? ActivityId { get; set; }
    public string Title { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public Guid SupervisorId { get; set; }
    public int Capacity { get; set; } = 10;
    public string RequiredTraining { get; set; } = "";
    public bool ApprovalRequired { get; set; } = true;
}

public class VolunteerAssignment : OperationalRecord, IOperationPerson
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string PersonName { get; set; } = "";

    public Guid ShiftId { get; set; }
    public Guid PersonId { get; set; }
    public string Status { get; set; } = "Applied";
    public DateTimeOffset? CheckIn { get; set; }
    public DateTimeOffset? CheckOut { get; set; }
    public int BreakMinutes { get; set; }
}

public class VolunteerHours : OperationalRecord, IOperationPerson
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string PersonName { get; set; } = "";

    public Guid AssignmentId { get; set; }
    public Guid PersonId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int BreakMinutes { get; set; }
    public decimal Hours { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Submitted";
    public Guid SubmittedBy { get; set; }
    public Guid? VerifiedBy { get; set; }
}

public class ImpactMetric : OperationalRecord
{
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "people";
    public string Description { get; set; } = "";
    public string DataType { get; set; } = "Decimal";
    public string Aggregation { get; set; } = "Sum";
    public string Category { get; set; } = "Community";
    public bool EvidenceRequired { get; set; } = true;
}

public class ProjectImpact : OperationalRecord
{
    public Guid ProjectId { get; set; }
    public Guid MetricId { get; set; }
    public string SourceKey { get; set; } = "";
    public decimal Value { get; set; }
    public bool Estimate { get; set; }
    public string Evidence { get; set; } = "";
    public string Status { get; set; } = "Submitted";
    public Guid SubmittedBy { get; set; }
    public Guid? RequestId { get; set; }
}

public class OperationRevision : OperationalRecord
{
    public string Kind { get; set; } = "";
    public Guid RecordId { get; set; }
    public string Body { get; set; } = "";
    public Guid ActorId { get; set; }
    public string Status { get; set; } = "";
}

public class DocumentVersion : OperationalRecord
{
    public Guid DocumentId { get; set; }
    public int Number { get; set; }
    public string ObjectKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
    public Guid UploaderId { get; set; }
}

public class CertificateTemplate : OperationalRecord
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Appreciation";
    public string Layout { get; set; } = "{}";
    public bool Active { get; set; } = true;
}

public class IssuedCertificate : OperationalRecord
{
    public Guid TemplateId { get; set; }
    public long TemplateVersion { get; set; }
    public Guid PersonId { get; set; }
    public string SourceType { get; set; } = "Membership";
    public Guid SourceId { get; set; }
    public string Number { get; set; } = "";

    [JsonIgnore]
    public string TokenHash { get; set; } = "";

    [JsonIgnore]
    public string ProtectedToken { get; set; } = "";
    public string Snapshot { get; set; } = "{}";
    public string Layout { get; set; } = "{}";
    public string Status { get; set; } = "Prepared";
    public Guid IssuerId { get; set; }
    public Guid? RequestId { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public bool PublicName { get; set; }
    public string RevocationReason { get; set; } = "";
    public Guid? ReplacesId { get; set; }
}

public partial class Db
{
    public DbSet<ClubMeeting> ClubMeetings => Set<ClubMeeting>();
    public DbSet<MeetingAttendance> MeetingAttendances => Set<MeetingAttendance>();
    public DbSet<MeetingMinutes> MeetingMinutes => Set<MeetingMinutes>();
    public DbSet<OperationTask> OperationTasks => Set<OperationTask>();
    public DbSet<OperationComment> OperationComments => Set<OperationComment>();
    public DbSet<ServiceProject> ServiceProjects => Set<ServiceProject>();
    public DbSet<ProjectOrganization> ProjectOrganizations => Set<ProjectOrganization>();
    public DbSet<ProjectActivity> ProjectActivities => Set<ProjectActivity>();
    public DbSet<VolunteerProfile> VolunteerProfiles => Set<VolunteerProfile>();
    public DbSet<VolunteerShift> VolunteerShifts => Set<VolunteerShift>();
    public DbSet<VolunteerAssignment> VolunteerAssignments => Set<VolunteerAssignment>();
    public DbSet<VolunteerHours> VolunteerHours => Set<VolunteerHours>();
    public DbSet<ImpactMetric> ImpactMetrics => Set<ImpactMetric>();
    public DbSet<ProjectImpact> ProjectImpacts => Set<ProjectImpact>();
    public DbSet<OperationRevision> OperationRevisions => Set<OperationRevision>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<CertificateTemplate> CertificateTemplates => Set<CertificateTemplate>();
    public DbSet<IssuedCertificate> IssuedCertificates => Set<IssuedCertificate>();

    private void ConfigureOperations(ModelBuilder b)
    {
        foreach (
            var t in new[]
            {
                typeof(ClubMeeting),
                typeof(MeetingAttendance),
                typeof(MeetingMinutes),
                typeof(OperationTask),
                typeof(OperationComment),
                typeof(ServiceProject),
                typeof(ProjectOrganization),
                typeof(ProjectActivity),
                typeof(VolunteerProfile),
                typeof(VolunteerShift),
                typeof(VolunteerAssignment),
                typeof(VolunteerHours),
                typeof(ImpactMetric),
                typeof(ProjectImpact),
                typeof(OperationRevision),
                typeof(DocumentVersion),
                typeof(CertificateTemplate),
                typeof(IssuedCertificate),
            }
        )
        {
            var e = b.Entity(t);
            e.HasBaseType((Type?)null);
            e.HasKey("Id");
            e.HasAlternateKey("TenantId", "Id");
            e.HasIndex("TenantId", "OrgId", "UpdatedAt");
            e.Property("Version").IsConcurrencyToken();
            e.HasOne(typeof(Org))
                .WithMany()
                .HasForeignKey("TenantId", "OrgId")
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict);
        }
        void F<T, P>(string key)
            where T : class
            where P : class =>
            b.Entity<T>()
                .HasOne<P>()
                .WithMany()
                .HasForeignKey("TenantId", key)
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict);
        F<ClubMeeting, ClubEvent>("EventId");
        F<ClubMeeting, EventSession>("SessionId");
        F<ClubMeeting, Committee>("CommitteeId");
        F<MeetingAttendance, ClubMeeting>("MeetingId");
        F<MeetingMinutes, ClubMeeting>("MeetingId");
        F<ProjectOrganization, ServiceProject>("ProjectId");
        F<ProjectActivity, ServiceProject>("ProjectId");
        F<VolunteerShift, ServiceProject>("ProjectId");
        F<VolunteerShift, ProjectActivity>("ActivityId");
        F<VolunteerAssignment, VolunteerShift>("ShiftId");
        F<VolunteerHours, VolunteerAssignment>("AssignmentId");
        F<VolunteerHours, ServiceProject>("ProjectId");
        F<ProjectImpact, ServiceProject>("ProjectId");
        F<ProjectImpact, ImpactMetric>("MetricId");
        F<DocumentVersion, GovernanceDocument>("DocumentId");
        F<IssuedCertificate, CertificateTemplate>("TemplateId");
        F<OperationComment, OperationTask>("TaskId");
        foreach (
            var t in new[]
            {
                typeof(MeetingAttendance),
                typeof(VolunteerProfile),
                typeof(VolunteerAssignment),
                typeof(VolunteerHours),
                typeof(IssuedCertificate),
            }
        )
            b.Entity(t)
                .HasOne(typeof(Person))
                .WithMany()
                .HasForeignKey("PersonId")
                .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ClubMeeting>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.StartsAt,
            });
        b.Entity<ClubMeeting>().HasIndex(x => new { x.SeriesId, x.StartsAt }).IsUnique();
        b.Entity<MeetingAttendance>().HasIndex(x => new { x.MeetingId, x.PersonId }).IsUnique();
        b.Entity<MeetingMinutes>().HasIndex(x => new { x.MeetingId, x.Revision }).IsUnique();
        b.Entity<ProjectOrganization>().HasIndex(x => new { x.ProjectId, x.OrgId }).IsUnique();
        b.Entity<VolunteerAssignment>().HasIndex(x => new { x.ShiftId, x.PersonId }).IsUnique();
        b.Entity<VolunteerProfile>().HasIndex(x => new { x.TenantId, x.PersonId }).IsUnique();
        b.Entity<VolunteerHours>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.PersonId,
                x.StartsAt,
                x.EndsAt,
            });
        b.Entity<VolunteerHours>().Property(x => x.Hours).HasPrecision(10, 2);
        b.Entity<ProjectImpact>()
            .HasIndex(x => new
            {
                x.ProjectId,
                x.MetricId,
                x.SourceKey,
            })
            .IsUnique();
        b.Entity<IssuedCertificate>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<IssuedCertificate>().HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        b.Entity<DocumentVersion>().HasIndex(x => new { x.DocumentId, x.Number }).IsUnique();
        b.Entity<OperationTask>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.PersonId,
                x.Status,
                x.DueAt,
            });
        b.Entity<ServiceProject>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.Status,
                x.StartsAt,
            });
        b.Entity<VolunteerHours>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.Status,
                x.StartsAt,
            });
        b.Entity<VolunteerAssignment>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.ShiftId,
                x.Status,
            });
        b.Entity<OperationRevision>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.RecordId,
                x.UpdatedAt,
            });
        b.Entity<OperationTask>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.Status,
                x.DueAt,
            });
        b.Entity<GovernanceDocument>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.Id,
            });
        b.Entity<GovernanceDocument>().Property(x => x.CurrentVersion).IsConcurrencyToken();
        b.Entity<IssuedCertificate>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.TemplateId,
                x.PersonId,
                x.SourceId,
                x.SourceType,
                x.TemplateVersion,
            });
    }
}
