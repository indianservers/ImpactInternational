using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalOfficeServiceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricalOfficeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OfficeTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsPresident = table.Column<bool>(type: "boolean", nullable: false),
                    StartYear = table.Column<int>(type: "integer", nullable: false),
                    EndYear = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalOfficeRecords", x => x.Id);
                    table.UniqueConstraint("AK_HistoricalOfficeRecords_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_HistoricalOfficeRecord_Years", "\"StartYear\" >= 1800 AND \"EndYear\" >= \"StartYear\" AND \"EndYear\" <= 9999");
                    table.ForeignKey(
                        name: "FK_HistoricalOfficeRecords_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalOfficeRecords_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalOfficeRecords_TenantId_OrgId",
                table: "HistoricalOfficeRecords",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalOfficeRecords_TenantId_OrgId_EndYear_StartYear",
                table: "HistoricalOfficeRecords",
                columns: new[] { "TenantId", "OrgId", "EndYear", "StartYear" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalOfficeRecords");
        }
    }
}
