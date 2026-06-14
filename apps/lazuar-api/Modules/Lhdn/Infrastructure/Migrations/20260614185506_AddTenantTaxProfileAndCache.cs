using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Lhdn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantTaxProfileAndCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Environment",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdType",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdValue",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MsicCode",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MyInvoisClientId",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MyInvoisClientSecret",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierTin",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextPollAt",
                schema: "lhdn",
                table: "TaxDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PollAttempts",
                schema: "lhdn",
                table: "TaxDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TinValidateCaches",
                schema: "lhdn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tin = table.Column<string>(type: "text", nullable: false),
                    IdType = table.Column<string>(type: "text", nullable: false),
                    IdValueHash = table.Column<string>(type: "text", nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    TaxpayerName = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TinValidateCaches", x => x.Id);
                });

            migrationBuilder.UpdateData(
                schema: "lhdn",
                table: "TenantConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "Environment", "IdType", "IdValue", "MsicCode", "MyInvoisClientId", "MyInvoisClientSecret", "SupplierTin" },
                values: new object[] { "SANDBOX", "BRN", "202401234567", "62010", null, null, "C12345678901" });

            migrationBuilder.CreateIndex(
                name: "IX_TinValidateCaches_OrganizationId_Tin_IdType_IdValueHash",
                schema: "lhdn",
                table: "TinValidateCaches",
                columns: new[] { "OrganizationId", "Tin", "IdType", "IdValueHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TinValidateCaches",
                schema: "lhdn");

            migrationBuilder.DropColumn(
                name: "Environment",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "IdType",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "IdValue",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "MsicCode",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "MyInvoisClientId",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "MyInvoisClientSecret",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "SupplierTin",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "NextPollAt",
                schema: "lhdn",
                table: "TaxDocuments");

            migrationBuilder.DropColumn(
                name: "PollAttempts",
                schema: "lhdn",
                table: "TaxDocuments");
        }
    }
}
