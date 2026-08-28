using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations;

[DbContext(typeof(PayDbContext))]
[Migration("20260828093000_OrgOneWebhookCiphertext")]
public partial class OrgOneWebhookCiphertext : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OneWebhookCiphertext",
            schema: "public",
            table: "org_settings",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "OneWebhookCiphertext",
            schema: "public",
            table: "org_settings");
    }
}
