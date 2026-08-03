using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Lhdn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeveloperApiKeyScopesAndKeyHint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing keys predate public hints; empty placeholder is fine for list UI.
            migrationBuilder.AddColumn<string>(
                name: "KeyHint",
                schema: "lhdn",
                table: "DeveloperApiKeys",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "****");

            // Legacy keys retain full document access so production integrators keep working.
            migrationBuilder.AddColumn<string>(
                name: "Scopes",
                schema: "lhdn",
                table: "DeveloperApiKeys",
                type: "text",
                nullable: false,
                defaultValue: "lhdn.documents:write lhdn.documents:read");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeyHint",
                schema: "lhdn",
                table: "DeveloperApiKeys");

            migrationBuilder.DropColumn(
                name: "Scopes",
                schema: "lhdn",
                table: "DeveloperApiKeys");
        }
    }
}
