using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefundSettleColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "public",
                table: "refunds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                schema: "public",
                table: "refunds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                schema: "public",
                table: "refunds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refunds_Status_NextAttemptAt",
                schema: "public",
                table: "refunds",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refunds_Status_NextAttemptAt",
                schema: "public",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "public",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "LastError",
                schema: "public",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "public",
                table: "refunds");
        }
    }
}
