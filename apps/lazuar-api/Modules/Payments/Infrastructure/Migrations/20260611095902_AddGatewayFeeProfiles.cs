using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGatewayFeeProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedFeePercentage",
                schema: "payments",
                table: "TenantPaymentConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedFee",
                schema: "payments",
                table: "TenantPaymentConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                schema: "payments",
                table: "TenantPaymentConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedFeePercentage",
                schema: "payments",
                table: "TenantPaymentConfigurations");

            migrationBuilder.DropColumn(
                name: "FixedFee",
                schema: "payments",
                table: "TenantPaymentConfigurations");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                schema: "payments",
                table: "TenantPaymentConfigurations");
        }
    }
}
