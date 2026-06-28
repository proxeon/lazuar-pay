using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    private record RawReminderScheduleDto(
        Guid Id, Guid? ProductId, string? ProductName, Guid TemplateId,
        string Channel, int DaysRelativeToDue, string TimeOfDay, bool IsEnabled, DateTime CreatedAt);

    public async Task<IEnumerable<ReminderScheduleDto>> GetReminderSchedulesAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT
                r.""Id"", r.""ProductId"", p.""Name"" as ProductName, r.""TemplateId"",
                r.""Channel"", r.""DaysRelativeToDue"", r.""TimeOfDay"", r.""IsEnabled"", r.""CreatedAt""
            FROM commerce.""ReminderSchedules"" r
            LEFT JOIN commerce.""Products"" p ON r.""ProductId"" = p.""Id""
            WHERE r.""OrganizationId"" = @OrgId
            ORDER BY r.""DaysRelativeToDue"", r.""TimeOfDay""";

        var rawSchedules = await connection.QueryAsync<RawReminderScheduleDto>(sql, new { OrgId = organizationId });

        return rawSchedules.Select(r => new ReminderScheduleDto
        {
            Id = r.Id.ToString(),
            ProductId = r.ProductId?.ToString(),
            ProductName = r.ProductName,
            TemplateId = r.TemplateId.ToString(),
            TemplateName = "Assigned Template",
            Channel = r.Channel,
            DaysRelativeToDue = r.DaysRelativeToDue,
            TimeOfDay = r.TimeOfDay,
            IsEnabled = r.IsEnabled,
            CreatedAt = new DateTimeOffset(r.CreatedAt)
        }).ToList();
    }
}
