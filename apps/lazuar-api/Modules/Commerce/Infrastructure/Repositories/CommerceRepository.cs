using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Application;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Infrastructure.Repositories;

public class CommerceRepository : ICommerceRepository
{
    private readonly CommerceDbContext _context;

    public CommerceRepository(CommerceDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Product?> GetProductBySlugAsync(Guid organizationId, string slug, CancellationToken ct = default)
    {
        return await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Slug == slug && p.IsActive, ct);
    }

    public async Task<Coupon?> GetCouponByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Coupon?> GetCouponByCodeAsync(Guid organizationId, string code, CancellationToken ct = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        return await _context.Coupons
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Code == normalizedCode && c.IsActive, ct);
    }

    public async Task<Coupon?> GetCouponByCodeWithLockAsync(Guid organizationId, string code, CancellationToken ct = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        return await _context.Coupons
            .FromSqlRaw(@"
                SELECT * FROM commerce.""Coupons"" 
                WHERE ""OrganizationId"" = {0} AND ""Code"" = {1} AND ""IsActive"" = true 
                FOR UPDATE", organizationId, normalizedCode)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CheckoutSession?> GetCheckoutSessionByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.CheckoutSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Subscription?> GetSubscriptionByIdAsync(Guid id, CancellationToken ct = default)
    {
        var local = _context.Subscriptions.Local.FirstOrDefault(s => s.Id == id);
        if (local != null) return local;

        return await _context.Subscriptions
            .IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<CommerceTransactionLog?> GetTransactionLogByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.TransactionLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<bool> HasChargeAttemptAsync(Guid subscriptionId, DateTime targetDate, CancellationToken ct = default)
    {
        return await _context.ChargeAttemptLogs
            .AnyAsync(l => l.SubscriptionId == subscriptionId && l.TargetBillingDate.Date == targetDate.Date, ct);
    }

    public async Task<DunningCampaign?> GetDunningCampaignByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        return await _context.DunningCampaigns
            .Include(c => c.Steps)
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == organizationId, ct);
    }

    public async Task<bool> HasAnyDunningCampaignAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _context.DunningCampaigns
            .AnyAsync(c => c.OrganizationId == organizationId, ct);
    }

    public async Task<bool> HasSubscriptionsAssignedToCampaignAsync(Guid campaignId, CancellationToken ct = default)
    {
        return await _context.Subscriptions
            .AnyAsync(s => s.CurrentDunningCampaignId == campaignId, ct);
    }

    public void AddProduct(Product product) => _context.Products.Add(product);
    public void AddSubscription(Subscription subscription) => _context.Subscriptions.Add(subscription);
    public void AddOrder(Order order) => _context.Orders.Add(order);
    public void AddChargeAttempt(ChargeAttemptLog log) => _context.ChargeAttemptLogs.Add(log);
    public void AddCheckoutSession(CheckoutSession session) => _context.CheckoutSessions.Add(session);
    public void AddCoupon(Coupon coupon) => _context.Coupons.Add(coupon);
    public void AddTransactionLog(CommerceTransactionLog log) => _context.TransactionLogs.Add(log);
    
    public void AddDunningCampaign(DunningCampaign campaign) => _context.DunningCampaigns.Add(campaign);
    public void RemoveDunningCampaign(DunningCampaign campaign) => _context.DunningCampaigns.Remove(campaign);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
