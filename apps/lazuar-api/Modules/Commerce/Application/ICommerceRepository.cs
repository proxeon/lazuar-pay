using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Application;

public interface ICommerceRepository
{
    Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default);
    Task<Coupon?> GetCouponByIdAsync(Guid id, CancellationToken ct = default);
    Task<CheckoutSession?> GetCheckoutSessionByIdAsync(Guid id, CancellationToken ct = default);
    Task<Subscription?> GetSubscriptionByIdAsync(Guid id, CancellationToken ct = default);
    Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasChargeAttemptAsync(Guid subscriptionId, DateTime targetDate, CancellationToken ct = default);

    void AddProduct(Product product);
    void AddSubscription(Subscription subscription);
    void AddOrder(Order order);
    void AddChargeAttempt(ChargeAttemptLog log);

    Task SaveChangesAsync(CancellationToken ct = default);
}
