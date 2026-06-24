using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.One.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApplyPendingOneChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bypassed: The AppAccessRequests table was never physically created in the database.
            /*
            migrationBuilder.DropTable(
                name: "AppAccessRequests",
                schema: "one");
            */
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bypassed matching the Up method
            /*
            migrationBuilder.CreateTable(
                name: "AppAccessRequests",
                schema: "one",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GlobalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedApps = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAccessRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAccessRequests_Status",
                schema: "one",
                table: "AppAccessRequests",
                column: "Status",
                filter: "\"Status\" = 'PENDING'");
            */
        }
    }
}
