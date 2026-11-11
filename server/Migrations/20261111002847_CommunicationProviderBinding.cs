using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationProviderBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TemplateVersion",
                table: "Mails",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "CommunicationTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TemplateVersion",
                table: "CommunicationBroadcasts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationTemplates_TenantId_AccountId",
                table: "CommunicationTemplates",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationTemplates_CommunicationAccounts_TenantId_Accou~",
                table: "CommunicationTemplates",
                columns: new[] { "TenantId", "AccountId" },
                principalTable: "CommunicationAccounts",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationTemplates_CommunicationAccounts_TenantId_Accou~",
                table: "CommunicationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationTemplates_TenantId_AccountId",
                table: "CommunicationTemplates");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "CommunicationTemplates");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "CommunicationBroadcasts");
        }
    }
}
