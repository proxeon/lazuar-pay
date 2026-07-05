using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Communications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBroadcasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditHoldId",
                schema: "communications",
                table: "Broadcasts");

            migrationBuilder.DropColumn(
                name: "CreditsReserved",
                schema: "communications",
                table: "Broadcasts");

            migrationBuilder.DropColumn(
                name: "CreditsUsed",
                schema: "communications",
                table: "Broadcasts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreditHoldId",
                schema: "communications",
                table: "Broadcasts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditsReserved",
                schema: "communications",
                table: "Broadcasts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CreditsUsed",
                schema: "communications",
                table: "Broadcasts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
