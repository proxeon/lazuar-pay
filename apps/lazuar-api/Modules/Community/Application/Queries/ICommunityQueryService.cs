using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Modules.Community.Application.Queries;

public record DeliveryHistoryItem(
    Guid Id,
    string Channel,
    string Recipient,
    string? TemplateName,
    string? Subject,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAt);

public record CommunityFaqItemDto(string Id, string Question, string Answer);

public record CommunityPlanDto(
    Guid Id, string Slug, string Name, string Audience, string ShortDescription, string LongDescription,
    decimal Price, string Interval, List<string> Features, string Methodology, List<CommunityFaqItemDto> Faq,
    bool IsActive, int DisplayOrder, int? MaxCapacity, int GracePeriodDays,
    string? TelegramInviteLink, string? WeeklyMeetingLink, 
    int EnrolledCount, int? SpotsRemaining, bool IsFull);

public record CommunitySubscriptionDto(
    Guid Id, Guid ClientProfileId, string CustomerName, string CustomerEmail, string CustomerPhone,
    Guid PlanId, string PlanName, decimal PlanPrice, string Status, string Source,
    bool IsReminderOnly, string? PreferredChannel, string? AdminNotes, DateTime? RemindersPausedUntil,
    DateTime? CurrentPeriodEnd, DateTime? NextBillingDate, int? DaysOverdue, DateTime CreatedAt);

public record CommunityReminderScheduleDto(
    Guid Id, Guid? PlanId, string? PlanName, Guid TemplateId, string TemplateName, 
    string Channel, int DaysRelativeToDue, string TimeOfDay, bool IsEnabled, DateTime CreatedAt);

public record CommunitySubscriberStatsDto(
    double Mrr,
    int ActiveSubscribers,
    int PastDueSubscribers,
    int CancelledSubscribers,
    int NetNewLast30Days,
    double ChurnRatePercentage,
    double AverageRevenuePerUser,
    double TotalRevenueCollected);

public interface ICommunityQueryService
{
    Task<IEnumerable<CommunityPlanDto>> GetAdminPlansAsync(Guid organizationId);
    Task<CommunityPlanDto?> GetAdminPlanByIdAsync(Guid organizationId, Guid planId);
    Task<IEnumerable<CommunityPlanDto>> GetPublicPlansAsync(Guid organizationId);
    
    Task<IEnumerable<CommunitySubscriptionDto>> GetSubscribersAsync(Guid organizationId);
    Task<CommunitySubscriptionDto?> GetPortalSubscriptionAsync(Guid organizationId, Guid subscriptionId);

    Task<IEnumerable<CommunityReminderScheduleDto>> GetReminderSchedulesAsync(Guid organizationId);
    Task<CommunitySubscriberStatsDto> GetSubscriberStatsAsync(Guid organizationId);
    
    // Cross-schema log read mapping
    Task<IEnumerable<DeliveryHistoryItem>> GetReminderHistoryAsync(Guid organizationId, Guid subscriptionId);
}
