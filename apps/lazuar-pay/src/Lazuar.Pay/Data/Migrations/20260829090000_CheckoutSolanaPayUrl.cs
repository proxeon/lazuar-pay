using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    [DbContext(typeof(PayDbContext))]
    [Migration("20260829090000_CheckoutSolanaPayUrl")]
    public partial class CheckoutSolanaPayUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SolanaPayUrl",
                schema: "public",
                table: "checkouts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SolanaPayUrl",
                schema: "public",
                table: "checkouts");
        }
    }
}
