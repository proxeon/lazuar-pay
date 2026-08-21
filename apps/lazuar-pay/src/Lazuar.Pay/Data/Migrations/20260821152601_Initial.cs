using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lazuar.Pay.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "charges",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    CheckoutId = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ProviderRef = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "checkouts",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    PublicToken = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Interval = table.Column<string>(type: "text", nullable: false),
                    SuccessUrl = table.Column<string>(type: "text", nullable: true),
                    CancelUrl = table.Column<string>(type: "text", nullable: true),
                    PspRedirectUrl = table.Column<string>(type: "text", nullable: true),
                    PayerName = table.Column<string>(type: "text", nullable: true),
                    PayerEmail = table.Column<string>(type: "text", nullable: true),
                    ProductId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_sequences",
                schema: "public",
                columns: table => new
                {
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    Series = table.Column<string>(type: "text", nullable: false),
                    YearMyt = table.Column<int>(type: "integer", nullable: false),
                    LastN = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_sequences", x => new { x.OrgId, x.Series, x.YearMyt });
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    CheckoutId = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gateway_credentials",
                schema: "public",
                columns: table => new
                {
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    Ciphertext = table.Column<string>(type: "text", nullable: false),
                    Last4 = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gateway_credentials", x => new { x.OrgId, x.Provider });
                });

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                schema: "public",
                columns: table => new
                {
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    CheckoutId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => new { x.OrgId, x.Key });
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    CheckoutId = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "journal_lines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EntryId = table.Column<string>(type: "text", nullable: false),
                    Account = table.Column<string>(type: "text", nullable: false),
                    Dc = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_lines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mail_outbox",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    ToEmail = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mail_outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "one_webhook_events",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DeliveryId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_one_webhook_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "org_settings",
                schema: "public",
                columns: table => new
                {
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    ChargesPaused = table.Column<bool>(type: "boolean", nullable: false),
                    SstRegistered = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_settings", x => x.OrgId);
                });

            migrationBuilder.CreateTable(
                name: "payers",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prices",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Interval = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "psp_webhook_events",
                schema: "public",
                columns: table => new
                {
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    EventId = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_psp_webhook_events", x => new { x.OrgId, x.Provider, x.EventId });
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    CheckoutId = table.Column<string>(type: "text", nullable: false),
                    PayerId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Interval = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_checkouts_OrgId",
                schema: "public",
                table: "checkouts",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_checkouts_PublicToken",
                schema: "public",
                table: "checkouts",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_one_webhook_events_DeliveryId",
                schema: "public",
                table: "one_webhook_events",
                column: "DeliveryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_OrgId",
                schema: "public",
                table: "products",
                column: "OrgId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "public");

            migrationBuilder.DropTable(
                name: "charges",
                schema: "public");

            migrationBuilder.DropTable(
                name: "checkouts",
                schema: "public");

            migrationBuilder.DropTable(
                name: "document_sequences",
                schema: "public");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "public");

            migrationBuilder.DropTable(
                name: "gateway_credentials",
                schema: "public");

            migrationBuilder.DropTable(
                name: "idempotency_keys",
                schema: "public");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "public");

            migrationBuilder.DropTable(
                name: "journal_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "mail_outbox",
                schema: "public");

            migrationBuilder.DropTable(
                name: "one_webhook_events",
                schema: "public");

            migrationBuilder.DropTable(
                name: "org_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "payers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "prices",
                schema: "public");

            migrationBuilder.DropTable(
                name: "products",
                schema: "public");

            migrationBuilder.DropTable(
                name: "psp_webhook_events",
                schema: "public");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "public");
        }
    }
}
