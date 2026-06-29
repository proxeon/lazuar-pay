using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Vault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVaultAssetProductIdsList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VaultAssets_ProductId",
                schema: "vault",
                table: "VaultAssets");

            migrationBuilder.DropColumn(
                name: "ProductId",
                schema: "vault",
                table: "VaultAssets");

            migrationBuilder.AddColumn<string>(
                name: "ProductIds",
                schema: "vault",
                table: "VaultAssets",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductIds",
                schema: "vault",
                table: "VaultAssets");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                schema: "vault",
                table: "VaultAssets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_VaultAssets_ProductId",
                schema: "vault",
                table: "VaultAssets",
                column: "ProductId",
                unique: true);
        }
    }
}
