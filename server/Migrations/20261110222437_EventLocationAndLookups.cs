using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class EventLocationAndLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Events",
                type: "double precision",
                nullable: true
            );

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Events",
                type: "double precision",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatorId",
                table: "Events",
                column: "CreatorId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Events_TenantId_ApprovalRequestId",
                table: "Events",
                columns: new[] { "TenantId", "ApprovalRequestId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventRegistrations_TenantId_ApprovalRequestId",
                table: "EventRegistrations",
                columns: new[] { "TenantId", "ApprovalRequestId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventInvitations_InviterId",
                table: "EventInvitations",
                column: "InviterId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventCheckins_OperatorId",
                table: "EventCheckins",
                column: "OperatorId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_EventCheckins_UserProfiles_OperatorId",
                table: "EventCheckins",
                column: "OperatorId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_EventInvitations_UserProfiles_InviterId",
                table: "EventInvitations",
                column: "InviterId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_EventRegistrations_GovernanceRequests_TenantId_ApprovalRequ~",
                table: "EventRegistrations",
                columns: new[] { "TenantId", "ApprovalRequestId" },
                principalTable: "GovernanceRequests",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Events_GovernanceRequests_TenantId_ApprovalRequestId",
                table: "Events",
                columns: new[] { "TenantId", "ApprovalRequestId" },
                principalTable: "GovernanceRequests",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Events_UserProfiles_CreatorId",
                table: "Events",
                column: "CreatorId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
            migrationBuilder.Sql(
                """
ALTER TABLE "Events" ADD CONSTRAINT "CK_Event_Location" CHECK (("Latitude" IS NULL AND "Longitude" IS NULL) OR ("Latitude" BETWEEN -90 AND 90 AND "Longitude" BETWEEN -180 AND 180));
ALTER TABLE "EventCheckins" ADD CONSTRAINT "CK_Event_Correction" CHECK(NOT "Voided" OR length("CorrectionReason")>=5);
CREATE FUNCTION clubs_event_session_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."SessionId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "EventSessions" s WHERE s."Id"=NEW."SessionId" AND s."TenantId"=NEW."TenantId" AND s."EventId"=NEW."EventId") THEN RAISE EXCEPTION 'Session belongs to a different event' USING ERRCODE='23514'; END IF; RETURN NEW; END $$;
CREATE TRIGGER event_checkin_session_link BEFORE INSERT OR UPDATE ON "EventCheckins" FOR EACH ROW EXECUTE FUNCTION clubs_event_session_guard();
CREATE TRIGGER event_booking_session_link BEFORE INSERT OR UPDATE ON "EventSessionBookings" FOR EACH ROW EXECUTE FUNCTION clubs_event_session_guard();
"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS clubs_event_session_guard() CASCADE;ALTER TABLE \"EventCheckins\" DROP CONSTRAINT IF EXISTS \"CK_Event_Correction\";"
            );
            migrationBuilder.DropForeignKey(
                name: "FK_EventCheckins_UserProfiles_OperatorId",
                table: "EventCheckins"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_EventInvitations_UserProfiles_InviterId",
                table: "EventInvitations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_EventRegistrations_GovernanceRequests_TenantId_ApprovalRequ~",
                table: "EventRegistrations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Events_GovernanceRequests_TenantId_ApprovalRequestId",
                table: "Events"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Events_UserProfiles_CreatorId",
                table: "Events"
            );

            migrationBuilder.DropIndex(name: "IX_Events_CreatorId", table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_TenantId_ApprovalRequestId",
                table: "Events"
            );

            migrationBuilder.DropIndex(
                name: "IX_EventRegistrations_TenantId_ApprovalRequestId",
                table: "EventRegistrations"
            );

            migrationBuilder.DropIndex(
                name: "IX_EventInvitations_InviterId",
                table: "EventInvitations"
            );

            migrationBuilder.DropIndex(name: "IX_EventCheckins_OperatorId", table: "EventCheckins");

            migrationBuilder.DropColumn(name: "Latitude", table: "Events");

            migrationBuilder.DropColumn(name: "Longitude", table: "Events");
        }
    }
}
