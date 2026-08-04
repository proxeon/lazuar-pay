using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeparateReceiptAndConsolidationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConsolidationStatus",
                schema: "billing",
                table: "LedgerEntries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerDocumentNumber",
                schema: "billing",
                table: "LedgerEntries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LhdnDocumentUuid",
                schema: "billing",
                table: "LedgerEntries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_OrganizationId_ConsolidationStatus_Timestamp",
                schema: "billing",
                table: "LedgerEntries",
                columns: new[] { "OrganizationId", "ConsolidationStatus", "Timestamp" });

            // Backfill: copy legacy TaxInvoiceId → CustomerDocumentNumber for local B2C receipts.
            // B2C_RECEIPT rows held the receipt number in TaxInvoiceId; LHDN VALID rows held UUID.
            migrationBuilder.Sql("""
                UPDATE billing."LedgerEntries"
                SET "CustomerDocumentNumber" = "TaxInvoiceId",
                    "ConsolidationStatus" = 'PENDING'
                WHERE "CustomerType" = 'B2C'
                  AND "LhdnValidationStatus" = 'B2C_RECEIPT'
                  AND "TaxInvoiceId" IS NOT NULL
                  AND "TaxInvoiceId" NOT LIKE 'B2C-CONS-%';

                UPDATE billing."LedgerEntries"
                SET "CustomerDocumentNumber" = "TaxInvoiceId",
                    "ConsolidationStatus" = 'CONSOLIDATED'
                WHERE "CustomerType" = 'B2C'
                  AND "LhdnValidationStatus" IN ('CONSOLIDATED_PENDING', 'VALID', 'CANCELLED')
                  AND "TaxInvoiceId" IS NOT NULL
                  AND "TaxInvoiceId" LIKE 'B2C-CONS-%';

                UPDATE billing."LedgerEntries"
                SET "LhdnDocumentUuid" = "TaxInvoiceId"
                WHERE "LhdnValidationStatus" IN ('VALID', 'CANCELLED')
                  AND "TaxInvoiceId" IS NOT NULL
                  AND "TaxInvoiceId" NOT LIKE 'B2C-CONS-%'
                  AND "TaxInvoiceId" NOT LIKE 'RCPT-%';

                UPDATE billing."LedgerEntries"
                SET "ConsolidationStatus" = 'NOT_REQUIRED'
                WHERE "CustomerType" = 'B2B'
                  AND "ConsolidationStatus" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_OrganizationId_ConsolidationStatus_Timestamp",
                schema: "billing",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "ConsolidationStatus",
                schema: "billing",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "CustomerDocumentNumber",
                schema: "billing",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "LhdnDocumentUuid",
                schema: "billing",
                table: "LedgerEntries");
        }
    }
}
