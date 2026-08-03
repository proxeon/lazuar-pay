using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Lhdn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLegalAddressAndDocumentInternalIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Postal",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "lhdn",
                table: "TenantConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "lhdn",
                table: "TenantConfigs",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "AddressLine1", "City", "Country", "LegalName", "Postal", "State" },
                values: new object[] { null, null, null, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDocuments_OrganizationId_InternalReferenceId",
                schema: "lhdn",
                table: "TaxDocuments",
                columns: new[] { "OrganizationId", "InternalReferenceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaxDocuments_OrganizationId_InternalReferenceId",
                schema: "lhdn",
                table: "TaxDocuments");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "LegalName",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "Postal",
                schema: "lhdn",
                table: "TenantConfigs");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "lhdn",
                table: "TenantConfigs");
        }
    }
}
