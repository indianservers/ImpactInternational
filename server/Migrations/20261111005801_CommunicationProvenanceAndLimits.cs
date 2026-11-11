using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationProvenanceAndLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "Mails",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mails_TenantId_CreatedAt_Channel_DeliveryStatus",
                table: "Mails",
                columns: new[] { "TenantId", "CreatedAt", "Channel", "DeliveryStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Mails_TenantId_EventId",
                table: "Mails",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_TenantId_SenderId_At",
                table: "CommunicationMessages",
                columns: new[] { "TenantId", "SenderId", "At" },
                filter: "\"ConversationId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Mails_Events_TenantId_EventId",
                table: "Mails",
                columns: new[] { "TenantId", "EventId" },
                principalTable: "Events",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mails_Events_TenantId_EventId",
                table: "Mails");

            migrationBuilder.DropIndex(
                name: "IX_Mails_TenantId_CreatedAt_Channel_DeliveryStatus",
                table: "Mails");

            migrationBuilder.DropIndex(
                name: "IX_Mails_TenantId_EventId",
                table: "Mails");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationMessages_TenantId_SenderId_At",
                table: "CommunicationMessages");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Mails");
        }
    }
}
