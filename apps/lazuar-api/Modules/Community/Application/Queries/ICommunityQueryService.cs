namespace Modules.Community.Application.Queries;

public record CommunityFaqItemDto(string Id, string Question, string Answer);

public record CommunityPlanDto(
    Guid Id, string Slug, string Name, string Audience, string ShortDescription, string LongDescription,
    decimal Price, string Interval, List<string> Features, string Methodology, List<CommunityFaqItemDto> Faq,
    bool IsActive, int DisplayOrder, int? MaxCapacity, int GracePeriodDays,
    string? TelegramInviteLink, string? WeeklyMeetingLink, 
    int EnrolledCount, int? SpotsRemaining, bool IsFull
);

public record CommunitySubscriptionDto(
    Guid Id, Guid ClientProfileId, string CustomerName, string CustomerEmail, string CustomerPhone,
    Guid PlanId, string PlanName, decimal PlanPrice, string Status, string Source,
    bool IsReminderOnly, string? PreferredChannel, string? AdminNotes, DateTime? RemindersPausedUntil,
    DateTime? CurrentPeriodEnd, DateTime? NextBillingDate, int? DaysOverdue, DateTime CreatedAt
);

public interface ICommunityQueryService
{
    Task<IEnumerable<CommunityPlanDto>> GetAdminPlansAsync(Guid organizationId);
    Task<CommunityPlanDto?> GetAdminPlanByIdAsync(Guid organizationId, Guid planId);
    Task<IEnumerable<CommunityPlanDto>> GetPublicPlansAsync(Guid organizationId);
    Task<IEnumerable<CommunitySubscriptionDto>> GetSubscribersAsync(Guid organizationId);
    
    /// <summary>
    /// Fetches a specific subscription to display in the public subscriber portal.
    /// </summary>
    Task<CommunitySubscriptionDto?> GetPortalSubscriptionAsync(Guid organizationId, Guid subscriptionId);
}
