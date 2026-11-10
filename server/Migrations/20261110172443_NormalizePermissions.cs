using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new { Id = table.Column<string>(type: "text", nullable: false) },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.InsertData(
                table: "Permissions",
                column: "Id",
                values: new object[]
                {
                    "audit.view",
                    "organization.approve",
                    "organization.archive",
                    "organization.create",
                    "organization.update",
                    "organization.view",
                    "reports.view",
                    "roles.assign",
                    "roles.manage",
                    "roles.view",
                    "settings.manage",
                    "settings.view",
                    "users.invite",
                    "users.manage",
                    "users.view",
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true
            );
            migrationBuilder.Sql(
                "INSERT INTO \"RolePermissions\" (\"Id\",\"RoleId\",\"PermissionId\") SELECT gen_random_uuid(), r.\"Id\", p FROM \"Roles\" r CROSS JOIN LATERAL unnest(r.\"Permissions\") p"
            );
            migrationBuilder.DropColumn(name: "Permissions", table: "Roles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RolePermissions");

            migrationBuilder.DropTable(name: "Permissions");

            migrationBuilder.AddColumn<string[]>(
                name: "Permissions",
                table: "Roles",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]
            );
        }
    }
}
