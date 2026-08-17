using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Commerce.Infrastructure;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations;

[DbContext(typeof(CommerceDbContext))]
[Migration("20260821120000_AddSubscriptionHasUnitSnapshot")]
public partial class AddSubscriptionHasUnitSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HasUnitSnapshot",
            schema: "commerce",
            table: "Subscriptions",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HasUnitSnapshot",
            schema: "commerce",
            table: "Subscriptions");
    }
}
