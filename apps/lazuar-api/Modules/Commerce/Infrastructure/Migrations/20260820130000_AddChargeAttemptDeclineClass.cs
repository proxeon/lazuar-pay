using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Commerce.Infrastructure;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CommerceDbContext))]
    [Migration("20260820130000_AddChargeAttemptDeclineClass")]
    public partial class AddChargeAttemptDeclineClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeclineClass",
                schema: "commerce",
                table: "ChargeAttemptLogs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeclineClass",
                schema: "commerce",
                table: "ChargeAttemptLogs");
        }
    }
}
