using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnrichChargeAttemptLogsForMultiRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChargeAttemptLogs_SubscriptionId_TargetBillingDate",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DunningCampaignId",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DunningStepId",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayName",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayResponseCode",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "BILLING");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "PENDING");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeAttemptLogs_SubscriptionId_TargetBillingDate_AttemptN~",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                columns: new[] { "SubscriptionId", "TargetBillingDate", "AttemptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChargeAttemptLogs_SubscriptionId_TargetBillingDate_AttemptN~",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.DropColumn(
                name: "DunningCampaignId",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.DropColumn(
                name: "DunningStepId",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.DropColumn(
                name: "GatewayName",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.DropColumn(
                name: "GatewayResponseCode",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "commerce",
                table: "ChargeAttemptLogs");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeAttemptLogs_SubscriptionId_TargetBillingDate",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                columns: new[] { "SubscriptionId", "TargetBillingDate" },
                unique: true);
        }
    }
}
