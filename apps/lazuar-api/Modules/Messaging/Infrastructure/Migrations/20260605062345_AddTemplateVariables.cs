using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateVariables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OptionalVariables",
                schema: "messaging",
                table: "MessageTemplates",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "RequiredVariables",
                schema: "messaging",
                table: "MessageTemplates",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptionalVariables",
                schema: "messaging",
                table: "MessageTemplates");

            migrationBuilder.DropColumn(
                name: "RequiredVariables",
                schema: "messaging",
                table: "MessageTemplates");
        }
    }
}
