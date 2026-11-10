using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MembershipWorkflows_TenantId",
                table: "MembershipWorkflows"
            );

            migrationBuilder.AddColumn<string>(
                name: "BrandingColor",
                table: "Orgs",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "ClubType",
                table: "Orgs",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Orgs",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<int>(
                name: "FiscalStartMonth",
                table: "Orgs",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Orgs",
                type: "double precision",
                nullable: true
            );

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Orgs",
                type: "double precision",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "MeetingDay",
                table: "Orgs",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "MeetingSchedule",
                table: "Orgs",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "MeetingVenue",
                table: "Orgs",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "OperatingPolicies",
                table: "Orgs",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Orgs",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "Orgs",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Registration",
                table: "Orgs",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "MembershipWorkflows",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<bool>(
                name: "AllowDelegation",
                table: "MembershipApprovalStages",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "ApplicationId",
                table: "MembershipApprovals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "GovernanceRequestId",
                table: "MembershipApprovals",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "PrincipalUserId",
                table: "MembershipApprovals",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "Assignments",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "ClubHealthConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadershipWeight = table.Column<int>(type: "integer", nullable: false),
                    MembershipWeight = table.Column<int>(type: "integer", nullable: false),
                    GovernanceWeight = table.Column<int>(type: "integer", nullable: false),
                    DataWeight = table.Column<int>(type: "integer", nullable: false),
                    MinimumActiveMembers = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    RequireClubChangeApproval = table.Column<bool>(
                        type: "boolean",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubHealthConfigurations", x => x.Id);
                    table.UniqueConstraint(
                        "AK_ClubHealthConfigurations_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_ClubHealthConfigurations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "CommitteeRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CanManage = table.Column<bool>(type: "boolean", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeRoles", x => x.Id);
                    table.UniqueConstraint(
                        "AK_CommitteeRoles_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_CommitteeRoles_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Committees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Committees", x => x.Id);
                    table.UniqueConstraint(
                        "AK_Committees_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_Committees_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Committees_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "GovernanceDelegations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegateId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernanceDelegations", x => x.Id);
                    table.UniqueConstraint(
                        "AK_GovernanceDelegations_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceDelegations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceDelegations_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceDelegations_Roles_TenantId_RoleId",
                        columns: x => new { x.TenantId, x.RoleId },
                        principalTable: "Roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "GovernanceResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EffectiveAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ProposerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernanceResolutions", x => x.Id);
                    table.UniqueConstraint(
                        "AK_GovernanceResolutions_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceResolutions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceResolutions_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "LeadershipTerms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Calendar = table.Column<string>(type: "text", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipTerms", x => x.Id);
                    table.UniqueConstraint(
                        "AK_LeadershipTerms_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_LeadershipTerms_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_LeadershipTerms_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "OfficerPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    DefaultMonths = table.Column<int>(type: "integer", nullable: false),
                    MaximumHolders = table.Column<int>(type: "integer", nullable: true),
                    Mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    RequireActiveMembership = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumMembershipDays = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ReportsToId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficerPositions", x => x.Id);
                    table.UniqueConstraint(
                        "AK_OfficerPositions_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_OfficerPositions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerPositions_Levels_TenantId_LevelId",
                        columns: x => new { x.TenantId, x.LevelId },
                        principalTable: "Levels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerPositions_OfficerPositions_TenantId_ReportsToId",
                        columns: x => new { x.TenantId, x.ReportsToId },
                        principalTable: "OfficerPositions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerPositions_Roles_TenantId_GrantRoleId",
                        columns: x => new { x.TenantId, x.GrantRoleId },
                        principalTable: "Roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "OrganizationHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Before = table.Column<string>(type: "text", nullable: false),
                    After = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationHistory", x => x.Id);
                    table.UniqueConstraint(
                        "AK_OrganizationHistory_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_OrganizationHistory_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OrganizationHistory_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "CommitteeActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitteeId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CommitteeActivities", x => x.Id);
                    table.UniqueConstraint(
                        "AK_CommitteeActivities_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_CommitteeActivities_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_CommitteeActivities_Committees_TenantId_CommitteeId",
                        columns: x => new { x.TenantId, x.CommitteeId },
                        principalTable: "Committees",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "CommitteeMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitteeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Responsibilities = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeMemberships", x => x.Id);
                    table.UniqueConstraint(
                        "AK_CommitteeMemberships_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_CommitteeMemberships_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_CommitteeMemberships_CommitteeRoles_TenantId_RoleId",
                        columns: x => new { x.TenantId, x.RoleId },
                        principalTable: "CommitteeRoles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_CommitteeMemberships_Committees_TenantId_CommitteeId",
                        columns: x => new { x.TenantId, x.CommitteeId },
                        principalTable: "Committees",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_CommitteeMemberships_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "GovernanceDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitteeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareWithSuccessors = table.Column<bool>(type: "boolean", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    RetainUntil = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Status = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernanceDocuments", x => x.Id);
                    table.UniqueConstraint(
                        "AK_GovernanceDocuments_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceDocuments_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceDocuments_Committees_TenantId_CommitteeId",
                        columns: x => new { x.TenantId, x.CommitteeId },
                        principalTable: "Committees",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceDocuments_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "OfficerAppointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: true),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EndsAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    EffectiveAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ProposerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficerAppointments", x => x.Id);
                    table.UniqueConstraint(
                        "AK_OfficerAppointments_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_OfficerAppointments_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerAppointments_GovernanceDocuments_TenantId_DocumentId",
                        columns: x => new { x.TenantId, x.DocumentId },
                        principalTable: "GovernanceDocuments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerAppointments_LeadershipTerms_TenantId_TermId",
                        columns: x => new { x.TenantId, x.TermId },
                        principalTable: "LeadershipTerms",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerAppointments_Memberships_TenantId_MembershipId",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerAppointments_OfficerPositions_TenantId_PositionId",
                        columns: x => new { x.TenantId, x.PositionId },
                        principalTable: "OfficerPositions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerAppointments_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerAppointments_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerAppointments_Roles_TenantId_GrantRoleId",
                        columns: x => new { x.TenantId, x.GrantRoleId },
                        principalTable: "Roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "LeadershipHandovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutgoingAppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomingAppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipHandovers", x => x.Id);
                    table.UniqueConstraint(
                        "AK_LeadershipHandovers_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_LeadershipHandovers_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_LeadershipHandovers_OfficerAppointments_TenantId_IncomingAp~",
                        columns: x => new { x.TenantId, x.IncomingAppointmentId },
                        principalTable: "OfficerAppointments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_LeadershipHandovers_OfficerAppointments_TenantId_OutgoingAp~",
                        columns: x => new { x.TenantId, x.OutgoingAppointmentId },
                        principalTable: "OfficerAppointments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_LeadershipHandovers_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "OfficerAppointmentHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficerAppointmentHistory", x => x.Id);
                    table.UniqueConstraint(
                        "AK_OfficerAppointmentHistory_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_OfficerAppointmentHistory_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_OfficerAppointmentHistory_OfficerAppointments_TenantId_Appo~",
                        columns: x => new { x.TenantId, x.AppointmentId },
                        principalTable: "OfficerAppointments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "GovernanceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentSequence = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommitteeId = table.Column<Guid>(type: "uuid", nullable: true),
                    HandoverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_GovernanceRequests", x => x.Id);
                    table.UniqueConstraint(
                        "AK_GovernanceRequests_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceRequests_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceRequests_Committees_TenantId_CommitteeId",
                        columns: x => new { x.TenantId, x.CommitteeId },
                        principalTable: "Committees",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceRequests_GovernanceDocuments_TenantId_DocumentId",
                        columns: x => new { x.TenantId, x.DocumentId },
                        principalTable: "GovernanceDocuments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceRequests_GovernanceResolutions_TenantId_Resolutio~",
                        columns: x => new { x.TenantId, x.ResolutionId },
                        principalTable: "GovernanceResolutions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceRequests_LeadershipHandovers_TenantId_HandoverId",
                        columns: x => new { x.TenantId, x.HandoverId },
                        principalTable: "LeadershipHandovers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceRequests_MembershipWorkflows_TenantId_WorkflowId",
                        columns: x => new { x.TenantId, x.WorkflowId },
                        principalTable: "MembershipWorkflows",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceRequests_OfficerAppointments_TenantId_Appointment~",
                        columns: x => new { x.TenantId, x.AppointmentId },
                        principalTable: "OfficerAppointments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_GovernanceRequests_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "HandoverChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandoverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverChecklistItems", x => x.Id);
                    table.UniqueConstraint(
                        "AK_HandoverChecklistItems_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_HandoverChecklistItems_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_HandoverChecklistItems_GovernanceDocuments_TenantId_Documen~",
                        columns: x => new { x.TenantId, x.DocumentId },
                        principalTable: "GovernanceDocuments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_HandoverChecklistItems_LeadershipHandovers_TenantId_Handove~",
                        columns: x => new { x.TenantId, x.HandoverId },
                        principalTable: "LeadershipHandovers",
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
                    "clubs.manage",
                    "clubs.view",
                    "committees.manage",
                    "committees.view",
                    "governance.approve",
                    "governance.configure",
                    "governance.documents",
                    "governance.reports",
                    "governance.restructure",
                    "leadership.manage",
                    "leadership.view",
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipWorkflows_TenantId_Kind",
                table: "MembershipWorkflows",
                columns: new[] { "TenantId", "Kind" },
                unique: true,
                filter: "\"Active\""
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApprovals_GovernanceRequestId_Revision_StageId_Ac~",
                table: "MembershipApprovals",
                columns: new[] { "GovernanceRequestId", "Revision", "StageId", "ActorId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApprovals_GovernanceRequestId_Revision_StageId_Pr~",
                table: "MembershipApprovals",
                columns: new[] { "GovernanceRequestId", "Revision", "StageId", "PrincipalUserId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApprovals_TenantId_GovernanceRequestId",
                table: "MembershipApprovals",
                columns: new[] { "TenantId", "GovernanceRequestId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId_AppointmentId",
                table: "Assignments",
                columns: new[] { "TenantId", "AppointmentId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ClubHealthConfigurations_TenantId",
                table: "ClubHealthConfigurations",
                column: "TenantId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeActivities_TenantId_CommitteeId",
                table: "CommitteeActivities",
                columns: new[] { "TenantId", "CommitteeId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMemberships_CommitteeId_Status_PersonId",
                table: "CommitteeMemberships",
                columns: new[] { "CommitteeId", "Status", "PersonId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMemberships_PersonId",
                table: "CommitteeMemberships",
                column: "PersonId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMemberships_TenantId_CommitteeId",
                table: "CommitteeMemberships",
                columns: new[] { "TenantId", "CommitteeId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMemberships_TenantId_RoleId",
                table: "CommitteeMemberships",
                columns: new[] { "TenantId", "RoleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeRoles_TenantId_Name",
                table: "CommitteeRoles",
                columns: new[] { "TenantId", "Name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Committees_TenantId_OrgId",
                table: "Committees",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Committees_TenantId_OrgId_Code",
                table: "Committees",
                columns: new[] { "TenantId", "OrgId", "Code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceDelegations_TenantId_OrgId",
                table: "GovernanceDelegations",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceDelegations_TenantId_RoleId",
                table: "GovernanceDelegations",
                columns: new[] { "TenantId", "RoleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceDocuments_TenantId_CommitteeId",
                table: "GovernanceDocuments",
                columns: new[] { "TenantId", "CommitteeId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceDocuments_TenantId_OrgId",
                table: "GovernanceDocuments",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceRequests_TenantId_AppointmentId",
                table: "GovernanceRequests",
                columns: new[] { "TenantId", "AppointmentId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceRequests_TenantId_CommitteeId",
                table: "GovernanceRequests",
                columns: new[] { "TenantId", "CommitteeId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceRequests_TenantId_DocumentId",
                table: "GovernanceRequests",
                columns: new[] { "TenantId", "DocumentId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceRequests_TenantId_HandoverId",
                table: "GovernanceRequests",
                columns: new[] { "TenantId", "HandoverId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceRequests_TenantId_OrgId",
                table: "GovernanceRequests",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceRequests_TenantId_OrgId_Status_CreatedAt",
                table: "GovernanceRequests",
                columns: new[] { "TenantId", "OrgId", "Status", "CreatedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceRequests_TenantId_ResolutionId",
                table: "GovernanceRequests",
                columns: new[] { "TenantId", "ResolutionId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceRequests_TenantId_WorkflowId",
                table: "GovernanceRequests",
                columns: new[] { "TenantId", "WorkflowId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceResolutions_TenantId_OrgId",
                table: "GovernanceResolutions",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceResolutions_TenantId_OrgId_Number",
                table: "GovernanceResolutions",
                columns: new[] { "TenantId", "OrgId", "Number" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_HandoverChecklistItems_TenantId_DocumentId",
                table: "HandoverChecklistItems",
                columns: new[] { "TenantId", "DocumentId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_HandoverChecklistItems_TenantId_HandoverId",
                table: "HandoverChecklistItems",
                columns: new[] { "TenantId", "HandoverId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipHandovers_TenantId_IncomingAppointmentId",
                table: "LeadershipHandovers",
                columns: new[] { "TenantId", "IncomingAppointmentId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipHandovers_TenantId_OrgId",
                table: "LeadershipHandovers",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipHandovers_TenantId_OutgoingAppointmentId",
                table: "LeadershipHandovers",
                columns: new[] { "TenantId", "OutgoingAppointmentId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipTerms_TenantId_OrgId",
                table: "LeadershipTerms",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerAppointmentHistory_TenantId_AppointmentId",
                table: "OfficerAppointmentHistory",
                columns: new[] { "TenantId", "AppointmentId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerAppointments_PersonId_StartsAt_EndsAt",
                table: "OfficerAppointments",
                columns: new[] { "PersonId", "StartsAt", "EndsAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerAppointments_TenantId_DocumentId",
                table: "OfficerAppointments",
                columns: new[] { "TenantId", "DocumentId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerAppointments_TenantId_GrantRoleId",
                table: "OfficerAppointments",
                columns: new[] { "TenantId", "GrantRoleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerAppointments_TenantId_MembershipId",
                table: "OfficerAppointments",
                columns: new[] { "TenantId", "MembershipId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerAppointments_TenantId_OrgId",
                table: "OfficerAppointments",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerAppointments_TenantId_OrgId_PositionId_StartsAt_Ends~",
                table: "OfficerAppointments",
                columns: new[] { "TenantId", "OrgId", "PositionId", "StartsAt", "EndsAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerAppointments_TenantId_OrgId_Status_EndsAt",
                table: "OfficerAppointments",
                columns: new[] { "TenantId", "OrgId", "Status", "EndsAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerAppointments_TenantId_PositionId",
                table: "OfficerAppointments",
                columns: new[] { "TenantId", "PositionId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerAppointments_TenantId_TermId",
                table: "OfficerAppointments",
                columns: new[] { "TenantId", "TermId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerPositions_TenantId_Code",
                table: "OfficerPositions",
                columns: new[] { "TenantId", "Code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerPositions_TenantId_GrantRoleId",
                table: "OfficerPositions",
                columns: new[] { "TenantId", "GrantRoleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerPositions_TenantId_LevelId",
                table: "OfficerPositions",
                columns: new[] { "TenantId", "LevelId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OfficerPositions_TenantId_ReportsToId",
                table: "OfficerPositions",
                columns: new[] { "TenantId", "ReportsToId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationHistory_TenantId_OrgId",
                table: "OrganizationHistory",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_OfficerAppointments_TenantId_AppointmentId",
                table: "Assignments",
                columns: new[] { "TenantId", "AppointmentId" },
                principalTable: "OfficerAppointments",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipApprovals_GovernanceRequests_TenantId_GovernanceR~",
                table: "MembershipApprovals",
                columns: new[] { "TenantId", "GovernanceRequestId" },
                principalTable: "GovernanceRequests",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict
            );
            migrationBuilder.Sql(
                """
ALTER TABLE "Orgs" DROP CONSTRAINT "CK_Org_Identity";
ALTER TABLE "Orgs" ADD CONSTRAINT "CK_Org_Identity" CHECK(length(trim("Name")) BETWEEN 2 AND 200 AND length("Code") BETWEEN 1 AND 40 AND "Status" IN ('Active','Pending','Archived','Proposed','Pending approval','Suspended','Inactive','Merged','Closed') AND "Id" IS DISTINCT FROM "ParentId");
ALTER TABLE "MembershipApprovals" ADD CONSTRAINT "CK_Approval_Target" CHECK(("ApplicationId" IS NULL) <> ("GovernanceRequestId" IS NULL));
ALTER TABLE "OfficerPositions" ADD CONSTRAINT "CK_Position_Rules" CHECK("DefaultMonths" BETWEEN 1 AND 120 AND ("MaximumHolders" IS NULL OR "MaximumHolders" BETWEEN 1 AND 100) AND "MinimumMembershipDays">=0 AND length("Title") BETWEEN 1 AND 120);
ALTER TABLE "OfficerAppointments" ADD CONSTRAINT "CK_Appointment_Term" CHECK("StartsAt"<"EndsAt" AND "EffectiveAt">="StartsAt" AND "EffectiveAt"<"EndsAt" AND "Status" IN ('Proposed','Pending approval','Approved','Scheduled','Active','Completed','Revoked','Cancelled'));
ALTER TABLE "LeadershipTerms" ADD CONSTRAINT "CK_Leadership_Term" CHECK("StartsAt"<"EndsAt" AND "Calendar" IN ('Calendar','Fiscal','Custom'));
ALTER TABLE "Committees" ADD CONSTRAINT "CK_Committee_Term" CHECK("StartsAt"<"EndsAt" AND "Status" IN ('Proposed','Pending approval','Active','Completed','Closed','Cancelled'));
ALTER TABLE "CommitteeMemberships" ADD CONSTRAINT "CK_Committee_Member_Term" CHECK("StartsAt"<"EndsAt" AND "Status" IN ('Active','Completed','Removed'));
ALTER TABLE "GovernanceRequests" ADD CONSTRAINT "CK_Governance_Status" CHECK("Status" IN ('Draft','Submitted','Under Review','Changes Requested','Approved','Rejected','Withdrawn','Cancelled'));
ALTER TABLE "GovernanceDocuments" ADD CONSTRAINT "CK_Governance_Document" CHECK(octet_length("Content")<=5242880 AND (octet_length("Content")>0 OR "Status"='Expired') AND "ContentType" IN ('application/pdf','image/png','image/jpeg'));
ALTER TABLE "ClubHealthConfigurations" ADD CONSTRAINT "CK_Health_Weights" CHECK("LeadershipWeight">=0 AND "MembershipWeight">=0 AND "GovernanceWeight">=0 AND "DataWeight">=0 AND "LeadershipWeight"+"MembershipWeight"+"GovernanceWeight"+"DataWeight"=100 AND "MinimumActiveMembers">0);
CREATE FUNCTION clubs_org_history() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 INSERT INTO "OrganizationHistory" ("Id","TenantId","OrgId","Before","After","ActorId","At") VALUES(gen_random_uuid(),NEW."TenantId",NEW."Id",CASE WHEN TG_OP='UPDATE' THEN to_jsonb(OLD)::text ELSE '{}' END,to_jsonb(NEW)::text,NULLIF(current_setting('clubs.actor',true),'')::uuid,now()); RETURN NULL; END $$;
CREATE TRIGGER org_change_history AFTER INSERT OR UPDATE ON "Orgs" FOR EACH ROW EXECUTE FUNCTION clubs_org_history();
CREATE TRIGGER org_history_immutable BEFORE UPDATE OR DELETE ON "OrganizationHistory" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE TRIGGER appointment_history_immutable BEFORE UPDATE OR DELETE ON "OfficerAppointmentHistory" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE TRIGGER committee_activity_immutable BEFORE UPDATE OR DELETE ON "CommitteeActivities" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();
CREATE FUNCTION clubs_appointment_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE maximum integer;
BEGIN
 PERFORM pg_advisory_xact_lock(hashtext(NEW."TenantId"::text));
 IF TG_OP='UPDATE' AND (NEW."PersonId"<>OLD."PersonId" OR NEW."OrgId"<>OLD."OrgId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."PositionId"<>OLD."PositionId") THEN RAISE EXCEPTION 'Appointment identity is immutable' USING ERRCODE='23514'; END IF;
 IF NOT EXISTS(SELECT 1 FROM "OfficerPositions" p JOIN "Orgs" o ON o."Id"=NEW."OrgId" AND o."TenantId"=p."TenantId" AND o."LevelId"=p."LevelId" WHERE p."Id"=NEW."PositionId" AND p."TenantId"=NEW."TenantId") THEN RAISE EXCEPTION 'Position level mismatch' USING ERRCODE='23514'; END IF;
 IF NEW."MembershipId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "Memberships" m WHERE m."Id"=NEW."MembershipId" AND m."PersonId"=NEW."PersonId" AND m."TenantId"=NEW."TenantId") THEN RAISE EXCEPTION 'Eligibility identity mismatch' USING ERRCODE='23514'; END IF;
 IF NEW."Status" IN ('Approved','Scheduled','Active') THEN
 SELECT "MaximumHolders" INTO maximum FROM "OfficerPositions" WHERE "Id"=NEW."PositionId";
 IF maximum IS NOT NULL AND EXISTS(SELECT 1 FROM (SELECT NEW."StartsAt" AS point UNION SELECT a."StartsAt" FROM "OfficerAppointments" a WHERE a."Id"<>NEW."Id" AND a."TenantId"=NEW."TenantId" AND a."OrgId"=NEW."OrgId" AND a."PositionId"=NEW."PositionId" AND a."Status" IN ('Approved','Scheduled','Active') AND a."StartsAt">NEW."StartsAt" AND a."StartsAt"<NEW."EndsAt") points WHERE (SELECT count(*) FROM "OfficerAppointments" a WHERE a."Id"<>NEW."Id" AND a."TenantId"=NEW."TenantId" AND a."OrgId"=NEW."OrgId" AND a."PositionId"=NEW."PositionId" AND a."Status" IN ('Approved','Scheduled','Active') AND a."StartsAt"<=points.point AND a."EndsAt">points.point)>=maximum) THEN RAISE EXCEPTION 'Office capacity exceeded' USING ERRCODE='23514'; END IF;
 END IF; RETURN NEW;
END $$;
CREATE TRIGGER appointment_integrity BEFORE INSERT OR UPDATE ON "OfficerAppointments" FOR EACH ROW EXECUTE FUNCTION clubs_appointment_guard();
CREATE FUNCTION clubs_office_assignment_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."AppointmentId" IS NOT NULL AND (NEW."MembershipId" IS NOT NULL OR NOT EXISTS(SELECT 1 FROM "OfficerAppointments" a JOIN "Persons" p ON p."Id"=a."PersonId" WHERE a."Id"=NEW."AppointmentId" AND a."TenantId"=NEW."TenantId" AND a."OrgId"=NEW."OrgId" AND a."GrantRoleId"=NEW."RoleId" AND p."UserId"=NEW."UserId" AND a."Status"='Active' AND a."StartsAt"<=now() AND a."EffectiveAt"<=now() AND a."EndsAt">now())) THEN RAISE EXCEPTION 'Invalid appointment-sourced access' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;
CREATE TRIGGER office_assignment_integrity BEFORE INSERT OR UPDATE ON "Assignments" FOR EACH ROW EXECUTE FUNCTION clubs_office_assignment_guard();
CREATE INDEX "IX_Appointment_Current" ON "OfficerAppointments" ("TenantId","OrgId","PositionId","EndsAt","StartsAt") WHERE "Status"='Active';
CREATE INDEX "IX_Appointment_Worker" ON "OfficerAppointments" ("Status","EffectiveAt","EndsAt") WHERE "Status" IN ('Approved','Scheduled','Active');
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE p."Id" IN ('clubs.view','clubs.manage','leadership.view','leadership.manage','governance.configure','governance.approve','governance.restructure','governance.documents','committees.view','committees.manage','governance.reports') AND (r."Name"='Association Super Admin' OR r."Name" IN ('Club President','Club Secretary','Club Manager') AND p."Id" NOT IN ('governance.configure','governance.restructure') AND (p."Id"<>'governance.approve' OR r."Name" IN ('Club President','Club Secretary'))) ON CONFLICT DO NOTHING;

"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER org_change_history ON \"Orgs\"; DROP FUNCTION clubs_org_history(); DROP TRIGGER office_assignment_integrity ON \"Assignments\"; DROP FUNCTION clubs_office_assignment_guard(); DROP TRIGGER appointment_integrity ON \"OfficerAppointments\"; DROP FUNCTION clubs_appointment_guard(); ALTER TABLE \"MembershipApprovals\" DROP CONSTRAINT \"CK_Approval_Target\";"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_OfficerAppointments_TenantId_AppointmentId",
                table: "Assignments"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipApprovals_GovernanceRequests_TenantId_GovernanceR~",
                table: "MembershipApprovals"
            );

            migrationBuilder.DropTable(name: "ClubHealthConfigurations");

            migrationBuilder.DropTable(name: "CommitteeActivities");

            migrationBuilder.DropTable(name: "CommitteeMemberships");

            migrationBuilder.DropTable(name: "GovernanceDelegations");

            migrationBuilder.DropTable(name: "GovernanceRequests");

            migrationBuilder.DropTable(name: "HandoverChecklistItems");

            migrationBuilder.DropTable(name: "OfficerAppointmentHistory");

            migrationBuilder.DropTable(name: "OrganizationHistory");

            migrationBuilder.DropTable(name: "CommitteeRoles");

            migrationBuilder.DropTable(name: "GovernanceResolutions");

            migrationBuilder.DropTable(name: "LeadershipHandovers");

            migrationBuilder.DropTable(name: "OfficerAppointments");

            migrationBuilder.DropTable(name: "GovernanceDocuments");

            migrationBuilder.DropTable(name: "LeadershipTerms");

            migrationBuilder.DropTable(name: "OfficerPositions");

            migrationBuilder.DropTable(name: "Committees");

            migrationBuilder.DropIndex(
                name: "IX_MembershipWorkflows_TenantId_Kind",
                table: "MembershipWorkflows"
            );

            migrationBuilder.DropIndex(
                name: "IX_MembershipApprovals_GovernanceRequestId_Revision_StageId_Ac~",
                table: "MembershipApprovals"
            );

            migrationBuilder.DropIndex(
                name: "IX_MembershipApprovals_GovernanceRequestId_Revision_StageId_Pr~",
                table: "MembershipApprovals"
            );

            migrationBuilder.DropIndex(
                name: "IX_MembershipApprovals_TenantId_GovernanceRequestId",
                table: "MembershipApprovals"
            );

            migrationBuilder.DropIndex(
                name: "IX_Assignments_TenantId_AppointmentId",
                table: "Assignments"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "clubs.manage"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "clubs.view"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "committees.manage"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "committees.view"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "governance.approve"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "governance.configure"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "governance.documents"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "governance.reports"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "governance.restructure"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "leadership.manage"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "leadership.view"
            );

            migrationBuilder.DropColumn(name: "BrandingColor", table: "Orgs");

            migrationBuilder.DropColumn(name: "ClubType", table: "Orgs");

            migrationBuilder.DropColumn(name: "Description", table: "Orgs");

            migrationBuilder.DropColumn(name: "FiscalStartMonth", table: "Orgs");

            migrationBuilder.DropColumn(name: "Latitude", table: "Orgs");

            migrationBuilder.DropColumn(name: "Longitude", table: "Orgs");

            migrationBuilder.DropColumn(name: "MeetingDay", table: "Orgs");

            migrationBuilder.DropColumn(name: "MeetingSchedule", table: "Orgs");

            migrationBuilder.DropColumn(name: "MeetingVenue", table: "Orgs");

            migrationBuilder.DropColumn(name: "OperatingPolicies", table: "Orgs");

            migrationBuilder.DropColumn(name: "PostalCode", table: "Orgs");

            migrationBuilder.DropColumn(name: "PreferredLanguage", table: "Orgs");

            migrationBuilder.DropColumn(name: "Registration", table: "Orgs");

            migrationBuilder.DropColumn(name: "Kind", table: "MembershipWorkflows");

            migrationBuilder.DropColumn(name: "AllowDelegation", table: "MembershipApprovalStages");

            migrationBuilder.DropColumn(name: "GovernanceRequestId", table: "MembershipApprovals");

            migrationBuilder.DropColumn(name: "PrincipalUserId", table: "MembershipApprovals");

            migrationBuilder.DropColumn(name: "AppointmentId", table: "Assignments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ApplicationId",
                table: "MembershipApprovals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MembershipWorkflows_TenantId",
                table: "MembershipWorkflows",
                column: "TenantId",
                unique: true,
                filter: "\"Active\""
            );
        }
    }
}
