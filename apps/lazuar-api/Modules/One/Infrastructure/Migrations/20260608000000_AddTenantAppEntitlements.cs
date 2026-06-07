using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.One.Infrastructure.Migrations
{
    public partial class AddTenantAppEntitlements : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantAppEntitlements",
                schema: "one",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantAppEntitlements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantAppEntitlements_OrganizationId_AppId",
                schema: "one",
                table: "TenantAppEntitlements",
                columns: new[] { "OrganizationId", "AppId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantAppEntitlements",
                schema: "one");
        }
    }
}
