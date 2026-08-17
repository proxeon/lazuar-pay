using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Commerce.Infrastructure;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CommerceDbContext))]
    [Migration("20260820140000_AddCommerceDisputes")]
    public partial class AddCommerceDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Disputes",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayTransactionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckoutSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disputes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_CreatedAt",
                schema: "commerce",
                table: "Disputes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_OrganizationId_GatewayTransactionId",
                schema: "commerce",
                table: "Disputes",
                columns: new[] { "OrganizationId", "GatewayTransactionId" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "InvoiceReminderDispatchLogs",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOffset = table.Column<int>(type: "integer", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceReminderDispatchLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceReminderDispatchLogs_SessionId_DayOffset",
                schema: "commerce",
                table: "InvoiceReminderDispatchLogs",
                columns: new[] { "SessionId", "DayOffset" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceReminderDispatchLogs",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "Disputes",
                schema: "commerce");
        }
    }
}
