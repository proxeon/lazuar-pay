using Dapper;
using System.Data;
using System.Text.Json;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application.Queries;
using Modules.CRM.Contracts;
using Lazuar.ApiTypes;

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

    private record SubStatsDto(string Status, bool IsReminderOnly, DateTime CreatedAt, DateTime UpdatedAt, decimal Price, string Interval);
    private record RawCashFlowTrendDto(string Month, decimal Amount);
    private record RawPaymentMethodDto(string Method, int Count, decimal TotalAmount);
    private record PlanEnrollmentCountDto(Guid PlanId, int Count);
    
    private record RawDeliveryLog(
        Guid Id, string Channel, string Recipient, string? TemplateName, 
        string? Subject, string Status, string? ErrorMessage, DateTime CreatedAt);

    private record RawPaymentRecordDto(
        Guid Id, decimal Amount, string Currency, string PaymentMethod,
        string? ReferenceNumber, string? ReceiptUrl, string RecordedBy,
        DateTime PeriodStart, DateTime PeriodEnd, string Status, string? Notes, DateTime CreatedAt);

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
                ""Id"", ""Slug"", ""Name"", ""Audience"", ""ShortDescription"", ""LongDescription"",
                ""Price"", ""Interval"", ""Features""::text, ""Methodology"", ""Faq""::text,
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
        var cashFlowTrend = rawTrend.Select(r => new CashFlowTrendDto { Month = r.Month, Amount = (double)r.Amount }).Reverse().ToList();

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
        var paymentMethods = rawMethods.Select(r => new PaymentMethodDto { Method = r.Method, Count = r.Count, Total_amount = (double)r.TotalAmount }).ToList();

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

        return new CommunitySubscriberStatsDto
        {
            Mrr = (double)mrr,
            Active_subscribers = activeSubs.Count,
            Past_due_subscribers = subs.Count(s => s.Status == "PAST_DUE"),
            Cancelled_subscribers = subs.Count(s => s.Status == "CANCELLED"),
            Net_new_last_30_days = netNewSubscribers,
            Churn_rate_percentage = churnRate,
            Average_revenue_per_user = arpu,
            Reminder_effectiveness_percentage = 85.0,
            Total_revenue_collected = (double)totalRevenue,
            Cash_flow_trend = cashFlowTrend,
            Payment_methods = paymentMethods
        };
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

    public async Task<PaginatedResponse<PaymentRecordDto>> GetPaymentHistoryAsync(Guid organizationId, Guid subscriptionId, int page, int limit)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;

        const string sql = @"
            SELECT COUNT(*)::int 
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND pr.""SubscriptionId"" = @SubId;

            SELECT 
                pr.""Id"", pr.""Amount"", pr.""Currency"", pr.""PaymentMethod"",
                pr.""ExternalReference"" as ReferenceNumber, pr.""ReceiptUrl"",
                pr.""RecordedBy"", pr.""PeriodStart"", pr.""PeriodEnd"",
                pr.""Status"", pr.""Notes"", pr.""CreatedAt""
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND pr.""SubscriptionId"" = @SubId
            ORDER BY pr.""CreatedAt"" DESC
            LIMIT @Limit OFFSET @Offset;";

        using var multi = await connection.QueryMultipleAsync(sql, new { OrgId = organizationId, SubId = subscriptionId, Limit = limit, Offset = offset });

        var totalCount = await multi.ReadFirstAsync<int>();
        var rawLogs = (await multi.ReadAsync<RawPaymentRecordDto>()).ToList();

        if (totalCount == 0) return new PaginatedResponse<PaymentRecordDto>(Enumerable.Empty<PaymentRecordDto>(), 0, page, limit);

        var dtos = rawLogs.Select(r => new PaymentRecordDto
        {
            Id = r.Id.ToString(),
            Amount = (double)r.Amount,
            Currency = r.Currency,
            Payment_method = r.PaymentMethod,
            Reference_number = r.ReferenceNumber,
            Receipt_url = r.ReceiptUrl,
            Recorded_by = r.RecordedBy,
            Period_start = new DateTimeOffset(r.PeriodStart),
            Period_end = new DateTimeOffset(r.PeriodEnd),
            Status = r.Status,
            Notes = r.Notes,
            Created_at = new DateTimeOffset(r.CreatedAt)
        });

        return new PaginatedResponse<PaymentRecordDto>(dtos, totalCount, page, limit);
    }

    private static CommunityPlanDto MapToPlanDto(RawPlanDto raw, int enrolledCount)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };
        var features = string.IsNullOrEmpty(raw.Features) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(raw.Features, options) ?? new List<string>();
        var faqList = string.IsNullOrEmpty(raw.Faq) ? new List<Lazuar.ApiTypes.FaqItemDto>() : JsonSerializer.Deserialize<List<Lazuar.ApiTypes.FaqItemDto>>(raw.Faq, options) ?? new List<Lazuar.ApiTypes.FaqItemDto>();
        var spotsRemaining = raw.MaxCapacity.HasValue ? Math.Max(0, raw.MaxCapacity.Value - enrolledCount) : (int?)null;
        var isFull = raw.MaxCapacity.HasValue && enrolledCount >= raw.MaxCapacity.Value;

        return new CommunityPlanDto
        {
            Id = raw.Id.ToString(),
            Slug = raw.Slug,
            Name = raw.Name,
            Audience = raw.Audience,
            Short_description = raw.ShortDescription,
            Long_description = raw.LongDescription ?? "",
            Price = (double)raw.Price,
            Interval = raw.Interval,
            Features = features,
            Methodology = raw.Methodology ?? "",
            Faq = faqList,
            Is_active = raw.IsActive,
            Display_order = raw.DisplayOrder,
            Max_capacity = raw.MaxCapacity,
            Grace_period_days = raw.GracePeriodDays,
            Telegram_invite_link = raw.TelegramInviteLink,
            Weekly_meeting_link = raw.WeeklyMeetingLink,
            Enrolled_count = enrolledCount,
            Spots_remaining = spotsRemaining,
            Is_full = isFull
        };
    }
}
