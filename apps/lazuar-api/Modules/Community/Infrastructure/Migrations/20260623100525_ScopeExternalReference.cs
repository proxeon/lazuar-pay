using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScopeExternalReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentRecords_ExternalReference",
                schema: "community",
                table: "PaymentRecords");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRecords_SubscriptionId",
                schema: "community",
                table: "PaymentRecords");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_SubscriptionId_ExternalReference",
                schema: "community",
                table: "PaymentRecords",
                columns: new[] { "SubscriptionId", "ExternalReference" },
                unique: true,
                filter: "\"ExternalReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentRecords_SubscriptionId_ExternalReference",
                schema: "community",
                table: "PaymentRecords");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_ExternalReference",
                schema: "community",
                table: "PaymentRecords",
                column: "ExternalReference",
                unique: true,
                filter: "\"ExternalReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_SubscriptionId",
                schema: "community",
                table: "PaymentRecords",
                column: "SubscriptionId");
        }
    }
}
