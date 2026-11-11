using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationInboundMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "CommunicationAttachments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "CommunicationAttachments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderAccountId",
                table: "CommunicationAttachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMediaId",
                table: "CommunicationAttachments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CommunicationAttachments",
                type: "text",
                nullable: false,
                defaultValue: "Stored");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAttachments_Status_NextAttemptAt",
                table: "CommunicationAttachments",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAttachments_TenantId_ProviderAccountId",
                table: "CommunicationAttachments",
                columns: new[] { "TenantId", "ProviderAccountId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationAttachments_CommunicationAccounts_TenantId_Pro~",
                table: "CommunicationAttachments",
                columns: new[] { "TenantId", "ProviderAccountId" },
                principalTable: "CommunicationAccounts",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationAttachments_CommunicationAccounts_TenantId_Pro~",
                table: "CommunicationAttachments");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationAttachments_Status_NextAttemptAt",
                table: "CommunicationAttachments");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationAttachments_TenantId_ProviderAccountId",
                table: "CommunicationAttachments");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "CommunicationAttachments");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "CommunicationAttachments");

            migrationBuilder.DropColumn(
                name: "ProviderAccountId",
                table: "CommunicationAttachments");

            migrationBuilder.DropColumn(
                name: "ProviderMediaId",
                table: "CommunicationAttachments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CommunicationAttachments");
        }
    }
}
