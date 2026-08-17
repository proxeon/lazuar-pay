using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Commerce.Infrastructure;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CommerceDbContext))]
    [Migration("20260818140000_AddProductSst")]
    public partial class AddProductSst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SstTaxType",
                schema: "commerce",
                table: "Products",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "06");

            migrationBuilder.AddColumn<decimal>(
                name: "SstRatePercent",
                schema: "commerce",
                table: "Products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SstTaxType",
                schema: "commerce",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SstRatePercent",
                schema: "commerce",
                table: "Products");
        }
    }
}
