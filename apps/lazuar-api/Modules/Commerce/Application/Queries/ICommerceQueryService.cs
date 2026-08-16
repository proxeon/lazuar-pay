using System;
using System.Collections.Generic;
using System.Threading;
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
    Task<IEnumerable<DunningCampaignDto>> GetDunningCampaignsAsync(Guid organizationId);
    
    Task<PaginatedResponse<CommerceSubscriptionDto>> GetSubscribersAsync(Guid organizationId, int page, int limit, string? searchTerm = null);
    Task<PaginatedResponse<TransactionLogDto>> GetTransactionsAsync(Guid organizationId, int page, int limit, string? status, string? gatewayName, string? searchTerm = null, Guid? subscriptionId = null);
    Task<IEnumerable<CouponDto>> GetCouponsAsync(Guid organizationId);
    Task<CommerceStatsDto> GetStatsAsync(Guid organizationId);
    /// <summary>
    /// Poll checkout session status for a tenant. Does not mint portal magic tokens.
    /// </summary>
    Task<CheckoutStatusDto?> GetCheckoutStatusAsync(Guid organizationId, Guid sessionId, CancellationToken ct = default);

    Task<PaginatedResponse<CustomCheckoutDto>> GetCustomCheckoutsAsync(Guid organizationId, int page, int limit);
    Task<CustomCheckoutDto?> GetCustomCheckoutBySessionIdAsync(Guid organizationId, Guid sessionId);
}
