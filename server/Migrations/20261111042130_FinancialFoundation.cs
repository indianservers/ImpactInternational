using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class FinancialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinanceEntitys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Decimals = table.Column<int>(type: "integer", nullable: false),
                    FiscalMonth = table.Column<int>(type: "integer", nullable: false),
                    TaxRegistration = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    InvoicePrefix = table.Column<string>(type: "text", nullable: false),
                    ReceiptPrefix = table.Column<string>(type: "text", nullable: false),
                    InvoiceSequence = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptSequence = table.Column<long>(type: "bigint", nullable: false),
                    AccountingMethod = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    GatewayKey = table.Column<string>(type: "text", nullable: false),
                    CollectionAuthority = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceEntitys", x => x.Id);
                    table.UniqueConstraint("AK_FinanceEntitys_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceEntitys_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceEntitys_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceEntitys_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DuesPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BillingOrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Months = table.Column<int>(type: "integer", nullable: false),
                    GraceDays = table.Column<int>(type: "integer", nullable: false),
                    Prorate = table.Column<bool>(type: "boolean", nullable: false),
                    Once = table.Column<bool>(type: "boolean", nullable: false),
                    AutoBill = table.Column<bool>(type: "boolean", nullable: false),
                    NextBillOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Components = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuesPlans", x => x.Id);
                    table.UniqueConstraint("AK_DuesPlans_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_DuesPlans_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_DuesPlans_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuesPlans_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuesPlans_MembershipCategories_TenantId_CategoryId",
                        columns: x => new { x.TenantId, x.CategoryId },
                        principalTable: "MembershipCategories",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuesPlans_Orgs_TenantId_BillingOrgId",
                        columns: x => new { x.TenantId, x.BillingOrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuesPlans_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceAccounts", x => x.Id);
                    table.UniqueConstraint("AK_FinanceAccounts_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceAccounts_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceAccounts_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceAccounts_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceAccounts_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceAudits", x => x.Id);
                    table.UniqueConstraint("AK_FinanceAudits_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceAudits_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceAudits_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceAudits_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceAudits_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceBanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MaskedNumber = table.Column<string>(type: "text", nullable: false),
                    ProtectedDetails = table.Column<string>(type: "text", nullable: false),
                    AccountCode = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceBanks", x => x.Id);
                    table.UniqueConstraint("AK_FinanceBanks_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceBanks_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceBanks_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBanks_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBanks_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ReplacesId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Lines = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceBudgets", x => x.Id);
                    table.UniqueConstraint("AK_FinanceBudgets_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceBudgets_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceBudgets_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBudgets_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBudgets_FinanceBudgets_TenantId_EntityId_ReplacesId",
                        columns: x => new { x.TenantId, x.EntityId, x.ReplacesId },
                        principalTable: "FinanceBudgets",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBudgets_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBudgets_GovernanceRequests_TenantId_RequestId",
                        columns: x => new { x.TenantId, x.RequestId },
                        principalTable: "GovernanceRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBudgets_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBudgets_ServiceProjects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "ServiceProjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceEventPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceEventPrices", x => x.Id);
                    table.UniqueConstraint("AK_FinanceEventPrices_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceEventPrices_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceEventPrices_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceEventPrices_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceExchangeRates", x => x.Id);
                    table.UniqueConstraint("AK_FinanceExchangeRates_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceExchangeRates_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceExchangeRates_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExchangeRates_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExchangeRates_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceGrants", x => x.Id);
                    table.UniqueConstraint("AK_FinanceGrants_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceGrants_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceGrants_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceGrants_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceGrants_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceOrganizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServedOrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Authority = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceOrganizations", x => x.Id);
                    table.UniqueConstraint("AK_FinanceOrganizations_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceOrganizations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceOrganizations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceOrganizations_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceOrganizations_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceOrganizations_Orgs_TenantId_ServedOrgId",
                        columns: x => new { x.TenantId, x.ServedOrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancePeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Closed = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancePeriods", x => x.Id);
                    table.UniqueConstraint("AK_FinancePeriods_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinancePeriods_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinancePeriods_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePeriods_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePeriods_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancePledges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    Contributor = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancePledges", x => x.Id);
                    table.UniqueConstraint("AK_FinancePledges_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinancePledges_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinancePledges_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePledges_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePledges_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePledges_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePledges_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePledges_ServiceProjects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "ServiceProjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceWebhooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderEventId = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceWebhooks", x => x.Id);
                    table.UniqueConstraint("AK_FinanceWebhooks_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceWebhooks_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceWebhooks_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceWebhooks_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceWebhooks_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    AfterMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Processed = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceJobs", x => x.Id);
                    table.UniqueConstraint("AK_FinanceJobs_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceJobs_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceJobs_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJobs_DuesPlans_TenantId_EntityId_PlanId",
                        columns: x => new { x.TenantId, x.EntityId, x.PlanId },
                        principalTable: "DuesPlans",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJobs_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJobs_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    Payer = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SettlementStatus = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Allocated = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Refunded = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    RecorderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "text", nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancePayments", x => x.Id);
                    table.UniqueConstraint("AK_FinancePayments_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinancePayments_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinancePayments_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePayments_FinanceBanks_TenantId_EntityId_BankAccountId",
                        columns: x => new { x.TenantId, x.EntityId, x.BankAccountId },
                        principalTable: "FinanceBanks",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePayments_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePayments_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePayments_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payee = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Lines = table.Column<string>(type: "text", nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: false),
                    EvidenceHash = table.Column<string>(type: "text", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    BudgetId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountCode = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Paid = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceExpenses", x => x.Id);
                    table.UniqueConstraint("AK_FinanceExpenses_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceExpenses_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceExpenses_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExpenses_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExpenses_FinanceBudgets_TenantId_EntityId_BudgetId",
                        columns: x => new { x.TenantId, x.EntityId, x.BudgetId },
                        principalTable: "FinanceBudgets",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExpenses_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExpenses_GovernanceRequests_TenantId_RequestId",
                        columns: x => new { x.TenantId, x.RequestId },
                        principalTable: "GovernanceRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExpenses_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExpenses_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceExpenses_ServiceProjects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "ServiceProjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceJournals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Narration = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReversesId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceJournals", x => x.Id);
                    table.UniqueConstraint("AK_FinanceJournals_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceJournals_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceJournals_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournals_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournals_FinanceJournals_TenantId_EntityId_ReversesId",
                        columns: x => new { x.TenantId, x.EntityId, x.ReversesId },
                        principalTable: "FinanceJournals",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournals_FinancePeriods_TenantId_EntityId_PeriodId",
                        columns: x => new { x.TenantId, x.EntityId, x.PeriodId },
                        principalTable: "FinancePeriods",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournals_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    PledgeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Customer = table.Column<string>(type: "text", nullable: false),
                    CustomerAddress = table.Column<string>(type: "text", nullable: false),
                    IssuerSnapshot = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Paid = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Credited = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemindedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceInvoices", x => x.Id);
                    table.UniqueConstraint("AK_FinanceInvoices_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceInvoices_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_DuesPlans_TenantId_EntityId_PlanId",
                        columns: x => new { x.TenantId, x.EntityId, x.PlanId },
                        principalTable: "DuesPlans",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_EventRegistrations_TenantId_RegistrationId",
                        columns: x => new { x.TenantId, x.RegistrationId },
                        principalTable: "EventRegistrations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_FinancePledges_TenantId_EntityId_PledgeId",
                        columns: x => new { x.TenantId, x.EntityId, x.PledgeId },
                        principalTable: "FinancePledges",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_Memberships_TenantId_MembershipId",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_ServiceProjects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "ServiceProjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceBankTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    ImportKey = table.Column<string>(type: "text", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceBankTransactions", x => x.Id);
                    table.UniqueConstraint("AK_FinanceBankTransactions_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceBankTransactions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceBankTransactions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBankTransactions_FinanceBanks_TenantId_EntityId_Bank~",
                        columns: x => new { x.TenantId, x.EntityId, x.BankAccountId },
                        principalTable: "FinanceBanks",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBankTransactions_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBankTransactions_FinancePayments_TenantId_EntityId_P~",
                        columns: x => new { x.TenantId, x.EntityId, x.PaymentId },
                        principalTable: "FinancePayments",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceBankTransactions_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceReimbursements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceReimbursements", x => x.Id);
                    table.UniqueConstraint("AK_FinanceReimbursements_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceReimbursements_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceReimbursements_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceReimbursements_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceReimbursements_FinanceExpenses_TenantId_EntityId_Exp~",
                        columns: x => new { x.TenantId, x.EntityId, x.ExpenseId },
                        principalTable: "FinanceExpenses",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceReimbursements_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceJournalLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceJournalLines", x => x.Id);
                    table.UniqueConstraint("AK_FinanceJournalLines_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceJournalLines_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceJournalLines_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournalLines_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournalLines_FinanceAccounts_TenantId_EntityId_Accou~",
                        columns: x => new { x.TenantId, x.EntityId, x.AccountId },
                        principalTable: "FinanceAccounts",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournalLines_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournalLines_FinanceJournals_TenantId_EntityId_Journ~",
                        columns: x => new { x.TenantId, x.EntityId, x.JournalId },
                        principalTable: "FinanceJournals",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournalLines_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournalLines_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceJournalLines_ServiceProjects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "ServiceProjects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceAllocations", x => x.Id);
                    table.UniqueConstraint("AK_FinanceAllocations_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceAllocations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceAllocations_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceAllocations_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceAllocations_FinanceInvoices_TenantId_EntityId_Invoic~",
                        columns: x => new { x.TenantId, x.EntityId, x.InvoiceId },
                        principalTable: "FinanceInvoices",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceAllocations_FinancePayments_TenantId_EntityId_Paymen~",
                        columns: x => new { x.TenantId, x.EntityId, x.PaymentId },
                        principalTable: "FinancePayments",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceAllocations_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceCredits", x => x.Id);
                    table.UniqueConstraint("AK_FinanceCredits_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceCredits_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceCredits_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCredits_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCredits_FinanceInvoices_TenantId_EntityId_InvoiceId",
                        columns: x => new { x.TenantId, x.EntityId, x.InvoiceId },
                        principalTable: "FinanceInvoices",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCredits_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeneficiaryEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Settled = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceEntitlements", x => x.Id);
                    table.UniqueConstraint("AK_FinanceEntitlements_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceEntitlements_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceEntitlements_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceEntitlements_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceEntitlements_FinanceInvoices_TenantId_EntityId_Invoi~",
                        columns: x => new { x.TenantId, x.EntityId, x.InvoiceId },
                        principalTable: "FinanceInvoices",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceEntitlements_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    CheckoutUrl = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceIntents", x => x.Id);
                    table.UniqueConstraint("AK_FinanceIntents_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceIntents_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceIntents_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceIntents_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceIntents_FinanceInvoices_TenantId_EntityId_InvoiceId",
                        columns: x => new { x.TenantId, x.EntityId, x.InvoiceId },
                        principalTable: "FinanceInvoices",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceIntents_FinancePayments_TenantId_EntityId_PaymentId",
                        columns: x => new { x.TenantId, x.EntityId, x.PaymentId },
                        principalTable: "FinancePayments",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceIntents_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Net = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    AccountCode = table.Column<string>(type: "text", nullable: false),
                    BeneficiaryEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceInvoiceLines", x => x.Id);
                    table.UniqueConstraint("AK_FinanceInvoiceLines_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceInvoiceLines_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceInvoiceLines_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoiceLines_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoiceLines_FinanceInvoices_TenantId_EntityId_Invoi~",
                        columns: x => new { x.TenantId, x.EntityId, x.InvoiceId },
                        principalTable: "FinanceInvoices",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoiceLines_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceRefunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceRefunds", x => x.Id);
                    table.UniqueConstraint("AK_FinanceRefunds_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceRefunds_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceRefunds_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceRefunds_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceRefunds_FinanceInvoices_TenantId_EntityId_InvoiceId",
                        columns: x => new { x.TenantId, x.EntityId, x.InvoiceId },
                        principalTable: "FinanceInvoices",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceRefunds_FinancePayments_TenantId_EntityId_PaymentId",
                        columns: x => new { x.TenantId, x.EntityId, x.PaymentId },
                        principalTable: "FinancePayments",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceRefunds_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BeneficiaryEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntitlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceTransfers", x => x.Id);
                    table.UniqueConstraint("AK_FinanceTransfers_TenantId_EntityId_Id", x => new { x.TenantId, x.EntityId, x.Id });
                    table.UniqueConstraint("AK_FinanceTransfers_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinanceTransfers_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceTransfers_FinanceEntitlements_TenantId_EntityId_Enti~",
                        columns: x => new { x.TenantId, x.EntityId, x.EntitlementId },
                        principalTable: "FinanceEntitlements",
                        principalColumns: new[] { "TenantId", "EntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceTransfers_FinanceEntitys_TenantId_EntityId",
                        columns: x => new { x.TenantId, x.EntityId },
                        principalTable: "FinanceEntitys",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceTransfers_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                column: "Id",
                values: new object[]
                {
                    "finance.approve",
                    "finance.configure",
                    "finance.manage",
                    "finance.post",
                    "finance.reconcile",
                    "finance.self",
                    "finance.view"
                });

            migrationBuilder.CreateIndex(
                name: "IX_DuesPlans_EntityId_AutoBill_NextBillOn",
                table: "DuesPlans",
                columns: new[] { "EntityId", "AutoBill", "NextBillOn" });

            migrationBuilder.CreateIndex(
                name: "IX_DuesPlans_EntityId_Id",
                table: "DuesPlans",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DuesPlans_TenantId_BillingOrgId",
                table: "DuesPlans",
                columns: new[] { "TenantId", "BillingOrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DuesPlans_TenantId_CategoryId",
                table: "DuesPlans",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_DuesPlans_TenantId_OrgId",
                table: "DuesPlans",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAccounts_EntityId_Code",
                table: "FinanceAccounts",
                columns: new[] { "EntityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAccounts_EntityId_Id",
                table: "FinanceAccounts",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAccounts_TenantId_OrgId",
                table: "FinanceAccounts",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAllocations_EntityId_Id",
                table: "FinanceAllocations",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAllocations_EntityId_Key",
                table: "FinanceAllocations",
                columns: new[] { "EntityId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAllocations_TenantId_EntityId_InvoiceId",
                table: "FinanceAllocations",
                columns: new[] { "TenantId", "EntityId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAllocations_TenantId_EntityId_PaymentId",
                table: "FinanceAllocations",
                columns: new[] { "TenantId", "EntityId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAllocations_TenantId_OrgId",
                table: "FinanceAllocations",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAudits_EntityId_Id",
                table: "FinanceAudits",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAudits_TenantId_OrgId",
                table: "FinanceAudits",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBanks_EntityId_Id",
                table: "FinanceBanks",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBanks_TenantId_OrgId",
                table: "FinanceBanks",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankTransactions_EntityId_Id",
                table: "FinanceBankTransactions",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankTransactions_EntityId_ImportKey",
                table: "FinanceBankTransactions",
                columns: new[] { "EntityId", "ImportKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankTransactions_TenantId_EntityId_BankAccountId",
                table: "FinanceBankTransactions",
                columns: new[] { "TenantId", "EntityId", "BankAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankTransactions_TenantId_EntityId_PaymentId",
                table: "FinanceBankTransactions",
                columns: new[] { "TenantId", "EntityId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBankTransactions_TenantId_OrgId",
                table: "FinanceBankTransactions",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBudgets_EntityId_Id",
                table: "FinanceBudgets",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBudgets_TenantId_EntityId_ReplacesId",
                table: "FinanceBudgets",
                columns: new[] { "TenantId", "EntityId", "ReplacesId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBudgets_TenantId_EventId",
                table: "FinanceBudgets",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBudgets_TenantId_OrgId",
                table: "FinanceBudgets",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBudgets_TenantId_ProjectId",
                table: "FinanceBudgets",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBudgets_TenantId_RequestId",
                table: "FinanceBudgets",
                columns: new[] { "TenantId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCredits_EntityId_Id",
                table: "FinanceCredits",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCredits_EntityId_Key",
                table: "FinanceCredits",
                columns: new[] { "EntityId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCredits_TenantId_EntityId_InvoiceId",
                table: "FinanceCredits",
                columns: new[] { "TenantId", "EntityId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCredits_TenantId_OrgId",
                table: "FinanceCredits",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceEntitlements_EntityId_Id",
                table: "FinanceEntitlements",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceEntitlements_EntityId_InvoiceId_BeneficiaryEntityId",
                table: "FinanceEntitlements",
                columns: new[] { "EntityId", "InvoiceId", "BeneficiaryEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceEntitlements_TenantId_EntityId_InvoiceId",
                table: "FinanceEntitlements",
                columns: new[] { "TenantId", "EntityId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceEntitlements_TenantId_OrgId",
                table: "FinanceEntitlements",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceEntitys_EntityId_Id",
                table: "FinanceEntitys",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceEntitys_TenantId_OrgId",
                table: "FinanceEntitys",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceEventPrices_EventId_Category",
                table: "FinanceEventPrices",
                columns: new[] { "EventId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceEventPrices_TenantId_EntityId",
                table: "FinanceEventPrices",
                columns: new[] { "TenantId", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceEventPrices_TenantId_EventId",
                table: "FinanceEventPrices",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExchangeRates_EntityId_Id",
                table: "FinanceExchangeRates",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExchangeRates_TenantId_OrgId",
                table: "FinanceExchangeRates",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExpenses_EntityId_Id",
                table: "FinanceExpenses",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExpenses_EntityId_Status_Date",
                table: "FinanceExpenses",
                columns: new[] { "EntityId", "Status", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExpenses_PersonId",
                table: "FinanceExpenses",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExpenses_TenantId_EntityId_BudgetId",
                table: "FinanceExpenses",
                columns: new[] { "TenantId", "EntityId", "BudgetId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExpenses_TenantId_EventId",
                table: "FinanceExpenses",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExpenses_TenantId_OrgId",
                table: "FinanceExpenses",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExpenses_TenantId_ProjectId",
                table: "FinanceExpenses",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceExpenses_TenantId_RequestId",
                table: "FinanceExpenses",
                columns: new[] { "TenantId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceGrants_EntityId_Id",
                table: "FinanceGrants",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceGrants_EntityId_UserId",
                table: "FinanceGrants",
                columns: new[] { "EntityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceGrants_TenantId_OrgId",
                table: "FinanceGrants",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceIntents_EntityId_Id",
                table: "FinanceIntents",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceIntents_EntityId_Key",
                table: "FinanceIntents",
                columns: new[] { "EntityId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceIntents_EntityId_ProviderId",
                table: "FinanceIntents",
                columns: new[] { "EntityId", "ProviderId" },
                unique: true,
                filter: "\"ProviderId\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceIntents_TenantId_EntityId_InvoiceId",
                table: "FinanceIntents",
                columns: new[] { "TenantId", "EntityId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceIntents_TenantId_EntityId_PaymentId",
                table: "FinanceIntents",
                columns: new[] { "TenantId", "EntityId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceIntents_TenantId_OrgId",
                table: "FinanceIntents",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoiceLines_EntityId_Id",
                table: "FinanceInvoiceLines",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoiceLines_TenantId_EntityId_InvoiceId",
                table: "FinanceInvoiceLines",
                columns: new[] { "TenantId", "EntityId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoiceLines_TenantId_OrgId",
                table: "FinanceInvoiceLines",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_EntityId_Id",
                table: "FinanceInvoices",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_EntityId_IdempotencyKey",
                table: "FinanceInvoices",
                columns: new[] { "EntityId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_EntityId_Number",
                table: "FinanceInvoices",
                columns: new[] { "EntityId", "Number" },
                unique: true,
                filter: "\"Number\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_EntityId_PersonId_Date",
                table: "FinanceInvoices",
                columns: new[] { "EntityId", "PersonId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_PersonId",
                table: "FinanceInvoices",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_TenantId_EntityId_PlanId",
                table: "FinanceInvoices",
                columns: new[] { "TenantId", "EntityId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_TenantId_EntityId_PledgeId",
                table: "FinanceInvoices",
                columns: new[] { "TenantId", "EntityId", "PledgeId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_TenantId_EventId",
                table: "FinanceInvoices",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_TenantId_MembershipId",
                table: "FinanceInvoices",
                columns: new[] { "TenantId", "MembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_TenantId_OrgId",
                table: "FinanceInvoices",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_TenantId_ProjectId",
                table: "FinanceInvoices",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_TenantId_RegistrationId",
                table: "FinanceInvoices",
                columns: new[] { "TenantId", "RegistrationId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJobs_EntityId_Id",
                table: "FinanceJobs",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJobs_EntityId_Status_CreatedAt",
                table: "FinanceJobs",
                columns: new[] { "EntityId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJobs_PlanId_PeriodStart_PeriodEnd",
                table: "FinanceJobs",
                columns: new[] { "PlanId", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJobs_TenantId_EntityId_PlanId",
                table: "FinanceJobs",
                columns: new[] { "TenantId", "EntityId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJobs_TenantId_OrgId",
                table: "FinanceJobs",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournalLines_EntityId_AccountId_JournalId",
                table: "FinanceJournalLines",
                columns: new[] { "EntityId", "AccountId", "JournalId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournalLines_EntityId_Id",
                table: "FinanceJournalLines",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournalLines_PersonId",
                table: "FinanceJournalLines",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournalLines_TenantId_EntityId_AccountId",
                table: "FinanceJournalLines",
                columns: new[] { "TenantId", "EntityId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournalLines_TenantId_EntityId_JournalId",
                table: "FinanceJournalLines",
                columns: new[] { "TenantId", "EntityId", "JournalId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournalLines_TenantId_EventId",
                table: "FinanceJournalLines",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournalLines_TenantId_OrgId",
                table: "FinanceJournalLines",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournalLines_TenantId_ProjectId",
                table: "FinanceJournalLines",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournals_EntityId_Date",
                table: "FinanceJournals",
                columns: new[] { "EntityId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournals_EntityId_Id",
                table: "FinanceJournals",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournals_EntityId_Source",
                table: "FinanceJournals",
                columns: new[] { "EntityId", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournals_ReversesId",
                table: "FinanceJournals",
                column: "ReversesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournals_TenantId_EntityId_PeriodId",
                table: "FinanceJournals",
                columns: new[] { "TenantId", "EntityId", "PeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournals_TenantId_EntityId_ReversesId",
                table: "FinanceJournals",
                columns: new[] { "TenantId", "EntityId", "ReversesId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceJournals_TenantId_OrgId",
                table: "FinanceJournals",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceOrganizations_EntityId_Id",
                table: "FinanceOrganizations",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceOrganizations_EntityId_ServedOrgId",
                table: "FinanceOrganizations",
                columns: new[] { "EntityId", "ServedOrgId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceOrganizations_TenantId_OrgId",
                table: "FinanceOrganizations",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceOrganizations_TenantId_ServedOrgId",
                table: "FinanceOrganizations",
                columns: new[] { "TenantId", "ServedOrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_EntityId_Date",
                table: "FinancePayments",
                columns: new[] { "EntityId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_EntityId_Id",
                table: "FinancePayments",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_EntityId_IdempotencyKey",
                table: "FinancePayments",
                columns: new[] { "EntityId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_EntityId_ReceiptNumber",
                table: "FinancePayments",
                columns: new[] { "EntityId", "ReceiptNumber" },
                unique: true,
                filter: "\"ReceiptNumber\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_EntityId_Reference",
                table: "FinancePayments",
                columns: new[] { "EntityId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_PersonId",
                table: "FinancePayments",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_TenantId_EntityId_BankAccountId",
                table: "FinancePayments",
                columns: new[] { "TenantId", "EntityId", "BankAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_TenantId_OrgId",
                table: "FinancePayments",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePeriods_EntityId_Id",
                table: "FinancePeriods",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePeriods_TenantId_OrgId",
                table: "FinancePeriods",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePledges_EntityId_Id",
                table: "FinancePledges",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePledges_PersonId",
                table: "FinancePledges",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePledges_TenantId_EventId",
                table: "FinancePledges",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePledges_TenantId_OrgId",
                table: "FinancePledges",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePledges_TenantId_ProjectId",
                table: "FinancePledges",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceRefunds_EntityId_Id",
                table: "FinanceRefunds",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceRefunds_EntityId_Reference",
                table: "FinanceRefunds",
                columns: new[] { "EntityId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceRefunds_TenantId_EntityId_InvoiceId",
                table: "FinanceRefunds",
                columns: new[] { "TenantId", "EntityId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceRefunds_TenantId_EntityId_PaymentId",
                table: "FinanceRefunds",
                columns: new[] { "TenantId", "EntityId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceRefunds_TenantId_OrgId",
                table: "FinanceRefunds",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReimbursements_EntityId_Id",
                table: "FinanceReimbursements",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReimbursements_EntityId_Reference",
                table: "FinanceReimbursements",
                columns: new[] { "EntityId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReimbursements_TenantId_EntityId_ExpenseId",
                table: "FinanceReimbursements",
                columns: new[] { "TenantId", "EntityId", "ExpenseId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReimbursements_TenantId_OrgId",
                table: "FinanceReimbursements",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransfers_EntityId_Id",
                table: "FinanceTransfers",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransfers_EntityId_Reference",
                table: "FinanceTransfers",
                columns: new[] { "EntityId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransfers_TenantId_EntityId_EntitlementId",
                table: "FinanceTransfers",
                columns: new[] { "TenantId", "EntityId", "EntitlementId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransfers_TenantId_OrgId",
                table: "FinanceTransfers",
                columns: new[] { "TenantId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceWebhooks_EntityId_Id",
                table: "FinanceWebhooks",
                columns: new[] { "EntityId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceWebhooks_EntityId_ProviderEventId",
                table: "FinanceWebhooks",
                columns: new[] { "EntityId", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceWebhooks_TenantId_OrgId",
                table: "FinanceWebhooks",
                columns: new[] { "TenantId", "OrgId" });
            migrationBuilder.Sql("""

CREATE OR REPLACE FUNCTION finance_immutable() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Final financial history is append only'; END $$;
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceJournals" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceJournalLines" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceCredits" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceAllocations" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceRefunds" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceReimbursements" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceTransfers" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceWebhooks" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceAudits" FOR EACH ROW EXECUTE FUNCTION finance_immutable();
CREATE TRIGGER finance_immutable BEFORE UPDATE OR DELETE ON "FinanceExchangeRates" FOR EACH ROW EXECUTE FUNCTION finance_immutable();

CREATE OR REPLACE FUNCTION finance_balance_check() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE jid uuid; debit numeric; credit numeric; n integer; locked boolean; edate date; pstart date; pend date;
BEGIN
 IF TG_TABLE_NAME='FinanceJournals' THEN jid:=NEW."Id"; ELSE jid:=NEW."JournalId"; END IF;
 SELECT SUM("Debit"),SUM("Credit"),COUNT(*) INTO debit,credit,n FROM "FinanceJournalLines" WHERE "JournalId"=jid;
 IF n<2 OR debit<=0 OR debit<>credit THEN RAISE EXCEPTION 'Journal must balance with at least two positive lines'; END IF;
 SELECT p."Closed",j."Date",p."StartsOn",p."EndsOn" INTO locked,edate,pstart,pend FROM "FinanceJournals" j JOIN "FinancePeriods" p ON p."Id"=j."PeriodId" WHERE j."Id"=jid FOR SHARE OF p;
 IF locked OR edate<pstart OR edate>pend THEN RAISE EXCEPTION 'Posting period is closed or date is outside period'; END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER finance_journal_balance AFTER INSERT ON "FinanceJournals" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_balance_check();
CREATE CONSTRAINT TRIGGER finance_line_balance AFTER INSERT ON "FinanceJournalLines" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_balance_check();
ALTER TABLE "FinanceJournalLines" ADD CONSTRAINT finance_one_side CHECK ("Debit">=0 AND "Credit">=0 AND (("Debit">0 AND "Credit"=0) OR ("Credit">0 AND "Debit"=0)));
ALTER TABLE "FinanceEntitys" ADD CONSTRAINT finance_entity_policy CHECK ("Decimals" BETWEEN 0 AND 4 AND "FiscalMonth" BETWEEN 1 AND 12 AND "Currency" ~ '^[A-Z]{3}$' AND "AccountingMethod"='Accrual' AND "EntityId"="Id");
ALTER TABLE "FinanceInvoices" ADD CONSTRAINT finance_invoice_balances CHECK ("Total">0 AND "Paid">=0 AND "Credited">=0 AND "Paid"+"Credited"<="Total" AND "State" IN ('Draft','Issued','Cancelled'));
ALTER TABLE "FinancePayments" ADD CONSTRAINT finance_payment_balances CHECK ("Amount">0 AND "Allocated">=0 AND "Refunded">=0 AND "Fee">=0 AND "Allocated"+"Refunded"<="Amount" AND "Fee"<="Amount" AND "Status" IN ('Pending','Verified'));
ALTER TABLE "FinanceExpenses" ADD CONSTRAINT finance_expense_amount CHECK ("Amount">0 AND "Paid">=0 AND "Paid"<="Amount");
ALTER TABLE "FinancePeriods" ADD CONSTRAINT finance_period_dates CHECK ("StartsOn"<="EndsOn");
ALTER TABLE "FinanceEntitlements" ADD CONSTRAINT finance_entitlement_balance CHECK ("Amount">=0 AND "Settled">=0 AND "Settled"<="Amount");
CREATE OR REPLACE FUNCTION finance_document_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE invoice_state text;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Financial records cannot be deleted'; END IF;
 IF TG_TABLE_NAME='FinanceInvoices' AND (to_jsonb(OLD)->>'State')<>'Draft' AND (to_jsonb(NEW)-ARRAY['Paid','Credited','RemindedAt','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Paid','Credited','RemindedAt','Version']) THEN RAISE EXCEPTION 'Issued invoice content is immutable'; END IF;
 IF TG_TABLE_NAME='FinancePayments' AND (to_jsonb(OLD)->>'Status')='Verified' AND (to_jsonb(NEW)-ARRAY['Allocated','Refunded','Fee','SettlementStatus','BankAccountId','SettledOn','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Allocated','Refunded','Fee','SettlementStatus','BankAccountId','SettledOn','Version']) THEN RAISE EXCEPTION 'Verified payment identity is immutable'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER finance_document_guard BEFORE UPDATE OR DELETE ON "FinanceInvoices" FOR EACH ROW EXECUTE FUNCTION finance_document_guard();
CREATE TRIGGER finance_document_guard BEFORE UPDATE OR DELETE ON "FinancePayments" FOR EACH ROW EXECUTE FUNCTION finance_document_guard();
CREATE OR REPLACE FUNCTION finance_invoice_line_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF EXISTS(SELECT 1 FROM "FinanceInvoices" WHERE "Id"=COALESCE(NEW."InvoiceId",OLD."InvoiceId") AND "State"<>'Draft') THEN RAISE EXCEPTION 'Finalized invoice lines are immutable'; END IF;
 RETURN COALESCE(NEW,OLD); END $$;
CREATE TRIGGER finance_invoice_line_guard BEFORE INSERT OR UPDATE OR DELETE ON "FinanceInvoiceLines" FOR EACH ROW EXECUTE FUNCTION finance_invoice_line_guard();
CREATE OR REPLACE FUNCTION finance_review_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Financial review history cannot be deleted'; END IF;
 IF (to_jsonb(OLD)->>'Status')<>'Draft' AND (to_jsonb(NEW)-ARRAY['Status','Paid','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Status','Paid','Version']) THEN RAISE EXCEPTION 'Reviewed financial content is immutable'; END IF; RETURN NEW; END $$;
CREATE TRIGGER finance_review_guard BEFORE UPDATE OR DELETE ON "FinanceExpenses" FOR EACH ROW EXECUTE FUNCTION finance_review_guard();
CREATE TRIGGER finance_review_guard BEFORE UPDATE OR DELETE ON "FinanceBudgets" FOR EACH ROW EXECUTE FUNCTION finance_review_guard();
CREATE OR REPLACE FUNCTION finance_plan_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF TG_OP='DELETE' OR (to_jsonb(NEW)-ARRAY['AutoBill','NextBillOn','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['AutoBill','NextBillOn','Version']) THEN RAISE EXCEPTION 'Fee schedule definitions are immutable; create a new effective-dated plan'; END IF;RETURN NEW;END $$;
CREATE TRIGGER finance_plan_guard BEFORE UPDATE OR DELETE ON "DuesPlans" FOR EACH ROW EXECUTE FUNCTION finance_plan_guard();
CREATE OR REPLACE FUNCTION finance_entity_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."Currency"<>OLD."Currency" OR NEW."Decimals"<>OLD."Decimals" OR NEW."TenantId"<>OLD."TenantId" OR NEW."OrgId"<>OLD."OrgId" THEN RAISE EXCEPTION 'Entity accounting identity is immutable'; END IF;RETURN NEW;END $$;
CREATE TRIGGER finance_entity_guard BEFORE UPDATE ON "FinanceEntitys" FOR EACH ROW EXECUTE FUNCTION finance_entity_guard();
CREATE OR REPLACE FUNCTION finance_money_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE currency_decimals integer; v jsonb; field text;
BEGIN
 SELECT "Decimals" INTO currency_decimals FROM "FinanceEntitys" WHERE "Id"=NEW."EntityId";
 v:=to_jsonb(NEW);
 FOREACH field IN ARRAY ARRAY['Amount','Paid','Credited','Allocated','Refunded','Fee','Subtotal','Tax','Total','Debit','Credit','Net','Discount','Rate','Settled'] LOOP
 IF v ? field AND (TG_TABLE_NAME<>'FinanceExchangeRates' OR field<>'Rate') AND (v->>field)::numeric <> ROUND((v->>field)::numeric,currency_decimals) THEN RAISE EXCEPTION 'Amount exceeds native currency precision';END IF;
 END LOOP;
 IF v ? 'Currency' AND TG_TABLE_NAME NOT IN ('FinanceExchangeRates') AND v->>'Currency'<>(SELECT "Currency" FROM "FinanceEntitys" WHERE "Id"=NEW."EntityId") THEN RAISE EXCEPTION 'Native financial currency mismatch';END IF;RETURN NEW;END $$;
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceInvoices" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceInvoiceLines" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinancePayments" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceAllocations" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceCredits" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceRefunds" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceJournalLines" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceExpenses" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceBudgets" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinancePledges" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceEntitlements" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceTransfers" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceReimbursements" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceBankTransactions" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();
CREATE TRIGGER finance_money_guard BEFORE INSERT OR UPDATE ON "FinanceEventPrices" FOR EACH ROW EXECUTE FUNCTION finance_money_guard();

INSERT INTO "Permissions" ("Id") VALUES ('finance.view') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.view' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.manage') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.manage' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.configure') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.configure' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.reconcile') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.reconcile' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.post') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.post' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.approve') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.approve' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('finance.self') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.self' FROM "Roles" WHERE "Name"='Association Super Admin' ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'finance.self' FROM "Roles" ON CONFLICT DO NOTHING;

""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceAllocations");

            migrationBuilder.DropTable(
                name: "FinanceAudits");

            migrationBuilder.DropTable(
                name: "FinanceBankTransactions");

            migrationBuilder.DropTable(
                name: "FinanceCredits");

            migrationBuilder.DropTable(
                name: "FinanceEventPrices");

            migrationBuilder.DropTable(
                name: "FinanceExchangeRates");

            migrationBuilder.DropTable(
                name: "FinanceGrants");

            migrationBuilder.DropTable(
                name: "FinanceIntents");

            migrationBuilder.DropTable(
                name: "FinanceInvoiceLines");

            migrationBuilder.DropTable(
                name: "FinanceJobs");

            migrationBuilder.DropTable(
                name: "FinanceJournalLines");

            migrationBuilder.DropTable(
                name: "FinanceOrganizations");

            migrationBuilder.DropTable(
                name: "FinanceRefunds");

            migrationBuilder.DropTable(
                name: "FinanceReimbursements");

            migrationBuilder.DropTable(
                name: "FinanceTransfers");

            migrationBuilder.DropTable(
                name: "FinanceWebhooks");

            migrationBuilder.DropTable(
                name: "FinanceAccounts");

            migrationBuilder.DropTable(
                name: "FinanceJournals");

            migrationBuilder.DropTable(
                name: "FinancePayments");

            migrationBuilder.DropTable(
                name: "FinanceExpenses");

            migrationBuilder.DropTable(
                name: "FinanceEntitlements");

            migrationBuilder.DropTable(
                name: "FinancePeriods");

            migrationBuilder.DropTable(
                name: "FinanceBanks");

            migrationBuilder.DropTable(
                name: "FinanceBudgets");

            migrationBuilder.DropTable(
                name: "FinanceInvoices");

            migrationBuilder.DropTable(
                name: "DuesPlans");

            migrationBuilder.DropTable(
                name: "FinancePledges");

            migrationBuilder.DropTable(
                name: "FinanceEntitys");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "finance.approve");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "finance.configure");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "finance.manage");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "finance.post");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "finance.reconcile");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "finance.self");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "finance.view");
        }
    }
}
