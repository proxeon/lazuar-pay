using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Payments.Infrastructure;

#nullable disable

namespace Modules.Payments.Infrastructure.Migrations;

[DbContext(typeof(PaymentsDbContext))]
[Migration("20260822120000_AddPaymentWebhookOrganizationId")]
public partial class AddPaymentWebhookOrganizationId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "OrganizationId",
            schema: "payments",
            table: "PaymentWebhookLogs",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.DropIndex(
            name: "IX_PaymentWebhookLogs_Provider_EventId",
            schema: "payments",
            table: "PaymentWebhookLogs");

        migrationBuilder.DropIndex(
            name: "IX_PaymentWebhookLogs_Provider_BusinessKey",
            schema: "payments",
            table: "PaymentWebhookLogs");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentWebhookLogs_OrganizationId_Provider_EventId",
            schema: "payments",
            table: "PaymentWebhookLogs",
            columns: new[] { "OrganizationId", "Provider", "EventId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PaymentWebhookLogs_OrganizationId_Provider_BusinessKey",
            schema: "payments",
            table: "PaymentWebhookLogs",
            columns: new[] { "OrganizationId", "Provider", "BusinessKey" },
            unique: true,
            filter: "\"BusinessKey\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PaymentWebhookLogs_OrganizationId_Provider_EventId",
            schema: "payments",
            table: "PaymentWebhookLogs");

        migrationBuilder.DropIndex(
            name: "IX_PaymentWebhookLogs_OrganizationId_Provider_BusinessKey",
            schema: "payments",
            table: "PaymentWebhookLogs");

        migrationBuilder.DropColumn(
            name: "OrganizationId",
            schema: "payments",
            table: "PaymentWebhookLogs");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentWebhookLogs_Provider_EventId",
            schema: "payments",
            table: "PaymentWebhookLogs",
            columns: new[] { "Provider", "EventId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PaymentWebhookLogs_Provider_BusinessKey",
            schema: "payments",
            table: "PaymentWebhookLogs",
            columns: new[] { "Provider", "BusinessKey" },
            unique: true,
            filter: "\"BusinessKey\" IS NOT NULL");
    }
}
