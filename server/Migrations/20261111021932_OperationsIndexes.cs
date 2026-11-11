using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class OperationsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VolunteerAssignments_TenantId_ShiftId",
                table: "VolunteerAssignments");

            migrationBuilder.DropIndex(
                name: "IX_IssuedCertificates_TenantId_TemplateId",
                table: "IssuedCertificates");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerHours_TenantId_OrgId_Status_StartsAt",
                table: "VolunteerHours",
                columns: new[] { "TenantId", "OrgId", "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_TenantId_ShiftId_Status",
                table: "VolunteerAssignments",
                columns: new[] { "TenantId", "ShiftId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceProjects_TenantId_OrgId_Status_StartsAt",
                table: "ServiceProjects",
                columns: new[] { "TenantId", "OrgId", "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationTasks_TenantId_OrgId_Status_DueAt",
                table: "OperationTasks",
                columns: new[] { "TenantId", "OrgId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationRevisions_TenantId_RecordId_UpdatedAt",
                table: "OperationRevisions",
                columns: new[] { "TenantId", "RecordId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificates_TenantId_TemplateId_PersonId_SourceId_So~",
                table: "IssuedCertificates",
                columns: new[] { "TenantId", "TemplateId", "PersonId", "SourceId", "SourceType", "TemplateVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceDocuments_TenantId_OrgId_Id",
                table: "GovernanceDocuments",
                columns: new[] { "TenantId", "OrgId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VolunteerHours_TenantId_OrgId_Status_StartsAt",
                table: "VolunteerHours");

            migrationBuilder.DropIndex(
                name: "IX_VolunteerAssignments_TenantId_ShiftId_Status",
                table: "VolunteerAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ServiceProjects_TenantId_OrgId_Status_StartsAt",
                table: "ServiceProjects");

            migrationBuilder.DropIndex(
                name: "IX_OperationTasks_TenantId_OrgId_Status_DueAt",
                table: "OperationTasks");

            migrationBuilder.DropIndex(
                name: "IX_OperationRevisions_TenantId_RecordId_UpdatedAt",
                table: "OperationRevisions");

            migrationBuilder.DropIndex(
                name: "IX_IssuedCertificates_TenantId_TemplateId_PersonId_SourceId_So~",
                table: "IssuedCertificates");

            migrationBuilder.DropIndex(
                name: "IX_GovernanceDocuments_TenantId_OrgId_Id",
                table: "GovernanceDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_TenantId_ShiftId",
                table: "VolunteerAssignments",
                columns: new[] { "TenantId", "ShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCertificates_TenantId_TemplateId",
                table: "IssuedCertificates",
                columns: new[] { "TenantId", "TemplateId" });
        }
    }
}
