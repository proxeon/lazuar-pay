using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaymentLinkLabelAndDeliveryOrgEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_org_webhook_deliveries_EventId",
                schema: "public",
                table: "org_webhook_deliveries");

            migrationBuilder.AddColumn<string>(
                name: "Label",
                schema: "public",
                table: "payment_links",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_webhook_deliveries_OrgId_EventId",
                schema: "public",
                table: "org_webhook_deliveries",
                columns: new[] { "OrgId", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_org_webhook_deliveries_OrgId_EventId",
                schema: "public",
                table: "org_webhook_deliveries");

            migrationBuilder.DropColumn(
                name: "Label",
                schema: "public",
                table: "payment_links");

            migrationBuilder.CreateIndex(
                name: "IX_org_webhook_deliveries_EventId",
                schema: "public",
                table: "org_webhook_deliveries",
                column: "EventId",
                unique: true);
        }
    }
}
