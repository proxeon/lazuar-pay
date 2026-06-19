using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLhdnConsolidationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MsicCode",
                schema: "billing",
                table: "LedgerLines",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxTypeCode",
                schema: "billing",
                table: "LedgerLines",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerType",
                schema: "billing",
                table: "LedgerEntries",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MsicCode",
                schema: "billing",
                table: "LedgerLines");

            migrationBuilder.DropColumn(
                name: "TaxTypeCode",
                schema: "billing",
                table: "LedgerLines");

            migrationBuilder.DropColumn(
                name: "CustomerType",
                schema: "billing",
                table: "LedgerEntries");
        }
    }
}
