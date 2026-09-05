using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditActorDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Actor",
                schema: "public",
                table: "audit_events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detail",
                schema: "public",
                table: "audit_events",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Actor",
                schema: "public",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "Detail",
                schema: "public",
                table: "audit_events");
        }
    }
}
