using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Ops.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUiRequestState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsResolved",
                schema: "ops",
                table: "Messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UiRequestJson",
                schema: "ops",
                table: "Messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsResolved",
                schema: "ops",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "UiRequestJson",
                schema: "ops",
                table: "Messages");
        }
    }
}
