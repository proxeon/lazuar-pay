using System;
using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations;

[DbContext(typeof(PayDbContext))]
[Migration("20260828120000_OrgWebhooks")]
public partial class OrgWebhooks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "org_webhook_endpoints",
            schema: "public",
            columns: table => new
            {
                OrgId = table.Column<string>(type: "text", nullable: false),
                Url = table.Column<string>(type: "text", nullable: false),
                SecretCiphertext = table.Column<string>(type: "text", nullable: false),
                SecretPrefix = table.Column<string>(type: "text", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_org_webhook_endpoints", x => x.OrgId);
            });

        migrationBuilder.CreateTable(
            name: "org_webhook_deliveries",
            schema: "public",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                OrgId = table.Column<string>(type: "text", nullable: false),
                EventId = table.Column<string>(type: "text", nullable: false),
                EventType = table.Column<string>(type: "text", nullable: false),
                PayloadJson = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastHttpStatus = table.Column<int>(type: "integer", nullable: true),
                LastError = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_org_webhook_deliveries", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_org_webhook_deliveries_EventId",
            schema: "public",
            table: "org_webhook_deliveries",
            column: "EventId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_org_webhook_deliveries_Status_NextAttemptAt",
            schema: "public",
            table: "org_webhook_deliveries",
            columns: new[] { "Status", "NextAttemptAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "org_webhook_deliveries", schema: "public");
        migrationBuilder.DropTable(name: "org_webhook_endpoints", schema: "public");
    }
}
