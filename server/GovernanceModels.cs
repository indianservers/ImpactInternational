using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class OfficerPosition : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LevelId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "Leadership";
    public int DefaultMonths { get; set; } = 12;
    public int? MaximumHolders { get; set; } = 1;
    public bool Mandatory { get; set; }
    public bool RequireActiveMembership { get; set; } = true;
    public int MinimumMembershipDays { get; set; }
    public bool ApprovalRequired { get; set; } = true;
    public Guid? ReportsToId { get; set; }
    public Guid? GrantRoleId { get; set; }
    public bool Active { get; set; } = true;
    public long Version { get; set; } = 1;
}

public class LeadershipTerm : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public string Name { get; set; } = "";
    public string Calendar { get; set; } = "Custom";
    public string TimeZone { get; set; } = "UTC";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public long Version { get; set; } = 1;
}

public class OfficerAppointment : ITenantRecord
{
    public string PositionTitle { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid PersonId { get; set; }
    public Guid PositionId { get; set; }
    public Guid? TermId { get; set; }
    public Guid? MembershipId { get; set; }
    public Guid? GrantRoleId { get; set; }
    public string Method { get; set; } = "Appointed";
    public string Status { get; set; } = "Proposed";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public Guid ProposerId { get; set; }
    public Guid? ApproverId { get; set; }
    public Guid? DocumentId { get; set; }
    public string Notes { get; set; } = "";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class OfficerAppointmentHistory : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AppointmentId { get; set; }
    public string Status { get; set; } = "";
    public string Detail { get; set; } = "";
    public Guid? ActorId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class LeadershipHandover : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid OutgoingAppointmentId { get; set; }
    public Guid IncomingAppointmentId { get; set; }
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public Guid CreatorId { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public string Snapshot { get; set; } = "{}";
    public long Version { get; set; } = 1;
    public DateTimeOffset? ArchivedAt { get; set; }
}

public class HandoverChecklistItem : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid HandoverId { get; set; }
    public string Label { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool Completed { get; set; }
    public Guid? DocumentId { get; set; }
}

public class Committee : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Category { get; set; } = "Custom";
    public string Notes { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Status { get; set; } = "Proposed";
    public long Version { get; set; } = 1;
}

public class CommitteeRole : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "Member";
    public bool CanManage { get; set; }
    public bool Active { get; set; } = true;
}

public class CommitteeMembership : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CommitteeId { get; set; }
    public Guid PersonId { get; set; }
    public Guid RoleId { get; set; }
    public string Responsibilities { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Status { get; set; } = "Active";
    public long Version { get; set; } = 1;
}

public class CommitteeActivity : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CommitteeId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class GovernanceRequest : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Payload { get; set; } = "{}";
    public string Status { get; set; } = "Draft";
    public Guid CreatorId { get; set; }
    public Guid? SubjectUserId { get; set; }
    public Guid? WorkflowId { get; set; }
    public int CurrentSequence { get; set; } = 1;
    public int Revision { get; set; } = 1;
    public Guid? AppointmentId { get; set; }
    public Guid? CommitteeId { get; set; }
    public Guid? HandoverId { get; set; }
    public Guid? ResolutionId { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? ResultId { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class GovernanceDelegation : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid RoleId { get; set; }
    public Guid DelegatorId { get; set; }
    public Guid DelegateId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool Revoked { get; set; }
}

public class GovernanceResolution : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTimeOffset Date { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public Guid ProposerId { get; set; }
    public string Status { get; set; } = "Draft";
    public long Version { get; set; } = 1;
}

public class GovernanceDocument : ITenantRecord
{
    public Guid? EventId { get; set; }
    public string ObjectKey { get; set; } = "";
    public string Folder { get; set; } = "General";
    public string Tags { get; set; } = "";
    public string Visibility { get; set; } = "Organization";
    public string SharedUsers { get; set; } = "[]";
    public Guid? OperationId { get; set; }
    public int CurrentVersion { get; set; } = 1;
    public Guid? ApprovalRequestId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid? CommitteeId { get; set; }
    public string Kind { get; set; } = "Policy";
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";

    [JsonIgnore]
    public byte[] Content { get; set; } = [];
    public string Sha256 { get; set; } = "";
    public Guid UploaderId { get; set; }
    public bool ShareWithSuccessors { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RetainUntil { get; set; }
    public string Status { get; set; } = "Available";
}

public class ClubHealthConfiguration : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public int LeadershipWeight { get; set; } = 30;
    public int MembershipWeight { get; set; } = 30;
    public int GovernanceWeight { get; set; } = 25;
    public int DataWeight { get; set; } = 15;
    public int MinimumActiveMembers { get; set; } = 10;
    public long Version { get; set; } = 1;
    public bool RequireClubChangeApproval { get; set; } = true;
}

public class OrganizationHistory : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public string Before { get; set; } = "{}";
    public string After { get; set; } = "{}";
    public Guid? ActorId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public partial class Db
{
    public DbSet<OfficerPosition> OfficerPositions => Set<OfficerPosition>();
    public DbSet<OfficerAppointment> OfficerAppointments => Set<OfficerAppointment>();
    public DbSet<OfficerAppointmentHistory> OfficerAppointmentHistory =>
        Set<OfficerAppointmentHistory>();
    public DbSet<LeadershipTerm> LeadershipTerms => Set<LeadershipTerm>();
    public DbSet<LeadershipHandover> LeadershipHandovers => Set<LeadershipHandover>();
    public DbSet<HandoverChecklistItem> HandoverChecklistItems => Set<HandoverChecklistItem>();
    public DbSet<Committee> Committees => Set<Committee>();
    public DbSet<CommitteeRole> CommitteeRoles => Set<CommitteeRole>();
    public DbSet<CommitteeMembership> CommitteeMemberships => Set<CommitteeMembership>();
    public DbSet<CommitteeActivity> CommitteeActivities => Set<CommitteeActivity>();
    public DbSet<GovernanceRequest> GovernanceRequests => Set<GovernanceRequest>();
    public DbSet<GovernanceDelegation> GovernanceDelegations => Set<GovernanceDelegation>();
    public DbSet<GovernanceResolution> GovernanceResolutions => Set<GovernanceResolution>();
    public DbSet<GovernanceDocument> GovernanceDocuments => Set<GovernanceDocument>();
    public DbSet<ClubHealthConfiguration> ClubHealthConfigurations =>
        Set<ClubHealthConfiguration>();
    public DbSet<OrganizationHistory> OrganizationHistory => Set<OrganizationHistory>();

    private void ConfigureGovernance(ModelBuilder b)
    {
        void F<T, P>(string fk)
            where T : class
            where P : class =>
            b.Entity<T>()
                .HasOne<P>()
                .WithMany()
                .HasForeignKey("TenantId", fk)
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict);
        foreach (
            var type in new[]
            {
                typeof(OfficerPosition),
                typeof(LeadershipTerm),
                typeof(OfficerAppointment),
                typeof(OfficerAppointmentHistory),
                typeof(LeadershipHandover),
                typeof(HandoverChecklistItem),
                typeof(Committee),
                typeof(CommitteeRole),
                typeof(CommitteeMembership),
                typeof(CommitteeActivity),
                typeof(GovernanceRequest),
                typeof(GovernanceDelegation),
                typeof(GovernanceResolution),
                typeof(GovernanceDocument),
                typeof(ClubHealthConfiguration),
                typeof(OrganizationHistory),
            }
        )
        {
            var e = b.Entity(type);
            e.HasAlternateKey("TenantId", "Id");
            e.HasOne(typeof(Tenant))
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict);
            if (type.GetProperty("OrgId") != null)
            {
                e.HasOne(typeof(Org))
                    .WithMany()
                    .HasForeignKey("TenantId", "OrgId")
                    .HasPrincipalKey("TenantId", "Id")
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex("TenantId", "OrgId");
            }
            if (type.GetProperty("Version") != null)
                e.Property("Version").IsConcurrencyToken();
        }
        F<OfficerPosition, Level>("LevelId");
        F<OfficerPosition, OfficerPosition>("ReportsToId");
        F<OfficerPosition, Role>("GrantRoleId");
        F<OfficerAppointment, OfficerPosition>("PositionId");
        F<OfficerAppointment, LeadershipTerm>("TermId");
        F<OfficerAppointment, Membership>("MembershipId");
        F<OfficerAppointment, Role>("GrantRoleId");
        F<OfficerAppointment, GovernanceDocument>("DocumentId");
        b.Entity<OfficerAppointment>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        F<OfficerAppointmentHistory, OfficerAppointment>("AppointmentId");
        F<LeadershipHandover, OfficerAppointment>("OutgoingAppointmentId");
        F<LeadershipHandover, OfficerAppointment>("IncomingAppointmentId");
        F<HandoverChecklistItem, LeadershipHandover>("HandoverId");
        F<HandoverChecklistItem, GovernanceDocument>("DocumentId");
        F<CommitteeMembership, Committee>("CommitteeId");
        F<CommitteeMembership, CommitteeRole>("RoleId");
        b.Entity<CommitteeMembership>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        F<CommitteeActivity, Committee>("CommitteeId");
        F<GovernanceRequest, MembershipWorkflow>("WorkflowId");
        F<GovernanceRequest, OfficerAppointment>("AppointmentId");
        F<GovernanceRequest, Committee>("CommitteeId");
        F<GovernanceRequest, LeadershipHandover>("HandoverId");
        F<GovernanceRequest, GovernanceResolution>("ResolutionId");
        F<GovernanceRequest, GovernanceDocument>("DocumentId");
        F<MembershipApproval, GovernanceRequest>("GovernanceRequestId");
        F<GovernanceDocument, Committee>("CommitteeId");
        F<GovernanceDelegation, Role>("RoleId");
        F<Assignment, OfficerAppointment>("AppointmentId");
        b.Entity<OfficerPosition>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.Entity<Committee>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.Code,
            })
            .IsUnique();
        b.Entity<CommitteeRole>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.Entity<ClubHealthConfiguration>().HasIndex(x => x.TenantId).IsUnique();
        b.Entity<GovernanceResolution>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.Number,
            })
            .IsUnique();
        b.Entity<OfficerAppointment>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.Status,
                x.EndsAt,
            });
        b.Entity<OfficerAppointment>()
            .HasIndex(x => new
            {
                x.PersonId,
                x.StartsAt,
                x.EndsAt,
            });
        b.Entity<OfficerAppointment>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.PositionId,
                x.StartsAt,
                x.EndsAt,
            });
        b.Entity<CommitteeMembership>()
            .HasIndex(x => new
            {
                x.CommitteeId,
                x.Status,
                x.PersonId,
            });
        b.Entity<GovernanceRequest>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.Status,
                x.CreatedAt,
            });
        b.Entity<MembershipWorkflow>()
            .HasIndex(x => new { x.TenantId, x.Kind })
            .IsUnique()
            .HasFilter("\"Active\"");
        b.Entity<MembershipApproval>()
            .HasIndex(x => new
            {
                x.GovernanceRequestId,
                x.Revision,
                x.StageId,
                x.ActorId,
            })
            .IsUnique();
        b.Entity<MembershipApproval>()
            .HasIndex(x => new
            {
                x.GovernanceRequestId,
                x.Revision,
                x.StageId,
                x.PrincipalUserId,
            })
            .IsUnique();
    }
}
