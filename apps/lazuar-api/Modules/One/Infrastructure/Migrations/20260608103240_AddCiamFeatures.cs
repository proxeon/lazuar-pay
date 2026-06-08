using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.One.Infrastructure.Migrations
{
    public partial class AddCiamFeatures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerificationExpiresAt",
                schema: "one",
                table: "GlobalUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationTokenHash",
                schema: "one",
                table: "GlobalUsers",
                type: "text",
                nullable: true);

            // Set defaultValue to true to grandfather in existing accounts (sysadmin, founder)
            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                schema: "one",
                table: "GlobalUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "one",
                table: "GlobalUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetExpiresAt",
                schema: "one",
                table: "GlobalUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetTokenHash",
                schema: "one",
                table: "GlobalUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                schema: "one",
                table: "GlobalUsers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Use SQL NOW() for existing records instead of year 0001
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "one",
                table: "GlobalUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.CreateTable(
                name: "WorkspaceInvitations",
                schema: "one",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceInvitations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalUsers_EmailVerificationTokenHash",
                schema: "one",
                table: "GlobalUsers",
                column: "EmailVerificationTokenHash",
                unique: true,
                filter: "\"EmailVerificationTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalUsers_PasswordResetTokenHash",
                schema: "one",
                table: "GlobalUsers",
                column: "PasswordResetTokenHash",
                unique: true,
                filter: "\"PasswordResetTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_OrganizationId_Email",
                schema: "one",
                table: "WorkspaceInvitations",
                columns: new[] { "OrganizationId", "Email" },
                filter: "\"Status\" = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_TokenHash",
                schema: "one",
                table: "WorkspaceInvitations",
                column: "TokenHash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceInvitations",
                schema: "one");

            migrationBuilder.DropIndex(
                name: "IX_GlobalUsers_EmailVerificationTokenHash",
                schema: "one",
                table: "GlobalUsers");

            migrationBuilder.DropIndex(
                name: "IX_GlobalUsers_PasswordResetTokenHash",
                schema: "one",
                table: "GlobalUsers");

            migrationBuilder.DropColumn(
                name: "EmailVerificationExpiresAt",
                schema: "one",
                table: "GlobalUsers");

            migrationBuilder.DropColumn(
                name: "EmailVerificationTokenHash",
                schema: "one",
                table: "GlobalUsers");

            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                schema: "one",
                table: "GlobalUsers");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "one",
                table: "GlobalUsers");

            migrationBuilder.DropColumn(
                name: "PasswordResetExpiresAt",
                schema: "one",
                table: "GlobalUsers");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenHash",
                schema: "one",
                table: "GlobalUsers");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                schema: "one",
                table: "GlobalUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "one",
                table: "GlobalUsers");
        }
    }
}
