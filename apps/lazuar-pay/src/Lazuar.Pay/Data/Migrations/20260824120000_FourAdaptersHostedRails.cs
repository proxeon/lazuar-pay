using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations;

[DbContext(typeof(PayDbContext))]
[Migration("20260824120000_FourAdaptersHostedRails")]
public partial class FourAdaptersHostedRails : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "WebhookCiphertext",
            schema: "public",
            table: "gateway_credentials",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicMerchantId",
            schema: "public",
            table: "gateway_credentials",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Environment",
            schema: "public",
            table: "gateway_credentials",
            type: "text",
            nullable: false,
            defaultValue: "test");

        migrationBuilder.AddColumn<string>(
            name: "ActiveProvider",
            schema: "public",
            table: "org_settings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Provider",
            schema: "public",
            table: "checkouts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderSessionId",
            schema: "public",
            table: "checkouts",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "WebhookCiphertext", schema: "public", table: "gateway_credentials");
        migrationBuilder.DropColumn(name: "PublicMerchantId", schema: "public", table: "gateway_credentials");
        migrationBuilder.DropColumn(name: "Environment", schema: "public", table: "gateway_credentials");
        migrationBuilder.DropColumn(name: "ActiveProvider", schema: "public", table: "org_settings");
        migrationBuilder.DropColumn(name: "Provider", schema: "public", table: "checkouts");
        migrationBuilder.DropColumn(name: "ProviderSessionId", schema: "public", table: "checkouts");
    }
}
