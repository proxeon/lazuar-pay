using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationCheckoutSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationCheckoutSessions",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SuccessUrl = table.Column<string>(type: "text", nullable: false),
                    CancelUrl = table.Column<string>(type: "text", nullable: false),
                    GatewayName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderSessionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GatewayTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    SetupFutureUsage = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationCheckoutSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCheckoutSessions_ExpiresAt",
                schema: "payments",
                table: "IntegrationCheckoutSessions",
                column: "ExpiresAt",
                filter: "\"Status\" = 'open'");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCheckoutSessions_OrganizationId_Id",
                schema: "payments",
                table: "IntegrationCheckoutSessions",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCheckoutSessions_OrganizationId_IdempotencyKey",
                schema: "payments",
                table: "IntegrationCheckoutSessions",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCheckoutSessions_OrganizationId_ProviderSessionId",
                schema: "payments",
                table: "IntegrationCheckoutSessions",
                columns: new[] { "OrganizationId", "ProviderSessionId" },
                filter: "\"ProviderSessionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationCheckoutSessions",
                schema: "payments");
        }
    }
}
