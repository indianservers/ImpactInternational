using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class ClubBankRemittanceLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "MatchedPaymentIds",
                table: "FinanceBankTransactions",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.AddColumn<Guid>(
                name: "RemittanceClubId",
                table: "FinanceBankTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinanceBankPaymentMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 20, scale: 4, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceBankPaymentMatches", x => x.Id);
                    table.UniqueConstraint("AK_FinanceBankPaymentMatches_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_FinanceBankPaymentMatch_Amount", "\"BankAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_FinanceBankPaymentMatches_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBankPaymentMatches_FinanceBankTransactions_TenantId_~",
                        columns: x => new { x.TenantId, x.EntityId, x.BankTransactionId },
                        principalTable: "FinanceBankTransactions",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBankPaymentMatches_FinancePayments_TenantId_EntityId~",
                        columns: x => new { x.TenantId, x.EntityId, x.PaymentId },
                        principalTable: "FinancePayments",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBankPaymentMatches_Orgs_TenantId_ClubId",
                        columns: x => new { x.TenantId, x.ClubId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankTransactions_MatchedPaymentIds",
                table: "FinanceBankTransactions",
                column: "MatchedPaymentIds")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankTransactions_TenantId_RemittanceClubId",
                table: "FinanceBankTransactions",
                columns: new[] { "TenantId", "RemittanceClubId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankPaymentMatches_BankTransactionId",
                table: "FinanceBankPaymentMatches",
                column: "BankTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankPaymentMatches_PaymentId",
                table: "FinanceBankPaymentMatches",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankPaymentMatches_TenantId_ClubId",
                table: "FinanceBankPaymentMatches",
                columns: new[] { "TenantId", "ClubId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankPaymentMatches_TenantId_EntityId_BankTransaction~",
                table: "FinanceBankPaymentMatches",
                columns: new[] { "TenantId", "EntityId", "BankTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankPaymentMatches_TenantId_EntityId_PaymentId",
                table: "FinanceBankPaymentMatches",
                columns: new[] { "TenantId", "EntityId", "PaymentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceBankTransactions_Orgs_TenantId_RemittanceClubId",
                table: "FinanceBankTransactions",
                columns: new[] { "TenantId", "RemittanceClubId" },
                principalTable: "Orgs",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            // Retain existing single-receipt matches in the same unique registry.
            migrationBuilder.Sql("""
                INSERT INTO "FinanceBankPaymentMatches"
                ("Id","BankTransactionId","PaymentId","ClubId","BankAmount","TenantId","OrgId","EntityId","Version")
                SELECT gen_random_uuid(),"Id","PaymentId",NULL,"Amount","TenantId","OrgId","EntityId",1
                FROM "FinanceBankTransactions"
                WHERE "Status"='Matched' AND "PaymentId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS(SELECT 1 FROM "FinanceBankTransactions" WHERE "RemittanceClubId" IS NOT NULL AND "Status"='Matched') THEN
                    RAISE EXCEPTION 'Club remittance reconciliation exists. Preserve the reconciliation links; restore an audited backup instead of dropping them.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_FinanceBankTransactions_Orgs_TenantId_RemittanceClubId",
                table: "FinanceBankTransactions");

            migrationBuilder.DropTable(
                name: "FinanceBankPaymentMatches");

            migrationBuilder.DropIndex(
                name: "IX_FinanceBankTransactions_MatchedPaymentIds",
                table: "FinanceBankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FinanceBankTransactions_TenantId_RemittanceClubId",
                table: "FinanceBankTransactions");

            migrationBuilder.DropColumn(
                name: "MatchedPaymentIds",
                table: "FinanceBankTransactions");

            migrationBuilder.DropColumn(
                name: "RemittanceClubId",
                table: "FinanceBankTransactions");
        }
    }
}
