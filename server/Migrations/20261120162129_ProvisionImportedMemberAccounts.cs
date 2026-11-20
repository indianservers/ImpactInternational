using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class ProvisionImportedMemberAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Clubs.MemberAccounts.ProvisionSql.Replace("@person_ids", "NULL::uuid[]"));
            migrationBuilder.Sql(Clubs.MemberAccounts.GrantsSql.Replace("@person_ids", "NULL::uuid[]"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Accounts may have been claimed and used since the backfill. Keep them on rollback.
        }
    }
}
