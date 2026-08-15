using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionDunningCampaignSnapshotJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DunningCampaignSnapshotJson",
                schema: "commerce",
                table: "Subscriptions",
                type: "jsonb",
                nullable: true);

            // Freeze current live campaign+steps for already-assigned rows so the first
            // post-deploy edit cannot rewrite an in-flight journey.
            migrationBuilder.Sql("""
                UPDATE commerce."Subscriptions" s
                SET "DunningCampaignSnapshotJson" = jsonb_build_object(
                    'v', 1,
                    'campaign_id', c."Id",
                    'captured_at', to_char((now() AT TIME ZONE 'utc'), 'YYYY-MM-DD"T"HH24:MI:SS"Z"'),
                    'name', c."Name",
                    'final_action', c."FinalAction",
                    'grace_period_days', c."GracePeriodDays",
                    'steps', COALESCE(st.steps, '[]'::jsonb)
                )
                FROM commerce."DunningCampaigns" c
                LEFT JOIN LATERAL (
                    SELECT jsonb_agg(
                        jsonb_build_object(
                            'id', d."Id",
                            'day_offset', d."DayOffset",
                            'action_type', d."ActionType",
                            'subject', d."Subject",
                            'email_body', d."EmailBody",
                            'whatsapp_body', d."WhatsAppBody"
                        ) ORDER BY d."DayOffset"
                    ) AS steps
                    FROM commerce."DunningSteps" d
                    WHERE d."DunningCampaignId" = c."Id"
                ) st ON TRUE
                WHERE s."CurrentDunningCampaignId" = c."Id"
                  AND s."DunningCampaignSnapshotJson" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DunningCampaignSnapshotJson",
                schema: "commerce",
                table: "Subscriptions");
        }
    }
}
