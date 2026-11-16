using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class IciTransferReviewStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MembershipTransfers_MembershipId",
                table: "MembershipTransfers");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTransfers_MembershipId",
                table: "MembershipTransfers",
                column: "MembershipId",
                unique: true,
                filter: "\"Status\" IN ('PendingSource','PendingDestination','PendingVicePresidentSource','PendingVicePresidentDestination','PendingAssociation','Scheduled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MembershipTransfers_MembershipId",
                table: "MembershipTransfers");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTransfers_MembershipId",
                table: "MembershipTransfers",
                column: "MembershipId",
                unique: true,
                filter: "\"Status\" IN ('PendingSource','PendingDestination','PendingAssociation','Scheduled')");
        }
    }
}
