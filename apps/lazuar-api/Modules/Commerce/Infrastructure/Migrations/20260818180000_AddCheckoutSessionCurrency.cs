using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutSessionCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "commerce",
                table: "CheckoutSessions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "MYR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "commerce",
                table: "CheckoutSessions");
        }
    }
}
