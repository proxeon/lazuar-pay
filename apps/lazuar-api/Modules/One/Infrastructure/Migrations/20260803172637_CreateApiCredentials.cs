using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.One.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateApiCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiCredentials",
                schema: "one",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Prefix = table.Column<string>(type: "text", nullable: false),
                    KeyHash = table.Column<string>(type: "text", nullable: false),
                    KeyHint = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Scopes = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCredentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentials_KeyHash",
                schema: "one",
                table: "ApiCredentials",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentials_OrganizationId",
                schema: "one",
                table: "ApiCredentials",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiCredentials",
                schema: "one");
        }
    }
}
