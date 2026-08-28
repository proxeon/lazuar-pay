using System;
using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations;

[DbContext(typeof(PayDbContext))]
[Migration("20260828180000_RefundsAndDunning")]
public partial class RefundsAndDunning : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AttemptCount",
            schema: "public",
            table: "subscriptions",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CreatedAt",
            schema: "public",
            table: "subscriptions",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero));

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PastDueAt",
            schema: "public",
            table: "subscriptions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_subscriptions_CheckoutId",
            schema: "public",
            table: "subscriptions",
            column: "CheckoutId");

        migrationBuilder.CreateIndex(
            name: "IX_subscriptions_OrgId",
            schema: "public",
            table: "subscriptions",
            column: "OrgId");

        migrationBuilder.CreateTable(
            name: "refunds",
            schema: "public",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                OrgId = table.Column<string>(type: "text", nullable: false),
                CheckoutId = table.Column<string>(type: "text", nullable: false),
                ChargeId = table.Column<string>(type: "text", nullable: true),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Currency = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                Provider = table.Column<string>(type: "text", nullable: false),
                ProviderRef = table.Column<string>(type: "text", nullable: true),
                Reason = table.Column<string>(type: "text", nullable: false),
                IdempotencyKey = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refunds", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_refunds_CheckoutId",
            schema: "public",
            table: "refunds",
            column: "CheckoutId");

        migrationBuilder.CreateIndex(
            name: "IX_refunds_OrgId",
            schema: "public",
            table: "refunds",
            column: "OrgId");

        migrationBuilder.CreateIndex(
            name: "IX_refunds_OrgId_IdempotencyKey",
            schema: "public",
            table: "refunds",
            columns: new[] { "OrgId", "IdempotencyKey" },
            unique: true,
            filter: "\"IdempotencyKey\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "refunds", schema: "public");
        migrationBuilder.DropIndex(name: "IX_subscriptions_CheckoutId", schema: "public", table: "subscriptions");
        migrationBuilder.DropIndex(name: "IX_subscriptions_OrgId", schema: "public", table: "subscriptions");
        migrationBuilder.DropColumn(name: "AttemptCount", schema: "public", table: "subscriptions");
        migrationBuilder.DropColumn(name: "CreatedAt", schema: "public", table: "subscriptions");
        migrationBuilder.DropColumn(name: "PastDueAt", schema: "public", table: "subscriptions");
    }
}
