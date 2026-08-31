using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    [DbContext(typeof(PayDbContext))]
    [Migration("20260830120000_CheckoutWatchClaimedAt")]
    public partial class CheckoutWatchClaimedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WatchClaimedAt",
                schema: "public",
                table: "checkouts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WatchClaimedAt",
                schema: "public",
                table: "checkouts");
        }
    }
}
