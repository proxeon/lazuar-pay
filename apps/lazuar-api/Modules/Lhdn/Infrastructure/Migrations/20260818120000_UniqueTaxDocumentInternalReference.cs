using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Lhdn.Infrastructure.Migrations;

/// <inheritdoc />
public partial class UniqueTaxDocumentInternalReference : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM lhdn."TaxDocuments" d
            WHERE d."Id" NOT IN (
                SELECT DISTINCT ON (k."OrganizationId", k."InternalReferenceId") k."Id"
                FROM lhdn."TaxDocuments" k
                ORDER BY k."OrganizationId", k."InternalReferenceId",
                    CASE k."ValidationStatus"
                        WHEN 'VALID' THEN 0
                        WHEN 'SUBMITTED' THEN 1
                        ELSE 2
                    END,
                    k."CreatedAt"
            );
            """);

        migrationBuilder.DropIndex(
            name: "IX_TaxDocuments_OrganizationId_InternalReferenceId",
            schema: "lhdn",
            table: "TaxDocuments");

        migrationBuilder.CreateIndex(
            name: "IX_TaxDocuments_OrganizationId_InternalReferenceId",
            schema: "lhdn",
            table: "TaxDocuments",
            columns: new[] { "OrganizationId", "InternalReferenceId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TaxDocuments_OrganizationId_InternalReferenceId",
            schema: "lhdn",
            table: "TaxDocuments");

        migrationBuilder.CreateIndex(
            name: "IX_TaxDocuments_OrganizationId_InternalReferenceId",
            schema: "lhdn",
            table: "TaxDocuments",
            columns: new[] { "OrganizationId", "InternalReferenceId" });
    }
}
