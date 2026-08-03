using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DunningEngineDayOffsetAndProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReminderDispatchLogs_SubscriptionId_ScheduleId_TargetBillin~",
                schema: "commerce",
                table: "ReminderDispatchLogs");

            migrationBuilder.AddColumn<int>(
                name: "LastCompletedDayOffset",
                schema: "commerce",
                table: "Subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DayOffset",
                schema: "commerce",
                table: "ReminderDispatchLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDispatchLogs_SubscriptionId_TargetBillingDate_DayOf~",
                schema: "commerce",
                table: "ReminderDispatchLogs",
                columns: new[] { "SubscriptionId", "TargetBillingDate", "DayOffset" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReminderDispatchLogs_SubscriptionId_TargetBillingDate_DayOf~",
                schema: "commerce",
                table: "ReminderDispatchLogs");

            migrationBuilder.DropColumn(
                name: "LastCompletedDayOffset",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "DayOffset",
                schema: "commerce",
                table: "ReminderDispatchLogs");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDispatchLogs_SubscriptionId_ScheduleId_TargetBillin~",
                schema: "commerce",
                table: "ReminderDispatchLogs",
                columns: new[] { "SubscriptionId", "ScheduleId", "TargetBillingDate" },
                unique: true);
        }
    }
}
