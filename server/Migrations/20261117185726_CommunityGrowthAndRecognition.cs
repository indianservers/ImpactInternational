using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class CommunityGrowthAndRecognition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "CommunityPosts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "CommunityPosts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "CommunityPosts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewOrgId",
                table: "CommunityPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "CommunityPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "CommunityPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceEventId",
                table: "CommunityPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "CommunityMedia",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "CommunityMedia",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                table: "CommunityMedia",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityParticipations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consent = table.Column<string>(type: "text", nullable: false),
                    PublicName = table.Column<bool>(type: "boolean", nullable: false),
                    Verified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Evidence = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityParticipations", x => x.Id);
                    table.UniqueConstraint("AK_ActivityParticipations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ActivityParticipations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityParticipations_CommunityPosts_TenantId_PostId",
                        columns: x => new { x.TenantId, x.PostId },
                        principalTable: "CommunityPosts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityParticipations_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessOpportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Subcategory = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    ClosesOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessOpportunities", x => x.Id);
                    table.UniqueConstraint("AK_BusinessOpportunities_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_BusinessOpportunities_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityDigestPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastQueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextDigestAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityDigestPreferences", x => x.Id);
                    table.UniqueConstraint("AK_CommunityDigestPreferences_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunityDigestPreferences_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityFollows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Target = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityFollows", x => x.Id);
                    table.UniqueConstraint("AK_CommunityFollows_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunityFollows_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirePublicApproval = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPolicies", x => x.Id);
                    table.UniqueConstraint("AK_CommunityPolicies_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunityPolicies_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    NameSnapshot = table.Column<string>(type: "text", nullable: false),
                    ActivitySnapshot = table.Column<string>(type: "text", nullable: false),
                    ActivityOn = table.Column<DateOnly>(type: "date", nullable: false),
                    OrganizationSnapshot = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RevocationReason = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ProtectedToken = table.Column<string>(type: "text", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityCertificates", x => x.Id);
                    table.UniqueConstraint("AK_ActivityCertificates_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ActivityCertificates_ActivityParticipations_TenantId_Partic~",
                        columns: x => new { x.TenantId, x.ParticipationId },
                        principalTable: "ActivityParticipations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityCertificates_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpportunityResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityResponses", x => x.Id);
                    table.UniqueConstraint("AK_OpportunityResponses_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_OpportunityResponses_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpportunityResponses_BusinessOpportunities_TenantId_Opportu~",
                        columns: x => new { x.TenantId, x.OpportunityId },
                        principalTable: "BusinessOpportunities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_TenantId_SourceEventId",
                table: "CommunityPosts",
                columns: new[] { "TenantId", "SourceEventId" },
                unique: true,
                filter: "\"SourceEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMedia_PostId_SourceDocumentId",
                table: "CommunityMedia",
                columns: new[] { "PostId", "SourceDocumentId" },
                unique: true,
                filter: "\"SourceDocumentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCertificates_ParticipationId",
                table: "ActivityCertificates",
                column: "ParticipationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCertificates_TenantId_ParticipationId",
                table: "ActivityCertificates",
                columns: new[] { "TenantId", "ParticipationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityCertificates_TokenHash",
                table: "ActivityCertificates",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityParticipations_PersonId",
                table: "ActivityParticipations",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityParticipations_PostId_PersonId",
                table: "ActivityParticipations",
                columns: new[] { "PostId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityParticipations_TenantId_PostId",
                table: "ActivityParticipations",
                columns: new[] { "TenantId", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOpportunities_TenantId_Status_ClosesOn",
                table: "BusinessOpportunities",
                columns: new[] { "TenantId", "Status", "ClosesOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDigestPreferences_Enabled_NextDigestAt",
                table: "CommunityDigestPreferences",
                columns: new[] { "Enabled", "NextDigestAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDigestPreferences_TenantId_UserId",
                table: "CommunityDigestPreferences",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityFollows_TenantId_UserId_Kind_Target",
                table: "CommunityFollows",
                columns: new[] { "TenantId", "UserId", "Kind", "Target" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPolicies_TenantId",
                table: "CommunityPolicies",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityResponses_OpportunityId_UserId",
                table: "OpportunityResponses",
                columns: new[] { "OpportunityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityResponses_TenantId_OpportunityId",
                table: "OpportunityResponses",
                columns: new[] { "TenantId", "OpportunityId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityPosts_Events_TenantId_SourceEventId",
                table: "CommunityPosts",
                columns: new[] { "TenantId", "SourceEventId" },
                principalTable: "Events",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunityPosts_Events_TenantId_SourceEventId",
                table: "CommunityPosts");

            migrationBuilder.DropTable(
                name: "ActivityCertificates");

            migrationBuilder.DropTable(
                name: "CommunityDigestPreferences");

            migrationBuilder.DropTable(
                name: "CommunityFollows");

            migrationBuilder.DropTable(
                name: "CommunityPolicies");

            migrationBuilder.DropTable(
                name: "OpportunityResponses");

            migrationBuilder.DropTable(
                name: "ActivityParticipations");

            migrationBuilder.DropTable(
                name: "BusinessOpportunities");

            migrationBuilder.DropIndex(
                name: "IX_CommunityPosts_TenantId_SourceEventId",
                table: "CommunityPosts");

            migrationBuilder.DropIndex(
                name: "IX_CommunityMedia_PostId_SourceDocumentId",
                table: "CommunityMedia");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "ReviewOrgId",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "SourceEventId",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "CommunityMedia");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "CommunityMedia");

            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                table: "CommunityMedia");
        }
    }
}
