// apps/lazuar-api/Modules/Community/Infrastructure/Services/CommunityQueryService.Plans.cs
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Community.Infrastructure.Services;

public partial class CommunityQueryService
{
    private record RawPlanDto(
        Guid Id, string Slug, string Name, string Audience,
        decimal Price, string Interval, string? AdminNotes,
        bool IsActive, int DisplayOrder, int? MaxCapacity, int GracePeriodDays,
        string? TelegramInviteLink, string? WeeklyMeetingLink);

    private record PlanEnrollmentCountDto(Guid PlanId, int Count);

    public async Task<IEnumerable<CommunityPlanDto>> GetAdminPlansAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT
                ""Id"", ""Slug"", ""Name"", ""Audience"",
                ""Price"", ""Interval"", ""AdminNotes"",
                ""IsActive"", ""DisplayOrder"", ""MaxCapacity"", ""GracePeriodDays"",
                ""TelegramInviteLink"", ""WeeklyMeetingLink""
            FROM community.""Plans""
            WHERE ""OrganizationId"" = @OrgId
            ORDER BY ""DisplayOrder"", ""Price""";

        var rawPlans = await connection.QueryAsync<RawPlanDto>(sql, new { OrgId = organizationId });

        const string countsSql = @"
            SELECT ""PlanId"", COUNT(*)::int as ""Count""
            FROM community.""Subscriptions""
            WHERE ""OrganizationId"" = @OrgId AND ""Status"" IN ('ACTIVE', 'PAST_DUE')
            GROUP BY ""PlanId""";

        var enrollmentCounts = (await connection.QueryAsync<PlanEnrollmentCountDto>(countsSql, new { OrgId = organizationId }))
            .ToDictionary(row => row.PlanId, row => row.Count);

        return rawPlans.Select(p => MapToPlanDto(p, enrollmentCounts.GetValueOrDefault(p.Id, 0)));
    }

    public async Task<CommunityPlanDto?> GetAdminPlanByIdAsync(Guid organizationId, Guid planId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT
                ""Id"", ""Slug"", ""Name"", ""Audience"",
                ""Price"", ""Interval"", ""AdminNotes"",
                ""IsActive"", ""DisplayOrder"", ""MaxCapacity"", ""GracePeriodDays"",
                ""TelegramInviteLink"", ""WeeklyMeetingLink""
            FROM community.""Plans""
            WHERE ""Id"" = @PlanId AND ""OrganizationId"" = @OrgId
            LIMIT 1";

        var rawPlan = await connection.QuerySingleOrDefaultAsync<RawPlanDto>(sql, new { PlanId = planId, OrgId = organizationId });
        if (rawPlan == null) return null;

        const string countSql = @"
            SELECT COUNT(*)::int FROM community.""Subscriptions""
            WHERE ""PlanId"" = @PlanId AND ""OrganizationId"" = @OrgId AND ""Status"" IN ('ACTIVE', 'PAST_DUE')";

        var enrolledCount = await connection.ExecuteScalarAsync<int>(countSql, new { PlanId = planId, OrgId = organizationId });

        return MapToPlanDto(rawPlan, enrolledCount);
    }

    public async Task<IEnumerable<CommunityPlanDto>> GetPublicPlansAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT
                ""Id"", ""Slug"", ""Name"", ""Audience"",
                ""Price"", ""Interval"", ""AdminNotes"",
                ""IsActive"", ""DisplayOrder"", ""MaxCapacity"", ""GracePeriodDays"",
                ""TelegramInviteLink"", ""WeeklyMeetingLink""
            FROM community.""Plans""
            WHERE ""OrganizationId"" = @OrgId AND ""IsActive"" = true
            ORDER BY ""DisplayOrder"", ""Price""";

        var rawPlans = await connection.QueryAsync<RawPlanDto>(sql, new { OrgId = organizationId });

        const string countsSql = @"
            SELECT ""PlanId"", COUNT(*)::int as ""Count""
            FROM community.""Subscriptions""
            WHERE ""OrganizationId"" = @OrgId AND ""Status"" IN ('ACTIVE', 'PAST_DUE')
            GROUP BY ""PlanId""";

        var enrollmentCounts = (await connection.QueryAsync<PlanEnrollmentCountDto>(countsSql, new { OrgId = organizationId }))
            .ToDictionary(row => row.PlanId, row => row.Count);

        return rawPlans.Select(p => MapToPlanDto(p, enrollmentCounts.GetValueOrDefault(p.Id, 0)));
    }
}
