using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    private record RawDunningCampaignDto(
        Guid Id, string Name, bool IsActive, string FinalAction, int GracePeriodDays,
        string? TargetProductIdsJson, string? TargetPaymentMethodsJson, string? StepsJson, DateTime CreatedAt);

    private record RawDunningStepDto(int DayOffset, Guid TemplateId, string Channel);

    public async Task<IEnumerable<DunningCampaignDto>> GetDunningCampaignsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            WITH StepData AS (
                SELECT 
                    ""DunningCampaignId"",
                    jsonb_agg(
                        jsonb_build_object(
                            'DayOffset', ""DayOffset"",
                            'TemplateId', ""TemplateId"",
                            'Channel', ""Channel""
                        ) ORDER BY ""DayOffset""
                    ) as StepsJson
                FROM commerce.""DunningSteps""
                GROUP BY ""DunningCampaignId""
            )
            SELECT
                c.""Id"", c.""Name"", c.""IsActive"", c.""FinalAction"", c.""GracePeriodDays"",
                c.""TargetProductIds""::text as TargetProductIdsJson, 
                c.""TargetPaymentMethods""::text as TargetPaymentMethodsJson,
                COALESCE(s.StepsJson::text, '[]') as StepsJson,
                c.""CreatedAt""
            FROM commerce.""DunningCampaigns"" c
            LEFT JOIN StepData s ON c.""Id"" = s.""DunningCampaignId""
            WHERE c.""OrganizationId"" = @OrgId
            ORDER BY c.""CreatedAt"" DESC";

        var rawCampaigns = await connection.QueryAsync<RawDunningCampaignDto>(sql, new { OrgId = organizationId });

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        return rawCampaigns.Select(c =>
        {
            List<Guid> targetProductIds = new();
            if (!string.IsNullOrWhiteSpace(c.TargetProductIdsJson))
            {
                try { targetProductIds = JsonSerializer.Deserialize<List<Guid>>(c.TargetProductIdsJson, jsonOptions) ?? new List<Guid>(); }
                catch { }
            }

            List<string> targetPaymentMethods = new();
            if (!string.IsNullOrWhiteSpace(c.TargetPaymentMethodsJson))
            {
                try { targetPaymentMethods = JsonSerializer.Deserialize<List<string>>(c.TargetPaymentMethodsJson, jsonOptions) ?? new List<string>(); }
                catch { }
            }

            List<RawDunningStepDto> rawSteps = new();
            if (!string.IsNullOrWhiteSpace(c.StepsJson))
            {
                try { rawSteps = JsonSerializer.Deserialize<List<RawDunningStepDto>>(c.StepsJson, jsonOptions) ?? new List<RawDunningStepDto>(); }
                catch { }
            }

            return new DunningCampaignDto
            {
                Id = c.Id.ToString(),
                Name = c.Name,
                Is_active = c.IsActive,
                Final_action = c.FinalAction,
                Grace_period_days = c.GracePeriodDays,
                Target_product_ids = targetProductIds.Select(id => id.ToString()).ToList(),
                Target_payment_methods = targetPaymentMethods,
                Steps = rawSteps.Select(s => new DunningStepDto
                {
                    Day_offset = s.DayOffset,
                    Template_id = s.TemplateId.ToString(),
                    Channel = s.Channel
                }).ToList(),
                Created_at = new DateTimeOffset(c.CreatedAt)
            };
        }).ToList();
    }
}
