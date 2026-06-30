using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDunningEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DunningCampaigns",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FinalAction = table.Column<string>(type: "text", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    TargetProductIds = table.Column<string>(type: "jsonb", nullable: false),
                    TargetPaymentMethods = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DunningCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DunningSteps",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DunningCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOffset = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DunningSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DunningSteps_DunningCampaigns_DunningCampaignId",
                        column: x => x.DunningCampaignId,
                        principalSchema: "commerce",
                        principalTable: "DunningCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DunningCampaigns_OrganizationId",
                schema: "commerce",
                table: "DunningCampaigns",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_DunningSteps_DunningCampaignId",
                schema: "commerce",
                table: "DunningSteps",
                column: "DunningCampaignId");

            // --- DATA BACKFILL SCRIPT: Migrates flat schedules into grouped Campaigns ---
            migrationBuilder.Sql(@"
                INSERT INTO commerce.""DunningCampaigns"" (
                    ""Id"", ""OrganizationId"", ""Name"", ""IsActive"", ""FinalAction"", 
                    ""GracePeriodDays"", ""TargetProductIds"", ""TargetPaymentMethods"", 
                    ""CreatedAt"", ""UpdatedAt""
                )
                SELECT DISTINCT ON (""OrganizationId"")
                    gen_random_uuid(), ""OrganizationId"", 'Legacy Global Recovery', true, 'CANCEL',
                    3, '[]'::jsonb, '[]'::jsonb, NOW(), NOW()
                FROM commerce.""ReminderSchedules"";

                INSERT INTO commerce.""DunningSteps"" (
                    ""Id"", ""DunningCampaignId"", ""DayOffset"", ""TemplateId"", ""Channel""
                )
                SELECT 
                    gen_random_uuid(), c.""Id"", r.""DaysRelativeToDue"", r.""TemplateId"", r.""Channel""
                FROM commerce.""ReminderSchedules"" r
                JOIN commerce.""DunningCampaigns"" c ON r.""OrganizationId"" = c.""OrganizationId"";
            ");

            migrationBuilder.DropTable(
                name: "ReminderSchedules",
                schema: "commerce");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentDunningCampaignId",
                schema: "commerce",
                table: "Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentDunningStepIndex",
                schema: "commerce",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DunningPausedUntil",
                schema: "commerce",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DunningSteps",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "DunningCampaigns",
                schema: "commerce");

            migrationBuilder.CreateTable(
                name: "ReminderSchedules",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DaysRelativeToDue = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeOfDay = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderSchedules_OrganizationId_DaysRelativeToDue",
                schema: "commerce",
                table: "ReminderSchedules",
                columns: new[] { "OrganizationId", "DaysRelativeToDue" });

            migrationBuilder.DropColumn(
                name: "CurrentDunningCampaignId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CurrentDunningStepIndex",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "DunningPausedUntil",
                schema: "commerce",
                table: "Subscriptions");
        }
    }
}
