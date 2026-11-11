using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class EventType : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public int DefaultCapacity { get; set; } = 100;
}

public class ClubEvent : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid TypeId { get; set; }
    public Guid? CommitteeId { get; set; }
    public Guid CreatorId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string TimeZone { get; set; } = "UTC";
    public string Language { get; set; } = "en";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string VenueType { get; set; } = "Physical";
    public string Venue { get; set; } = "";
    public string OnlineUrl { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Visibility { get; set; } = "Restricted";
    public string Approval { get; set; } = "Draft";
    public string Publication { get; set; } = "Draft";
    public DateTimeOffset? PublishedAt { get; set; }
    public string Lifecycle { get; set; } = "Scheduled";
    public bool RegistrationEnabled { get; set; } = true;
    public bool RsvpRequired { get; set; } = true;
    public DateTimeOffset RegistrationStartsAt { get; set; }
    public DateTimeOffset RegistrationEndsAt { get; set; }
    public DateTimeOffset RsvpDeadline { get; set; }
    public int Capacity { get; set; } = 100;
    public bool Waitlist { get; set; } = true;
    public string WaitlistPolicy { get; set; } = "Allocate";
    public int OfferMinutes { get; set; } = 30;
    public bool AllowGuests { get; set; }
    public int MaxCompanions { get; set; }
    public bool RegistrationApproval { get; set; }
    public bool Paid { get; set; }
    public string CancellationPolicy { get; set; } = "Cancel before registration closes.";
    public string Fields { get; set; } = "[]";
    public Guid? ApprovalRequestId { get; set; }
    public DateTimeOffset? ReminderAt { get; set; }
    public bool ReminderSent { get; set; }
    public bool CompletionSent { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class EventCohost : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid OrgId { get; set; }
}

public class EventAudience : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid? OrgId { get; set; }
    public Guid? CommitteeId { get; set; }
    public Guid? PersonId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? RoleId { get; set; }
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public string Status { get; set; } = "Active";
    public string RegistrationStatus { get; set; } = "";
}

public class EventSession : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Speaker { get; set; } = "";
    public string Room { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int Capacity { get; set; } = 100;
    public bool RegistrationRequired { get; set; } = true;
    public bool AttendanceRequired { get; set; } = true;
    public bool MembersOnly { get; set; }
    public string Status { get; set; } = "Active";
}

public class EventInvitation : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid? PersonId { get; set; }
    public Guid InviterId { get; set; }
    public string RecipientKey { get; set; } = "";
    public string GuestName { get; set; } = "";

    [JsonIgnore]
    public string Email { get; set; } = "";

    [JsonIgnore]
    public string TokenHash { get; set; } = "";

    [JsonIgnore]
    public string ProtectedToken { get; set; } = "";
    public string Context { get; set; } = "";
    public string Message { get; set; } = "";
    public string Status { get; set; } = "Queued";
    public DateTimeOffset SendAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid? MailId { get; set; }
    public string Rsvp { get; set; } = "Awaiting Response";
    public DateTimeOffset? RespondedAt { get; set; }
}

public class EventRsvpHistory : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid InvitationId { get; set; }
    public string Response { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class EventRegistration : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid? PersonId { get; set; }
    public Guid? MembershipId { get; set; }
    public Guid? InvitationId { get; set; }
    public string ParticipantKey { get; set; } = "";
    public string Name { get; set; } = "";

    [JsonIgnore]
    public string Email { get; set; } = "";
    public string Category { get; set; } = "Member";
    public string Status { get; set; } = "Pending";
    public int Companions { get; set; }
    public string GuestNames { get; set; } = "[]";

    [JsonIgnore]
    public string Responses { get; set; } = "{}";

    [JsonIgnore]
    public string Accessibility { get; set; } = "";

    [JsonIgnore]
    public string Dietary { get; set; } = "";

    [JsonIgnore]
    public string Emergency { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? OfferExpiresAt { get; set; }
    public bool FeedbackSubmitted { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public long Version { get; set; } = 1;
}

public class EventSessionBooking : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid SessionId { get; set; }
    public Guid RegistrationId { get; set; }
    public bool Active { get; set; } = true;
}

public class EventTicket : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid RegistrationId { get; set; }

    [JsonIgnore]
    public string TokenHash { get; set; } = "";

    [JsonIgnore]
    public string ProtectedToken { get; set; } = "";
    public bool Revoked { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class EventCheckin : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid OperatorId { get; set; }
    public string Device { get; set; } = "";
    public string EntryPoint { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public bool Voided { get; set; }
    public string CorrectionReason { get; set; } = "";
}

public class EventStaff : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid PersonId { get; set; }
    public string Role { get; set; } = "Volunteer";
    public string Capability { get; set; } = "Volunteer";
    public string Responsibilities { get; set; } = "";
    public string Tasks { get; set; } = "[]";
    public string TaskStatus { get; set; } = "Pending";
    public string Notes { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset? ShiftCheckinAt { get; set; }
    public long Version { get; set; } = 1;
}

public class EventFeedbackForm : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public string Questions { get; set; } = "[]";
    public bool Anonymous { get; set; }
    public DateTimeOffset Deadline { get; set; }
}

public class EventFeedbackResponse : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid? RegistrationId { get; set; }
    public string Answers { get; set; } = "{}";
    public DateOnly Day { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
}

public class EventSavedFilter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string Filters { get; set; } = "{}";
}

public class EventNotice : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public string Message { get; set; } = "";
    public string Audience { get; set; } = "Registrations";
    public Guid? Cursor { get; set; }
    public bool Completed { get; set; }
}

public partial class Db
{
    public DbSet<EventNotice> EventNotices => Set<EventNotice>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<ClubEvent> Events => Set<ClubEvent>();
    public DbSet<EventCohost> EventCohosts => Set<EventCohost>();
    public DbSet<EventAudience> EventAudiences => Set<EventAudience>();
    public DbSet<EventSession> EventSessions => Set<EventSession>();
    public DbSet<EventInvitation> EventInvitations => Set<EventInvitation>();
    public DbSet<EventRsvpHistory> EventRsvpHistory => Set<EventRsvpHistory>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
    public DbSet<EventSessionBooking> EventSessionBookings => Set<EventSessionBooking>();
    public DbSet<EventTicket> EventTickets => Set<EventTicket>();
    public DbSet<EventCheckin> EventCheckins => Set<EventCheckin>();
    public DbSet<EventStaff> EventStaff => Set<EventStaff>();
    public DbSet<EventFeedbackForm> EventFeedbackForms => Set<EventFeedbackForm>();
    public DbSet<EventFeedbackResponse> EventFeedbackResponses => Set<EventFeedbackResponse>();
    public DbSet<EventSavedFilter> EventSavedFilters => Set<EventSavedFilter>();

    private void ConfigureEvents(ModelBuilder b)
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
                typeof(EventNotice),
                typeof(EventType),
                typeof(ClubEvent),
                typeof(EventCohost),
                typeof(EventAudience),
                typeof(EventSession),
                typeof(EventInvitation),
                typeof(EventRsvpHistory),
                typeof(EventRegistration),
                typeof(EventSessionBooking),
                typeof(EventTicket),
                typeof(EventCheckin),
                typeof(EventStaff),
                typeof(EventFeedbackForm),
                typeof(EventFeedbackResponse),
            }
        )
        {
            var e = b.Entity(type);
            e.HasAlternateKey("TenantId", "Id");
            if (type.GetProperty("EventId") != null)
                e.HasOne(typeof(ClubEvent))
                    .WithMany()
                    .HasForeignKey("TenantId", "EventId")
                    .HasPrincipalKey("TenantId", "Id")
                    .OnDelete(DeleteBehavior.Restrict);
            if (type.GetProperty("Version") != null)
                e.Property("Version").IsConcurrencyToken();
        }
        b.Entity<ClubEvent>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<EventInvitation>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.InviterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<EventCheckin>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);
        F<ClubEvent, GovernanceRequest>("ApprovalRequestId");
        F<EventRegistration, GovernanceRequest>("ApprovalRequestId");
        F<ClubEvent, Org>("OrgId");
        F<ClubEvent, EventType>("TypeId");
        F<ClubEvent, Committee>("CommitteeId");
        F<EventCohost, Org>("OrgId");
        F<EventAudience, Org>("OrgId");
        F<EventAudience, Committee>("CommitteeId");
        F<EventAudience, MembershipCategory>("CategoryId");
        F<EventAudience, Role>("RoleId");
        F<EventRsvpHistory, EventInvitation>("InvitationId");
        F<EventRegistration, Membership>("MembershipId");
        F<EventRegistration, EventInvitation>("InvitationId");
        F<EventSessionBooking, EventSession>("SessionId");
        F<EventSessionBooking, EventRegistration>("RegistrationId");
        F<EventTicket, EventRegistration>("RegistrationId");
        F<EventCheckin, EventRegistration>("RegistrationId");
        F<EventCheckin, EventSession>("SessionId");
        F<EventFeedbackResponse, EventRegistration>("RegistrationId");
        F<GovernanceDocument, ClubEvent>("EventId");
        foreach (
            var type in new[]
            {
                typeof(EventAudience),
                typeof(EventInvitation),
                typeof(EventRegistration),
                typeof(EventStaff),
            }
        )
            b.Entity(type)
                .HasOne(typeof(Person))
                .WithMany()
                .HasForeignKey("PersonId")
                .OnDelete(DeleteBehavior.Restrict);
        b.Entity<EventNotice>().HasIndex(x => new { x.Completed, x.EventId });
        b.Entity<EventType>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.Entity<ClubEvent>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.OrgId,
                x.StartsAt,
                x.Id,
            });
        b.Entity<ClubEvent>()
            .HasIndex(x => new
            {
                x.Publication,
                x.StartsAt,
                x.Id,
            });
        b.Entity<EventInvitation>().HasIndex(x => new { x.EventId, x.RecipientKey }).IsUnique();
        b.Entity<EventInvitation>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<EventInvitation>().HasIndex(x => new { x.Status, x.SendAt });
        b.Entity<EventInvitation>().HasIndex(x => new { x.PersonId, x.SendAt });
        b.Entity<EventRegistration>().HasIndex(x => new { x.EventId, x.ParticipantKey }).IsUnique();
        b.Entity<EventRegistration>()
            .HasIndex(x => new
            {
                x.EventId,
                x.Status,
                x.CreatedAt,
                x.Id,
            });
        b.Entity<EventRegistration>().HasIndex(x => new { x.PersonId, x.EventId });
        b.Entity<EventSessionBooking>()
            .HasIndex(x => new { x.RegistrationId, x.SessionId })
            .IsUnique();
        b.Entity<EventTicket>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<EventTicket>().HasIndex(x => new { x.RegistrationId, x.Revoked });
        b.Entity<EventCheckin>()
            .HasIndex(x => new { x.RegistrationId, x.SessionId })
            .IsUnique()
            .AreNullsDistinct(false);
        b.Entity<EventCheckin>()
            .HasIndex(x => new
            {
                x.EventId,
                x.SessionId,
                x.Voided,
            });
        b.Entity<EventStaff>().HasIndex(x => new { x.EventId, x.PersonId }).IsUnique();
        b.Entity<EventStaff>().HasIndex(x => new { x.PersonId, x.Active });
        b.Entity<EventFeedbackForm>().HasIndex(x => x.EventId).IsUnique();
        b.Entity<EventFeedbackResponse>()
            .HasIndex(x => new { x.EventId, x.RegistrationId })
            .IsUnique();
        b.Entity<EventSavedFilter>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
