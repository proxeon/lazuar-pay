using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWave3SubscriptionBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                schema: "commerce",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PendingQuantity",
                schema: "commerce",
                table: "Subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingProductId",
                schema: "commerce",
                table: "Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceId",
                schema: "commerce",
                table: "Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitAmount",
                schema: "commerce",
                table: "Subscriptions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BillingInterval",
                schema: "commerce",
                table: "Subscriptions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                schema: "commerce",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CollectionPausedUntil",
                schema: "commerce",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasOpenDispute",
                schema: "commerce",
                table: "Subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TrialDays",
                schema: "commerce",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceId",
                schema: "commerce",
                table: "CheckoutSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                schema: "commerce",
                table: "CheckoutSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductPrices",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Interval = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPrices_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "commerce",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_ProductId_Interval",
                schema: "commerce",
                table: "ProductPrices",
                columns: new[] { "ProductId", "Interval" },
                unique: true);

            migrationBuilder.Sql(
                """
                UPDATE commerce."Subscriptions" s
                SET "UnitAmount" = p."Price"
                FROM commerce."Products" p
                WHERE s."ProductId" = p."Id" AND s."UnitAmount" = 0;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO commerce."ProductPrices" ("Id", "ProductId", "Interval", "Amount", "IsDefault")
                SELECT gen_random_uuid(), p."Id", p."Interval", p."Price", true
                FROM commerce."Products" p
                WHERE NOT EXISTS (
                    SELECT 1 FROM commerce."ProductPrices" pp
                    WHERE pp."ProductId" = p."Id" AND pp."Interval" = p."Interval");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductPrices",
                schema: "commerce");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PendingQuantity",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PendingProductId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PriceId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "UnitAmount",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "BillingInterval",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CollectionPausedUntil",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "HasOpenDispute",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "TrialDays",
                schema: "commerce",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PriceId",
                schema: "commerce",
                table: "CheckoutSessions");

            migrationBuilder.DropColumn(
                name: "DueAt",
                schema: "commerce",
                table: "CheckoutSessions");
        }
    }
}
