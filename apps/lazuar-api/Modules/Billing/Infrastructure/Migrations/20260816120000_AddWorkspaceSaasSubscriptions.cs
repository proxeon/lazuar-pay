using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Billing.Infrastructure;

#nullable disable

namespace Modules.Billing.Infrastructure.Migrations
{
    [DbContext(typeof(BillingDbContext))]
    [Migration("20260816120000_AddWorkspaceSaasSubscriptions")]
    public partial class AddWorkspaceSaasSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceSaasSubscriptions",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentPeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextInvoiceAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastGatewayTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceSaasSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSaasSubscriptions_OrganizationId",
                schema: "billing",
                table: "WorkspaceSaasSubscriptions",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceSaasSubscriptions",
                schema: "billing");
        }
    }
}
