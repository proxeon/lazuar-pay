using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentConfigEnvironment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Environment",
                schema: "payments",
                table: "TenantPaymentConfigurations",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "test");

            // Existing rows were already charging; do not silently retarget them to sandbox.
            migrationBuilder.Sql(
                """UPDATE payments."TenantPaymentConfigurations" SET "Environment" = 'live';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Environment",
                schema: "payments",
                table: "TenantPaymentConfigurations");
        }
    }
}
