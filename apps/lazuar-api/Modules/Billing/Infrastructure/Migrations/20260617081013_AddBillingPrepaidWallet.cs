using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingPrepaidWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantCreditBalances",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableCredits = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantCreditBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditLedgers",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantCreditBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditLedgers_TenantCreditBalances_TenantCreditBalanceId",
                        column: x => x.TenantCreditBalanceId,
                        principalSchema: "billing",
                        principalTable: "TenantCreditBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgers_TenantCreditBalanceId",
                schema: "billing",
                table: "CreditLedgers",
                column: "TenantCreditBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantCreditBalances_OrganizationId",
                schema: "billing",
                table: "TenantCreditBalances",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditLedgers",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "TenantCreditBalances",
                schema: "billing");
        }
    }
}
