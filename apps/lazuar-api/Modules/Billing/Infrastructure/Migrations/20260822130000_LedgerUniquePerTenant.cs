using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Billing.Infrastructure;

#nullable disable

namespace Modules.Billing.Infrastructure.Migrations;

[DbContext(typeof(BillingDbContext))]
[Migration("20260822130000_LedgerUniquePerTenant")]
public partial class LedgerUniquePerTenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_LedgerEntries_ReferenceType_ReferenceId",
            schema: "billing",
            table: "LedgerEntries");

        migrationBuilder.CreateIndex(
            name: "IX_LedgerEntries_OrganizationId_ReferenceType_ReferenceId",
            schema: "billing",
            table: "LedgerEntries",
            columns: new[] { "OrganizationId", "ReferenceType", "ReferenceId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_LedgerEntries_OrganizationId_ReferenceType_ReferenceId",
            schema: "billing",
            table: "LedgerEntries");

        migrationBuilder.CreateIndex(
            name: "IX_LedgerEntries_ReferenceType_ReferenceId",
            schema: "billing",
            table: "LedgerEntries",
            columns: new[] { "ReferenceType", "ReferenceId" },
            unique: true);
    }
}
