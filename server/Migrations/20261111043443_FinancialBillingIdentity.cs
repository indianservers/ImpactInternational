using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class FinancialBillingIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "FinanceInvoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChargeCode",
                table: "DuesPlans",
                type: "text",
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql("""

ALTER TABLE "DuesPlans" DISABLE TRIGGER finance_plan_guard;
UPDATE "DuesPlans" SET "ChargeCode"=upper(trim("Name")) WHERE "ChargeCode"='';
ALTER TABLE "DuesPlans" ENABLE TRIGGER finance_plan_guard;
CREATE OR REPLACE FUNCTION finance_plan_dates() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 PERFORM pg_advisory_xact_lock(hashtextextended(NEW."TenantId"::text,7));
 IF NEW."ChargeCode"='' OR length(NEW."ChargeCode")>150 THEN RAISE EXCEPTION 'Charge code required';END IF;
 IF EXISTS(SELECT 1 FROM "DuesPlans" p WHERE p."Id"<>NEW."Id" AND p."EntityId"=NEW."EntityId" AND p."BillingOrgId"=NEW."BillingOrgId" AND p."CategoryId" IS NOT DISTINCT FROM NEW."CategoryId" AND p."ChargeCode"=NEW."ChargeCode" AND p."EffectiveFrom"<=NEW."EffectiveTo" AND p."EffectiveTo">=NEW."EffectiveFrom") THEN RAISE EXCEPTION 'Effective fee definitions with the same charge code cannot overlap'; END IF;RETURN NEW;
END $$;
CREATE TRIGGER finance_plan_dates BEFORE INSERT ON "DuesPlans" FOR EACH ROW EXECUTE FUNCTION finance_plan_dates();
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE r."Name"='Club Treasurer' AND (p."Id" LIKE 'finance.%' OR p."Id" IN ('governance.approve','governance.documents','operations.view','operations.documents')) ON CONFLICT DO NOTHING;

""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "FinanceInvoices");

            migrationBuilder.DropColumn(
                name: "ChargeCode",
                table: "DuesPlans");
        }
    }
}
