using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditHoldsAndIdempotencyLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "xmin",
                schema: "billing",
                table: "TenantCreditBalances",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CreditDeductionIdempotencyLogs",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DeductedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditDeductionIdempotencyLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditHolds",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalAmount = table.Column<int>(type: "integer", nullable: false),
                    RemainingAmount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditHolds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditDeductionIdempotencyLogs_OrganizationId_IdempotencyKey",
                schema: "billing",
                table: "CreditDeductionIdempotencyLogs",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditHolds_OrganizationId_CorrelationId",
                schema: "billing",
                table: "CreditHolds",
                columns: new[] { "OrganizationId", "CorrelationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditDeductionIdempotencyLogs",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "CreditHolds",
                schema: "billing");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "billing",
                table: "TenantCreditBalances");
        }
    }
}
