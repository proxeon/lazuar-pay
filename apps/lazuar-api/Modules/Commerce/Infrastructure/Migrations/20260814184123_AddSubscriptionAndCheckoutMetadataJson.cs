using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionAndCheckoutMetadataJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                schema: "commerce",
                table: "Subscriptions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                schema: "commerce",
                table: "CheckoutSessions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetadataJson",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                schema: "commerce",
                table: "CheckoutSessions");
        }
    }
}
