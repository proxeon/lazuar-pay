using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.One.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookEndpointEnabledEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnabledEvents",
                schema: "one",
                table: "TenantWebhookEndpoints",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnabledEvents",
                schema: "one",
                table: "TenantWebhookEndpoints");
        }
    }
}
