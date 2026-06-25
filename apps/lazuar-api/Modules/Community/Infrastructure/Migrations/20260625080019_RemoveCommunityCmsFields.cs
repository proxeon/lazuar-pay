using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCommunityCmsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Faq",
                schema: "community",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Features",
                schema: "community",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "LongDescription",
                schema: "community",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Methodology",
                schema: "community",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                schema: "community",
                table: "Plans");

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                schema: "community",
                table: "Plans",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNotes",
                schema: "community",
                table: "Plans");

            migrationBuilder.AddColumn<string>(
                name: "Faq",
                schema: "community",
                table: "Plans",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Features",
                schema: "community",
                table: "Plans",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LongDescription",
                schema: "community",
                table: "Plans",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Methodology",
                schema: "community",
                table: "Plans",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                schema: "community",
                table: "Plans",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
