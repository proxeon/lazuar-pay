using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Communications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSuppressionEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SuppressionEntries",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuppressionEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuppressionEntries_OrganizationId_Email",
                schema: "communications",
                table: "SuppressionEntries",
                columns: new[] { "OrganizationId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuppressionEntries",
                schema: "communications");
        }
    }
}
