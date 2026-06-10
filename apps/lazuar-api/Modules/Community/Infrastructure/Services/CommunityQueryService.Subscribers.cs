using Dapper;
using System.Data;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Community.Infrastructure.Services;

public partial class CommunityQueryService
{
    private record RawSubDto(
        Guid Id, Guid OrganizationId, Guid ClientProfileId, Guid PlanId,
        string PlanName, decimal PlanPrice, string Status, string Source,
        bool IsReminderOnly, string? PreferredChannel, string? AdminNotes,
        DateTime? RemindersPausedUntil, DateTime? CurrentPeriodEnd,
        DateTime? NextBillingDate, DateTime CreatedAt);

    public async Task<PaginatedResponse<CommunitySubscriptionDto>> GetSubscribersAsync(Guid organizationId, int page, int limit)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;

        const string sql = @"
            SELECT COUNT(*)::int
            FROM community.""Subscriptions""
            WHERE ""OrganizationId"" = @OrgId AND ""Status"" != 'PENDING';

            SELECT
                s.""Id"", s.""OrganizationId"", s.""ClientProfileId"", s.""PlanId"",
                p.""Name"" as ""PlanName"", p.""Price"" as ""PlanPrice"",
                s.""Status"", s.""Source"", s.""IsReminderOnly"", s.""PreferredChannel"",
                s.""AdminNotes"", s.""RemindersPausedUntil"", s.""CurrentPeriodEnd"",
                s.""NextRenewalDate"" as ""NextBillingDate"", s.""CreatedAt""
            FROM community.""Subscriptions"" s
            JOIN community.""Plans"" p ON s.""PlanId"" = p.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND s.""Status"" != 'PENDING'
            ORDER BY s.""CreatedAt"" DESC
            LIMIT @Limit OFFSET @Offset;";

        using var multi = await connection.QueryMultipleAsync(sql, new { OrgId = organizationId, Limit = limit, Offset = offset });
        var totalCount = await multi.ReadFirstAsync<int>();
        var rawSubs = (await multi.ReadAsync<RawSubDto>()).ToList();

        if (totalCount == 0) return new PaginatedResponse<CommunitySubscriptionDto>(Enumerable.Empty<CommunitySubscriptionDto>(), 0, page, limit);

        var profileIds = rawSubs.Select(x => x.ClientProfileId).Distinct();
        var profiles = await _crmQueryService.GetClientProfilesAsync(profileIds);
        var profileDict = profiles.ToDictionary(p => p.Id);

        var now = DateTime.UtcNow;
        var dtos = rawSubs.Select(s =>
        {
            profileDict.TryGetValue(s.ClientProfileId, out var profile);
            var daysOverdue = (s.Status is "PAST_DUE" or "EXPIRED" or "CANCELLED")
                && s.NextBillingDate.HasValue
                ? Math.Max(0, (int)(now - s.NextBillingDate.Value).TotalDays)
                : (int?)null;

            return new CommunitySubscriptionDto
            {
                Id = s.Id.ToString(),
                Client_profile_id = s.ClientProfileId.ToString(),
                Customer_name = profile?.FullName ?? "Unknown",
                Customer_email = profile?.Email ?? "",
                Customer_phone = profile?.Phone ?? "",
                Plan_id = s.PlanId.ToString(),
                Plan_name = s.PlanName,
                Plan_price = (double)s.PlanPrice,
                Status = s.Status,
                Source = s.Source,
                Is_reminder_only = s.IsReminderOnly,
                Preferred_channel = s.PreferredChannel,
                Admin_notes = s.AdminNotes,
                Reminders_paused_until = s.RemindersPausedUntil.HasValue ? new DateTimeOffset(s.RemindersPausedUntil.Value) : null,
                Current_period_end = s.CurrentPeriodEnd.HasValue ? new DateTimeOffset(s.CurrentPeriodEnd.Value) : null,
                Next_billing_date = s.NextBillingDate.HasValue ? new DateTimeOffset(s.NextBillingDate.Value) : null,
                Days_overdue = daysOverdue,
                Created_at = new DateTimeOffset(s.CreatedAt)
            };
        });

        return new PaginatedResponse<CommunitySubscriptionDto>(dtos, totalCount, page, limit);
    }

    public async Task<CommunitySubscriptionDto?> GetPortalSubscriptionAsync(Guid organizationId, Guid subscriptionId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT
                s.""Id"", s.""OrganizationId"", s.""ClientProfileId"", s.""PlanId"",
                p.""Name"" as ""PlanName"", p.""Price"" as ""PlanPrice"",
                s.""Status"", s.""Source"", s.""IsReminderOnly"", s.""PreferredChannel"",
                s.""AdminNotes"", s.""RemindersPausedUntil"", s.""CurrentPeriodEnd"",
                s.""NextRenewalDate"" as ""NextBillingDate"", s.""CreatedAt""
            FROM community.""Subscriptions"" s
            JOIN community.""Plans"" p ON s.""PlanId"" = p.""Id""
            WHERE s.""Id"" = @SubId AND s.""OrganizationId"" = @OrgId
            LIMIT 1";

        var rawSub = await connection.QuerySingleOrDefaultAsync<RawSubDto>(sql, new { SubId = subscriptionId, OrgId = organizationId });
        if (rawSub == null) return null;

        var profile = await _crmQueryService.GetClientProfileAsync(rawSub.ClientProfileId);
        var now = DateTime.UtcNow;
        var daysOverdue = (rawSub.Status is "PAST_DUE" or "EXPIRED" or "CANCELLED") && rawSub.NextBillingDate.HasValue
            ? Math.Max(0, (int)(now - rawSub.NextBillingDate.Value).TotalDays)
            : (int?)null;

        return new CommunitySubscriptionDto
        {
            Id = rawSub.Id.ToString(),
            Client_profile_id = rawSub.ClientProfileId.ToString(),
            Customer_name = profile?.FullName ?? "Unknown",
            Customer_email = profile?.Email ?? "",
            Customer_phone = profile?.Phone ?? "",
            Plan_id = rawSub.PlanId.ToString(),
            Plan_name = rawSub.PlanName,
            Plan_price = (double)rawSub.PlanPrice,
            Status = rawSub.Status,
            Source = rawSub.Source,
            Is_reminder_only = rawSub.IsReminderOnly,
            Preferred_channel = rawSub.PreferredChannel,
            Admin_notes = rawSub.AdminNotes,
            Reminders_paused_until = rawSub.RemindersPausedUntil.HasValue ? new DateTimeOffset(rawSub.RemindersPausedUntil.Value) : null,
            Current_period_end = rawSub.CurrentPeriodEnd.HasValue ? new DateTimeOffset(rawSub.CurrentPeriodEnd.Value) : null,
            Next_billing_date = rawSub.NextBillingDate.HasValue ? new DateTimeOffset(rawSub.NextBillingDate.Value) : null,
            Days_overdue = daysOverdue,
            Created_at = new DateTimeOffset(rawSub.CreatedAt)
        };
    }
}
