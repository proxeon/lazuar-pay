using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.One.Infrastructure.Migrations
{
    public partial class DropLegacySchemas : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS community CASCADE;");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS vault CASCADE;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
