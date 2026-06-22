using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lazuar.ApiTypes;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries;

public record GlobalTransactionDto(
    Guid Id,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Status,
    DateTime CreatedAt,
    Guid ClientProfileId
);

public interface ICommunityQueryService
{
    Task<IEnumerable<CommunityPlanDto>> GetAdminPlansAsync(Guid organizationId);
    Task<CommunityPlanDto?> GetAdminPlanByIdAsync(Guid organizationId, Guid planId);
    Task<IEnumerable<CommunityPlanDto>> GetPublicPlansAsync(Guid organizationId);
    Task<PaginatedResponse<CommunitySubscriptionDto>> GetSubscribersAsync(Guid organizationId, int page, int limit, string? searchTerm = null);
    Task<CommunitySubscriptionDto?> GetPortalSubscriptionAsync(Guid organizationId, Guid subscriptionId);
    Task<IEnumerable<CommunityReminderScheduleDto>> GetReminderSchedulesAsync(Guid organizationId);
    Task<CommunitySubscriberStatsDto> GetSubscriberStatsAsync(Guid organizationId);
    Task<IEnumerable<DeliveryHistoryItemDto>> GetReminderHistoryAsync(Guid organizationId, Guid subscriptionId);
    Task<PaginatedResponse<PaymentRecordDto>> GetPaymentHistoryAsync(Guid organizationId, Guid subscriptionId, int page, int limit);
    Task<IEnumerable<GlobalTransactionDto>> GetGlobalTransactionsAsync(Guid organizationId, DateTime? fromDate, DateTime? toDate, string? status);
    Task<IEnumerable<CouponDto>> GetCouponsAsync(Guid organizationId);
    Task<IEnumerable<MyGlobalSubscriptionDto>> GetMyGlobalSubscriptionsAsync(Guid globalUserId);
}
