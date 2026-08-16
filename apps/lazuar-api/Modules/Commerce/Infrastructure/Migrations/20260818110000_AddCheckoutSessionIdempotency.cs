using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutSessionIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "commerce",
                table: "CheckoutSessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                schema: "commerce",
                table: "CheckoutSessions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayCheckoutUrl",
                schema: "commerce",
                table: "CheckoutSessions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSessions_OrganizationId_IdempotencyKey",
                schema: "commerce",
                table: "CheckoutSessions",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CheckoutSessions_OrganizationId_IdempotencyKey",
                schema: "commerce",
                table: "CheckoutSessions");

            migrationBuilder.DropColumn(
                name: "GatewayCheckoutUrl",
                schema: "commerce",
                table: "CheckoutSessions");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                schema: "commerce",
                table: "CheckoutSessions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "commerce",
                table: "CheckoutSessions");
        }
    }
}
