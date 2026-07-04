using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    public partial class RefactorDunningEngine : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                schema: "commerce",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionType",
                schema: "commerce",
                table: "DunningSteps",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmailBody",
                schema: "commerce",
                table: "DunningSteps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                schema: "commerce",
                table: "DunningSteps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppBody",
                schema: "commerce",
                table: "DunningSteps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChurnedSubscriptions",
                schema: "commerce",
                table: "DunningCampaigns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PriorityOrder",
                schema: "commerce",
                table: "DunningCampaigns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RecoveredRevenue",
                schema: "commerce",
                table: "DunningCampaigns",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SavedSubscriptions",
                schema: "commerce",
                table: "DunningCampaigns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // DATA PRESERVATION: Safely copy existing templates only if the communications table exists (bypasses isolated test db crashes)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT FROM information_schema.tables 
                        WHERE table_schema = 'communications' 
                        AND table_name = 'MessageTemplates'
                    ) THEN
                        UPDATE commerce.""DunningSteps"" ds
                        SET ""Subject"" = mt.""Subject"",
                            ""EmailBody"" = mt.""EmailBody"",
                            ""WhatsAppBody"" = mt.""WhatsAppBody"",
                            ""ActionType"" = CASE 
                                WHEN mt.""Channel"" = 'ALL' THEN 'EMAIL'
                                ELSE mt.""Channel""
                            END
                        FROM communications.""MessageTemplates"" mt
                        WHERE ds.""TemplateId"" = mt.""Id"";
                    END IF;
                END
                $$;
            ");

            // Ensure ActionType is not null for any steps that didn't have a matching template
            migrationBuilder.Sql(@"
                UPDATE commerce.""DunningSteps""
                SET ""ActionType"" = 'EMAIL'
                WHERE ""ActionType"" IS NULL OR ""ActionType"" = '';
            ");

            migrationBuilder.DropColumn(
                name: "Channel",
                schema: "commerce",
                table: "DunningSteps");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                schema: "commerce",
                table: "DunningSteps");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                schema: "commerce",
                table: "DunningSteps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                schema: "commerce",
                table: "DunningSteps",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ActionType",
                schema: "commerce",
                table: "DunningSteps");

            migrationBuilder.DropColumn(
                name: "EmailBody",
                schema: "commerce",
                table: "DunningSteps");

            migrationBuilder.DropColumn(
                name: "Subject",
                schema: "commerce",
                table: "DunningSteps");

            migrationBuilder.DropColumn(
                name: "WhatsAppBody",
                schema: "commerce",
                table: "DunningSteps");

            migrationBuilder.DropColumn(
                name: "ChurnedSubscriptions",
                schema: "commerce",
                table: "DunningCampaigns");

            migrationBuilder.DropColumn(
                name: "PriorityOrder",
                schema: "commerce",
                table: "DunningCampaigns");

            migrationBuilder.DropColumn(
                name: "RecoveredRevenue",
                schema: "commerce",
                table: "DunningCampaigns");

            migrationBuilder.DropColumn(
                name: "SavedSubscriptions",
                schema: "commerce",
                table: "DunningCampaigns");
        }
    }
}
