using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class ActivityGoogleDriveMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AudienceOrgId",
                table: "CommunityPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriveFolderId",
                table: "CommunityPosts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "GoogleConnectionId",
                table: "CommunityPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "CommunityPosts",
                type: "text",
                nullable: false,
                defaultValue: "Hierarchy");

            migrationBuilder.CreateTable(
                name: "GoogleMediaConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleSubject = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    RefreshTokenProtected = table.Column<string>(type: "text", nullable: false),
                    AccessTokenProtected = table.Column<string>(type: "text", nullable: false),
                    AccessExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RootFolderId = table.Column<string>(type: "text", nullable: false),
                    ActiveForUploads = table.Column<bool>(type: "boolean", nullable: false),
                    Connected = table.Column<bool>(type: "boolean", nullable: false),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleMediaConnections", x => x.Id);
                    table.UniqueConstraint("AK_GoogleMediaConnections_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_GoogleMediaConnections_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoogleMediaOAuthStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    StateHash = table.Column<string>(type: "text", nullable: false),
                    VerifierProtected = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Consumed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleMediaOAuthStates", x => x.Id);
                    table.UniqueConstraint("AK_GoogleMediaOAuthStates_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_GoogleMediaOAuthStates_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    DriveFileId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityMedia", x => x.Id);
                    table.UniqueConstraint("AK_CommunityMedia_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommunityMedia_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityMedia_CommunityPosts_TenantId_PostId",
                        columns: x => new { x.TenantId, x.PostId },
                        principalTable: "CommunityPosts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityMedia_GoogleMediaConnections_TenantId_ConnectionId",
                        columns: x => new { x.TenantId, x.ConnectionId },
                        principalTable: "GoogleMediaConnections",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_TenantId_AudienceOrgId",
                table: "CommunityPosts",
                columns: new[] { "TenantId", "AudienceOrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_TenantId_GoogleConnectionId",
                table: "CommunityPosts",
                columns: new[] { "TenantId", "GoogleConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_TenantId_Status_Visibility_CreatedAt",
                table: "CommunityPosts",
                columns: new[] { "TenantId", "Status", "Visibility", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMedia_DriveFileId",
                table: "CommunityMedia",
                column: "DriveFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMedia_PostId_Status_CreatedAt",
                table: "CommunityMedia",
                columns: new[] { "PostId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMedia_TenantId_ConnectionId",
                table: "CommunityMedia",
                columns: new[] { "TenantId", "ConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMedia_TenantId_PostId",
                table: "CommunityMedia",
                columns: new[] { "TenantId", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMediaConnections_TenantId",
                table: "GoogleMediaConnections",
                column: "TenantId",
                unique: true,
                filter: "\"ActiveForUploads\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMediaConnections_TenantId_GoogleSubject",
                table: "GoogleMediaConnections",
                columns: new[] { "TenantId", "GoogleSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMediaOAuthStates_StateHash",
                table: "GoogleMediaOAuthStates",
                column: "StateHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityPosts_GoogleMediaConnections_TenantId_GoogleConnec~",
                table: "CommunityPosts",
                columns: new[] { "TenantId", "GoogleConnectionId" },
                principalTable: "GoogleMediaConnections",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityPosts_Orgs_TenantId_AudienceOrgId",
                table: "CommunityPosts",
                columns: new[] { "TenantId", "AudienceOrgId" },
                principalTable: "Orgs",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunityPosts_GoogleMediaConnections_TenantId_GoogleConnec~",
                table: "CommunityPosts");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunityPosts_Orgs_TenantId_AudienceOrgId",
                table: "CommunityPosts");

            migrationBuilder.DropTable(
                name: "CommunityMedia");

            migrationBuilder.DropTable(
                name: "GoogleMediaOAuthStates");

            migrationBuilder.DropTable(
                name: "GoogleMediaConnections");

            migrationBuilder.DropIndex(
                name: "IX_CommunityPosts_TenantId_AudienceOrgId",
                table: "CommunityPosts");

            migrationBuilder.DropIndex(
                name: "IX_CommunityPosts_TenantId_GoogleConnectionId",
                table: "CommunityPosts");

            migrationBuilder.DropIndex(
                name: "IX_CommunityPosts_TenantId_Status_Visibility_CreatedAt",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "AudienceOrgId",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "DriveFolderId",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "GoogleConnectionId",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "CommunityPosts");
        }
    }
}
