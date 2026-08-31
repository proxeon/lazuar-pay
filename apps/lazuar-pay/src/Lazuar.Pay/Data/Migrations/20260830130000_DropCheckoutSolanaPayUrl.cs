using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    [DbContext(typeof(PayDbContext))]
    [Migration("20260830130000_DropCheckoutSolanaPayUrl")]
    public partial class DropCheckoutSolanaPayUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE public.checkouts
                SET "PspRedirectUrl" = "SolanaPayUrl"
                WHERE "SolanaPayUrl" IS NOT NULL
                  AND ("PspRedirectUrl" IS NULL OR "PspRedirectUrl" = '');
                """);

            migrationBuilder.DropColumn(
                name: "SolanaPayUrl",
                schema: "public",
                table: "checkouts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SolanaPayUrl",
                schema: "public",
                table: "checkouts",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE public.checkouts
                SET "SolanaPayUrl" = "PspRedirectUrl",
                    "PspRedirectUrl" = NULL
                WHERE "PspRedirectUrl" LIKE 'solana:%';
                """);
        }
    }
}
