using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class CommunicationAccount : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public string Name { get; set; } = "";
    public string Channel { get; set; } = "Email";
    public string Provider { get; set; } = "SMTP";
    public string Sender { get; set; } = "";
    public string Reference { get; set; } = "";
    public string BusinessId { get; set; } = "";

    [JsonIgnore]
    public string Secret { get; set; } = "";

    [JsonIgnore]
    public string WebhookSecret { get; set; } = "";
    public string VerifyTokenHash { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Verified { get; set; }
    public bool Inbound { get; set; }
    public int PerMinute { get; set; } = 30;
    public DateTimeOffset WindowAt { get; set; } = DateTimeOffset.UtcNow;
    public int WindowCount { get; set; }
    public string CallbackUrl { get; set; } = "";
    public long Version { get; set; } = 1;
}

public class Conversation : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? CommitteeId { get; set; }
    public Guid? EventId { get; set; }
    public string Kind { get; set; } = "Direct";
    public string Channel { get; set; } = "InApp";
    public string Title { get; set; } = "";
    public string ExternalThread { get; set; } = "";
    public string Status { get; set; } = "Open";
    public string Priority { get; set; } = "Normal";
    public string Tags { get; set; } = "";
    public Guid? AssignedId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastInboundAt { get; set; }
    public string Preview { get; set; } = "";
    public long Version { get; set; } = 1;
}

public class ConversationParticipant : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public bool Staff { get; set; }
    public bool Muted { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset ReadAt { get; set; } = DateTimeOffset.MinValue;
}

public class CommunicationMessage : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? BroadcastId { get; set; }
    public Guid? SenderId { get; set; }
    public Guid? PersonId { get; set; }
    public Guid? ReplyToId { get; set; }
    public string Body { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Direction { get; set; } = "Outbound";
    public bool Internal { get; set; }
    public string ClientKey { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class CommunicationAttachment : ITenantRecord
{
    public Guid? ProviderAccountId { get; set; }

    [JsonIgnore]
    public string ProviderMediaId { get; set; } = "";
    public string Status { get; set; } = "Stored";
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MessageId { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public long Size { get; set; }

    [JsonIgnore]
    public string StorageKey { get; set; } = "";
    public string Hash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddYears(1);
}

public class CommunicationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Channel { get; set; } = "InApp";
    public bool Transactional { get; set; } = true;
    public bool Promotional { get; set; }
    public bool Direct { get; set; } = true;
    public bool Birthday { get; set; }
    public int QuietStart { get; set; } = 22;
    public int QuietEnd { get; set; } = 8;
    public bool QuietEnabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class CommunicationSuppression : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = "Email";

    [JsonIgnore]
    public string AddressHash { get; set; } = "";
    public string Reason { get; set; } = "OptOut";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class CommunicationTemplate : ITenantRecord
{
    public Guid? AccountId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public string Name { get; set; } = "";
    public string Channel { get; set; } = "InApp";
    public string Category { get; set; } = "General";
    public string Language { get; set; } = "en";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string ProviderStatus { get; set; } = "Not synchronized";
    public bool Active { get; set; } = true;
    public long Version { get; set; } = 1;
}

public class CommunicationRevision : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ResourceId { get; set; }
    public string Kind { get; set; } = "Template";
    public long Version { get; set; }
    public string Snapshot { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class CommunicationAudience : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public string Name { get; set; } = "";
    public string Rules { get; set; } = "{}";
    public long Version { get; set; } = 1;
}

public class CommunicationBroadcast : ITenantRecord
{
    public long TemplateVersion { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? TemplateId { get; set; }
    public Guid? EventId { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public string Kind { get; set; } = "Campaign";
    public string Category { get; set; } = "General";
    public string Channel { get; set; } = "InApp";
    public string Purpose { get; set; } = "Promotional";
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Rules { get; set; } = "{}";
    public string ContentHash { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public bool Acknowledgment { get; set; }
    public bool Pinned { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset SendAt { get; set; } = DateTimeOffset.UtcNow;
    public string TimeZone { get; set; } = "UTC";
    public int RepeatDays { get; set; }
    public int Cycles { get; set; } = 1;
    public int CurrentCycle { get; set; } = 1;
    public DateTimeOffset? CompletedAt { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class CommunicationRecipient : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BroadcastId { get; set; }
    public Guid PersonId { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = "";
    public string Club { get; set; } = "";
    public string MemberNumber { get; set; } = "";
    public int QueuedCycle { get; set; }
    public string Exclusion { get; set; } = "";
    public DateTimeOffset? SeenAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
}

public class CommunicationProviderEvent : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string EventKey { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Error { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class CommunicationAutomation : ITenantRecord
{
    public string Definition {get;set;} = "";

    public Guid? ApprovalRequestId { get; set; }
    public string Status { get; set; } = "Draft";
    public string ContentHash { get; set; } = "";
    public long TemplateVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid CreatorId { get; set; }
    public string Name { get; set; } = "";
    public string Trigger { get; set; } = "membership.activated";
    public Guid TemplateId { get; set; }
    public Guid? AccountId { get; set; }
    public bool Active { get; set; }
    public int DelayMinutes { get; set; }
    public int DailyLimit { get; set; } = 100;
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; } = 1;
}

public class CommunicationExecution : ITenantRecord
{
    public string Trace {get;set;} = "[]";
    public long RuleVersion {get;set;}
    public string DefinitionSnapshot {get;set;} = "";
    public int Step {get;set;}
    public DateTimeOffset NextAttemptAt {get;set;}=DateTimeOffset.UtcNow;
    public int Attempts {get;set;}
    public string Error {get;set;} = "";
    public Guid? GeneratedResourceId {get;set;}
    public Guid? OrgId {get;set;}

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AutomationId { get; set; }
    public string SourceKey { get; set; } = "";
    public Guid? MailId { get; set; }
    public string Status { get; set; } = "Queued";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class CommunicationPolicy : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public bool MemberDirect { get; set; } = true;
    public int ApprovalThreshold { get; set; } = 1;
    public int RetentionDays { get; set; } = 365;
}

public partial class Db
{
    public DbSet<CommunicationAccount> CommunicationAccounts => Set<CommunicationAccount>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants =>
        Set<ConversationParticipant>();
    public DbSet<CommunicationMessage> CommunicationMessages => Set<CommunicationMessage>();
    public DbSet<CommunicationAttachment> CommunicationAttachments =>
        Set<CommunicationAttachment>();
    public DbSet<CommunicationPreference> CommunicationPreferences =>
        Set<CommunicationPreference>();
    public DbSet<CommunicationSuppression> CommunicationSuppressions =>
        Set<CommunicationSuppression>();
    public DbSet<CommunicationTemplate> CommunicationTemplates => Set<CommunicationTemplate>();
    public DbSet<CommunicationRevision> CommunicationRevisions => Set<CommunicationRevision>();
    public DbSet<CommunicationAudience> CommunicationAudiences => Set<CommunicationAudience>();
    public DbSet<CommunicationBroadcast> CommunicationBroadcasts => Set<CommunicationBroadcast>();
    public DbSet<CommunicationRecipient> CommunicationRecipients => Set<CommunicationRecipient>();
    public DbSet<CommunicationProviderEvent> CommunicationProviderEvents =>
        Set<CommunicationProviderEvent>();
    public DbSet<CommunicationAutomation> CommunicationAutomations =>
        Set<CommunicationAutomation>();
    public DbSet<CommunicationExecution> CommunicationExecutions => Set<CommunicationExecution>();
    public DbSet<CommunicationPolicy> CommunicationPolicies => Set<CommunicationPolicy>();

    private static void ConfigureCommunications(ModelBuilder b)
    {
        foreach (
            var t in new[]
            {
                typeof(CommunicationAccount),
                typeof(Conversation),
                typeof(ConversationParticipant),
                typeof(CommunicationMessage),
                typeof(CommunicationAttachment),
                typeof(CommunicationSuppression),
                typeof(CommunicationTemplate),
                typeof(CommunicationRevision),
                typeof(CommunicationAudience),
                typeof(CommunicationBroadcast),
                typeof(CommunicationRecipient),
                typeof(CommunicationProviderEvent),
                typeof(CommunicationAutomation),
                typeof(CommunicationExecution),
                typeof(CommunicationPolicy),
            }
        )
        {
            var e = b.Entity(t);
            e.HasAlternateKey("TenantId", "Id");
            e.HasOne(typeof(Tenant))
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict);
            if (t.GetProperty("OrgId") != null)
                e.HasOne(typeof(Org))
                    .WithMany()
                    .HasForeignKey("TenantId", "OrgId")
                    .HasPrincipalKey("TenantId", "Id")
                    .OnDelete(DeleteBehavior.Restrict);
            if (t.GetProperty("Version") != null)
                e.Property("Version").IsConcurrencyToken();
        }
        void F<T, P>(string fk)
            where T : class
            where P : class =>
            b.Entity<T>()
                .HasOne<P>()
                .WithMany()
                .HasForeignKey("TenantId", fk)
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict);
        F<Conversation, CommunicationAccount>("AccountId");
        F<Conversation, Committee>("CommitteeId");
        F<Conversation, ClubEvent>("EventId");
        F<ConversationParticipant, Conversation>("ConversationId");
        F<CommunicationMessage, Conversation>("ConversationId");
        F<CommunicationMessage, CommunicationBroadcast>("BroadcastId");
        F<CommunicationAttachment, CommunicationMessage>("MessageId");
        F<CommunicationAttachment, CommunicationAccount>("ProviderAccountId");
        b.Entity<CommunicationAttachment>().Property(x => x.Status).HasDefaultValue("Stored");
        b.Entity<CommunicationAttachment>().HasIndex(x => new { x.Status, x.NextAttemptAt });
        F<CommunicationBroadcast, CommunicationAccount>("AccountId");
        F<CommunicationBroadcast, CommunicationTemplate>("TemplateId");
        F<CommunicationTemplate, CommunicationAccount>("AccountId");
        F<CommunicationBroadcast, ClubEvent>("EventId");
        F<CommunicationBroadcast, GovernanceRequest>("ApprovalRequestId");
        F<CommunicationRecipient, CommunicationBroadcast>("BroadcastId");
        F<CommunicationProviderEvent, CommunicationAccount>("AccountId");
        F<CommunicationAutomation, CommunicationTemplate>("TemplateId");
        F<CommunicationAutomation, GovernanceRequest>("ApprovalRequestId");
        F<CommunicationAutomation, CommunicationAccount>("AccountId");
        F<CommunicationExecution, CommunicationAutomation>("AutomationId");
        b.Entity<ConversationParticipant>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunicationRecipient>()
            .HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunicationPreference>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ConversationParticipant>()
            .HasIndex(x => new { x.ConversationId, x.UserId })
            .IsUnique();
        b.Entity<ConversationParticipant>()
            .HasIndex(x => new
            {
                x.UserId,
                x.Active,
                x.ConversationId,
            });
        b.Entity<Conversation>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.UpdatedAt,
                x.Id,
            });
        b.Entity<Conversation>()
            .HasIndex(x => new { x.AccountId, x.ExternalThread })
            .IsUnique()
            .HasFilter("\"ExternalThread\"<>''");
        b.Entity<CommunicationMessage>()
            .HasIndex(x => new
            {
                x.ConversationId,
                x.At,
                x.Id,
            });
        b.Entity<CommunicationMessage>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.SenderId,
                x.ClientKey,
            })
            .IsUnique();
        b.Entity<CommunicationMessage>().HasIndex(x => new { x.BroadcastId, x.At });
        b.Entity<CommunicationPreference>().HasIndex(x => new { x.UserId, x.Channel }).IsUnique();
        b.Entity<CommunicationRecipient>()
            .HasIndex(x => new { x.BroadcastId, x.PersonId })
            .IsUnique();
        b.Entity<CommunicationRecipient>()
            .HasIndex(x => new
            {
                x.BroadcastId,
                x.QueuedCycle,
                x.Id,
            });
        b.Entity<CommunicationRecipient>().HasIndex(x => new { x.UserId, x.BroadcastId });
        b.Entity<CommunicationProviderEvent>()
            .HasIndex(x => new { x.AccountId, x.EventKey })
            .IsUnique();
        b.Entity<CommunicationSuppression>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.Channel,
                x.AddressHash,
            })
            .IsUnique();
        b.Entity<CommunicationExecution>()
            .HasIndex(x => new { x.AutomationId, x.SourceKey })
            .IsUnique();
        b.Entity<CommunicationBroadcast>().HasIndex(x => new { x.Status, x.SendAt });
        b.Entity<CommunicationPolicy>().HasIndex(x => x.OrgId).IsUnique();
        b.Entity<CommunicationRevision>().HasIndex(x => new { x.ResourceId, x.Version }).IsUnique();
        b.Entity<Mail>()
            .HasOne<ClubEvent>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EventId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunicationMessage>()
            .HasIndex(m => new
            {
                m.TenantId,
                m.SenderId,
                m.At,
            })
            .HasFilter("\"ConversationId\" IS NOT NULL");
        b.Entity<Mail>()
            .HasIndex(m => new
            {
                m.TenantId,
                m.CreatedAt,
                m.Channel,
                m.DeliveryStatus,
            });
        b.Entity<Mail>().Property(m => m.Channel).HasDefaultValue("Email");
        b.Entity<Mail>().Property(m => m.Purpose).HasDefaultValue("Transactional");
        b.Entity<Mail>().Property(m => m.DeliveryStatus).HasDefaultValue("Pending");
        b.Entity<Mail>().Property(m => m.TemplateParameters).HasDefaultValue("[]");
        b.Entity<Mail>().HasIndex(x => new { x.CommunicationId }).IsUnique();
        b.Entity<Mail>()
            .HasOne<CommunicationMessage>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CommunicationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Mail>()
            .HasOne<CommunicationAccount>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.AccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
