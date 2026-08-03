using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentWebhookBusinessKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessKey",
                schema: "payments",
                table: "PaymentWebhookLogs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookLogs_Provider_BusinessKey",
                schema: "payments",
                table: "PaymentWebhookLogs",
                columns: new[] { "Provider", "BusinessKey" },
                unique: true,
                filter: "\"BusinessKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentWebhookLogs_Provider_BusinessKey",
                schema: "payments",
                table: "PaymentWebhookLogs");

            migrationBuilder.DropColumn(
                name: "BusinessKey",
                schema: "payments",
                table: "PaymentWebhookLogs");
        }
    }
}
