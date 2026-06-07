using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationQueue",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "AutomationRules",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "MessageTemplates",
                schema: "messaging");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageTemplates",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    MetaTemplateName = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OptionalVariables = table.Column<string>(type: "jsonb", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredVariables = table.Column<string>(type: "jsonb", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRules",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggerType = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRules_MessageTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "messaging",
                        principalTable: "MessageTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AutomationQueue",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    AutomationRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StepName = table.Column<string>(type: "text", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggerType = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationQueue_AutomationRules_AutomationRuleId",
                        column: x => x.AutomationRuleId,
                        principalSchema: "messaging",
                        principalTable: "AutomationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationQueue_AutomationRuleId",
                schema: "messaging",
                table: "AutomationQueue",
                column: "AutomationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationQueue_OrganizationId",
                schema: "messaging",
                table: "AutomationQueue",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationQueue_Status_ScheduledAt",
                schema: "messaging",
                table: "AutomationQueue",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_OrganizationId_TriggerType",
                schema: "messaging",
                table: "AutomationRules",
                columns: new[] { "OrganizationId", "TriggerType" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_TemplateId",
                schema: "messaging",
                table: "AutomationRules",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_OrganizationId",
                schema: "messaging",
                table: "MessageTemplates",
                column: "OrganizationId");
        }
    }
}
