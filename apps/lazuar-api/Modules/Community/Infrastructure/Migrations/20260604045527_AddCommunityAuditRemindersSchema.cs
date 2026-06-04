using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityAuditRemindersSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewaySubscriptionId",
                schema: "community",
                table: "Subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentGatewaySessionId",
                schema: "community",
                table: "Subscriptions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewaySubscriptionId",
                schema: "community",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentGatewaySessionId",
                schema: "community",
                table: "Subscriptions");
        }
    }
}
