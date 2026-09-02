using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    /// <inheritdoc />
    public partial class LatePayRefundOnePerCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue 009: duplicate late_pay refunds per checkout may already exist — Stripe async
            // payments deliver two distinct success events and each one used to book its own
            // refund. Keep the best row per checkout (prefer a succeeded refund, then the oldest)
            // before the unique index is created; the dropped rows are the phantom duplicates.
            migrationBuilder.Sql(
                """
                DELETE FROM public.refunds
                WHERE "Reason" = 'late_pay' AND "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id", ROW_NUMBER() OVER (
                            PARTITION BY "CheckoutId"
                            ORDER BY ("Status" = 'succeeded') DESC, "CreatedAt" ASC, "Id" ASC) AS rn
                        FROM public.refunds
                        WHERE "Reason" = 'late_pay') ranked
                    WHERE ranked.rn > 1)
                """);

            migrationBuilder.CreateIndex(
                name: "IX_refunds_CheckoutId_late_pay",
                schema: "public",
                table: "refunds",
                column: "CheckoutId",
                unique: true,
                filter: "\"Reason\" = 'late_pay'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refunds_CheckoutId_late_pay",
                schema: "public",
                table: "refunds");
        }
    }
}
