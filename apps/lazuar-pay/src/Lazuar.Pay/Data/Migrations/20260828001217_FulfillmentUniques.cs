using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    /// <inheritdoc />
    public partial class FulfillmentUniques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_documents_CheckoutId",
                schema: "public",
                table: "documents",
                column: "CheckoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_OrgId_Number",
                schema: "public",
                table: "documents",
                columns: new[] { "OrgId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_charges_CheckoutId",
                schema: "public",
                table: "charges",
                column: "CheckoutId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_documents_CheckoutId",
                schema: "public",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_OrgId_Number",
                schema: "public",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_charges_CheckoutId",
                schema: "public",
                table: "charges");
        }
    }
}
