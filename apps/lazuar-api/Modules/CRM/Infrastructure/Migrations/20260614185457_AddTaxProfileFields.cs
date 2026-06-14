using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.CRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine3",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdType",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdValue",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateCode",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tin",
                schema: "crm",
                table: "ClientProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLine1",
                schema: "crm",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                schema: "crm",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "AddressLine3",
                schema: "crm",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "crm",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                schema: "crm",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "IdType",
                schema: "crm",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "IdValue",
                schema: "crm",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "crm",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "StateCode",
                schema: "crm",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "Tin",
                schema: "crm",
                table: "ClientProfiles");
        }
    }
}
