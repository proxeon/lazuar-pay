using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Application;

public interface ICommerceRepository
{
    Task<Product?> GetProductByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetProductsByIdsAsync(Guid organizationId, IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> ListProductsAsync(Guid organizationId, CancellationToken ct = default);
    Task<Product?> GetProductBySlugAsync(Guid organizationId, string slug, CancellationToken ct = default);
    Task<Coupon?> GetCouponByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<Coupon?> GetCouponByCodeAsync(Guid organizationId, string code, CancellationToken ct = default);
    Task<Coupon?> GetCouponByCodeWithLockAsync(Guid organizationId, string code, CancellationToken ct = default);
    Task<CheckoutSession?> GetCheckoutSessionByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<CheckoutSession?> GetCheckoutSessionByIdempotencyKeyAsync(
        Guid organizationId,
        string idempotencyKey,
        CancellationToken ct = default);
    Task<Subscription?> GetSubscriptionByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    /// <summary>
    /// Capability load for a portal/arrears HMAC token. The token is the secret; the caller
    /// must then scope every later read with <see cref="GetSubscriptionByIdAsync(Guid, Guid, CancellationToken)"/>.
    /// </summary>
    Task<Subscription?> GetSubscriptionByIdForPortalTokenAsync(Guid id, CancellationToken ct = default);
    Task<Subscription?> GetNewestSubscriptionForClientAsync(Guid organizationId, Guid clientProfileId, CancellationToken ct = default);
    Task<Order?> GetOrderByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<CommerceTransactionLog?> GetTransactionLogByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CommerceTransactionLog>> GetTransactionLogsByCustomerEmailAsync(
        Guid organizationId,
        string customerEmail,
        CancellationToken ct = default);
    Task<CommerceTransactionLog?> GetConfirmedTransactionLogByReferenceAsync(
        Guid organizationId,
        Guid subscriptionId,
        string externalReference,
        CancellationToken ct = default);
    Task<bool> HasActiveSubscriptionAsync(
        Guid organizationId,
        Guid clientProfileId,
        Guid productId,
        CancellationToken ct = default);
    Task<bool> HasChargeAttemptAsync(Guid subscriptionId, DateTime targetDate, CancellationToken ct = default);
    
    Task<DunningCampaign?> GetDunningCampaignByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<bool> HasAnyDunningCampaignAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> HasSubscriptionsAssignedToCampaignAsync(Guid organizationId, Guid campaignId, CancellationToken ct = default);

    void AddProduct(Product product);
    void AddSubscription(Subscription subscription);
    void AddOrder(Order order);
    void AddChargeAttempt(ChargeAttemptLog log);
    void AddCheckoutSession(CheckoutSession session);
    void AddCoupon(Coupon coupon);
    void AddTransactionLog(CommerceTransactionLog log);
    
    void AddDunningCampaign(DunningCampaign campaign);
    void RemoveDunningCampaign(DunningCampaign campaign);

    Task SaveChangesAsync(CancellationToken ct = default);
}
