using Dapper;
using System.Data;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Community.Infrastructure.Services;

public partial class CommunityQueryService
{
    private record RawReminderScheduleDto(
        Guid Id, Guid? PlanId, string? PlanName, Guid TemplateId,
        string Channel, int DaysRelativeToDue, string TimeOfDay, bool IsEnabled, DateTime CreatedAt);

    private record RawDeliveryLog(
        Guid Id, string Channel, string Recipient, string? TemplateName,
        string? Subject, string Status, string? ErrorMessage, DateTime CreatedAt);

    public async Task<IEnumerable<CommunityReminderScheduleDto>> GetReminderSchedulesAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT
                r.""Id"", r.""PlanId"", p.""Name"" as PlanName, r.""TemplateId"",
                r.""Channel"", r.""DaysRelativeToDue"", r.""TimeOfDay"", r.""IsEnabled"", r.""CreatedAt""
            FROM community.""ReminderSchedules"" r
            LEFT JOIN community.""Plans"" p ON r.""PlanId"" = p.""Id""
            WHERE r.""OrganizationId"" = @OrgId
            ORDER BY r.""DaysRelativeToDue"", r.""TimeOfDay""";

        var rawSchedules = await connection.QueryAsync<RawReminderScheduleDto>(sql, new { OrgId = organizationId });
        var scheduleList = rawSchedules.ToList();

        if (scheduleList.Count == 0) return Enumerable.Empty<CommunityReminderScheduleDto>();

        var templateIds = scheduleList.Select(x => x.TemplateId).Distinct();
        var templates = await _messageTemplateQueryService.GetTemplatesAsync(templateIds);
        var templateDict = templates.ToDictionary(t => t.Id);

        return scheduleList.Select(r =>
        {
            var templateName = templateDict.TryGetValue(r.TemplateId.ToString(), out var t) ? t.Name : "Unknown Template";
            return new CommunityReminderScheduleDto
            {
                Id = r.Id.ToString(),
                Plan_id = r.PlanId?.ToString(),
                Plan_name = r.PlanName,
                Template_id = r.TemplateId.ToString(),
                Template_name = templateName,
                Channel = r.Channel,
                Days_relative_to_due = r.DaysRelativeToDue,
                Time_of_day = r.TimeOfDay,
                Is_enabled = r.IsEnabled,
                Created_at = new DateTimeOffset(r.CreatedAt)
            };
        });
    }

    public async Task<IEnumerable<DeliveryHistoryItemDto>> GetReminderHistoryAsync(Guid organizationId, Guid subscriptionId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT
                ""Id"",
                ""Channel"",
                ""RecipientIdentifier"" as Recipient,
                ""TemplateName"",
                ""Subject"",
                ""Status"",
                ""ErrorMessage"",
                ""CreatedAt""
            FROM messaging.""MessageLogs""
            WHERE ""OrganizationId"" = @OrgId AND ""BookingId"" = @SubId
            ORDER BY ""CreatedAt"" DESC
            LIMIT 50";

        var rawLogs = await connection.QueryAsync<RawDeliveryLog>(sql, new { OrgId = organizationId, SubId = subscriptionId });

        return rawLogs.Select(r => new DeliveryHistoryItemDto
        {
            Id = r.Id.ToString(),
            Channel = r.Channel,
            Recipient = r.Recipient,
            Template_name = r.TemplateName,
            Subject = r.Subject,
            Status = r.Status,
            Error_message = r.ErrorMessage,
            Created_at = new DateTimeOffset(r.CreatedAt)
        });
    }
}
