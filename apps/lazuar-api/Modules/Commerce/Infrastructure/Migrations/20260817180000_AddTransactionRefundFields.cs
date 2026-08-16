using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionRefundFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayName",
                schema: "commerce",
                table: "TransactionLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                schema: "commerce",
                table: "TransactionLogs",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                schema: "commerce",
                table: "TransactionLogs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayName",
                schema: "commerce",
                table: "TransactionLogs");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                schema: "commerce",
                table: "TransactionLogs");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                schema: "commerce",
                table: "TransactionLogs");
        }
    }
}
