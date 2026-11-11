using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class IdCardItemTenantIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_IdCardJobItems_TenantId_CardId",
                table: "IdCardJobItems",
                columns: new[] { "TenantId", "CardId" });

            migrationBuilder.AddForeignKey(
                name: "FK_IdCardJobItems_DigitalCards_TenantId_CardId",
                table: "IdCardJobItems",
                columns: new[] { "TenantId", "CardId" },
                principalTable: "DigitalCards",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdCardJobItems_DigitalCards_TenantId_CardId",
                table: "IdCardJobItems");

            migrationBuilder.DropIndex(
                name: "IX_IdCardJobItems_TenantId_CardId",
                table: "IdCardJobItems");
        }
    }
}
