using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Audits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audits", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Mails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    Link = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    Used = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mails", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: true),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Verified = table.Column<bool>(type: "boolean", nullable: false),
                    PlatformAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Levels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ParentLevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsClub = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Levels", x => x.Id);
                    table.UniqueConstraint("AK_Levels_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Levels_Levels_TenantId_ParentLevelId",
                        columns: x => new { x.TenantId, x.ParentLevelId },
                        principalTable: "Levels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Levels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Permissions = table.Column<string[]>(type: "text[]", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.UniqueConstraint("AK_Roles_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Roles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ExpiresAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Orgs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ShortName = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Website = table.Column<string>(type: "text", nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    CharterDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orgs", x => x.Id);
                    table.UniqueConstraint("AK_Orgs_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Orgs_Levels_TenantId_LevelId",
                        columns: x => new { x.TenantId, x.LevelId },
                        principalTable: "Levels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Orgs_Orgs_TenantId_ParentId",
                        columns: x => new { x.TenantId, x.ParentId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Orgs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assignments_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Assignments_Roles_TenantId_RoleId",
                        columns: x => new { x.TenantId, x.RoleId },
                        principalTable: "Roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Assignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId_OrgId",
                table: "Assignments",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId_RoleId",
                table: "Assignments",
                columns: new[] { "TenantId", "RoleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_UserId_RoleId_OrgId",
                table: "Assignments",
                columns: new[] { "UserId", "RoleId", "OrgId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Audits_TenantId_At",
                table: "Audits",
                columns: new[] { "TenantId", "At" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Levels_TenantId_Name",
                table: "Levels",
                columns: new[] { "TenantId", "Name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Levels_TenantId_ParentLevelId",
                table: "Levels",
                columns: new[] { "TenantId", "ParentLevelId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Mails_TokenHash",
                table: "Mails",
                column: "TokenHash",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Orgs_TenantId_Code",
                table: "Orgs",
                columns: new[] { "TenantId", "Code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Orgs_TenantId_LevelId_Status",
                table: "Orgs",
                columns: new[] { "TenantId", "LevelId", "Status" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Orgs_TenantId_ParentId_Name",
                table: "Orgs",
                columns: new[] { "TenantId", "ParentId", "Name" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId_ExpiresAt",
                table: "Sessions",
                columns: new[] { "UserId", "ExpiresAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Assignments");

            migrationBuilder.DropTable(name: "Audits");

            migrationBuilder.DropTable(name: "Mails");

            migrationBuilder.DropTable(name: "Sessions");

            migrationBuilder.DropTable(name: "Settings");

            migrationBuilder.DropTable(name: "Orgs");

            migrationBuilder.DropTable(name: "Roles");

            migrationBuilder.DropTable(name: "Users");

            migrationBuilder.DropTable(name: "Levels");

            migrationBuilder.DropTable(name: "Tenants");
        }
    }
}
