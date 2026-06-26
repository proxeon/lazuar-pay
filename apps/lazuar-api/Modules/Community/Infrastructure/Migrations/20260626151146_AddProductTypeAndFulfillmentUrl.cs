using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTypeAndFulfillmentUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FulfillmentFileUrl",
                schema: "community",
                table: "Plans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                schema: "community",
                table: "Plans",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfillmentFileUrl",
                schema: "community",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "ProductType",
                schema: "community",
                table: "Plans");
        }
    }
}
