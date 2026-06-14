using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Ops.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateToolHistoryToArrays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ToolStatus",
                schema: "ops",
                table: "Messages",
                newName: "ExecutedToolsJson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExecutedToolsJson",
                schema: "ops",
                table: "Messages",
                newName: "ToolStatus");
        }
    }
}
