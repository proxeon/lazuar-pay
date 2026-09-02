using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefundDocumentsShareCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_documents_CheckoutId",
                schema: "public",
                table: "documents");

            migrationBuilder.CreateIndex(
                name: "IX_documents_CheckoutId",
                schema: "public",
                table: "documents",
                column: "CheckoutId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_documents_CheckoutId",
                schema: "public",
                table: "documents");

            migrationBuilder.CreateIndex(
                name: "IX_documents_CheckoutId",
                schema: "public",
                table: "documents",
                column: "CheckoutId",
                unique: true);
        }
    }
}
