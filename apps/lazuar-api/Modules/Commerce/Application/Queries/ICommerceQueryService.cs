using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Application.Queries;

public record CheckoutStatusDto(string Status, string? Token);

public interface ICommerceQueryService
{
    Task<IEnumerable<ProductDto>> GetProductsAsync(Guid organizationId);
    Task<ProductDto?> GetProductByIdAsync(Guid organizationId, Guid productId);
    Task<AggregatedPortalDataResponse?> GetPortalDataAsync(Guid organizationId, Guid subscriptionId);
    Task<IEnumerable<ReminderScheduleDto>> GetReminderSchedulesAsync(Guid organizationId);
    
    Task<PaginatedResponse<CommerceSubscriptionDto>> GetSubscribersAsync(Guid organizationId, int page, int limit, string? searchTerm = null);
    Task<PaginatedResponse<TransactionLogDto>> GetTransactionsAsync(Guid organizationId, int page, int limit, string? status, string? paymentMethod, string? searchTerm = null);
    Task<IEnumerable<CouponDto>> GetCouponsAsync(Guid organizationId);
    Task<CommerceStatsDto> GetStatsAsync(Guid organizationId);
    Task<CheckoutStatusDto?> GetCheckoutStatusAsync(Guid sessionId, CancellationToken ct = default);
}
