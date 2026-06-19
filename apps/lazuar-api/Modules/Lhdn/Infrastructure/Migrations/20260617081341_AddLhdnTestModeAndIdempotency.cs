using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Lhdn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLhdnTestModeAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTestMode",
                schema: "lhdn",
                table: "TaxDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "IdempotencyLogs",
                schema: "lhdn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDocuments_IsTestMode",
                schema: "lhdn",
                table: "TaxDocuments",
                column: "IsTestMode");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyLogs_OrganizationId_IdempotencyKey",
                schema: "lhdn",
                table: "IdempotencyLogs",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyLogs",
                schema: "lhdn");

            migrationBuilder.DropIndex(
                name: "IX_TaxDocuments_IsTestMode",
                schema: "lhdn",
                table: "TaxDocuments");

            migrationBuilder.DropColumn(
                name: "IsTestMode",
                schema: "lhdn",
                table: "TaxDocuments");
        }
    }
}
