using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitTemplateBodies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new columns as nullable to prevent default value injection errors
            migrationBuilder.AddColumn<string>(
                name: "EmailBody",
                schema: "community",
                table: "MessageTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppBody",
                schema: "community",
                table: "MessageTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailBody",
                schema: "community",
                table: "BroadcastCampaigns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppBody",
                schema: "community",
                table: "BroadcastCampaigns",
                type: "text",
                nullable: true);

            // 2. Safely copy the data. Use regex to strip HTML tags for the WhatsApp plain-text version.
            migrationBuilder.Sql(@"
                UPDATE community.""MessageTemplates"" 
                SET 
                    ""EmailBody"" = ""Body"", 
                    ""WhatsAppBody"" = REGEXP_REPLACE(REPLACE(REPLACE(""Body"", '<br>', CHR(10)), '<br/>', CHR(10)), '<[^>]*>', '', 'g');

                UPDATE community.""BroadcastCampaigns"" 
                SET 
                    ""EmailBody"" = ""Body"", 
                    ""WhatsAppBody"" = REGEXP_REPLACE(REPLACE(REPLACE(""Body"", '<br>', CHR(10)), '<br/>', CHR(10)), '<[^>]*>', '', 'g');
            ");

            // 3. Enforce the non-nullable constraints
            migrationBuilder.AlterColumn<string>(
                name: "EmailBody",
                schema: "community",
                table: "MessageTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "WhatsAppBody",
                schema: "community",
                table: "MessageTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "EmailBody",
                schema: "community",
                table: "BroadcastCampaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "WhatsAppBody",
                schema: "community",
                table: "BroadcastCampaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            // 4. Drop the legacy columns
            migrationBuilder.DropColumn(
                name: "Body",
                schema: "community",
                table: "MessageTemplates");

            migrationBuilder.DropColumn(
                name: "Body",
                schema: "community",
                table: "BroadcastCampaigns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore legacy columns
            migrationBuilder.AddColumn<string>(
                name: "Body",
                schema: "community",
                table: "MessageTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                schema: "community",
                table: "BroadcastCampaigns",
                type: "text",
                nullable: true);

            // Revert data
            migrationBuilder.Sql(@"
                UPDATE community.""MessageTemplates"" SET ""Body"" = ""EmailBody"";
                UPDATE community.""BroadcastCampaigns"" SET ""Body"" = ""EmailBody"";
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                schema: "community",
                table: "MessageTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                schema: "community",
                table: "BroadcastCampaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Drop new columns
            migrationBuilder.DropColumn(name: "EmailBody", schema: "community", table: "MessageTemplates");
            migrationBuilder.DropColumn(name: "WhatsAppBody", schema: "community", table: "MessageTemplates");
            migrationBuilder.DropColumn(name: "EmailBody", schema: "community", table: "BroadcastCampaigns");
            migrationBuilder.DropColumn(name: "WhatsAppBody", schema: "community", table: "BroadcastCampaigns");
        }
    }
}
