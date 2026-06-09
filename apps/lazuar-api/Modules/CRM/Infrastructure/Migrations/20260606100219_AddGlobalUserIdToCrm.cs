using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.CRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalUserIdToCrm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GlobalUserId",
                schema: "crm",
                table: "ClientProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_GlobalUserId",
                schema: "crm",
                table: "ClientProfiles",
                column: "GlobalUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientProfiles_GlobalUserId",
                schema: "crm",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "GlobalUserId",
                schema: "crm",
                table: "ClientProfiles");
        }
    }
}
