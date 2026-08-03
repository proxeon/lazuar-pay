using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxInboxRetryAndDeadLetter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedAt_OccurredOn",
                schema: "billing",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_ProcessedAt_ReceivedAt",
                schema: "billing",
                table: "InboxMessages");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "billing",
                table: "OutboxMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                schema: "billing",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "billing",
                table: "OutboxMessages",
                type: "text",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "billing",
                table: "InboxMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                schema: "billing",
                table: "InboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "billing",
                table: "InboxMessages",
                type: "text",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_NextAttemptAt_OccurredOn",
                schema: "billing",
                table: "OutboxMessages",
                columns: new[] { "NextAttemptAt", "OccurredOn" },
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_NextAttemptAt_ReceivedAt",
                schema: "billing",
                table: "InboxMessages",
                columns: new[] { "NextAttemptAt", "ReceivedAt" },
                filter: "\"ProcessedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_NextAttemptAt_OccurredOn",
                schema: "billing",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_NextAttemptAt_ReceivedAt",
                schema: "billing",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "billing",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "billing",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "billing",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "billing",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "billing",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "billing",
                table: "InboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_OccurredOn",
                schema: "billing",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "OccurredOn" },
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedAt_ReceivedAt",
                schema: "billing",
                table: "InboxMessages",
                columns: new[] { "ProcessedAt", "ReceivedAt" },
                filter: "\"ProcessedAt\" IS NULL");
        }
    }
}
