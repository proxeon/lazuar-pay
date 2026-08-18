using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.One.Infrastructure.Migrations;

/// <inheritdoc />
public partial class UniquePendingWorkspaceInvitation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE one."WorkspaceInvitations" d
            SET "Status" = 'REVOKED', "UpdatedAt" = NOW()
            WHERE d."Status" = 'PENDING'
              AND d."Id" NOT IN (
                SELECT DISTINCT ON (k."OrganizationId", k."Email") k."Id"
                FROM one."WorkspaceInvitations" k
                WHERE k."Status" = 'PENDING'
                ORDER BY k."OrganizationId", k."Email", k."CreatedAt" DESC
              );
            """);

        migrationBuilder.DropIndex(
            name: "IX_WorkspaceInvitations_OrganizationId_Email",
            schema: "one",
            table: "WorkspaceInvitations");

        migrationBuilder.CreateIndex(
            name: "IX_WorkspaceInvitations_OrganizationId_Email",
            schema: "one",
            table: "WorkspaceInvitations",
            columns: new[] { "OrganizationId", "Email" },
            unique: true,
            filter: "\"Status\" = 'PENDING'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_WorkspaceInvitations_OrganizationId_Email",
            schema: "one",
            table: "WorkspaceInvitations");

        migrationBuilder.CreateIndex(
            name: "IX_WorkspaceInvitations_OrganizationId_Email",
            schema: "one",
            table: "WorkspaceInvitations",
            columns: new[] { "OrganizationId", "Email" },
            filter: "\"Status\" = 'PENDING'");
    }
}
