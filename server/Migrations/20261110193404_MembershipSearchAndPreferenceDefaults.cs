using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class MembershipSearchAndPreferenceDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Memberships_Number_Search" ON "Memberships" USING gin ("Number" gin_trgm_ops);
                UPDATE "UserProfiles" SET "Locale"='en' WHERE "Locale"='';
                UPDATE "UserProfiles" SET "TimeZone"='UTC' WHERE "TimeZone"='';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX \"IX_Memberships_Number_Search\";");
        }
    }
}
