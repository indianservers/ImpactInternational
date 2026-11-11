using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Mails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CommunicationId",
                table: "Mails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "Mails",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Mails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "DeadLetter",
                table: "Mails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderId",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SenderAssignmentId",
                table: "Mails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SenderId",
                table: "Mails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TemplateParameters",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CommunicationAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    Sender = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    BusinessId = table.Column<string>(type: "text", nullable: false),
                    Secret = table.Column<string>(type: "text", nullable: false),
                    WebhookSecret = table.Column<string>(type: "text", nullable: false),
                    VerifyTokenHash = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Verified = table.Column<bool>(type: "boolean", nullable: false),
                    Inbound = table.Column<bool>(type: "boolean", nullable: false),
                    PerMinute = table.Column<int>(type: "integer", nullable: false),
                    WindowAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WindowCount = table.Column<int>(type: "integer", nullable: false),
                    CallbackUrl = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationAccounts", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationAccounts_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationAccounts_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationAccounts_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationAudiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Rules = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationAudiences", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationAudiences_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationAudiences_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationAudiences_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberDirect = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalThreshold = table.Column<int>(type: "integer", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationPolicies", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationPolicies_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationPolicies_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationPolicies_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Transactional = table.Column<bool>(type: "boolean", nullable: false),
                    Promotional = table.Column<bool>(type: "boolean", nullable: false),
                    Direct = table.Column<bool>(type: "boolean", nullable: false),
                    Birthday = table.Column<bool>(type: "boolean", nullable: false),
                    QuietStart = table.Column<int>(type: "integer", nullable: false),
                    QuietEnd = table.Column<int>(type: "integer", nullable: false),
                    QuietEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationPreferences_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationRevisions", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationRevisions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationRevisions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationSuppressions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    AddressHash = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationSuppressions", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationSuppressions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationSuppressions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ProviderName = table.Column<string>(type: "text", nullable: false),
                    ProviderStatus = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationTemplates", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationTemplates_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationTemplates_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationTemplates_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationProviderEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKey = table.Column<string>(type: "text", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationProviderEvents", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationProviderEvents_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationProviderEvents_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationProviderEvents_CommunicationAccounts_TenantId_~",
                        columns: x => new { x.TenantId, x.AccountId },
                        principalTable: "CommunicationAccounts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommitteeId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ExternalThread = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: false),
                    AssignedId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastInboundAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Preview = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.UniqueConstraint("AK_Conversations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Conversations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_Committees_TenantId_CommitteeId",
                        columns: x => new { x.TenantId, x.CommitteeId },
                        principalTable: "Committees",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_CommunicationAccounts_TenantId_AccountId",
                        columns: x => new { x.TenantId, x.AccountId },
                        principalTable: "CommunicationAccounts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationAutomations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: false),
                    TemplateVersion = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Trigger = table.Column<string>(type: "text", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    DelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    DailyLimit = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationAutomations", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationAutomations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationAutomations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationAutomations_CommunicationAccounts_TenantId_Acc~",
                        columns: x => new { x.TenantId, x.AccountId },
                        principalTable: "CommunicationAccounts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationAutomations_CommunicationTemplates_TenantId_Te~",
                        columns: x => new { x.TenantId, x.TemplateId },
                        principalTable: "CommunicationTemplates",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationAutomations_GovernanceRequests_TenantId_Approv~",
                        columns: x => new { x.TenantId, x.ApprovalRequestId },
                        principalTable: "GovernanceRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationAutomations_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationBroadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Rules = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Acknowledgment = table.Column<bool>(type: "boolean", nullable: false),
                    Pinned = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SendAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    RepeatDays = table.Column<int>(type: "integer", nullable: false),
                    Cycles = table.Column<int>(type: "integer", nullable: false),
                    CurrentCycle = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationBroadcasts", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationBroadcasts_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationBroadcasts_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationBroadcasts_CommunicationAccounts_TenantId_Acco~",
                        columns: x => new { x.TenantId, x.AccountId },
                        principalTable: "CommunicationAccounts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationBroadcasts_CommunicationTemplates_TenantId_Tem~",
                        columns: x => new { x.TenantId, x.TemplateId },
                        principalTable: "CommunicationTemplates",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationBroadcasts_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationBroadcasts_GovernanceRequests_TenantId_Approva~",
                        columns: x => new { x.TenantId, x.ApprovalRequestId },
                        principalTable: "GovernanceRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationBroadcasts_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Staff = table.Column<bool>(type: "boolean", nullable: false),
                    Muted = table.Column<bool>(type: "boolean", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationParticipants", x => x.Id);
                    table.UniqueConstraint("AK_ConversationParticipants_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Conversations_TenantId_Conversatio~",
                        columns: x => new { x.TenantId, x.ConversationId },
                        principalTable: "Conversations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKey = table.Column<string>(type: "text", nullable: false),
                    MailId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationExecutions", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationExecutions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationExecutions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationExecutions_CommunicationAutomations_TenantId_A~",
                        columns: x => new { x.TenantId, x.AutomationId },
                        principalTable: "CommunicationAutomations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BroadcastId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplyToId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    Internal = table.Column<bool>(type: "boolean", nullable: false),
                    ClientKey = table.Column<string>(type: "text", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationMessages", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationMessages_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationMessages_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationMessages_CommunicationBroadcasts_TenantId_Broa~",
                        columns: x => new { x.TenantId, x.BroadcastId },
                        principalTable: "CommunicationBroadcasts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationMessages_Conversations_TenantId_ConversationId",
                        columns: x => new { x.TenantId, x.ConversationId },
                        principalTable: "Conversations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationMessages_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BroadcastId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Club = table.Column<string>(type: "text", nullable: false),
                    MemberNumber = table.Column<string>(type: "text", nullable: false),
                    QueuedCycle = table.Column<int>(type: "integer", nullable: false),
                    Exclusion = table.Column<string>(type: "text", nullable: false),
                    SeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationRecipients", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationRecipients_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationRecipients_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationRecipients_CommunicationBroadcasts_TenantId_Br~",
                        columns: x => new { x.TenantId, x.BroadcastId },
                        principalTable: "CommunicationBroadcasts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationRecipients_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationAttachments", x => x.Id);
                    table.UniqueConstraint("AK_CommunicationAttachments_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunicationAttachments_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationAttachments_CommunicationMessages_TenantId_Mes~",
                        columns: x => new { x.TenantId, x.MessageId },
                        principalTable: "CommunicationMessages",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                column: "Id",
                values: new object[]
                {
                    "communications.configure",
                    "communications.inbox",
                    "communications.manage",
                    "communications.reports",
                    "communications.send",
                    "communications.view"
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mails_CommunicationId",
                table: "Mails",
                column: "CommunicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mails_TenantId_AccountId",
                table: "Mails",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_Mails_TenantId_CommunicationId",
                table: "Mails",
                columns: new[] { "TenantId", "CommunicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAccounts_TenantId_OrgId",
                table: "CommunicationAccounts",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAttachments_TenantId_MessageId",
                table: "CommunicationAttachments",
                columns: new[] { "TenantId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAudiences_TenantId_OrgId",
                table: "CommunicationAudiences",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAutomations_TenantId_AccountId",
                table: "CommunicationAutomations",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAutomations_TenantId_ApprovalRequestId",
                table: "CommunicationAutomations",
                columns: new[] { "TenantId", "ApprovalRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAutomations_TenantId_OrgId",
                table: "CommunicationAutomations",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAutomations_TenantId_TemplateId",
                table: "CommunicationAutomations",
                columns: new[] { "TenantId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationBroadcasts_Status_SendAt",
                table: "CommunicationBroadcasts",
                columns: new[] { "Status", "SendAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationBroadcasts_TenantId_AccountId",
                table: "CommunicationBroadcasts",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationBroadcasts_TenantId_ApprovalRequestId",
                table: "CommunicationBroadcasts",
                columns: new[] { "TenantId", "ApprovalRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationBroadcasts_TenantId_EventId",
                table: "CommunicationBroadcasts",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationBroadcasts_TenantId_OrgId",
                table: "CommunicationBroadcasts",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationBroadcasts_TenantId_TemplateId",
                table: "CommunicationBroadcasts",
                columns: new[] { "TenantId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationExecutions_AutomationId_SourceKey",
                table: "CommunicationExecutions",
                columns: new[] { "AutomationId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationExecutions_TenantId_AutomationId",
                table: "CommunicationExecutions",
                columns: new[] { "TenantId", "AutomationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_BroadcastId_At",
                table: "CommunicationMessages",
                columns: new[] { "BroadcastId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_ConversationId_At_Id",
                table: "CommunicationMessages",
                columns: new[] { "ConversationId", "At", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_TenantId_BroadcastId",
                table: "CommunicationMessages",
                columns: new[] { "TenantId", "BroadcastId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_TenantId_ConversationId",
                table: "CommunicationMessages",
                columns: new[] { "TenantId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_TenantId_OrgId",
                table: "CommunicationMessages",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_TenantId_SenderId_ClientKey",
                table: "CommunicationMessages",
                columns: new[] { "TenantId", "SenderId", "ClientKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationPolicies_OrgId",
                table: "CommunicationPolicies",
                column: "OrgId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationPolicies_TenantId_OrgId",
                table: "CommunicationPolicies",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationPreferences_UserId_Channel",
                table: "CommunicationPreferences",
                columns: new[] { "UserId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationProviderEvents_AccountId_EventKey",
                table: "CommunicationProviderEvents",
                columns: new[] { "AccountId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationProviderEvents_TenantId_AccountId",
                table: "CommunicationProviderEvents",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationRecipients_BroadcastId_PersonId",
                table: "CommunicationRecipients",
                columns: new[] { "BroadcastId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationRecipients_BroadcastId_QueuedCycle_Id",
                table: "CommunicationRecipients",
                columns: new[] { "BroadcastId", "QueuedCycle", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationRecipients_PersonId",
                table: "CommunicationRecipients",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationRecipients_TenantId_BroadcastId",
                table: "CommunicationRecipients",
                columns: new[] { "TenantId", "BroadcastId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationRecipients_UserId_BroadcastId",
                table: "CommunicationRecipients",
                columns: new[] { "UserId", "BroadcastId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationRevisions_ResourceId_Version",
                table: "CommunicationRevisions",
                columns: new[] { "ResourceId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationSuppressions_TenantId_Channel_AddressHash",
                table: "CommunicationSuppressions",
                columns: new[] { "TenantId", "Channel", "AddressHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationTemplates_TenantId_OrgId",
                table: "CommunicationTemplates",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_ConversationId_UserId",
                table: "ConversationParticipants",
                columns: new[] { "ConversationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_TenantId_ConversationId",
                table: "ConversationParticipants",
                columns: new[] { "TenantId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_UserId_Active_ConversationId",
                table: "ConversationParticipants",
                columns: new[] { "UserId", "Active", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AccountId_ExternalThread",
                table: "Conversations",
                columns: new[] { "AccountId", "ExternalThread" },
                unique: true,
                filter: "\"ExternalThread\"<>''");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_AccountId",
                table: "Conversations",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_CommitteeId",
                table: "Conversations",
                columns: new[] { "TenantId", "CommitteeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_EventId",
                table: "Conversations",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_OrgId",
                table: "Conversations",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_UpdatedAt_Id",
                table: "Conversations",
                columns: new[] { "TenantId", "UpdatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Mails_CommunicationAccounts_TenantId_AccountId",
                table: "Mails",
                columns: new[] { "TenantId", "AccountId" },
                principalTable: "CommunicationAccounts",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Mails_CommunicationMessages_TenantId_CommunicationId",
                table: "Mails",
                columns: new[] { "TenantId", "CommunicationId" },
                principalTable: "CommunicationMessages",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""

INSERT INTO "Permissions"("Id") VALUES('communications.view'),('communications.send'),('communications.manage'),('communications.inbox'),('communications.reports'),('communications.configure') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions"("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE p."Id" LIKE 'communications.%' AND (r."Name" IN ('Association Super Admin','Platform Administrator') OR (r."Name" IN ('Club Manager','Club Secretary','Club President') AND p."Id"<>'communications.configure') OR (r."Name" IN ('Club Member','Applicant') AND p."Id"='communications.view')) ON CONFLICT DO NOTHING;
ALTER TABLE "CommunicationAccounts" ADD CONSTRAINT communication_account_channel CHECK (("Channel"='Email' AND "Provider"='SMTP') OR ("Channel"='WhatsApp' AND "Provider"='Meta') OR ("Channel"='SMS' AND "Provider"='Twilio'));
ALTER TABLE "CommunicationAccounts" ADD CONSTRAINT communication_account_rate CHECK ("PerMinute">0 AND "PerMinute"<=1000);
ALTER TABLE "CommunicationBroadcasts" ADD CONSTRAINT communication_broadcast_state CHECK ("Status" IN ('Draft','Review','Approved','Scheduled','Sending','Completed','Partially Completed','Failed','Rejected','Changes Requested','Cancelled','Archived'));
CREATE INDEX "IX_Mails_CommunicationDispatch" ON "Mails"("Channel","NextAttemptAt","Id") WHERE "SentAt" IS NULL AND NOT "Suppressed" AND NOT "DeadLetter";
CREATE INDEX "IX_CommunicationMessages_Provider" ON "CommunicationMessages"("TenantId","ProviderId") WHERE "ProviderId"<>'';
CREATE FUNCTION clubs_communication_revision_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Communication revisions are immutable' USING ERRCODE='23514';END $$;
CREATE TRIGGER communication_revision_immutable BEFORE UPDATE OR DELETE ON "CommunicationRevisions" FOR EACH ROW EXECUTE FUNCTION clubs_communication_revision_guard();
CREATE FUNCTION clubs_communication_broadcast_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF OLD."Status"<>'Draft' AND (NEW."Body",NEW."Subject",NEW."Rules",NEW."Channel",NEW."OrgId",NEW."CreatorId",NEW."AccountId",NEW."Purpose",NEW."ContentHash") IS DISTINCT FROM (OLD."Body",OLD."Subject",OLD."Rules",OLD."Channel",OLD."OrgId",OLD."CreatorId",OLD."AccountId",OLD."Purpose",OLD."ContentHash") THEN RAISE EXCEPTION 'Approved broadcast identity/content is immutable; duplicate for revision' USING ERRCODE='23514';END IF;RETURN NEW;END $$;
CREATE TRIGGER communication_broadcast_immutable BEFORE UPDATE ON "CommunicationBroadcasts" FOR EACH ROW EXECUTE FUNCTION clubs_communication_broadcast_guard();
CREATE FUNCTION clubs_communication_recipient_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF (NEW."BroadcastId",NEW."PersonId",NEW."UserId",NEW."Name",NEW."Club",NEW."MemberNumber") IS DISTINCT FROM (OLD."BroadcastId",OLD."PersonId",OLD."UserId",OLD."Name",OLD."Club",OLD."MemberNumber") THEN RAISE EXCEPTION 'Approved recipient identity snapshot is immutable' USING ERRCODE='23514';END IF;RETURN NEW;END $$;
CREATE TRIGGER communication_recipient_immutable BEFORE UPDATE ON "CommunicationRecipients" FOR EACH ROW EXECUTE FUNCTION clubs_communication_recipient_guard();

""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION clubs_communication_revision_guard() CASCADE;DROP FUNCTION clubs_communication_broadcast_guard() CASCADE;DROP FUNCTION clubs_communication_recipient_guard() CASCADE;DROP INDEX IF EXISTS \"IX_Mails_CommunicationDispatch\";");
            migrationBuilder.DropForeignKey(
                name: "FK_Mails_CommunicationAccounts_TenantId_AccountId",
                table: "Mails");

            migrationBuilder.DropForeignKey(
                name: "FK_Mails_CommunicationMessages_TenantId_CommunicationId",
                table: "Mails");

            migrationBuilder.DropTable(
                name: "CommunicationAttachments");

            migrationBuilder.DropTable(
                name: "CommunicationAudiences");

            migrationBuilder.DropTable(
                name: "CommunicationExecutions");

            migrationBuilder.DropTable(
                name: "CommunicationPolicies");

            migrationBuilder.DropTable(
                name: "CommunicationPreferences");

            migrationBuilder.DropTable(
                name: "CommunicationProviderEvents");

            migrationBuilder.DropTable(
                name: "CommunicationRecipients");

            migrationBuilder.DropTable(
                name: "CommunicationRevisions");

            migrationBuilder.DropTable(
                name: "CommunicationSuppressions");

            migrationBuilder.DropTable(
                name: "ConversationParticipants");

            migrationBuilder.DropTable(
                name: "CommunicationMessages");

            migrationBuilder.DropTable(
                name: "CommunicationAutomations");

            migrationBuilder.DropTable(
                name: "CommunicationBroadcasts");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "CommunicationTemplates");

            migrationBuilder.DropTable(
                name: "CommunicationAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Mails_CommunicationId",
                table: "Mails");

            migrationBuilder.DropIndex(
                name: "IX_Mails_TenantId_AccountId",
                table: "Mails");

            migrationBuilder.DropIndex(
                name: "IX_Mails_TenantId_CommunicationId",
                table: "Mails");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "communications.configure");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "communications.inbox");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "communications.manage");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "communications.reports");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "communications.send");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "communications.view");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "CommunicationId",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "DeadLetter",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "SenderAssignmentId",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "SenderId",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "TemplateParameters",
                table: "Mails");
        }
    }
}
