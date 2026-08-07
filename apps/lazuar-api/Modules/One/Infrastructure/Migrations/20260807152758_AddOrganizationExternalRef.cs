using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.One.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationExternalRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalOrgId",
                schema: "one",
                table: "Organizations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalProduct",
                schema: "one",
                table: "Organizations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_ExternalProduct_ExternalOrgId",
                schema: "one",
                table: "Organizations",
                columns: new[] { "ExternalProduct", "ExternalOrgId" },
                unique: true,
                filter: "\"ExternalProduct\" IS NOT NULL AND \"ExternalOrgId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Organizations_ExternalProduct_ExternalOrgId",
                schema: "one",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ExternalOrgId",
                schema: "one",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ExternalProduct",
                schema: "one",
                table: "Organizations");
        }
    }
}
