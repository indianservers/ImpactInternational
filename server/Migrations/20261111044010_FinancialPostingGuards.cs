using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class FinancialPostingGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FinanceRefunds_InvoiceId",
                table: "FinanceRefunds",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceRefunds_PaymentId",
                table: "FinanceRefunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournalLines_JournalId",
                table: "FinanceJournalLines",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoiceLines_InvoiceId",
                table: "FinanceInvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAllocations_InvoiceId",
                table: "FinanceAllocations",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAllocations_PaymentId",
                table: "FinanceAllocations",
                column: "PaymentId");
            migrationBuilder.Sql("""

CREATE OR REPLACE FUNCTION finance_document_totals() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE iid uuid; pid uuid; inv "FinanceInvoices"%ROWTYPE; pay "FinancePayments"%ROWTYPE; paid numeric; credited numeric; total numeric; refunds numeric; allocated numeric;
BEGIN
 IF TG_TABLE_NAME='FinanceInvoices' THEN iid:=NEW."Id";
 ELSIF TG_TABLE_NAME IN ('FinanceAllocations','FinanceCredits','FinanceRefunds','FinanceInvoiceLines') THEN iid:=NEW."InvoiceId"; END IF;
 IF TG_TABLE_NAME='FinancePayments' THEN pid:=NEW."Id";
 ELSIF TG_TABLE_NAME IN ('FinanceAllocations','FinanceRefunds') THEN pid:=NEW."PaymentId"; END IF;
 IF iid IS NOT NULL THEN
 SELECT * INTO inv FROM "FinanceInvoices" WHERE "Id"=iid;
 SELECT COALESCE(SUM("Amount"),0) INTO paid FROM "FinanceAllocations" WHERE "InvoiceId"=iid;
 SELECT COALESCE(SUM("Amount"),0) INTO refunds FROM "FinanceRefunds" WHERE "InvoiceId"=iid;
 SELECT COALESCE(SUM("Amount"),0) INTO credited FROM "FinanceCredits" WHERE "InvoiceId"=iid;
 SELECT COALESCE(SUM("Total"),0) INTO total FROM "FinanceInvoiceLines" WHERE "InvoiceId"=iid;
 IF inv."Paid"<>paid-refunds OR inv."Credited"<>credited OR inv."Total"<>total THEN RAISE EXCEPTION 'Invoice totals do not reconcile to immutable source records';END IF;
 IF inv."State"='Issued' AND NOT EXISTS(SELECT 1 FROM "FinanceJournals" WHERE "EntityId"=inv."EntityId" AND "Source"='invoice:'||iid::text) THEN RAISE EXCEPTION 'Issued invoice requires posted journal';END IF;
 END IF;
 IF pid IS NOT NULL THEN
 SELECT * INTO pay FROM "FinancePayments" WHERE "Id"=pid;
 SELECT COALESCE(SUM("Amount"),0) INTO allocated FROM "FinanceAllocations" WHERE "PaymentId"=pid;
 SELECT COALESCE(SUM("Amount"),0) INTO refunds FROM "FinanceRefunds" WHERE "PaymentId"=pid;
 SELECT COALESCE(SUM("Amount"),0) INTO total FROM "FinanceRefunds" WHERE "PaymentId"=pid AND "InvoiceId" IS NOT NULL;
 IF pay."Allocated"<>allocated-total OR pay."Refunded"<>refunds THEN RAISE EXCEPTION 'Payment totals do not reconcile to immutable source records';END IF;
 IF pay."Status"='Verified' AND NOT EXISTS(SELECT 1 FROM "FinanceJournals" WHERE "EntityId"=pay."EntityId" AND "Source"='payment:'||pid::text) THEN RAISE EXCEPTION 'Verified payment requires collection journal';END IF;
 END IF; RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinanceInvoices" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinanceInvoiceLines" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinancePayments" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinanceAllocations" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinanceCredits" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();
CREATE CONSTRAINT TRIGGER finance_document_totals AFTER INSERT OR UPDATE ON "FinanceRefunds" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_document_totals();

CREATE OR REPLACE FUNCTION finance_period_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 PERFORM pg_advisory_xact_lock(hashtextextended(NEW."TenantId"::text,7));
 IF EXISTS(SELECT 1 FROM "FinancePeriods" p WHERE p."Id"<>NEW."Id" AND p."EntityId"=NEW."EntityId" AND p."StartsOn"<=NEW."EndsOn" AND p."EndsOn">=NEW."StartsOn") THEN RAISE EXCEPTION 'Accounting periods cannot overlap'; END IF;
 IF TG_OP='UPDATE' AND (NEW."StartsOn"<>OLD."StartsOn" OR NEW."EndsOn"<>OLD."EndsOn") AND EXISTS(SELECT 1 FROM "FinanceJournals" j WHERE j."PeriodId"=OLD."Id") THEN RAISE EXCEPTION 'Posted accounting period dates are immutable';END IF;RETURN NEW;END $$;
CREATE TRIGGER finance_period_guard BEFORE INSERT OR UPDATE ON "FinancePeriods" FOR EACH ROW EXECUTE FUNCTION finance_period_guard();

""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinanceRefunds_InvoiceId",
                table: "FinanceRefunds");

            migrationBuilder.DropIndex(
                name: "IX_FinanceRefunds_PaymentId",
                table: "FinanceRefunds");

            migrationBuilder.DropIndex(
                name: "IX_FinanceJournalLines_JournalId",
                table: "FinanceJournalLines");

            migrationBuilder.DropIndex(
                name: "IX_FinanceInvoiceLines_InvoiceId",
                table: "FinanceInvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_FinanceAllocations_InvoiceId",
                table: "FinanceAllocations");

            migrationBuilder.DropIndex(
                name: "IX_FinanceAllocations_PaymentId",
                table: "FinanceAllocations");
        }
    }
}
