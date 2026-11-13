using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class MemberAddressClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "PersonAddresses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "PersonAddresses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"PersonAddresses\" SET \"Type\" = 'Residential'; UPDATE \"PersonAddresses\" SET \"IsPrimary\" = true WHERE \"Id\" IN (SELECT DISTINCT ON (\"PersonId\") \"Id\" FROM \"PersonAddresses\" ORDER BY \"PersonId\", \"Id\");");

            migrationBuilder.CreateIndex(
                name: "IX_PersonAddresses_Primary",
                table: "PersonAddresses",
                column: "PersonId",
                unique: true,
                filter: "\"IsPrimary\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersonAddresses_Primary",
                table: "PersonAddresses");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "PersonAddresses");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "PersonAddresses");
        }
    }
}
