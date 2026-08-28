using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillNullCheckoutProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE public.checkouts AS c
                SET "Provider" = s."ActiveProvider"
                FROM public.org_settings AS s
                WHERE c."OrgId" = s."OrgId"
                  AND c."Provider" IS NULL
                  AND s."ActiveProvider" IS NOT NULL
                  AND btrim(s."ActiveProvider") <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
