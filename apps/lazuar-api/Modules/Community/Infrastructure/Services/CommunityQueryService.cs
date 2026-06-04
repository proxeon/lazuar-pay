using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application.Queries;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Community.Infrastructure.Services;

public class CommunityQueryService : ICommunityQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IMessageTemplateQueryService _messageTemplateQueryService;

    private record RawPlanDto(
        Guid Id, string Slug, string Name, string Audience, string ShortDescription, string LongDescription,
        decimal Price, string Interval, string Features, string Methodology, string Faq,
        bool IsActive, int DisplayOrder, int? MaxCapacity, int GracePeriodDays,
        string? TelegramInviteLink, string? WeeklyMeetingLink);

    private record RawSubDto(
        Guid Id, Guid OrganizationId, Guid ClientProfileId, Guid PlanId,
        string PlanName, decimal PlanPrice, string Status, string Source,
        bool IsReminderOnly, string? PreferredChannel, string? AdminNotes,
        DateTime? RemindersPausedUntil, DateTime? CurrentPeriodEnd,
        DateTime? NextBillingDate, DateTime CreatedAt);

    private record RawReminderScheduleDto(
        Guid Id, Guid? PlanId, string? PlanName, Guid TemplateId, 
        string Channel, int DaysRelativeToDue, string TimeOfDay, bool IsEnabled, DateTime CreatedAt);

    // Private Sub/Stats DTOs strictly avoiding dynamics issues 
    private record SubStatsDto(string Status, bool IsReminderOnly, DateTime CreatedAt, DateTime UpdatedAt, decimal Price, string Interval);
    private record RawCashFlowTrendDto(string Month, decimal Amount);
    private record RawPaymentMethodDto(string Method, int Count, decimal TotalAmount);

    public CommunityQueryService(
        [FromKeyedServices("CommunitySqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        ICrmQueryService crmQueryService,
        IMessageTemplateQueryService messageTemplateQueryService)
    {
        _connectionFactory = connectionFactory;
        _crmQueryService = crmQueryService;
        _messageTemplateQueryService = messageTemplateQueryService;
    }

    public async Task<IEnumerable<CommunityPlanDto>> GetAdminPlansAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 
                ""Id"", ""Slug"", ""Name"", ""Audience"", ""ShortDescription"", ""LongDescription"",
                ""Price"", ""Interval"", ""Features""::text, ""Methodology"", ""Faq""::text,
                ""IsActive"", ""DisplayOrder"", ""MaxCapacity"", ""GracePeriodDays"",
                ""TelegramInviteLink"", ""WeeklyMeetingLink""
            FROM community.""Plans"" 
            WHERE ""OrganizationId"" = @OrgId 
            ORDER BY ""DisplayOrder"", ""Price""";

        var rawPlans = await connection.QueryAsync<RawPlanDto>(sql, new { OrgId = organizationId });

        const string countsSql = @"
            SELECT ""PlanId"", COUNT(*) as Count 
            FROM community.""Subscriptions"" 
            WHERE ""OrganizationId"" = @OrgId AND ""Status"" IN ('ACTIVE', 'PAST_DUE')
            GROUP BY ""PlanId""";
            
        var enrollmentCounts = (await connection.QueryAsync(countsSql, new { OrgId = organizationId }))
            .ToDictionary(row => (Guid)row.PlanId, row => (int)row.Count);

        return rawPlans.Select(p => MapToPlanDto(p, enrollmentCounts.GetValueOrDefault(p.Id, 0)));
    }

    public async Task<CommunityPlanDto?> GetAdminPlanByIdAsync(Guid organizationId, Guid planId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 
                ""Id"", ""Slug"", ""Name"", ""Audience"", ""ShortDescription"", ""LongDescription"",
                ""Price"", ""Interval"", ""Features""::text, ""Methodology"", ""Faq""::text,
                ""IsActive"", ""DisplayOrder"", ""MaxCapacity"", ""GracePeriodDays"",
                ""TelegramInviteLink"", ""WeeklyMeetingLink""
            FROM community.""Plans"" 
            WHERE ""Id"" = @PlanId AND ""OrganizationId"" = @OrgId 
            LIMIT 1";
        var rawPlan = await connection.QuerySingleOrDefaultAsync<RawPlanDto>(sql, new { PlanId = planId, OrgId = organizationId });

        if (rawPlan == null) return null;

        const string countSql = @"
            SELECT COUNT(*) FROM community.""Subscriptions"" 
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
                ""Id"", ""Slug"", ""Name"", ""Audience"", ""ShortDescription"", ""LongDescription"",
                ""Price"", ""Interval"", ""Features""::text, ""Methodology"", ""Faq""::text,
                ""IsActive"", ""DisplayOrder"", ""MaxCapacity"", ""GracePeriodDays"",
                ""TelegramInviteLink"", ""WeeklyMeetingLink""
            FROM community.""Plans"" 
            WHERE ""OrganizationId"" = @OrgId AND ""IsActive"" = true 
            ORDER BY ""DisplayOrder"", ""Price""";

        var rawPlans = await connection.QueryAsync<RawPlanDto>(sql, new { OrgId = organizationId });

        const string countsSql = @"
            SELECT ""PlanId"", COUNT(*) as Count 
            FROM community.""Subscriptions"" 
            WHERE ""OrganizationId"" = @OrgId AND ""Status"" IN ('ACTIVE', 'PAST_DUE')
            GROUP BY ""PlanId""";
            
        var enrollmentCounts = (await connection.QueryAsync(countsSql, new { OrgId = organizationId }))
            .ToDictionary(row => (Guid)row.PlanId, row => (int)row.Count);

        return rawPlans.Select(p => MapToPlanDto(p, enrollmentCounts.GetValueOrDefault(p.Id, 0)));
    }

    public async Task<IEnumerable<CommunitySubscriptionDto>> GetSubscribersAsync(Guid organizationId)
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
            WHERE s.""OrganizationId"" = @OrgId AND s.""Status"" != 'PENDING'
            ORDER BY s.""CreatedAt"" DESC";

        var rawSubs = await connection.QueryAsync<RawSubDto>(sql, new { OrgId = organizationId });
        var subList = rawSubs.ToList();

        if (subList.Count == 0) return Enumerable.Empty<CommunitySubscriptionDto>();

        var profileIds = subList.Select(x => x.ClientProfileId).Distinct();
        var profiles = await _crmQueryService.GetClientProfilesAsync(profileIds);
        var profileDict = profiles.ToDictionary(p => p.Id);

        var now = DateTime.UtcNow;

        return subList.Select(s =>
        {
            profileDict.TryGetValue(s.ClientProfileId, out var profile);
            
            var daysOverdue = (s.Status is "PAST_DUE" or "EXPIRED" or "CANCELLED")
                              && s.NextBillingDate.HasValue
                ? Math.Max(0, (int)(now - s.NextBillingDate.Value).TotalDays)
                : (int?)null;

            return new CommunitySubscriptionDto(
                Id: s.Id, ClientProfileId: s.ClientProfileId, CustomerName: profile?.FullName ?? "Unknown",
                CustomerEmail: profile?.Email ?? "", CustomerPhone: profile?.Phone ?? "", PlanId: s.PlanId,
                PlanName: s.PlanName, PlanPrice: s.PlanPrice, Status: s.Status, Source: s.Source,
                IsReminderOnly: s.IsReminderOnly, PreferredChannel: s.PreferredChannel, AdminNotes: s.AdminNotes,
                RemindersPausedUntil: s.RemindersPausedUntil, CurrentPeriodEnd: s.CurrentPeriodEnd,
                NextBillingDate: s.NextBillingDate, DaysOverdue: daysOverdue, CreatedAt: s.CreatedAt
            );
        });
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

        return new CommunitySubscriptionDto(
            Id: rawSub.Id, ClientProfileId: rawSub.ClientProfileId, CustomerName: profile?.FullName ?? "Unknown",
            CustomerEmail: profile?.Email ?? "", CustomerPhone: profile?.Phone ?? "", PlanId: rawSub.PlanId,
            PlanName: rawSub.PlanName, PlanPrice: rawSub.PlanPrice, Status: rawSub.Status, Source: rawSub.Source,
            IsReminderOnly: rawSub.IsReminderOnly, PreferredChannel: rawSub.PreferredChannel, AdminNotes: rawSub.AdminNotes,
            RemindersPausedUntil: rawSub.RemindersPausedUntil, CurrentPeriodEnd: rawSub.CurrentPeriodEnd,
            NextBillingDate: rawSub.NextBillingDate, DaysOverdue: daysOverdue, CreatedAt: rawSub.CreatedAt
        );
    }

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
            var templateName = templateDict.TryGetValue(r.TemplateId, out var t) ? t.Name : "Unknown Template";
            return new CommunityReminderScheduleDto(
                r.Id, r.PlanId, r.PlanName, r.TemplateId, templateName, 
                r.Channel, r.DaysRelativeToDue, r.TimeOfDay, r.IsEnabled, r.CreatedAt);
        });
    }

    public async Task<CommunitySubscriberStatsDto> GetSubscriberStatsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string subSql = @"
            SELECT 
                s.""Status"" as Status, s.""IsReminderOnly"" as IsReminderOnly, 
                s.""CreatedAt"" as CreatedAt, s.""UpdatedAt"" as UpdatedAt,
                p.""Price"" as Price, p.""Interval"" as Interval
            FROM community.""Subscriptions"" s
            JOIN community.""Plans"" p ON s.""PlanId"" = p.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND s.""Status"" != 'PENDING'";

        var subs = (await connection.QueryAsync<SubStatsDto>(subSql, new { OrgId = organizationId })).ToList();

        const string revSql = @"
            SELECT COALESCE(SUM(pr.""Amount""), 0.0) 
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND pr.""Status"" = 'CONFIRMED'";

        var totalRevenue = await connection.ExecuteScalarAsync<decimal>(revSql, new { OrgId = organizationId });

        const string trendSql = @"
            SELECT 
                to_char(pr.""CreatedAt"", 'Mon YYYY') as Month,
                SUM(pr.""Amount"") as Amount
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND pr.""Status"" = 'CONFIRMED'
            GROUP BY to_char(pr.""CreatedAt"", 'Mon YYYY'), to_char(pr.""CreatedAt"", 'YYYY-MM')
            ORDER BY to_char(pr.""CreatedAt"", 'YYYY-MM') DESC
            LIMIT 6";

        var rawTrend = await connection.QueryAsync<RawCashFlowTrendDto>(trendSql, new { OrgId = organizationId });
        var cashFlowTrend = rawTrend.Select(r => new CashFlowTrendDto(r.Month, r.Amount)).Reverse().ToList();

        const string methodsSql = @"
            SELECT 
                pr.""PaymentMethod"" as Method,
                COUNT(*)::int as Count,
                SUM(pr.""Amount"") as TotalAmount
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND pr.""Status"" = 'CONFIRMED'
            GROUP BY pr.""PaymentMethod""";

        var rawMethods = await connection.QueryAsync<RawPaymentMethodDto>(methodsSql, new { OrgId = organizationId });
        var paymentMethods = rawMethods.Select(r => new PaymentMethodDto(r.Method, r.Count, r.TotalAmount)).ToList();

        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        var activeSubs = subs.Where(s => s.Status == "ACTIVE" || s.Status == "PAST_DUE").ToList();

        var mrr = activeSubs
            .Where(s => !s.IsReminderOnly)
            .Sum(s => s.Interval == "yr" ? s.Price / 12m : s.Price);

        var cancelledLast30 = subs.Count(s => s.Status == "CANCELLED" && s.UpdatedAt >= thirtyDaysAgo);
        var newActiveLast30 = activeSubs.Count(s => s.CreatedAt >= thirtyDaysAgo);
        var netNewSubscribers = newActiveLast30 - cancelledLast30;

        var active30DaysAgo = activeSubs.Count + cancelledLast30 - newActiveLast30;
        double churnRate = active30DaysAgo > 0 ? Math.Round((double)cancelledLast30 / active30DaysAgo * 100, 2) : 0;

        var truePlatformActive = activeSubs.Count(s => !s.IsReminderOnly);
        double arpu = truePlatformActive > 0 ? (double)(mrr / truePlatformActive) : 0;

        return new CommunitySubscriberStatsDto(
            (double)mrr,
            activeSubs.Count,
            subs.Count(s => s.Status == "PAST_DUE"),
            subs.Count(s => s.Status == "CANCELLED"),
            netNewSubscribers,
            churnRate,
            arpu,
            85.0, // Mock ReminderEffectivenessPercentage tracking,
            (double)totalRevenue,
            cashFlowTrend,
            paymentMethods
        );
    }

    public async Task<IEnumerable<DeliveryHistoryItem>> GetReminderHistoryAsync(Guid organizationId, Guid subscriptionId)
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

        return await connection.QueryAsync<DeliveryHistoryItem>(sql, new { OrgId = organizationId, SubId = subscriptionId });
    }

    private static CommunityPlanDto MapToPlanDto(RawPlanDto raw, int enrolledCount)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };
        var features = string.IsNullOrEmpty(raw.Features) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(raw.Features, options) ?? new List<string>();
        var faq = string.IsNullOrEmpty(raw.Faq) ? new List<CommunityFaqItemDto>() : JsonSerializer.Deserialize<List<CommunityFaqItemDto>>(raw.Faq, options) ?? new List<CommunityFaqItemDto>();
        var spotsRemaining = raw.MaxCapacity.HasValue ? Math.Max(0, raw.MaxCapacity.Value - enrolledCount) : (int?)null;
        var isFull = raw.MaxCapacity.HasValue && enrolledCount >= raw.MaxCapacity.Value;

        return new CommunityPlanDto(
            raw.Id, raw.Slug, raw.Name, raw.Audience, raw.ShortDescription, raw.LongDescription,
            raw.Price, raw.Interval, features, raw.Methodology, faq, raw.IsActive, raw.DisplayOrder,
            raw.MaxCapacity, raw.GracePeriodDays, raw.TelegramInviteLink, raw.WeeklyMeetingLink,
            enrolledCount, spotsRemaining, isFull
        );
    }
}
