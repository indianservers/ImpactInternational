using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class MembershipDocumentRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "MembershipDocuments" DROP CONSTRAINT "CK_Document_Size";
                ALTER TABLE "MembershipDocuments" ADD CONSTRAINT "CK_Document_Size" CHECK(
                  (("Status"='Expired' AND octet_length("Content")=0)
                    OR ("Status"<>'Expired' AND octet_length("Content") BETWEEN 1 AND 5242880))
                  AND "ContentType" IN ('application/pdf','image/png','image/jpeg'));
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refuses downgrade while purged documents exist rather than fabricating content.
            migrationBuilder.Sql(
                """
                ALTER TABLE "MembershipDocuments" DROP CONSTRAINT "CK_Document_Size";
                ALTER TABLE "MembershipDocuments" ADD CONSTRAINT "CK_Document_Size" CHECK(
                  octet_length("Content") BETWEEN 1 AND 5242880
                  AND "ContentType" IN ('application/pdf','image/png','image/jpeg'));
                """
            );
        }
    }
}
