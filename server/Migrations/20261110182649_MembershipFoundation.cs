using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class MembershipFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "UserProfiles",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Mails",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<Guid>(
                name: "OrgId",
                table: "Mails",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<bool>(
                name: "Suppressed",
                table: "Mails",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Mails",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "MembershipId",
                table: "Assignments",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "MailDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MailId = table.Column<Guid>(type: "uuid", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailDeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailDeliveryAttempts_Mails_MailId",
                        column: x => x.MailId,
                        principalTable: "Mails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MemberSavedViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Configuration = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberSavedViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberSavedViews_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ValidityMonths = table.Column<int>(type: "integer", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipCategories", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MembershipCategories_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MembershipCategories_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipPolicies",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowMultipleClubs = table.Column<bool>(type: "boolean", nullable: false),
                    SourceReviewRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AssociationReviewRequired = table.Column<bool>(
                        type: "boolean",
                        nullable: false
                    ),
                    FinancialClearanceRequired = table.Column<bool>(
                        type: "boolean",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipPolicies", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_MembershipPolicies_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipWorkflows", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MembershipWorkflows_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MembershipWorkflows_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Persons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalName = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    SearchName = table.Column<string>(type: "text", nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    Anniversary = table.Column<DateOnly>(type: "date", nullable: true),
                    Company = table.Column<string>(type: "text", nullable: false),
                    Designation = table.Column<string>(type: "text", nullable: false),
                    Industry = table.Column<string>(type: "text", nullable: false),
                    Profession = table.Column<string>(type: "text", nullable: false),
                    BusinessCategory = table.Column<string>(type: "text", nullable: false),
                    WorkLocation = table.Column<string>(type: "text", nullable: false),
                    Website = table.Column<string>(type: "text", nullable: false),
                    Networking = table.Column<string>(type: "text", nullable: false),
                    Photo = table.Column<byte[]>(type: "bytea", nullable: true),
                    PhotoType = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Persons_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MemberImportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Mapping = table.Column<string>(type: "text", nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ProcessedRows = table.Column<int>(type: "integer", nullable: false),
                    ImportedRows = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    RequestKey = table.Column<string>(type: "text", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    CompletedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberImportJobs", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MemberImportJobs_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MemberImportJobs_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MemberImportJobs_MembershipCategories_TenantId_CategoryId",
                        columns: x => new { x.TenantId, x.CategoryId },
                        principalTable: "MembershipCategories",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MemberImportJobs_Orgs_TenantId_ClubId",
                        columns: x => new { x.TenantId, x.ClubId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ProtectedToken = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    UsageLimit = table.Column<int>(type: "integer", nullable: false),
                    Uses = table.Column<int>(type: "integer", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipInvitations", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MembershipInvitations_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MembershipInvitations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipInvitations_MembershipCategories_TenantId_Categor~",
                        columns: x => new { x.TenantId, x.CategoryId },
                        principalTable: "MembershipCategories",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipInvitations_Orgs_TenantId_ClubId",
                        columns: x => new { x.TenantId, x.ClubId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipApprovalStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    RequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipApprovalStages", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MembershipApprovalStages_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApprovalStages_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApprovalStages_MembershipWorkflows_TenantId_Workf~",
                        columns: x => new { x.TenantId, x.WorkflowId },
                        principalTable: "MembershipWorkflows",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MemberActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberActivities", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MemberActivities_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MemberActivities_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MemberActivities_Orgs_TenantId_ClubId",
                        columns: x => new { x.TenantId, x.ClubId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MemberActivities_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MemberRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Relationship = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberRelationships", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MemberRelationships_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MemberRelationships_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MemberRelationships_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MemberRelationships_Persons_RelatedPersonId",
                        column: x => x.RelatedPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "PersonAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    PostalCode = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonAddresses_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "PersonContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    NormalizedValue = table.Column<string>(type: "text", nullable: false),
                    Verified = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonContacts_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "PersonPrivacy",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    DirectoryVisible = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayEmail = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayPhone = table.Column<bool>(type: "boolean", nullable: false),
                    ProfessionalVisible = table.Column<bool>(type: "boolean", nullable: false),
                    BirthdayAnnouncements = table.Column<bool>(type: "boolean", nullable: false),
                    Marketing = table.Column<bool>(type: "boolean", nullable: false),
                    PhotoVisible = table.Column<bool>(type: "boolean", nullable: false),
                    AllowContactRequests = table.Column<bool>(type: "boolean", nullable: false),
                    CardNameVisible = table.Column<bool>(type: "boolean", nullable: false),
                    TransactionalEmail = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonPrivacy", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_PersonPrivacy_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "PersonTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonTags_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MemberImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    Candidates = table.Column<string>(type: "text", nullable: false),
                    ExistingPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreateSeparate = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberImportRows", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MemberImportRows_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MemberImportRows_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MemberImportRows_MemberImportJobs_TenantId_JobId",
                        columns: x => new { x.TenantId, x.JobId },
                        principalTable: "MemberImportJobs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: true),
                    Statement = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CurrentSequence = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    SubmittedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipApplications", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MembershipApplications_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApplications_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApplications_MembershipCategories_TenantId_Catego~",
                        columns: x => new { x.TenantId, x.CategoryId },
                        principalTable: "MembershipCategories",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApplications_MembershipInvitations_TenantId_Invit~",
                        columns: x => new { x.TenantId, x.InvitationId },
                        principalTable: "MembershipInvitations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApplications_MembershipWorkflows_TenantId_Workflo~",
                        columns: x => new { x.TenantId, x.WorkflowId },
                        principalTable: "MembershipWorkflows",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApplications_Orgs_TenantId_ClubId",
                        columns: x => new { x.TenantId, x.ClubId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApplications_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApplications_UserProfiles_ApplicantUserId",
                        column: x => x.ApplicantUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "PersonLinkRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewReason = table.Column<string>(type: "text", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonLinkRequests", x => x.Id);
                    table.UniqueConstraint(
                        "AK_PersonLinkRequests_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_PersonLinkRequests_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_PersonLinkRequests_MembershipInvitations_TenantId_Invitatio~",
                        columns: x => new { x.TenantId, x.InvitationId },
                        principalTable: "MembershipInvitations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_PersonLinkRequests_Orgs_TenantId_ClubId",
                        columns: x => new { x.TenantId, x.ClubId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_PersonLinkRequests_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_PersonLinkRequests_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipStageRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipStageRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipStageRoles_MembershipApprovalStages_TenantId_Stag~",
                        columns: x => new { x.TenantId, x.StageId },
                        principalTable: "MembershipApprovalStages",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipStageRoles_Roles_TenantId_RoleId",
                        columns: x => new { x.TenantId, x.RoleId },
                        principalTable: "Roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipApprovals", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MembershipApprovals_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApprovals_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApprovals_MembershipApplications_TenantId_Applica~",
                        columns: x => new { x.TenantId, x.ApplicationId },
                        principalTable: "MembershipApplications",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipApprovals_MembershipApprovalStages_TenantId_Stage~",
                        columns: x => new { x.TenantId, x.StageId },
                        principalTable: "MembershipApprovalStages",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    FinancialHold = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    SourceApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportRowId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => x.Id);
                    table.UniqueConstraint(
                        "AK_Memberships_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_Memberships_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Memberships_MemberImportRows_TenantId_ImportRowId",
                        columns: x => new { x.TenantId, x.ImportRowId },
                        principalTable: "MemberImportRows",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Memberships_MembershipApplications_TenantId_SourceApplicati~",
                        columns: x => new { x.TenantId, x.SourceApplicationId },
                        principalTable: "MembershipApplications",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Memberships_MembershipCategories_TenantId_CategoryId",
                        columns: x => new { x.TenantId, x.CategoryId },
                        principalTable: "MembershipCategories",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Memberships_Orgs_TenantId_ClubId",
                        columns: x => new { x.TenantId, x.ClubId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Memberships_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "DigitalCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ProtectedToken = table.Column<string>(type: "text", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    RevokedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalCards", x => x.Id);
                    table.UniqueConstraint(
                        "AK_DigitalCards_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_DigitalCards_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_DigitalCards_Memberships_TenantId_MembershipId",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MemberContactRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberContactRequests", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MemberContactRequests_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MemberContactRequests_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MemberContactRequests_Memberships_TenantId_MembershipId",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    RetainUntil = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipDocuments", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MembershipDocuments_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MembershipDocuments_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipDocuments_Memberships_TenantId_MembershipId",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<string>(type: "text", nullable: false),
                    NewStatus = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    RecordedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipHistory", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MembershipHistory_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MembershipHistory_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipHistory_Memberships_TenantId_MembershipId",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipRenewals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestKey = table.Column<string>(type: "text", nullable: false),
                    PreviousEnd = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    NewEnd = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipRenewals", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MembershipRenewals_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MembershipRenewals_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipRenewals_Memberships_TenantId_MembershipId",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MembershipTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    SourceRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AssociationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ClearanceRequired = table.Column<bool>(type: "boolean", nullable: false),
                    FinancialCleared = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipTransfers", x => x.Id);
                    table.UniqueConstraint(
                        "AK_MembershipTransfers_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_MembershipTransfers_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipTransfers_MembershipCategories_TenantId_CategoryId",
                        columns: x => new { x.TenantId, x.CategoryId },
                        principalTable: "MembershipCategories",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipTransfers_Memberships_TenantId_DestinationMembers~",
                        columns: x => new { x.TenantId, x.DestinationMembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipTransfers_Memberships_TenantId_MembershipId",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MembershipTransfers_Orgs_TenantId_DestinationClubId",
                        columns: x => new { x.TenantId, x.DestinationClubId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "DigitalCardVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: true),
                    Valid = table.Column<bool>(type: "boolean", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalCardVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalCardVerifications_DigitalCards_CardId",
                        column: x => x.CardId,
                        principalTable: "DigitalCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TransferDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferDecisions", x => x.Id);
                    table.UniqueConstraint(
                        "AK_TransferDecisions_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_TransferDecisions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_TransferDecisions_MembershipTransfers_TenantId_TransferId",
                        columns: x => new { x.TenantId, x.TransferId },
                        principalTable: "MembershipTransfers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "Body", "Subject" },
                values: new object[,]
                {
                    { "application.approved", "{detail}", "ClubSphere · application approved" },
                    { "application.changes", "{detail}", "ClubSphere · application changes" },
                    { "application.rejected", "{detail}", "ClubSphere · application rejected" },
                    { "application.submitted", "{detail}", "ClubSphere · application submitted" },
                    { "approval.request", "{detail}", "ClubSphere · approval request" },
                    { "contact.request", "{detail}", "ClubSphere · contact request" },
                    { "contact.response", "{detail}", "ClubSphere · contact response" },
                    { "import.completed", "{detail}", "ClubSphere · import completed" },
                    { "membership.activated", "{detail}", "ClubSphere · membership activated" },
                    { "membership.invitation", "{detail}", "ClubSphere · membership invitation" },
                    { "membership.renewed", "{detail}", "ClubSphere · membership renewed" },
                    { "transfer.status", "{detail}", "ClubSphere · transfer status" },
                }
            );

            migrationBuilder.InsertData(
                table: "Permissions",
                column: "Id",
                values: new object[]
                {
                    "directory.view",
                    "members.approve",
                    "members.cards",
                    "members.contacts.view",
                    "members.documents",
                    "members.export",
                    "members.import",
                    "members.invite",
                    "members.link",
                    "members.manage",
                    "members.personal.view",
                    "members.transfer.approve",
                    "members.view",
                    "members.workflow.manage",
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId_MembershipId",
                table: "Assignments",
                columns: new[] { "TenantId", "MembershipId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCards_MembershipId",
                table: "DigitalCards",
                column: "MembershipId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCards_TenantId_MembershipId",
                table: "DigitalCards",
                columns: new[] { "TenantId", "MembershipId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCards_TokenHash",
                table: "DigitalCards",
                column: "TokenHash",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCardVerifications_At",
                table: "DigitalCardVerifications",
                column: "At"
            );

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCardVerifications_CardId",
                table: "DigitalCardVerifications",
                column: "CardId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MailDeliveryAttempts_MailId",
                table: "MailDeliveryAttempts",
                column: "MailId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivities_PersonId",
                table: "MemberActivities",
                column: "PersonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivities_TenantId_ClubId",
                table: "MemberActivities",
                columns: new[] { "TenantId", "ClubId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivities_TenantId_PersonId_At",
                table: "MemberActivities",
                columns: new[] { "TenantId", "PersonId", "At" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberContactRequests_TenantId_MembershipId",
                table: "MemberContactRequests",
                columns: new[] { "TenantId", "MembershipId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberImportJobs_Status_LeaseUntil",
                table: "MemberImportJobs",
                columns: new[] { "Status", "LeaseUntil" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberImportJobs_TenantId_CategoryId",
                table: "MemberImportJobs",
                columns: new[] { "TenantId", "CategoryId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberImportJobs_TenantId_ClubId",
                table: "MemberImportJobs",
                columns: new[] { "TenantId", "ClubId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberImportJobs_TenantId_CreatorId_RequestKey",
                table: "MemberImportJobs",
                columns: new[] { "TenantId", "CreatorId", "RequestKey" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberImportRows_JobId_RowNumber",
                table: "MemberImportRows",
                columns: new[] { "JobId", "RowNumber" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberImportRows_TenantId_JobId",
                table: "MemberImportRows",
                columns: new[] { "TenantId", "JobId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberRelationships_PersonId",
                table: "MemberRelationships",
                column: "PersonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberRelationships_RelatedPersonId",
                table: "MemberRelationships",
                column: "RelatedPersonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberSavedViews_TenantId_UserId_Name",
                table: "MemberSavedViews",
                columns: new[] { "TenantId", "UserId", "Name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MemberSavedViews_UserId",
                table: "MemberSavedViews",
                column: "UserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_ApplicantUserId",
                table: "MembershipApplications",
                column: "ApplicantUserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_InvitationId_ApplicantUserId",
                table: "MembershipApplications",
                columns: new[] { "InvitationId", "ApplicantUserId" },
                unique: true,
                filter: "\"InvitationId\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_PersonId_ClubId_CategoryId",
                table: "MembershipApplications",
                columns: new[] { "PersonId", "ClubId", "CategoryId" },
                unique: true,
                filter: "\"Status\" IN ('Draft','Submitted','Under Review','Changes Requested')"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_TenantId_CategoryId",
                table: "MembershipApplications",
                columns: new[] { "TenantId", "CategoryId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_TenantId_ClubId_Status_CurrentSequen~",
                table: "MembershipApplications",
                columns: new[] { "TenantId", "ClubId", "Status", "CurrentSequence" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_TenantId_InvitationId",
                table: "MembershipApplications",
                columns: new[] { "TenantId", "InvitationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_TenantId_WorkflowId",
                table: "MembershipApplications",
                columns: new[] { "TenantId", "WorkflowId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApprovals_ApplicationId_Revision_StageId_ActorId",
                table: "MembershipApprovals",
                columns: new[] { "ApplicationId", "Revision", "StageId", "ActorId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApprovals_TenantId_ApplicationId",
                table: "MembershipApprovals",
                columns: new[] { "TenantId", "ApplicationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApprovals_TenantId_StageId",
                table: "MembershipApprovals",
                columns: new[] { "TenantId", "StageId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApprovalStages_TenantId_WorkflowId",
                table: "MembershipApprovalStages",
                columns: new[] { "TenantId", "WorkflowId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApprovalStages_WorkflowId_Sequence",
                table: "MembershipApprovalStages",
                columns: new[] { "WorkflowId", "Sequence" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipCategories_TenantId_Code",
                table: "MembershipCategories",
                columns: new[] { "TenantId", "Code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipDocuments_TenantId_MembershipId_At",
                table: "MembershipDocuments",
                columns: new[] { "TenantId", "MembershipId", "At" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipHistory_TenantId_MembershipId_EffectiveAt",
                table: "MembershipHistory",
                columns: new[] { "TenantId", "MembershipId", "EffectiveAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvitations_TenantId_CategoryId",
                table: "MembershipInvitations",
                columns: new[] { "TenantId", "CategoryId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvitations_TenantId_ClubId",
                table: "MembershipInvitations",
                columns: new[] { "TenantId", "ClubId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvitations_TokenHash",
                table: "MembershipInvitations",
                column: "TokenHash",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRenewals_MembershipId_RequestKey",
                table: "MembershipRenewals",
                columns: new[] { "MembershipId", "RequestKey" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRenewals_TenantId_MembershipId",
                table: "MembershipRenewals",
                columns: new[] { "TenantId", "MembershipId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_ImportRowId",
                table: "Memberships",
                column: "ImportRowId",
                unique: true,
                filter: "\"ImportRowId\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_Number",
                table: "Memberships",
                column: "Number",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_PersonId",
                table: "Memberships",
                column: "PersonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_SourceApplicationId",
                table: "Memberships",
                column: "SourceApplicationId",
                unique: true,
                filter: "\"SourceApplicationId\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_CategoryId",
                table: "Memberships",
                columns: new[] { "TenantId", "CategoryId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_ClubId_Status_Id",
                table: "Memberships",
                columns: new[] { "TenantId", "ClubId", "Status", "Id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_ImportRowId",
                table: "Memberships",
                columns: new[] { "TenantId", "ImportRowId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_JoinedAt",
                table: "Memberships",
                columns: new[] { "TenantId", "JoinedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_PersonId_ClubId_CategoryId",
                table: "Memberships",
                columns: new[] { "TenantId", "PersonId", "ClubId", "CategoryId" },
                unique: true,
                filter: "\"Status\" IN ('Active','Inactive','Suspended','Expired')"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_SourceApplicationId",
                table: "Memberships",
                columns: new[] { "TenantId", "SourceApplicationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_Status_EndsAt",
                table: "Memberships",
                columns: new[] { "TenantId", "Status", "EndsAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipStageRoles_StageId_RoleId",
                table: "MembershipStageRoles",
                columns: new[] { "StageId", "RoleId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipStageRoles_TenantId_RoleId",
                table: "MembershipStageRoles",
                columns: new[] { "TenantId", "RoleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipStageRoles_TenantId_StageId",
                table: "MembershipStageRoles",
                columns: new[] { "TenantId", "StageId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTransfers_MembershipId",
                table: "MembershipTransfers",
                column: "MembershipId",
                unique: true,
                filter: "\"Status\" IN ('PendingSource','PendingDestination','PendingAssociation','Scheduled')"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTransfers_TenantId_CategoryId",
                table: "MembershipTransfers",
                columns: new[] { "TenantId", "CategoryId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTransfers_TenantId_DestinationClubId",
                table: "MembershipTransfers",
                columns: new[] { "TenantId", "DestinationClubId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTransfers_TenantId_DestinationMembershipId",
                table: "MembershipTransfers",
                columns: new[] { "TenantId", "DestinationMembershipId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTransfers_TenantId_MembershipId",
                table: "MembershipTransfers",
                columns: new[] { "TenantId", "MembershipId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipWorkflows_TenantId",
                table: "MembershipWorkflows",
                column: "TenantId",
                unique: true,
                filter: "\"Active\""
            );

            migrationBuilder.CreateIndex(
                name: "IX_PersonAddresses_City_PersonId",
                table: "PersonAddresses",
                columns: new[] { "City", "PersonId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_PersonAddresses_PersonId",
                table: "PersonAddresses",
                column: "PersonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PersonContacts_NormalizedValue",
                table: "PersonContacts",
                column: "NormalizedValue"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PersonContacts_PersonId",
                table: "PersonContacts",
                column: "PersonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PersonLinkRequests_InvitationId_UserId",
                table: "PersonLinkRequests",
                columns: new[] { "InvitationId", "UserId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_PersonLinkRequests_PersonId_UserId",
                table: "PersonLinkRequests",
                columns: new[] { "PersonId", "UserId" },
                unique: true,
                filter: "\"Status\"='Pending'"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PersonLinkRequests_TenantId_ClubId",
                table: "PersonLinkRequests",
                columns: new[] { "TenantId", "ClubId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_PersonLinkRequests_TenantId_InvitationId",
                table: "PersonLinkRequests",
                columns: new[] { "TenantId", "InvitationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_PersonLinkRequests_UserId",
                table: "PersonLinkRequests",
                column: "UserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Persons_UserId",
                table: "Persons",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PersonTags_PersonId_Kind_Value",
                table: "PersonTags",
                columns: new[] { "PersonId", "Kind", "Value" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_TransferDecisions_TenantId_TransferId",
                table: "TransferDecisions",
                columns: new[] { "TenantId", "TransferId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TransferDecisions_TransferId_Stage",
                table: "TransferDecisions",
                columns: new[] { "TransferId", "Stage" },
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_Memberships_TenantId_MembershipId",
                table: "Assignments",
                columns: new[] { "TenantId", "MembershipId" },
                principalTable: "Memberships",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict
            );
            migrationBuilder.Sql(
                """

CREATE SEQUENCE membership_numbers;
UPDATE "UserProfiles" SET "Theme"='light' WHERE "Theme"='';
CREATE INDEX "IX_Persons_SearchName_Trigram" ON "Persons" USING gin ("SearchName" gin_trgm_ops);
CREATE INDEX "IX_Persons_LegalName_Normalized" ON "Persons" (lower("LegalName"));
ALTER TABLE "Persons" ADD CONSTRAINT "CK_Person_Name" CHECK(length(trim("LegalName")) BETWEEN 2 AND 200 AND length("DisplayName") BETWEEN 1 AND 200);
ALTER TABLE "Memberships" ADD CONSTRAINT "CK_Membership_Status" CHECK("Status" IN ('Active','Inactive','Suspended','Expired','Transferred','Resigned','Terminated','Deceased'));
ALTER TABLE "Memberships" ADD CONSTRAINT "CK_Membership_Dates" CHECK("EndsAt" IS NULL OR "EndsAt">"JoinedAt");
ALTER TABLE "MembershipApplications" ADD CONSTRAINT "CK_Application_Status" CHECK("Status" IN ('Draft','Submitted','Under Review','Changes Requested','Approved','Rejected','Withdrawn','Cancelled'));
ALTER TABLE "MembershipApprovalStages" ADD CONSTRAINT "CK_Stage_Quorum" CHECK("RequiredApprovals" BETWEEN 1 AND 20 AND "Sequence">0);
ALTER TABLE "MembershipInvitations" ADD CONSTRAINT "CK_Invitation_Limits" CHECK("UsageLimit" BETWEEN 1 AND 20 AND "Uses" BETWEEN 0 AND "UsageLimit" AND "ExpiresAt">"CreatedAt");
ALTER TABLE "MembershipDocuments" ADD CONSTRAINT "CK_Document_Size" CHECK(octet_length("Content") BETWEEN 1 AND 5242880 AND "ContentType" IN ('application/pdf','image/png','image/jpeg'));
ALTER TABLE "MemberImportJobs" ADD CONSTRAINT "CK_Import_Size" CHECK(octet_length("Payload")<=10485760);
CREATE FUNCTION clubs_membership_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 PERFORM pg_advisory_xact_lock(hashtext(NEW."PersonId"::text));
 IF NOT EXISTS(SELECT 1 FROM "Orgs" o JOIN "Levels" l ON o."LevelId"=l."Id" AND o."TenantId"=l."TenantId" WHERE o."Id"=NEW."ClubId" AND o."TenantId"=NEW."TenantId" AND l."IsClub") THEN RAISE EXCEPTION 'Membership target must be a club in its association' USING ERRCODE='23514'; END IF;
 IF TG_OP='UPDATE' AND (NEW."TenantId"<>OLD."TenantId" OR NEW."PersonId"<>OLD."PersonId" OR NEW."ClubId"<>OLD."ClubId") THEN RAISE EXCEPTION 'Affiliation identity is immutable; use a transfer' USING ERRCODE='23514'; END IF;
 IF NEW."Status" IN ('Active','Inactive','Suspended','Expired') AND EXISTS(SELECT 1 FROM "MembershipPolicies" p WHERE p."TenantId"=NEW."TenantId" AND NOT p."AllowMultipleClubs") AND EXISTS(SELECT 1 FROM "Memberships" m WHERE m."TenantId"=NEW."TenantId" AND m."PersonId"=NEW."PersonId" AND m."ClubId"<>NEW."ClubId" AND m."Id"<>NEW."Id" AND m."Status" IN ('Active','Inactive','Suspended','Expired')) THEN RAISE EXCEPTION 'Single-club association policy violated' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER membership_integrity_guard BEFORE INSERT OR UPDATE ON "Memberships" FOR EACH ROW EXECUTE FUNCTION clubs_membership_guard();
CREATE FUNCTION clubs_membership_history_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Membership decisions and effective-dated history are append-only' USING ERRCODE='23514'; END $$;
CREATE TRIGGER membership_history_immutable BEFORE UPDATE OR DELETE ON "MembershipHistory" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE TRIGGER membership_approval_immutable BEFORE UPDATE OR DELETE ON "MembershipApprovals" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE TRIGGER transfer_decision_immutable BEFORE UPDATE OR DELETE ON "TransferDecisions" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
INSERT INTO "MembershipPolicies" ("TenantId","AllowMultipleClubs","SourceReviewRequired","AssociationReviewRequired","FinancialClearanceRequired") SELECT "Id",true,true,false,false FROM "Tenants";
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE ((EXISTS(SELECT 1 FROM "RolePermissions" g WHERE g."RoleId"=r."Id" AND g."PermissionId"='roles.manage') AND EXISTS(SELECT 1 FROM "RolePermissions" g WHERE g."RoleId"=r."Id" AND g."PermissionId"='settings.manage') AND EXISTS(SELECT 1 FROM "RolePermissions" g WHERE g."RoleId"=r."Id" AND g."PermissionId"='roles.assign')) AND (p."Id" LIKE 'members.%' OR p."Id"='directory.view')) OR (r."Name" IN ('Club Member','Guest / Applicant') AND p."Id"='directory.view') OR (r."Name" IN ('Club Secretary','Club President','Club Manager','Club Treasurer','International Administrator','National Administrator','District Administrator','Regional Administrator','Zone Administrator') AND p."Id" IN ('members.view','members.manage','members.contacts.view','members.export','members.import','members.invite','members.transfer.approve','members.link','members.documents','members.cards','directory.view')) OR (r."Name" IN ('Club Secretary','Club President') AND p."Id"='members.approve') ON CONFLICT ("RoleId","PermissionId") DO NOTHING;

"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER membership_integrity_guard ON \"Memberships\"; DROP FUNCTION clubs_membership_guard(); DROP FUNCTION clubs_membership_history_guard() CASCADE; DROP SEQUENCE membership_numbers;"
            );
            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_Memberships_TenantId_MembershipId",
                table: "Assignments"
            );

            migrationBuilder.DropTable(name: "DigitalCardVerifications");

            migrationBuilder.DropTable(name: "MailDeliveryAttempts");

            migrationBuilder.DropTable(name: "MemberActivities");

            migrationBuilder.DropTable(name: "MemberContactRequests");

            migrationBuilder.DropTable(name: "MemberRelationships");

            migrationBuilder.DropTable(name: "MemberSavedViews");

            migrationBuilder.DropTable(name: "MembershipApprovals");

            migrationBuilder.DropTable(name: "MembershipDocuments");

            migrationBuilder.DropTable(name: "MembershipHistory");

            migrationBuilder.DropTable(name: "MembershipPolicies");

            migrationBuilder.DropTable(name: "MembershipRenewals");

            migrationBuilder.DropTable(name: "MembershipStageRoles");

            migrationBuilder.DropTable(name: "NotificationTemplates");

            migrationBuilder.DropTable(name: "PersonAddresses");

            migrationBuilder.DropTable(name: "PersonContacts");

            migrationBuilder.DropTable(name: "PersonLinkRequests");

            migrationBuilder.DropTable(name: "PersonPrivacy");

            migrationBuilder.DropTable(name: "PersonTags");

            migrationBuilder.DropTable(name: "TransferDecisions");

            migrationBuilder.DropTable(name: "DigitalCards");

            migrationBuilder.DropTable(name: "MembershipApprovalStages");

            migrationBuilder.DropTable(name: "MembershipTransfers");

            migrationBuilder.DropTable(name: "Memberships");

            migrationBuilder.DropTable(name: "MemberImportRows");

            migrationBuilder.DropTable(name: "MembershipApplications");

            migrationBuilder.DropTable(name: "MemberImportJobs");

            migrationBuilder.DropTable(name: "MembershipInvitations");

            migrationBuilder.DropTable(name: "MembershipWorkflows");

            migrationBuilder.DropTable(name: "Persons");

            migrationBuilder.DropTable(name: "MembershipCategories");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_TenantId_MembershipId",
                table: "Assignments"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "directory.view"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.approve"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.cards"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.contacts.view"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.documents"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.export"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.import"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.invite"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.link"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.manage"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.personal.view"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.transfer.approve"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.view"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "members.workflow.manage"
            );

            migrationBuilder.DropColumn(name: "Theme", table: "UserProfiles");

            migrationBuilder.DropColumn(name: "Body", table: "Mails");

            migrationBuilder.DropColumn(name: "OrgId", table: "Mails");

            migrationBuilder.DropColumn(name: "Recipient", table: "Mails");

            migrationBuilder.DropColumn(name: "Subject", table: "Mails");

            migrationBuilder.DropColumn(name: "Suppressed", table: "Mails");

            migrationBuilder.DropColumn(name: "TenantId", table: "Mails");

            migrationBuilder.DropColumn(name: "MembershipId", table: "Assignments");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Mails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );
        }
    }
}
