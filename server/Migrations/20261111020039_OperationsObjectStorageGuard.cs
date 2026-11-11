using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class OperationsObjectStorageGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
            ALTER TABLE "GovernanceDocuments" DROP CONSTRAINT "CK_Governance_Document";
            ALTER TABLE "GovernanceDocuments" ADD CONSTRAINT "CK_Governance_Document" CHECK(octet_length("Content")<=5242880 AND (octet_length("Content")>0 OR "ObjectKey" ~ '^[0-9a-f]{32}$' OR "Status"='Expired') AND "ContentType" IN ('application/pdf','image/png','image/jpeg'));
            """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
