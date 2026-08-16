using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.CRM.Infrastructure;

#nullable disable

namespace Modules.CRM.Infrastructure.Migrations
{
    [DbContext(typeof(CrmDbContext))]
    [Migration("20260818120000_AddClientProfileCompanyName")]
    public partial class AddClientProfileCompanyName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyName",
                schema: "crm",
                table: "ClientProfiles");
        }
    }
}
