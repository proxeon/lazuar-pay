using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Commerce.Infrastructure;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CommerceDbContext))]
    [Migration("20260817190000_AddTransactionLogSubscriptionId")]
    public partial class AddTransactionLogSubscriptionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionId",
                schema: "commerce",
                table: "TransactionLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_OrganizationId_SubscriptionId_CreatedAt",
                schema: "commerce",
                table: "TransactionLogs",
                columns: new[] { "OrganizationId", "SubscriptionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionLogs_OrganizationId_SubscriptionId_CreatedAt",
                schema: "commerce",
                table: "TransactionLogs");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                schema: "commerce",
                table: "TransactionLogs");
        }
    }
}
