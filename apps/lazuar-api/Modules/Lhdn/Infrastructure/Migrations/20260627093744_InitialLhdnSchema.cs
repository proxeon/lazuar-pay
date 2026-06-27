using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Lhdn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLhdnSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "lhdn");

            migrationBuilder.CreateTable(
                name: "CountryCodes",
                schema: "lhdn",
                columns: table => new
                {
                    Code = table.Column<string>(type: "text", nullable: false),
                    CountryName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryCodes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "DeveloperApiKeys",
                schema: "lhdn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Prefix = table.Column<string>(type: "text", nullable: false),
                    KeyHash = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeveloperApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyLogs",
                schema: "lhdn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "lhdn",
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
                name: "MsicCodes",
                schema: "lhdn",
                columns: table => new
                {
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CategoryReference = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MsicCodes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "lhdn",
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

            migrationBuilder.CreateTable(
                name: "TaxDocuments",
                schema: "lhdn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InternalReferenceId = table.Column<string>(type: "text", nullable: false),
                    DocumentHash = table.Column<string>(type: "text", nullable: false),
                    RawXmlContent = table.Column<string>(type: "text", nullable: false),
                    LhdnUuid = table.Column<string>(type: "text", nullable: true),
                    SubmissionUid = table.Column<string>(type: "text", nullable: true),
                    LongId = table.Column<string>(type: "text", nullable: true),
                    ValidationStatus = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    IsTestMode = table.Column<bool>(type: "boolean", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextPollAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PollAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxTypes",
                schema: "lhdn",
                columns: table => new
                {
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "TenantConfigs",
                schema: "lhdn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntermediaryMode = table.Column<bool>(type: "boolean", nullable: false),
                    SupplierTin = table.Column<string>(type: "text", nullable: false),
                    IdType = table.Column<string>(type: "text", nullable: false),
                    IdValue = table.Column<string>(type: "text", nullable: false),
                    Environment = table.Column<string>(type: "text", nullable: false),
                    MsicCode = table.Column<string>(type: "text", nullable: true),
                    MyInvoisClientId = table.Column<string>(type: "text", nullable: true),
                    MyInvoisClientSecret = table.Column<string>(type: "text", nullable: true),
                    EncryptedPfxBase64 = table.Column<string>(type: "text", nullable: true),
                    PfxPasswordCiphertext = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TinValidateCaches",
                schema: "lhdn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tin = table.Column<string>(type: "text", nullable: false),
                    IdType = table.Column<string>(type: "text", nullable: false),
                    IdValueHash = table.Column<string>(type: "text", nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    TaxpayerName = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TinValidateCaches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptions",
                schema: "lhdn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Secret = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "lhdn",
                table: "TenantConfigs",
                columns: new[] { "Id", "CreatedAt", "EncryptedPfxBase64", "Environment", "IdType", "IdValue", "IntermediaryMode", "MsicCode", "MyInvoisClientId", "MyInvoisClientSecret", "OrganizationId", "PfxPasswordCiphertext", "SupplierTin", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "SANDBOX", "BRN", "202401234567", true, "62010", null, null, new Guid("7d97963c-063c-4598-86cc-9ddd9d47d9b1"), null, "C12345678901", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperApiKeys_KeyHash",
                schema: "lhdn",
                table: "DeveloperApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperApiKeys_OrganizationId",
                schema: "lhdn",
                table: "DeveloperApiKeys",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyLogs_OrganizationId_IdempotencyKey",
                schema: "lhdn",
                table: "IdempotencyLogs",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedAt_ReceivedAt",
                schema: "lhdn",
                table: "InboxMessages",
                columns: new[] { "ProcessedAt", "ReceivedAt" },
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_OccurredOn",
                schema: "lhdn",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "OccurredOn" },
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDocuments_IsTestMode",
                schema: "lhdn",
                table: "TaxDocuments",
                column: "IsTestMode");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDocuments_OrganizationId_ValidationStatus",
                schema: "lhdn",
                table: "TaxDocuments",
                columns: new[] { "OrganizationId", "ValidationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDocuments_ValidationStatus",
                schema: "lhdn",
                table: "TaxDocuments",
                column: "ValidationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigs_OrganizationId",
                schema: "lhdn",
                table: "TenantConfigs",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TinValidateCaches_OrganizationId_Tin_IdType_IdValueHash",
                schema: "lhdn",
                table: "TinValidateCaches",
                columns: new[] { "OrganizationId", "Tin", "IdType", "IdValueHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_OrganizationId",
                schema: "lhdn",
                table: "WebhookSubscriptions",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CountryCodes",
                schema: "lhdn");

            migrationBuilder.DropTable(
                name: "DeveloperApiKeys",
                schema: "lhdn");

            migrationBuilder.DropTable(
                name: "IdempotencyLogs",
                schema: "lhdn");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "lhdn");

            migrationBuilder.DropTable(
                name: "MsicCodes",
                schema: "lhdn");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "lhdn");

            migrationBuilder.DropTable(
                name: "TaxDocuments",
                schema: "lhdn");

            migrationBuilder.DropTable(
                name: "TaxTypes",
                schema: "lhdn");

            migrationBuilder.DropTable(
                name: "TenantConfigs",
                schema: "lhdn");

            migrationBuilder.DropTable(
                name: "TinValidateCaches",
                schema: "lhdn");

            migrationBuilder.DropTable(
                name: "WebhookSubscriptions",
                schema: "lhdn");
        }
    }
}
