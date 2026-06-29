using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCommunitySpaceProductIdsList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommunitySpaces_ProductId",
                schema: "community",
                table: "CommunitySpaces");

            migrationBuilder.DropColumn(
                name: "ProductId",
                schema: "community",
                table: "CommunitySpaces");

            migrationBuilder.AddColumn<string>(
                name: "ProductIds",
                schema: "community",
                table: "CommunitySpaces",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductIds",
                schema: "community",
                table: "CommunitySpaces");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                schema: "community",
                table: "CommunitySpaces",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_CommunitySpaces_ProductId",
                schema: "community",
                table: "CommunitySpaces",
                column: "ProductId",
                unique: true);
        }
    }
}
