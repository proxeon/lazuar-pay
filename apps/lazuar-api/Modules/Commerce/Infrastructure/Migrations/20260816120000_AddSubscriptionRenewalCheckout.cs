using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionRenewalCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentRenewalCheckoutForDate",
                schema: "commerce",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentRenewalCheckoutUrl",
                schema: "commerce",
                table: "Subscriptions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentRenewalCheckoutForDate",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CurrentRenewalCheckoutUrl",
                schema: "commerce",
                table: "Subscriptions");
        }
    }
}
