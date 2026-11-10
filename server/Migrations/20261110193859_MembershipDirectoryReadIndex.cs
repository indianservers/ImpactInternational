using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class MembershipDirectoryReadIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Membership_Directory_Cover\" ON \"Memberships\" (\"TenantId\",\"ClubId\") INCLUDE (\"PersonId\",\"Status\",\"EndsAt\");"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX \"IX_Membership_Directory_Cover\";");
        }
    }
}
