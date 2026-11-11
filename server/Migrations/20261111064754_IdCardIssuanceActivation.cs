using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class IdCardIssuanceActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DigitalCards_MembershipId",
                table: "DigitalCards");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCards_MembershipId",
                table: "DigitalCards",
                column: "MembershipId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL AND \"State\" IN ('Legacy','Issued')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DigitalCards_MembershipId",
                table: "DigitalCards");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCards_MembershipId",
                table: "DigitalCards",
                column: "MembershipId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL AND \"State\" IN ('Legacy','Issued','Generated')");
        }
    }
}
