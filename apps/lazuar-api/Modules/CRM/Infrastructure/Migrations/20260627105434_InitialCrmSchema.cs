using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.CRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCrmSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.CreateTable(
                name: "ClientProfiles",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlobalUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FullName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Tin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IdType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IdValue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AddressLine3 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    City = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StateCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ConsentedToMarketing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_GlobalUserId",
                schema: "crm",
                table: "ClientProfiles",
                column: "GlobalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_OrganizationId_Email_Phone",
                schema: "crm",
                table: "ClientProfiles",
                columns: new[] { "OrganizationId", "Email", "Phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedAt_ReceivedAt",
                schema: "crm",
                table: "InboxMessages",
                columns: new[] { "ProcessedAt", "ReceivedAt" },
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_OccurredOn",
                schema: "crm",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "OccurredOn" },
                filter: "\"ProcessedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientProfiles",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "crm");
        }
    }
}
