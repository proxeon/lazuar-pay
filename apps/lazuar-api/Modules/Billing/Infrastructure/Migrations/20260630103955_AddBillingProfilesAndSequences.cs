using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingProfilesAndSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentSequences",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prefix = table.Column<string>(type: "text", nullable: false),
                    CurrentValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantBillingProfiles",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalName = table.Column<string>(type: "text", nullable: false),
                    Tin = table.Column<string>(type: "text", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "text", nullable: true),
                    SstRegistrationNumber = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AddressLine3 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    City = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StateCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBillingProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSequences_OrganizationId_Prefix",
                schema: "billing",
                table: "DocumentSequences",
                columns: new[] { "OrganizationId", "Prefix" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingProfiles_OrganizationId",
                schema: "billing",
                table: "TenantBillingProfiles",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentSequences",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "TenantBillingProfiles",
                schema: "billing");
        }
    }
}
