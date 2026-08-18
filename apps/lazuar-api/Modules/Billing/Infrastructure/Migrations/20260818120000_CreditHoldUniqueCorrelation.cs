using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Billing.Infrastructure;

#nullable disable

namespace Modules.Billing.Infrastructure.Migrations;

[DbContext(typeof(BillingDbContext))]
[Migration("20260818120000_CreditHoldUniqueCorrelation")]
public partial class CreditHoldUniqueCorrelation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CreditHolds_OrganizationId_CorrelationId",
            schema: "billing",
            table: "CreditHolds");

        migrationBuilder.CreateIndex(
            name: "IX_CreditHolds_OrganizationId_CorrelationId",
            schema: "billing",
            table: "CreditHolds",
            columns: new[] { "OrganizationId", "CorrelationId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CreditHolds_OrganizationId_CorrelationId",
            schema: "billing",
            table: "CreditHolds");

        migrationBuilder.CreateIndex(
            name: "IX_CreditHolds_OrganizationId_CorrelationId",
            schema: "billing",
            table: "CreditHolds",
            columns: new[] { "OrganizationId", "CorrelationId" });
    }
}
