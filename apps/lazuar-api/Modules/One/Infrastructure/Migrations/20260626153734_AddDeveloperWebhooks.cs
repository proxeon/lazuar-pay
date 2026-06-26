using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.One.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeveloperWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantWebhookEndpoints",
                schema: "one",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    SecretKey = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantWebhookEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveryOutboxes",
                schema: "one",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveryOutboxes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantWebhookEndpoints_OrganizationId",
                schema: "one",
                table: "TenantWebhookEndpoints",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryOutboxes_Status_NextAttemptAt",
                schema: "one",
                table: "WebhookDeliveryOutboxes",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantWebhookEndpoints",
                schema: "one");

            migrationBuilder.DropTable(
                name: "WebhookDeliveryOutboxes",
                schema: "one");
        }
    }
}
