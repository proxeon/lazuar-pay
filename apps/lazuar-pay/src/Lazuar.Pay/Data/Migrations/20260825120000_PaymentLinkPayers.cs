using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations;

[DbContext(typeof(PayDbContext))]
[Migration("20260825120000_PaymentLinkPayers")]
public partial class PaymentLinkPayers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "payment_links",
            schema: "public",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                OrgId = table.Column<string>(type: "text", nullable: false),
                PublicToken = table.Column<string>(type: "text", nullable: false),
                Provider = table.Column<string>(type: "text", nullable: false),
                ProductId = table.Column<string>(type: "text", nullable: true),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "text", nullable: false),
                MaxPayers = table.Column<int>(type: "integer", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payment_links", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_payment_links_OrgId",
            schema: "public",
            table: "payment_links",
            column: "OrgId");

        migrationBuilder.CreateIndex(
            name: "IX_payment_links_PublicToken",
            schema: "public",
            table: "payment_links",
            column: "PublicToken",
            unique: true);

        migrationBuilder.AddColumn<string>(
            name: "PaymentLinkId",
            schema: "public",
            table: "checkouts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SlotKey",
            schema: "public",
            table: "checkouts",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_checkouts_PaymentLinkId",
            schema: "public",
            table: "checkouts",
            column: "PaymentLinkId");

        migrationBuilder.CreateIndex(
            name: "IX_checkouts_PaymentLinkId_SlotKey",
            schema: "public",
            table: "checkouts",
            columns: ["PaymentLinkId", "SlotKey"],
            unique: true,
            filter: "\"SlotKey\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_checkouts_PaymentLinkId_SlotKey",
            schema: "public",
            table: "checkouts");

        migrationBuilder.DropIndex(
            name: "IX_checkouts_PaymentLinkId",
            schema: "public",
            table: "checkouts");

        migrationBuilder.DropColumn(name: "PaymentLinkId", schema: "public", table: "checkouts");
        migrationBuilder.DropColumn(name: "SlotKey", schema: "public", table: "checkouts");
        migrationBuilder.DropTable(name: "payment_links", schema: "public");
    }
}
