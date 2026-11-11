using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class ClubOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalRequestId",
                table: "GovernanceDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentVersion",
                table: "GovernanceDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Folder",
                table: "GovernanceDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                table: "GovernanceDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OperationId",
                table: "GovernanceDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharedUsers",
                table: "GovernanceDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "GovernanceDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "GovernanceDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CertificateTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Layout = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateTemplates", x => x.Id);
                    table.UniqueConstraint("AK_CertificateTemplates_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CertificateTemplates_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificateTemplates_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClubMeetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    OrganizerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChairId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecretaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommitteeId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    OnlineUrl = table.Column<string>(type: "text", nullable: false),
                    Visibility = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuorumMinimum = table.Column<int>(type: "integer", nullable: false),
                    QuorumPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    VotingCategories = table.Column<string>(type: "text", nullable: false),
                    Agenda = table.Column<string>(type: "text", nullable: false),
                    AgendaStatus = table.Column<string>(type: "text", nullable: false),
                    RequiredPeople = table.Column<string>(type: "text", nullable: false),
                    OptionalPeople = table.Column<string>(type: "text", nullable: false),
                    AgendaRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubMeetings", x => x.Id);
                    table.UniqueConstraint("AK_ClubMeetings_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ClubMeetings_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClubMeetings_Committees_TenantId_CommitteeId",
                        columns: x => new { x.TenantId, x.CommitteeId },
                        principalTable: "Committees",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClubMeetings_EventSessions_TenantId_SessionId",
                        columns: x => new { x.TenantId, x.SessionId },
                        principalTable: "EventSessions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClubMeetings_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClubMeetings_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.Id);
                    table.UniqueConstraint("AK_DocumentVersions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_GovernanceDocuments_TenantId_DocumentId",
                        columns: x => new { x.TenantId, x.DocumentId },
                        principalTable: "GovernanceDocuments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<string>(type: "text", nullable: false),
                    Aggregation = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    EvidenceRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMetrics", x => x.Id);
                    table.UniqueConstraint("AK_ImpactMetrics_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMetrics_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImpactMetrics_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationRevisions", x => x.Id);
                    table.UniqueConstraint("AK_OperationRevisions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_OperationRevisions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationRevisions_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommitteeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Blocker = table.Column<string>(type: "text", nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: false),
                    Dependencies = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemindedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationTasks", x => x.Id);
                    table.UniqueConstraint("AK_OperationTasks_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_OperationTasks_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationTasks_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Objectives = table.Column<string>(type: "text", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitteeId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TargetBeneficiaries = table.Column<int>(type: "integer", nullable: false),
                    PlannedVolunteers = table.Column<int>(type: "integer", nullable: false),
                    PlannedHours = table.Column<decimal>(type: "numeric", nullable: false),
                    EstimatedBudget = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceProjects", x => x.Id);
                    table.UniqueConstraint("AK_ServiceProjects_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ServiceProjects_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceProjects_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Skills = table.Column<string>(type: "text", nullable: false),
                    Interests = table.Column<string>(type: "text", nullable: false),
                    Availability = table.Column<string>(type: "text", nullable: false),
                    Training = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerProfiles", x => x.Id);
                    table.UniqueConstraint("AK_VolunteerProfiles_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_VolunteerProfiles_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerProfiles_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerProfiles_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IssuedCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersion = table.Column<long>(type: "bigint", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ProtectedToken = table.Column<string>(type: "text", nullable: false),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    Layout = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IssuerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublicName = table.Column<bool>(type: "boolean", nullable: false),
                    RevocationReason = table.Column<string>(type: "text", nullable: false),
                    ReplacesId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedCertificates", x => x.Id);
                    table.UniqueConstraint("AK_IssuedCertificates_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_IssuedCertificates_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssuedCertificates_CertificateTemplates_TenantId_TemplateId",
                        columns: x => new { x.TenantId, x.TemplateId },
                        principalTable: "CertificateTemplates",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssuedCertificates_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssuedCertificates_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAttendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Rsvp = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAttendances", x => x.Id);
                    table.UniqueConstraint("AK_MeetingAttendances_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_MeetingAttendances_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingAttendances_ClubMeetings_TenantId_MeetingId",
                        columns: x => new { x.TenantId, x.MeetingId },
                        principalTable: "ClubMeetings",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingAttendances_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingAttendances_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMinutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PreparedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMinutes", x => x.Id);
                    table.UniqueConstraint("AK_MeetingMinutes_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_MeetingMinutes_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingMinutes_ClubMeetings_TenantId_MeetingId",
                        columns: x => new { x.TenantId, x.MeetingId },
                        principalTable: "ClubMeetings",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingMinutes_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationComments", x => x.Id);
                    table.UniqueConstraint("AK_OperationComments_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_OperationComments_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationComments_OperationTasks_TenantId_TaskId",
                        columns: x => new { x.TenantId, x.TaskId },
                        principalTable: "OperationTasks",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationComments_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectActivities", x => x.Id);
                    table.UniqueConstraint("AK_ProjectActivities_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProjectActivities_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectActivities_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectActivities_ServiceProjects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "ServiceProjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectImpacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKey = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    Estimate = table.Column<bool>(type: "boolean", nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectImpacts", x => x.Id);
                    table.UniqueConstraint("AK_ProjectImpacts_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProjectImpacts_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectImpacts_ImpactMetrics_TenantId_MetricId",
                        columns: x => new { x.TenantId, x.MetricId },
                        principalTable: "ImpactMetrics",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectImpacts_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectImpacts_ServiceProjects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "ServiceProjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectOrganizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Contribution = table.Column<string>(type: "text", nullable: false),
                    EstimatedContribution = table.Column<decimal>(type: "numeric", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectOrganizations", x => x.Id);
                    table.UniqueConstraint("AK_ProjectOrganizations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProjectOrganizations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectOrganizations_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectOrganizations_ServiceProjects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "ServiceProjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    RequiredTraining = table.Column<string>(type: "text", nullable: false),
                    ApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerShifts", x => x.Id);
                    table.UniqueConstraint("AK_VolunteerShifts_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_VolunteerShifts_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerShifts_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerShifts_ProjectActivities_TenantId_ActivityId",
                        columns: x => new { x.TenantId, x.ActivityId },
                        principalTable: "ProjectActivities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerShifts_ServiceProjects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "ServiceProjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CheckIn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckOut = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerAssignments", x => x.Id);
                    table.UniqueConstraint("AK_VolunteerAssignments_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_VolunteerShifts_TenantId_ShiftId",
                        columns: x => new { x.TenantId, x.ShiftId },
                        principalTable: "VolunteerShifts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerHours", x => x.Id);
                    table.UniqueConstraint("AK_VolunteerHours_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_VolunteerHours_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerHours_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerHours_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerHours_ServiceProjects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "ServiceProjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerHours_VolunteerAssignments_TenantId_AssignmentId",
                        columns: x => new { x.TenantId, x.AssignmentId },
                        principalTable: "VolunteerAssignments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                column: "Id",
                values: new object[]
                {
                    "operations.certificates",
                    "operations.documents",
                    "operations.manage",
                    "operations.verify",
                    "operations.view"
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateTemplates_TenantId_OrgId_UpdatedAt",
                table: "CertificateTemplates",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMeetings_SeriesId_StartsAt",
                table: "ClubMeetings",
                columns: new[] { "SeriesId", "StartsAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubMeetings_TenantId_CommitteeId",
                table: "ClubMeetings",
                columns: new[] { "TenantId", "CommitteeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMeetings_TenantId_EventId",
                table: "ClubMeetings",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMeetings_TenantId_OrgId_StartsAt",
                table: "ClubMeetings",
                columns: new[] { "TenantId", "OrgId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMeetings_TenantId_OrgId_UpdatedAt",
                table: "ClubMeetings",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMeetings_TenantId_SessionId",
                table: "ClubMeetings",
                columns: new[] { "TenantId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId_Number",
                table: "DocumentVersions",
                columns: new[] { "DocumentId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_TenantId_DocumentId",
                table: "DocumentVersions",
                columns: new[] { "TenantId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_TenantId_OrgId_UpdatedAt",
                table: "DocumentVersions",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImpactMetrics_TenantId_OrgId_UpdatedAt",
                table: "ImpactMetrics",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificates_PersonId",
                table: "IssuedCertificates",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificates_TenantId_Number",
                table: "IssuedCertificates",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificates_TenantId_OrgId_UpdatedAt",
                table: "IssuedCertificates",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificates_TenantId_TemplateId",
                table: "IssuedCertificates",
                columns: new[] { "TenantId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificates_TokenHash",
                table: "IssuedCertificates",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendances_MeetingId_PersonId",
                table: "MeetingAttendances",
                columns: new[] { "MeetingId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendances_PersonId",
                table: "MeetingAttendances",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendances_TenantId_MeetingId",
                table: "MeetingAttendances",
                columns: new[] { "TenantId", "MeetingId" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendances_TenantId_OrgId_UpdatedAt",
                table: "MeetingAttendances",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutes_MeetingId_Revision",
                table: "MeetingMinutes",
                columns: new[] { "MeetingId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutes_TenantId_MeetingId",
                table: "MeetingMinutes",
                columns: new[] { "TenantId", "MeetingId" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutes_TenantId_OrgId_UpdatedAt",
                table: "MeetingMinutes",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationComments_TenantId_OrgId_UpdatedAt",
                table: "OperationComments",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationComments_TenantId_TaskId",
                table: "OperationComments",
                columns: new[] { "TenantId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationRevisions_TenantId_OrgId_UpdatedAt",
                table: "OperationRevisions",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationTasks_TenantId_OrgId_UpdatedAt",
                table: "OperationTasks",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationTasks_TenantId_PersonId_Status_DueAt",
                table: "OperationTasks",
                columns: new[] { "TenantId", "PersonId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActivities_TenantId_OrgId_UpdatedAt",
                table: "ProjectActivities",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActivities_TenantId_ProjectId",
                table: "ProjectActivities",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectImpacts_ProjectId_MetricId_SourceKey",
                table: "ProjectImpacts",
                columns: new[] { "ProjectId", "MetricId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectImpacts_TenantId_MetricId",
                table: "ProjectImpacts",
                columns: new[] { "TenantId", "MetricId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectImpacts_TenantId_OrgId_UpdatedAt",
                table: "ProjectImpacts",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectImpacts_TenantId_ProjectId",
                table: "ProjectImpacts",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectOrganizations_ProjectId_OrgId",
                table: "ProjectOrganizations",
                columns: new[] { "ProjectId", "OrgId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectOrganizations_TenantId_OrgId_UpdatedAt",
                table: "ProjectOrganizations",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectOrganizations_TenantId_ProjectId",
                table: "ProjectOrganizations",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceProjects_TenantId_OrgId_UpdatedAt",
                table: "ServiceProjects",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_PersonId",
                table: "VolunteerAssignments",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_ShiftId_PersonId",
                table: "VolunteerAssignments",
                columns: new[] { "ShiftId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_TenantId_OrgId_UpdatedAt",
                table: "VolunteerAssignments",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_TenantId_ShiftId",
                table: "VolunteerAssignments",
                columns: new[] { "TenantId", "ShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerHours_PersonId",
                table: "VolunteerHours",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerHours_TenantId_AssignmentId",
                table: "VolunteerHours",
                columns: new[] { "TenantId", "AssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerHours_TenantId_OrgId_UpdatedAt",
                table: "VolunteerHours",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerHours_TenantId_PersonId_StartsAt_EndsAt",
                table: "VolunteerHours",
                columns: new[] { "TenantId", "PersonId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerHours_TenantId_ProjectId",
                table: "VolunteerHours",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerProfiles_PersonId",
                table: "VolunteerProfiles",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerProfiles_TenantId_OrgId_UpdatedAt",
                table: "VolunteerProfiles",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerProfiles_TenantId_PersonId",
                table: "VolunteerProfiles",
                columns: new[] { "TenantId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerShifts_TenantId_ActivityId",
                table: "VolunteerShifts",
                columns: new[] { "TenantId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerShifts_TenantId_OrgId_UpdatedAt",
                table: "VolunteerShifts",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerShifts_TenantId_ProjectId",
                table: "VolunteerShifts",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.Sql("""
            UPDATE "GovernanceDocuments" SET "CurrentVersion"=1,"Visibility"='Organization',"SharedUsers"='[]',"Folder"='General' WHERE "ObjectKey"='';
            INSERT INTO "RolePermissions"("Id","RoleId","PermissionId")
            SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p
            WHERE p."Id" LIKE 'operations.%' AND (r."Name" IN ('Association Super Admin','Club President','Club Secretary','Club Manager') OR (p."Id"='operations.view' AND r."Name" IN ('Club Member','Applicant')))
            AND NOT EXISTS(SELECT 1 FROM "RolePermissions" x WHERE x."RoleId"=r."Id" AND x."PermissionId"=p."Id");
            ALTER TABLE "ClubMeetings" ADD CONSTRAINT "CK_MeetingDates" CHECK ("EndsAt">"StartsAt" AND "QuorumMinimum">=0 AND "QuorumPercent" BETWEEN 0 AND 100);
            ALTER TABLE "VolunteerHours" ADD CONSTRAINT "CK_HourInterval" CHECK ("EndsAt">"StartsAt" AND "Hours">=0 AND "BreakMinutes">=0);
            ALTER TABLE "VolunteerShifts" ADD CONSTRAINT "CK_ShiftCapacity" CHECK ("Capacity">0 AND "EndsAt">"StartsAt");
            ALTER TABLE "ProjectImpacts" ADD CONSTRAINT "CK_ImpactValue" CHECK ("Value">=0);
            CREATE FUNCTION guard_operation_history() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Operational revisions are immutable'; END $$;
            CREATE TRIGGER operation_history_immutable BEFORE UPDATE OR DELETE ON "OperationRevisions" FOR EACH ROW EXECUTE FUNCTION guard_operation_history();
            CREATE FUNCTION guard_issued_certificate() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF OLD."Snapshot" IS DISTINCT FROM NEW."Snapshot" OR OLD."Layout" IS DISTINCT FROM NEW."Layout" OR OLD."PersonId" IS DISTINCT FROM NEW."PersonId" OR OLD."Number" IS DISTINCT FROM NEW."Number" OR OLD."TokenHash" IS DISTINCT FROM NEW."TokenHash" THEN RAISE EXCEPTION 'Certificate identity and content are immutable'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER certificate_content_immutable BEFORE UPDATE ON "IssuedCertificates" FOR EACH ROW EXECUTE FUNCTION guard_issued_certificate();
            CREATE FUNCTION guard_minutes_body() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF OLD."Status" IN ('Review','Published') AND OLD."Body" IS DISTINCT FROM NEW."Body" THEN RAISE EXCEPTION 'Reviewed minutes require a new revision'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER minutes_body_immutable BEFORE UPDATE ON "MeetingMinutes" FOR EACH ROW EXECUTE FUNCTION guard_minutes_body();
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS guard_operation_history() CASCADE; DROP FUNCTION IF EXISTS guard_issued_certificate() CASCADE; DROP FUNCTION IF EXISTS guard_minutes_body() CASCADE;");
            migrationBuilder.DropTable(
                name: "DocumentVersions");

            migrationBuilder.DropTable(
                name: "IssuedCertificates");

            migrationBuilder.DropTable(
                name: "MeetingAttendances");

            migrationBuilder.DropTable(
                name: "MeetingMinutes");

            migrationBuilder.DropTable(
                name: "OperationComments");

            migrationBuilder.DropTable(
                name: "OperationRevisions");

            migrationBuilder.DropTable(
                name: "ProjectImpacts");

            migrationBuilder.DropTable(
                name: "ProjectOrganizations");

            migrationBuilder.DropTable(
                name: "VolunteerHours");

            migrationBuilder.DropTable(
                name: "VolunteerProfiles");

            migrationBuilder.DropTable(
                name: "CertificateTemplates");

            migrationBuilder.DropTable(
                name: "ClubMeetings");

            migrationBuilder.DropTable(
                name: "OperationTasks");

            migrationBuilder.DropTable(
                name: "ImpactMetrics");

            migrationBuilder.DropTable(
                name: "VolunteerAssignments");

            migrationBuilder.DropTable(
                name: "VolunteerShifts");

            migrationBuilder.DropTable(
                name: "ProjectActivities");

            migrationBuilder.DropTable(
                name: "ServiceProjects");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "operations.certificates");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "operations.documents");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "operations.manage");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "operations.verify");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "operations.view");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                table: "GovernanceDocuments");

            migrationBuilder.DropColumn(
                name: "CurrentVersion",
                table: "GovernanceDocuments");

            migrationBuilder.DropColumn(
                name: "Folder",
                table: "GovernanceDocuments");

            migrationBuilder.DropColumn(
                name: "ObjectKey",
                table: "GovernanceDocuments");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "GovernanceDocuments");

            migrationBuilder.DropColumn(
                name: "SharedUsers",
                table: "GovernanceDocuments");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "GovernanceDocuments");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "GovernanceDocuments");
        }
    }
}
