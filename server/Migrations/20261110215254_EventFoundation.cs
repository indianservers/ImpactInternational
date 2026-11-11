using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class EventFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "GovernanceDocuments",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "EventSavedFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Filters = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSavedFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSavedFilters_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultCapacity = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventTypes", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventTypes_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventTypes_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitteeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    VenueType = table.Column<string>(type: "text", nullable: false),
                    Venue = table.Column<string>(type: "text", nullable: false),
                    OnlineUrl = table.Column<string>(type: "text", nullable: false),
                    Contact = table.Column<string>(type: "text", nullable: false),
                    Visibility = table.Column<string>(type: "text", nullable: false),
                    Approval = table.Column<string>(type: "text", nullable: false),
                    Publication = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Lifecycle = table.Column<string>(type: "text", nullable: false),
                    RegistrationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RsvpRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RegistrationStartsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    RegistrationEndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    RsvpDeadline = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Waitlist = table.Column<bool>(type: "boolean", nullable: false),
                    WaitlistPolicy = table.Column<string>(type: "text", nullable: false),
                    OfferMinutes = table.Column<int>(type: "integer", nullable: false),
                    AllowGuests = table.Column<bool>(type: "boolean", nullable: false),
                    MaxCompanions = table.Column<int>(type: "integer", nullable: false),
                    RegistrationApproval = table.Column<bool>(type: "boolean", nullable: false),
                    Paid = table.Column<bool>(type: "boolean", nullable: false),
                    CancellationPolicy = table.Column<string>(type: "text", nullable: false),
                    Fields = table.Column<string>(type: "text", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReminderAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    ReminderSent = table.Column<bool>(type: "boolean", nullable: false),
                    CompletionSent = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.UniqueConstraint("AK_Events_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Events_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Events_Committees_TenantId_CommitteeId",
                        columns: x => new { x.TenantId, x.CommitteeId },
                        principalTable: "Committees",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Events_EventTypes_TenantId_TypeId",
                        columns: x => new { x.TenantId, x.TypeId },
                        principalTable: "EventTypes",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Events_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventAudiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommitteeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    City = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RegistrationStatus = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventAudiences", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventAudiences_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventAudiences_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventAudiences_Committees_TenantId_CommitteeId",
                        columns: x => new { x.TenantId, x.CommitteeId },
                        principalTable: "Committees",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventAudiences_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventAudiences_MembershipCategories_TenantId_CategoryId",
                        columns: x => new { x.TenantId, x.CategoryId },
                        principalTable: "MembershipCategories",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventAudiences_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventAudiences_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventAudiences_Roles_TenantId_RoleId",
                        columns: x => new { x.TenantId, x.RoleId },
                        principalTable: "Roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventCohosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventCohosts", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventCohosts_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventCohosts_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventCohosts_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventCohosts_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventFeedbackForms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Questions = table.Column<string>(type: "text", nullable: false),
                    Anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    Deadline = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventFeedbackForms", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventFeedbackForms_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventFeedbackForms_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventFeedbackForms_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    InviterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientKey = table.Column<string>(type: "text", nullable: false),
                    GuestName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ProtectedToken = table.Column<string>(type: "text", nullable: false),
                    Context = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SendAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    SentAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    ExpiresAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    MailId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rsvp = table.Column<string>(type: "text", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventInvitations", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventInvitations_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventInvitations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventInvitations_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventInvitations_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Speaker = table.Column<string>(type: "text", nullable: false),
                    Room = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    RegistrationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AttendanceRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MembersOnly = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSessions", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventSessions_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventSessions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventSessions_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventStaff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Capability = table.Column<string>(type: "text", nullable: false),
                    Responsibilities = table.Column<string>(type: "text", nullable: false),
                    Tasks = table.Column<string>(type: "text", nullable: false),
                    TaskStatus = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    ShiftCheckinAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStaff", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventStaff_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventStaff_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventStaff_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventStaff_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParticipantKey = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Companions = table.Column<int>(type: "integer", nullable: false),
                    GuestNames = table.Column<string>(type: "text", nullable: false),
                    Responses = table.Column<string>(type: "text", nullable: false),
                    Accessibility = table.Column<string>(type: "text", nullable: false),
                    Dietary = table.Column<string>(type: "text", nullable: false),
                    Emergency = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    OfferExpiresAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    FeedbackSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRegistrations", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventRegistrations_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventRegistrations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventRegistrations_EventInvitations_TenantId_InvitationId",
                        columns: x => new { x.TenantId, x.InvitationId },
                        principalTable: "EventInvitations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventRegistrations_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventRegistrations_Memberships_TenantId_MembershipId",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventRegistrations_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventRsvpHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Response = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRsvpHistory", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventRsvpHistory_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventRsvpHistory_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventRsvpHistory_EventInvitations_TenantId_InvitationId",
                        columns: x => new { x.TenantId, x.InvitationId },
                        principalTable: "EventInvitations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventCheckins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Device = table.Column<string>(type: "text", nullable: false),
                    EntryPoint = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Voided = table.Column<bool>(type: "boolean", nullable: false),
                    CorrectionReason = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventCheckins", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventCheckins_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventCheckins_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventCheckins_EventRegistrations_TenantId_RegistrationId",
                        columns: x => new { x.TenantId, x.RegistrationId },
                        principalTable: "EventRegistrations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventCheckins_EventSessions_TenantId_SessionId",
                        columns: x => new { x.TenantId, x.SessionId },
                        principalTable: "EventSessions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventCheckins_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventFeedbackResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Answers = table.Column<string>(type: "text", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventFeedbackResponses", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventFeedbackResponses_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventFeedbackResponses_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventFeedbackResponses_EventRegistrations_TenantId_Registra~",
                        columns: x => new { x.TenantId, x.RegistrationId },
                        principalTable: "EventRegistrations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventFeedbackResponses_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventSessionBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSessionBookings", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventSessionBookings_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventSessionBookings_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventSessionBookings_EventRegistrations_TenantId_Registrati~",
                        columns: x => new { x.TenantId, x.RegistrationId },
                        principalTable: "EventRegistrations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventSessionBookings_EventSessions_TenantId_SessionId",
                        columns: x => new { x.TenantId, x.SessionId },
                        principalTable: "EventSessions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventSessionBookings_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "EventTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ProtectedToken = table.Column<string>(type: "text", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventTickets", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventTickets_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventTickets_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventTickets_EventRegistrations_TenantId_RegistrationId",
                        columns: x => new { x.TenantId, x.RegistrationId },
                        principalTable: "EventRegistrations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventTickets_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.InsertData(
                table: "Permissions",
                column: "Id",
                values: new object[]
                {
                    "events.checkin",
                    "events.configure",
                    "events.manage",
                    "events.reports",
                    "events.view",
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceDocuments_TenantId_EventId",
                table: "GovernanceDocuments",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventAudiences_PersonId",
                table: "EventAudiences",
                column: "PersonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventAudiences_TenantId_CategoryId",
                table: "EventAudiences",
                columns: new[] { "TenantId", "CategoryId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventAudiences_TenantId_CommitteeId",
                table: "EventAudiences",
                columns: new[] { "TenantId", "CommitteeId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventAudiences_TenantId_EventId",
                table: "EventAudiences",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventAudiences_TenantId_OrgId",
                table: "EventAudiences",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventAudiences_TenantId_RoleId",
                table: "EventAudiences",
                columns: new[] { "TenantId", "RoleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventCheckins_EventId_SessionId_Voided",
                table: "EventCheckins",
                columns: new[] { "EventId", "SessionId", "Voided" }
            );

            migrationBuilder
                .CreateIndex(
                    name: "IX_EventCheckins_RegistrationId_SessionId",
                    table: "EventCheckins",
                    columns: new[] { "RegistrationId", "SessionId" },
                    unique: true
                )
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_EventCheckins_TenantId_EventId",
                table: "EventCheckins",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventCheckins_TenantId_RegistrationId",
                table: "EventCheckins",
                columns: new[] { "TenantId", "RegistrationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventCheckins_TenantId_SessionId",
                table: "EventCheckins",
                columns: new[] { "TenantId", "SessionId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventCohosts_TenantId_EventId",
                table: "EventCohosts",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventCohosts_TenantId_OrgId",
                table: "EventCohosts",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventFeedbackForms_EventId",
                table: "EventFeedbackForms",
                column: "EventId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventFeedbackForms_TenantId_EventId",
                table: "EventFeedbackForms",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventFeedbackResponses_EventId_RegistrationId",
                table: "EventFeedbackResponses",
                columns: new[] { "EventId", "RegistrationId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventFeedbackResponses_TenantId_EventId",
                table: "EventFeedbackResponses",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventFeedbackResponses_TenantId_RegistrationId",
                table: "EventFeedbackResponses",
                columns: new[] { "TenantId", "RegistrationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventInvitations_EventId_RecipientKey",
                table: "EventInvitations",
                columns: new[] { "EventId", "RecipientKey" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventInvitations_PersonId_SendAt",
                table: "EventInvitations",
                columns: new[] { "PersonId", "SendAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventInvitations_Status_SendAt",
                table: "EventInvitations",
                columns: new[] { "Status", "SendAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventInvitations_TenantId_EventId",
                table: "EventInvitations",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventInvitations_TokenHash",
                table: "EventInvitations",
                column: "TokenHash",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventRegistrations_EventId_ParticipantKey",
                table: "EventRegistrations",
                columns: new[] { "EventId", "ParticipantKey" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventRegistrations_EventId_Status_CreatedAt_Id",
                table: "EventRegistrations",
                columns: new[] { "EventId", "Status", "CreatedAt", "Id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventRegistrations_PersonId_EventId",
                table: "EventRegistrations",
                columns: new[] { "PersonId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventRegistrations_TenantId_EventId",
                table: "EventRegistrations",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventRegistrations_TenantId_InvitationId",
                table: "EventRegistrations",
                columns: new[] { "TenantId", "InvitationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventRegistrations_TenantId_MembershipId",
                table: "EventRegistrations",
                columns: new[] { "TenantId", "MembershipId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventRsvpHistory_TenantId_InvitationId",
                table: "EventRsvpHistory",
                columns: new[] { "TenantId", "InvitationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_Publication_StartsAt_Id",
                table: "Events",
                columns: new[] { "Publication", "StartsAt", "Id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_TenantId_CommitteeId",
                table: "Events",
                columns: new[] { "TenantId", "CommitteeId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_TenantId_OrgId_StartsAt_Id",
                table: "Events",
                columns: new[] { "TenantId", "OrgId", "StartsAt", "Id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_TenantId_TypeId",
                table: "Events",
                columns: new[] { "TenantId", "TypeId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSavedFilters_UserId",
                table: "EventSavedFilters",
                column: "UserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSessionBookings_RegistrationId_SessionId",
                table: "EventSessionBookings",
                columns: new[] { "RegistrationId", "SessionId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSessionBookings_TenantId_EventId",
                table: "EventSessionBookings",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSessionBookings_TenantId_RegistrationId",
                table: "EventSessionBookings",
                columns: new[] { "TenantId", "RegistrationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSessionBookings_TenantId_SessionId",
                table: "EventSessionBookings",
                columns: new[] { "TenantId", "SessionId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventSessions_TenantId_EventId",
                table: "EventSessions",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventStaff_EventId_PersonId",
                table: "EventStaff",
                columns: new[] { "EventId", "PersonId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventStaff_PersonId_Active",
                table: "EventStaff",
                columns: new[] { "PersonId", "Active" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventStaff_TenantId_EventId",
                table: "EventStaff",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventTickets_RegistrationId_Revoked",
                table: "EventTickets",
                columns: new[] { "RegistrationId", "Revoked" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventTickets_TenantId_EventId",
                table: "EventTickets",
                columns: new[] { "TenantId", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventTickets_TenantId_RegistrationId",
                table: "EventTickets",
                columns: new[] { "TenantId", "RegistrationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventTickets_TokenHash",
                table: "EventTickets",
                column: "TokenHash",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventTypes_TenantId_Name",
                table: "EventTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_GovernanceDocuments_Events_TenantId_EventId",
                table: "GovernanceDocuments",
                columns: new[] { "TenantId", "EventId" },
                principalTable: "Events",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.Sql(
                """
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE p."Id" LIKE 'events.%' AND (r."Name"='Association Super Admin' OR (r."Name" IN ('Club Manager','Club President','Club Secretary') AND p."Id"<>'events.configure') OR (r."Name" IN ('Club Member','Guest / Applicant') AND p."Id"='events.view')) ON CONFLICT DO NOTHING;
INSERT INTO "NotificationTemplates" ("Id","Subject","Body") VALUES ('event.update','ClubSphere event update','{detail}'),('event.invitation','You are invited � ClubSphere','{detail}') ON CONFLICT DO NOTHING;
ALTER TABLE "Events" ADD CONSTRAINT "CK_Event_Config" CHECK ("StartsAt"<"EndsAt" AND "RegistrationStartsAt"<"RegistrationEndsAt" AND "RegistrationEndsAt"<="EndsAt" AND "Capacity">0 AND "MaxCompanions" BETWEEN 0 AND 10 AND "Approval" IN ('Draft','Pending Approval','Changes Requested','Rejected','Approved') AND "Publication" IN ('Draft','Published') AND "Lifecycle" IN ('Scheduled','Completed','Cancelled','Archived'));
ALTER TABLE "EventSessions" ADD CONSTRAINT "CK_Event_Session" CHECK("StartsAt"<"EndsAt" AND "Capacity">0);
ALTER TABLE "EventRegistrations" ADD CONSTRAINT "CK_Event_Registration" CHECK("Companions">=0 AND "Status" IN ('Pending','Awaiting Approval','Confirmed','Waitlisted','Offered','Cancelled','Rejected'));
CREATE TRIGGER event_rsvp_history_immutable BEFORE UPDATE OR DELETE ON "EventRsvpHistory" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE FUNCTION clubs_event_registration_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ev "Events"; used integer;
BEGIN
 SELECT * INTO ev FROM "Events" WHERE "Id"=NEW."EventId" AND "TenantId"=NEW."TenantId" FOR UPDATE;
 IF NEW."Status" IN ('Confirmed','Offered') THEN
  IF ev."Lifecycle" IN ('Cancelled','Archived') OR ev."Publication"<>'Published' OR ev."Paid" THEN RAISE EXCEPTION 'Event cannot confirm registration' USING ERRCODE='23514'; END IF;
  SELECT COALESCE(sum(1+"Companions"),0) INTO used FROM "EventRegistrations" WHERE "EventId"=NEW."EventId" AND "Id"<>NEW."Id" AND "Status" IN ('Confirmed','Offered');
  IF used+1+NEW."Companions">ev."Capacity" THEN RAISE EXCEPTION 'Event capacity exceeded' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER event_registration_capacity BEFORE INSERT OR UPDATE ON "EventRegistrations" FOR EACH ROW EXECUTE FUNCTION clubs_event_registration_guard();
CREATE FUNCTION clubs_event_link_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM "EventRegistrations" r WHERE r."Id"=NEW."RegistrationId" AND r."EventId"=NEW."EventId" AND r."TenantId"=NEW."TenantId") THEN RAISE EXCEPTION 'Wrong event registration' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER event_ticket_link BEFORE INSERT OR UPDATE ON "EventTickets" FOR EACH ROW EXECUTE FUNCTION clubs_event_link_guard();
CREATE TRIGGER event_checkin_link BEFORE INSERT OR UPDATE ON "EventCheckins" FOR EACH ROW EXECUTE FUNCTION clubs_event_link_guard();
CREATE TRIGGER event_session_booking_link BEFORE INSERT OR UPDATE ON "EventSessionBookings" FOR EACH ROW EXECUTE FUNCTION clubs_event_link_guard();
"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS clubs_event_registration_guard() CASCADE; DROP FUNCTION IF EXISTS clubs_event_link_guard() CASCADE;"
            );
            migrationBuilder.DropForeignKey(
                name: "FK_GovernanceDocuments_Events_TenantId_EventId",
                table: "GovernanceDocuments"
            );

            migrationBuilder.DropTable(name: "EventAudiences");

            migrationBuilder.DropTable(name: "EventCheckins");

            migrationBuilder.DropTable(name: "EventCohosts");

            migrationBuilder.DropTable(name: "EventFeedbackForms");

            migrationBuilder.DropTable(name: "EventFeedbackResponses");

            migrationBuilder.DropTable(name: "EventRsvpHistory");

            migrationBuilder.DropTable(name: "EventSavedFilters");

            migrationBuilder.DropTable(name: "EventSessionBookings");

            migrationBuilder.DropTable(name: "EventStaff");

            migrationBuilder.DropTable(name: "EventTickets");

            migrationBuilder.DropTable(name: "EventSessions");

            migrationBuilder.DropTable(name: "EventRegistrations");

            migrationBuilder.DropTable(name: "EventInvitations");

            migrationBuilder.DropTable(name: "Events");

            migrationBuilder.DropTable(name: "EventTypes");

            migrationBuilder.DropIndex(
                name: "IX_GovernanceDocuments_TenantId_EventId",
                table: "GovernanceDocuments"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "events.checkin"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "events.configure"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "events.manage"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "events.reports"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "events.view"
            );

            migrationBuilder.DropColumn(name: "EventId", table: "GovernanceDocuments");
        }
    }
}
