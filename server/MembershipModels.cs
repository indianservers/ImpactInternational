using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Clubs;

public interface ITenantRecord
{
    Guid Id { get; set; }
    Guid TenantId { get; set; }
}

public class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string LegalName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SearchName { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public string Gender { get; set; } = "";
    public DateOnly? Anniversary { get; set; }
    public string Company { get; set; } = "";
    public string Designation { get; set; } = "";
    public string Industry { get; set; } = "";
    public string Profession { get; set; } = "";
    public string BusinessCategory { get; set; } = "";
    public string WorkLocation { get; set; } = "";
    public string Website { get; set; } = "";
    public string Networking { get; set; } = "";

    [JsonIgnore]
    public byte[]? Photo { get; set; }
    public string PhotoType { get; set; } = "";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public PersonPrivacy Privacy { get; set; } = new();
    public List<PersonContact> Contacts { get; set; } = [];
    public List<PersonAddress> Addresses { get; set; } = [];
    public List<PersonTag> Tags { get; set; } = [];
}

public class PersonContact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public string Kind { get; set; } = "Email";
    public string Label { get; set; } = "Personal";
    public string Value { get; set; } = "";
    public string NormalizedValue { get; set; } = "";
    public bool Verified { get; set; }
}

public class PersonAddress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public string Country { get; set; } = "";
    public string State { get; set; } = "";
    public string City { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Address { get; set; } = "";
}

public class PersonTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public string Kind { get; set; } = "Skill";
    public string Value { get; set; } = "";
}

public class PersonPrivacy
{
    public Guid PersonId { get; set; }
    public bool DirectoryVisible { get; set; }
    public bool DisplayEmail { get; set; }
    public bool DisplayPhone { get; set; }
    public bool ProfessionalVisible { get; set; }
    public bool BirthdayAnnouncements { get; set; }
    public bool Marketing { get; set; }
    public bool PhotoVisible { get; set; }
    public bool AllowContactRequests { get; set; } = true;
    public bool CardNameVisible { get; set; }
    public bool TransactionalEmail { get; set; } = true;
    public long Version { get; set; } = 1;
}

public class MembershipCategory : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int? ValidityMonths { get; set; }
    public bool Active { get; set; } = true;
}

public class Membership : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public Guid ClubId { get; set; }
    public Guid CategoryId { get; set; }
    public string Number { get; set; } = "";
    public string Status { get; set; } = "Active";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndsAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool FinancialHold { get; set; }
    public long Version { get; set; } = 1;
    public Guid? SourceApplicationId { get; set; }
    public Guid? ImportRowId { get; set; }
    public Person Person { get; set; } = null!;
    public Org Club { get; set; } = null!;
    public MembershipCategory Category { get; set; } = null!;
}

public class MembershipHistory : ITenantRecord
{
    public Guid? CategoryId { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public string OldStatus { get; set; } = "";
    public string NewStatus { get; set; } = "Active";
    public string Reason { get; set; } = "";
    public DateTimeOffset EffectiveAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ActorId { get; set; }
    public Guid? ApprovalId { get; set; }
}

public class MembershipWorkflow : ITenantRecord
{
    public string Kind { get; set; } = "Membership";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public int Version { get; set; } = 1;
    public bool Active { get; set; } = true;
    public List<MembershipApprovalStage> Stages { get; set; } = [];
}

public class MembershipApprovalStage : ITenantRecord
{
    public bool AllowDelegation { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WorkflowId { get; set; }
    public string Name { get; set; } = "";
    public int Sequence { get; set; }
    public int RequiredApprovals { get; set; } = 1;
    public List<MembershipStageRole> Roles { get; set; } = [];
}

public class MembershipStageRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid StageId { get; set; }
    public Guid RoleId { get; set; }
}

public class MembershipApplication : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public Guid ClubId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? WorkflowId { get; set; }
    public string Statement { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public int Revision { get; set; }
    public int CurrentSequence { get; set; } = 1;
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? InvitationId { get; set; }
}

public class MembershipApproval : ITenantRecord
{
    public Guid? GovernanceRequestId { get; set; }
    public Guid? PrincipalUserId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? ApplicationId { get; set; }
    public Guid StageId { get; set; }
    public int Revision { get; set; }
    public Guid ActorId { get; set; }
    public string Decision { get; set; } = "Approve";
    public string Comment { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class MembershipInvitation : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ClubId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid InviterId { get; set; }
    public string Email { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public string Kind { get; set; } = "Membership";
    public Guid? PersonId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int UsageLimit { get; set; } = 1;
    public int Uses { get; set; }
    public bool Revoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class PersonLinkRequest : ITenantRecord
{
    public Guid InvitationId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public Guid UserId { get; set; }
    public Guid ClubId { get; set; }
    public string Status { get; set; } = "Pending";
    public string ReviewReason { get; set; } = "";
    public Guid? ReviewerId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class MembershipPolicy
{
    public Guid TenantId { get; set; }
    public bool AllowMultipleClubs { get; set; } = true;
    public bool SourceReviewRequired { get; set; } = true;
    public bool AssociationReviewRequired { get; set; }
    public bool FinancialClearanceRequired { get; set; }
}

public class MembershipTransfer : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid DestinationClubId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid? DestinationMembershipId { get; set; }
    public string Status { get; set; } = "PendingSource";
    public string Reason { get; set; } = "";
    public DateTimeOffset EffectiveAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; } = 1;
    public bool SourceRequired { get; set; } = true;
    public bool AssociationRequired { get; set; }
    public bool ClearanceRequired { get; set; }
    public bool FinancialCleared { get; set; }
}

public class TransferDecision : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid TransferId { get; set; }
    public string Stage { get; set; } = "";
    public string Decision { get; set; } = "";
    public string Comment { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class MembershipRenewal : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid ActorId { get; set; }
    public string RequestKey { get; set; } = "";
    public DateTimeOffset? PreviousEnd { get; set; }
    public DateTimeOffset NewEnd { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class MembershipDocument : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public string Kind { get; set; } = "Supporting";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";

    [JsonIgnore]
    public byte[] Content { get; set; } = [];
    public string Sha256 { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public Guid UploaderId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RetainUntil { get; set; }
}

public class MemberRelationship : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public Guid RelatedPersonId { get; set; }
    public string Relationship { get; set; } = "";
}

public class MemberActivity : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public Guid ClubId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class DigitalCard : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public string TokenHash { get; set; } = "";

    [JsonIgnore]
    public string ProtectedToken { get; set; } = "";
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? TemplateVersionId {get;set;}
    public string State {get;set;}="Legacy";
    public string Serial {get;set;}="";
    [JsonIgnore] public string Snapshot {get;set;}="{}";
    [JsonIgnore] public string Files {get;set;}="{}";
    public Guid? CreatorId {get;set;}
    public Guid? RequestId {get;set;}
    public Guid? ReplacesId {get;set;}
    public DateTimeOffset? ExpiryAt {get;set;}
    public long Version {get;set;}=1;
}

public class DigitalCardVerification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CardId { get; set; }
    public bool Valid { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class MemberImportJob : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ScopeAssignmentId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid ClubId { get; set; }
    public Guid CategoryId { get; set; }
    public string FileName { get; set; } = "";

    [JsonIgnore]
    public byte[] Payload { get; set; } = [];
    public string Hash { get; set; } = "";
    public string Mapping { get; set; } = "{}";
    public string Headers { get; set; } = "[]";
    public string Status { get; set; } = "Uploaded";
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int ImportedRows { get; set; }
    public string Error { get; set; } = "";
    public string RequestKey { get; set; } = "";
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public class MemberImportRow : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public int RowNumber { get; set; }
    public string Data { get; set; } = "{}";
    public string Status { get; set; } = "Valid";
    public string Error { get; set; } = "";
    public string Candidates { get; set; } = "[]";
    public Guid? ExistingPersonId { get; set; }
    public bool CreateSeparate { get; set; }
}

public class MemberSavedView
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Configuration { get; set; } = "{}";
}

public class NotificationTemplate
{
    public string Id { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}

public class MailDeliveryAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MailId { get; set; }
    public bool Succeeded { get; set; }
    public string ErrorCode { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class MemberContactRequest : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid RequesterId { get; set; }
    public string Message { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public partial class Db
{
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<PersonContact> PersonContacts => Set<PersonContact>();
    public DbSet<PersonAddress> PersonAddresses => Set<PersonAddress>();
    public DbSet<PersonPrivacy> PersonPrivacy => Set<PersonPrivacy>();
    public DbSet<PersonTag> PersonTags => Set<PersonTag>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<MembershipCategory> MembershipCategories => Set<MembershipCategory>();
    public DbSet<MembershipHistory> MembershipHistory => Set<MembershipHistory>();
    public DbSet<MembershipWorkflow> MembershipWorkflows => Set<MembershipWorkflow>();
    public DbSet<MembershipApprovalStage> MembershipApprovalStages =>
        Set<MembershipApprovalStage>();
    public DbSet<MembershipStageRole> MembershipStageRoles => Set<MembershipStageRole>();
    public DbSet<MembershipApplication> MembershipApplications => Set<MembershipApplication>();
    public DbSet<MembershipApproval> MembershipApprovals => Set<MembershipApproval>();
    public DbSet<MembershipInvitation> MembershipInvitations => Set<MembershipInvitation>();
    public DbSet<PersonLinkRequest> PersonLinkRequests => Set<PersonLinkRequest>();
    public DbSet<MembershipPolicy> MembershipPolicies => Set<MembershipPolicy>();
    public DbSet<MembershipTransfer> MembershipTransfers => Set<MembershipTransfer>();
    public DbSet<TransferDecision> TransferDecisions => Set<TransferDecision>();
    public DbSet<MembershipRenewal> MembershipRenewals => Set<MembershipRenewal>();
    public DbSet<MembershipDocument> MembershipDocuments => Set<MembershipDocument>();
    public DbSet<MemberRelationship> MemberRelationships => Set<MemberRelationship>();
    public DbSet<MemberActivity> MemberActivities => Set<MemberActivity>();
    public DbSet<DigitalCard> DigitalCards => Set<DigitalCard>();
    public DbSet<DigitalCardVerification> DigitalCardVerifications =>
        Set<DigitalCardVerification>();
    public DbSet<MemberImportJob> MemberImportJobs => Set<MemberImportJob>();
    public DbSet<MemberImportRow> MemberImportRows => Set<MemberImportRow>();
    public DbSet<MemberSavedView> MemberSavedViews => Set<MemberSavedView>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<MailDeliveryAttempt> MailDeliveryAttempts => Set<MailDeliveryAttempt>();
    public DbSet<MemberContactRequest> MemberContactRequests => Set<MemberContactRequest>();

    private void ConfigureMembership(ModelBuilder b)
    {
        ConfigureCommunications(b);
        ConfigureEvents(b);
        ConfigureOperations(b);
        ConfigureFinance(b);
        ConfigureIntelligence(b);
        ConfigureIdCards(b);
        ConfigureGovernance(b);
        ConfigureRoleHistory(b);
        b.Entity<NotificationTemplate>()
            .HasData(
                new[]
                {
                    "application.submitted",
                    "application.rejected",
                    "application.changes",
                    "application.approved",
                    "approval.request",
                    "membership.activated",
                    "membership.invitation",
                    "transfer.status",
                    "membership.renewed",
                    "contact.request",
                    "contact.response",
                    "import.completed",
                    "governance.review",
                    "governance.update",
                    "leadership.transition",
                }.Select(id => new NotificationTemplate
                {
                    Id = id,
                    Subject = "ClubSphere · " + id.Replace('.', ' '),
                    Body = "{detail}",
                })
            );
        b.Entity<Person>().HasIndex(x => x.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
        b.Entity<Person>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Person>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Person>()
            .HasMany(x => x.Contacts)
            .WithOne()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Person>()
            .HasMany(x => x.Addresses)
            .WithOne()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Person>()
            .HasMany(x => x.Tags)
            .WithOne()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Person>()
            .HasOne(x => x.Privacy)
            .WithOne()
            .HasForeignKey<PersonPrivacy>(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<PersonPrivacy>().HasKey(x => x.PersonId);
        b.Entity<PersonPrivacy>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<PersonContact>().HasIndex(x => x.NormalizedValue);
        b.Entity<PersonTag>()
            .HasIndex(x => new
            {
                x.PersonId,
                x.Kind,
                x.Value,
            })
            .IsUnique();
        b.Entity<PersonAddress>().HasIndex(x => new { x.City, x.PersonId });
        foreach (
            var t in b
                .Model.GetEntityTypes()
                .Where(x => typeof(ITenantRecord).IsAssignableFrom(x.ClrType))
                .ToList()
        )
        {
            var e = b.Entity(t.ClrType);
            e.HasAlternateKey("TenantId", "Id");
            e.HasOne(typeof(Tenant))
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict);
        }
        b.Entity<Membership>()
            .HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Membership>()
            .HasOne(x => x.Club)
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ClubId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Membership>()
            .HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CategoryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Membership>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Membership>().HasIndex(x => x.Number).IsUnique();
        b.Entity<Membership>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.PersonId,
                x.ClubId,
                x.CategoryId,
            })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Active','Inactive','Suspended','Expired')");
        b.Entity<Membership>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.ClubId,
                x.Status,
                x.Id,
            });
        b.Entity<Membership>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.Status,
                x.EndsAt,
            });
        b.Entity<Membership>().HasIndex(x => new { x.TenantId, x.JoinedAt });
        b.Entity<Membership>()
            .HasOne<MembershipApplication>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SourceApplicationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Membership>()
            .HasOne<MemberImportRow>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ImportRowId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Membership>()
            .HasIndex(x => x.SourceApplicationId)
            .IsUnique()
            .HasFilter("\"SourceApplicationId\" IS NOT NULL");
        b.Entity<Membership>()
            .HasIndex(x => x.ImportRowId)
            .IsUnique()
            .HasFilter("\"ImportRowId\" IS NOT NULL");
        b.Entity<MembershipCategory>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.Entity<MembershipHistory>()
            .HasOne<Membership>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipHistory>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.MembershipId,
                x.EffectiveAt,
            });
        b.Entity<MembershipWorkflow>()
            .HasMany(x => x.Stages)
            .WithOne()
            .HasForeignKey(x => new { x.TenantId, x.WorkflowId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipApprovalStage>()
            .HasIndex(x => new { x.WorkflowId, x.Sequence })
            .IsUnique();
        b.Entity<MembershipApprovalStage>()
            .HasMany(x => x.Roles)
            .WithOne()
            .HasForeignKey(x => new { x.TenantId, x.StageId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipStageRole>()
            .HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RoleId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipStageRole>().HasIndex(x => new { x.StageId, x.RoleId }).IsUnique();
        b.Entity<MembershipApplication>()
            .HasOne<MembershipInvitation>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.InvitationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipApplication>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipApplication>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ApplicantUserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipApplication>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ClubId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipApplication>()
            .HasOne<MembershipCategory>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CategoryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipApplication>()
            .HasOne<MembershipWorkflow>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.WorkflowId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipApplication>()
            .HasIndex(x => new { x.InvitationId, x.ApplicantUserId })
            .IsUnique()
            .HasFilter("\"InvitationId\" IS NOT NULL");
        b.Entity<MembershipApplication>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<MembershipApplication>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.ClubId,
                x.Status,
                x.CurrentSequence,
            });
        b.Entity<MembershipApplication>()
            .HasIndex(x => new
            {
                x.PersonId,
                x.ClubId,
                x.CategoryId,
            })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Draft','Submitted','Under Review','Changes Requested')");
        b.Entity<MembershipApproval>()
            .HasOne<MembershipApplication>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipApproval>()
            .HasOne<MembershipApprovalStage>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.StageId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipApproval>()
            .HasIndex(x => new
            {
                x.ApplicationId,
                x.Revision,
                x.StageId,
                x.ActorId,
            })
            .IsUnique();
        b.Entity<MembershipInvitation>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<MembershipInvitation>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ClubId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipInvitation>()
            .HasOne<MembershipCategory>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CategoryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PersonLinkRequest>()
            .HasOne<MembershipInvitation>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.InvitationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PersonLinkRequest>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PersonLinkRequest>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PersonLinkRequest>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ClubId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PersonLinkRequest>().HasIndex(x => new { x.InvitationId, x.UserId }).IsUnique();
        b.Entity<PersonLinkRequest>()
            .HasIndex(x => new { x.PersonId, x.UserId })
            .IsUnique()
            .HasFilter("\"Status\"='Pending'");
        b.Entity<MembershipPolicy>().HasKey(x => x.TenantId);
        b.Entity<MembershipPolicy>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipTransfer>()
            .HasOne<Membership>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.DestinationMembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipTransfer>()
            .HasOne<Membership>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipTransfer>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.DestinationClubId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipTransfer>()
            .HasOne<MembershipCategory>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CategoryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipTransfer>()
            .HasIndex(x => x.MembershipId)
            .IsUnique()
            .HasFilter(
                "\"Status\" IN ('PendingSource','PendingDestination','PendingAssociation','Scheduled')"
            );
        b.Entity<MembershipTransfer>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<TransferDecision>()
            .HasOne<MembershipTransfer>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.TransferId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransferDecision>().HasIndex(x => new { x.TransferId, x.Stage }).IsUnique();
        b.Entity<MembershipRenewal>()
            .HasOne<Membership>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipRenewal>()
            .HasIndex(x => new { x.MembershipId, x.RequestKey })
            .IsUnique();
        b.Entity<MembershipDocument>()
            .HasOne<Membership>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipDocument>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.MembershipId,
                x.At,
            });
        b.Entity<MemberRelationship>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberRelationship>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.RelatedPersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberActivity>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberActivity>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ClubId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberActivity>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.PersonId,
                x.At,
            });
        b.Entity<DigitalCard>()
            .HasOne<Membership>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DigitalCard>().HasIndex(x => x.TokenHash).IsUnique();

        b.Entity<DigitalCardVerification>()
            .HasOne<DigitalCard>()
            .WithMany()
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DigitalCardVerification>().HasIndex(x => x.At);
        b.Entity<MemberImportJob>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ClubId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberImportJob>()
            .HasOne<MembershipCategory>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CategoryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberImportJob>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.CreatorId,
                x.RequestKey,
            })
            .IsUnique();
        b.Entity<MemberImportJob>().HasIndex(x => new { x.Status, x.LeaseUntil });
        b.Entity<MemberImportJob>().Property(x => x.Status).IsConcurrencyToken();
        b.Entity<MemberImportRow>().Property(x => x.Status).IsConcurrencyToken();
        b.Entity<MemberImportRow>()
            .HasOne<MemberImportJob>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.JobId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<MemberImportRow>().HasIndex(x => new { x.JobId, x.RowNumber }).IsUnique();
        b.Entity<MemberSavedView>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<MemberSavedView>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.UserId,
                x.Name,
            })
            .IsUnique();
        b.Entity<NotificationTemplate>().HasKey(x => x.Id);
        b.Entity<MailDeliveryAttempt>()
            .HasOne<Mail>()
            .WithMany()
            .HasForeignKey(x => x.MailId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<MemberContactRequest>()
            .HasOne<Membership>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Assignment>()
            .HasOne<Membership>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
