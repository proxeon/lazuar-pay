using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponReservationsAndPendingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PendingCouponId",
                schema: "community",
                table: "Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOriginalPrice",
                schema: "community",
                table: "Coupons",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ReservedCount",
                schema: "community",
                table: "Coupons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PendingCouponId",
                schema: "community",
                table: "Subscriptions",
                column: "PendingCouponId",
                filter: "\"PendingCouponId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_PendingCouponId",
                schema: "community",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PendingCouponId",
                schema: "community",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "MinimumOriginalPrice",
                schema: "community",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "ReservedCount",
                schema: "community",
                table: "Coupons");
        }
    }
}
