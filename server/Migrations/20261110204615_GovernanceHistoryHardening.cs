using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceHistoryHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrganizationName",
                table: "OfficerAppointments",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "PositionTitle",
                table: "OfficerAppointments",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "Body", "Subject" },
                values: new object[,]
                {
                    { "governance.review", "{detail}", "ClubSphere · governance review" },
                    { "governance.update", "{detail}", "ClubSphere · governance update" },
                    { "leadership.transition", "{detail}", "ClubSphere · leadership transition" },
                }
            );
            migrationBuilder.Sql(
                """
UPDATE "OfficerAppointments" a SET "PositionTitle"=COALESCE((SELECT au."Changes" FROM "Audits" au WHERE au."Action"='appointment.proposed' AND au."Resource"=a."Id"::text ORDER BY au."At" LIMIT 1),p."Title"), "OrganizationName"=o."Name" FROM "OfficerPositions" p,"Orgs" o WHERE p."Id"=a."PositionId" AND o."Id"=a."OrgId";
CREATE INDEX "IX_Orgs_Code_Search" ON "Orgs" USING gin ("Code" gin_trgm_ops);
CREATE FUNCTION clubs_office_snapshot_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF (OLD."PositionTitle"<>'' AND NEW."PositionTitle"<>OLD."PositionTitle") OR (OLD."OrganizationName"<>'' AND NEW."OrganizationName"<>OLD."OrganizationName") THEN RAISE EXCEPTION 'Historical office labels are immutable' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;
CREATE TRIGGER appointment_snapshot_integrity BEFORE UPDATE ON "OfficerAppointments" FOR EACH ROW EXECUTE FUNCTION clubs_office_snapshot_guard();

"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER appointment_snapshot_integrity ON \"OfficerAppointments\"; DROP FUNCTION clubs_office_snapshot_guard(); DROP INDEX \"IX_Orgs_Code_Search\";"
            );

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: "governance.review"
            );

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: "governance.update"
            );

            migrationBuilder.DeleteData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: "leadership.transition"
            );

            migrationBuilder.DropColumn(name: "OrganizationName", table: "OfficerAppointments");

            migrationBuilder.DropColumn(name: "PositionTitle", table: "OfficerAppointments");
        }
    }
}
