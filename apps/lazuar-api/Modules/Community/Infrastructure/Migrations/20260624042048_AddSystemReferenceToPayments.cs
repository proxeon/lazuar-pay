using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemReferenceToPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the column initially as nullable to prevent constraint violations on existing rows
            migrationBuilder.AddColumn<string>(
                name: "SystemReference",
                schema: "community",
                table: "PaymentRecords",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // 2. Backfill historical records deterministically based on their existing data
            migrationBuilder.Sql(@"
                UPDATE community.""PaymentRecords""
                SET ""SystemReference"" = CASE
                    WHEN ""Amount"" < 0 THEN 'RFD-'
                    WHEN ""Amount"" = 0 THEN 'CMP-'
                    WHEN ""PaymentMethod"" = 'ONLINE_GATEWAY' THEN 'ONL-'
                    ELSE 'MNL-'
                END || UPPER(SUBSTRING(""Id""::text FROM 25 FOR 12))
                WHERE ""SystemReference"" IS NULL;
            ");

            // 3. Lock down the column to be strictly required
            migrationBuilder.AlterColumn<string>(
                name: "SystemReference",
                schema: "community",
                table: "PaymentRecords",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            // 4. Apply the unique index
            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_SystemReference",
                schema: "community",
                table: "PaymentRecords",
                column: "SystemReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentRecords_SystemReference",
                schema: "community",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "SystemReference",
                schema: "community",
                table: "PaymentRecords");
        }
    }
}
