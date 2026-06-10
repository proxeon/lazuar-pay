using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Ops.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "ops",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "ops",
                table: "Conversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "ops",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "ops",
                table: "Conversations");
        }
    }
}
