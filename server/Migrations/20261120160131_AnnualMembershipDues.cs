using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class AnnualMembershipDues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnnualDuesPayments",
                columns: table => new
                {
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleYear = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualDuesPayments", x => new { x.MembershipId, x.CycleYear });
                    table.ForeignKey(
                        name: "FK_AnnualDuesPayments_Memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnnualDuesSettings",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiryMonth = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDay = table.Column<int>(type: "integer", nullable: false),
                    FirstHalfFee = table.Column<decimal>(type: "numeric", nullable: false),
                    SecondHalfFee = table.Column<decimal>(type: "numeric", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualDuesSettings", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_AnnualDuesSettings_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                WITH local_date AS (
                    SELECT (now() AT TIME ZONE 'Asia/Kolkata')::date AS today
                ), cycle AS (
                    SELECT make_date(extract(year FROM today)::int
                        + CASE WHEN today > make_date(extract(year FROM today)::int, 3, 31) THEN 1 ELSE 0 END,
                        3, 31) AS ends_on
                    FROM local_date
                )
                UPDATE "Memberships" member
                SET "EndsAt" = ((cycle.ends_on + 1)::timestamp AT TIME ZONE 'Asia/Kolkata') - interval '1 millisecond',
                    "UpdatedAt" = now(),
                    "Version" = member."Version" + 1
                FROM "Associations" association, cycle
                WHERE member."TenantId" = association."Id" AND association."Name" = 'Impact International'
                    AND member."Status" IN ('Active', 'Inactive', 'Suspended');
                """);

            migrationBuilder.Sql("""
                WITH local_date AS (
                    SELECT (now() AT TIME ZONE 'Asia/Kolkata')::date AS today
                ), cycle AS (
                    SELECT extract(year FROM today)::int
                        + CASE WHEN today > make_date(extract(year FROM today)::int, 3, 31) THEN 1 ELSE 0 END AS year
                    FROM local_date
                )
                INSERT INTO "AnnualDuesPayments" ("MembershipId", "CycleYear", "Amount", "PaidOn")
                SELECT invoice."MembershipId", cycle.year, sum(allocation."Amount"), max(payment."Date")
                FROM "FinanceInvoices" invoice
                JOIN "Memberships" member ON member."Id" = invoice."MembershipId" AND member."TenantId" = invoice."TenantId"
                JOIN "Associations" association ON association."Id" = invoice."TenantId"
                JOIN "FinanceAllocations" allocation ON allocation."InvoiceId" = invoice."Id"
                JOIN "FinancePayments" payment ON payment."Id" = allocation."PaymentId"
                CROSS JOIN cycle
                CROSS JOIN local_date
                WHERE association."Name" = 'Impact International'
                    AND invoice."State" = 'Issued' AND invoice."Currency" = 'INR'
                    AND invoice."Purpose" IN ('ClubDues', 'JoiningFee', 'Renewal')
                    AND invoice."Total" > 0 AND invoice."Paid" >= invoice."Total"
                    AND payment."Status" = 'Verified' AND payment."Refunded" = 0
                    AND coalesce(invoice."PeriodStart", invoice."Date") BETWEEN make_date(cycle.year - 1, 4, 1) AND local_date.today
                    AND payment."Date" BETWEEN make_date(cycle.year - 1, 4, 1) AND local_date.today
                GROUP BY invoice."MembershipId", cycle.year
                HAVING sum(allocation."Amount") > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnualDuesPayments");

            migrationBuilder.DropTable(
                name: "AnnualDuesSettings");
        }
    }
}
