using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExistingIssuedCardExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReuseIssued",
                table: "IdCardJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION clubs_idcard_job_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW."TemplateVersionId"<>OLD."TemplateVersionId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."OrgId"<>OLD."OrgId" OR NEW."CreatorId"<>OLD."CreatorId" OR NEW."Total"<>OLD."Total" OR NEW."Format"<>OLD."Format" OR NEW."PrintLayout"<>OLD."PrintLayout" OR NEW."Reissue"<>OLD."Reissue" OR NEW."ReuseIssued"<>OLD."ReuseIssued" THEN RAISE EXCEPTION 'Generation selection and configuration are immutable'; END IF; RETURN NEW; END $$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION clubs_idcard_job_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW."TemplateVersionId"<>OLD."TemplateVersionId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."OrgId"<>OLD."OrgId" OR NEW."CreatorId"<>OLD."CreatorId" OR NEW."Total"<>OLD."Total" OR NEW."Format"<>OLD."Format" OR NEW."PrintLayout"<>OLD."PrintLayout" OR NEW."Reissue"<>OLD."Reissue"  THEN RAISE EXCEPTION 'Generation selection and configuration are immutable'; END IF; RETURN NEW; END $$;
""");
            migrationBuilder.DropColumn(
                name: "ReuseIssued",
                table: "IdCardJobs");
        }
    }
}
