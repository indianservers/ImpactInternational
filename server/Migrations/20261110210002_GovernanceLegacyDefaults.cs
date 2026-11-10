using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceLegacyDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "MembershipWorkflows" old SET "Active"=false WHERE old."Kind"='' AND old."Active" AND EXISTS(SELECT 1 FROM "MembershipWorkflows" modern WHERE modern."TenantId"=old."TenantId" AND modern."Kind"='Membership' AND modern."Active");
                UPDATE "MembershipWorkflows" SET "Kind"='Membership' WHERE "Kind"='';
                ALTER TABLE "MembershipWorkflows" ALTER COLUMN "Kind" SET DEFAULT 'Membership';
                UPDATE "Orgs" SET "FiscalStartMonth"=CASE WHEN "FiscalStartMonth"=0 THEN 1 ELSE "FiscalStartMonth" END,
                  "BrandingColor"=CASE WHEN "BrandingColor"='' THEN '#2563eb' ELSE "BrandingColor" END,
                  "PreferredLanguage"=CASE WHEN "PreferredLanguage"='' THEN 'en' ELSE "PreferredLanguage" END,
                  "ClubType"=CASE WHEN "ClubType"='' THEN 'Community' ELSE "ClubType" END
                WHERE "FiscalStartMonth"=0 OR "BrandingColor"='' OR "PreferredLanguage"='' OR "ClubType"='';
                ALTER TABLE "Orgs" ALTER COLUMN "FiscalStartMonth" SET DEFAULT 1;
                ALTER TABLE "Orgs" ALTER COLUMN "BrandingColor" SET DEFAULT '#2563eb';
                ALTER TABLE "Orgs" ALTER COLUMN "PreferredLanguage" SET DEFAULT 'en';
                ALTER TABLE "Orgs" ALTER COLUMN "ClubType" SET DEFAULT 'Community';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
