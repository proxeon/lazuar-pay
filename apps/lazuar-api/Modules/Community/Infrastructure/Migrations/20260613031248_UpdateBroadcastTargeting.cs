using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBroadcastTargeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                schema: "community",
                table: "BroadcastCampaigns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "TargetIsReminderOnly",
                schema: "community",
                table: "BroadcastCampaigns",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetStatus",
                schema: "community",
                table: "BroadcastCampaigns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Channel",
                schema: "community",
                table: "BroadcastCampaigns");

            migrationBuilder.DropColumn(
                name: "TargetIsReminderOnly",
                schema: "community",
                table: "BroadcastCampaigns");

            migrationBuilder.DropColumn(
                name: "TargetStatus",
                schema: "community",
                table: "BroadcastCampaigns");
        }
    }
}
