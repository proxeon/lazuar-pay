using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentWebhookOutboxMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OutboxMessageId",
                schema: "payments",
                table: "PaymentWebhookLogs",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OutboxMessageId",
                schema: "payments",
                table: "PaymentWebhookLogs");
        }
    }
}
