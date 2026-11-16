using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class IciRoleWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityProgram",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IciPortfolio",
                table: "Assignments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IciSlot",
                table: "Assignments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "IciPrivateProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingCareer = table.Column<string>(type: "text", nullable: false),
                    LeadershipCareer = table.Column<string>(type: "text", nullable: false),
                    ProtectedBankDetails = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IciPrivateProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_IciPrivateProfiles_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IciRegionCoverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IciRegionCoverages", x => x.Id);
                    table.UniqueConstraint("AK_IciRegionCoverages_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_IciRegionCoverages_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IciRegionCoverages_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IciRegionCoverages_Orgs_TenantId_RegionId",
                        columns: x => new { x.TenantId, x.RegionId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IciRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorRole = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetOrgId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Portfolio = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MemberCount = table.Column<int>(type: "integer", nullable: false),
                    VolunteerCount = table.Column<int>(type: "integer", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ReviewRoute = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    PaymentReference = table.Column<string>(type: "text", nullable: false),
                    PaymentVerifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IciRequests", x => x.Id);
                    table.UniqueConstraint("AK_IciRequests_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_IciRequests_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IciRequests_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IciDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorRole = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IciDecisions", x => x.Id);
                    table.UniqueConstraint("AK_IciDecisions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_IciDecisions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IciDecisions_IciRequests_TenantId_RequestId",
                        columns: x => new { x.TenantId, x.RequestId },
                        principalTable: "IciRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IciSubscriptionMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IciSubscriptionMembers", x => x.Id);
                    table.UniqueConstraint("AK_IciSubscriptionMembers_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_IciSubscriptionMembers_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IciSubscriptionMembers_IciRequests_TenantId_RequestId",
                        columns: x => new { x.TenantId, x.RequestId },
                        principalTable: "IciRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                column: "Id",
                values: new object[]
                {
                    "ici.review",
                    "ici.submit",
                    "ici.team",
                    "ici.view"
                });

            migrationBuilder.CreateIndex(
                name: "IX_IciDecisions_RequestId_Revision_Stage_ActorId",
                table: "IciDecisions",
                columns: new[] { "RequestId", "Revision", "Stage", "ActorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IciDecisions_TenantId_RequestId",
                table: "IciDecisions",
                columns: new[] { "TenantId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_IciRegionCoverages_AssignmentId_RegionId",
                table: "IciRegionCoverages",
                columns: new[] { "AssignmentId", "RegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IciRegionCoverages_TenantId_RegionId",
                table: "IciRegionCoverages",
                columns: new[] { "TenantId", "RegionId" });

            migrationBuilder.CreateIndex(
                name: "IX_IciRequests_TenantId_OrgId_Kind_Status",
                table: "IciRequests",
                columns: new[] { "TenantId", "OrgId", "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IciSubscriptionMembers_TenantId_OrgId_PersonId_PeriodKey",
                table: "IciSubscriptionMembers",
                columns: new[] { "TenantId", "OrgId", "PersonId", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IciSubscriptionMembers_TenantId_RequestId",
                table: "IciSubscriptionMembers",
                columns: new[] { "TenantId", "RequestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IciDecisions");

            migrationBuilder.DropTable(
                name: "IciPrivateProfiles");

            migrationBuilder.DropTable(
                name: "IciRegionCoverages");

            migrationBuilder.DropTable(
                name: "IciSubscriptionMembers");

            migrationBuilder.DropTable(
                name: "IciRequests");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "ici.review");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "ici.submit");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "ici.team");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "ici.view");

            migrationBuilder.DropColumn(
                name: "ActivityProgram",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IciPortfolio",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "IciSlot",
                table: "Assignments");
        }
    }
}
