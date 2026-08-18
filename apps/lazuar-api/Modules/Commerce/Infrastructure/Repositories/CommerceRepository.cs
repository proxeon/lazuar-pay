using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Application;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Infrastructure.Repositories;

public class CommerceRepository : ICommerceRepository, ICommerceTransactional
{
    private readonly CommerceDbContext _context;

    public CommerceRepository(CommerceDbContext context)
    {
        _context = context;
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (!_context.Database.IsRelational())
        {
            await action(ct);
            return;
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            await action(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Products
            .IgnoreQueryFilters()
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Product>> GetProductsByIdsAsync(Guid organizationId, IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return Array.Empty<Product>();
        }

        return await _context.Products
            .IgnoreQueryFilters()
            .Include(p => p.Prices)
            .Where(p => p.OrganizationId == organizationId && idList.Contains(p.Id))
            .ToListAsync(ct);
    }

    public async Task<Product?> GetProductBySlugAsync(Guid organizationId, string slug, CancellationToken ct = default)
    {
        return await _context.Products
            .IgnoreQueryFilters()
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Slug == slug && p.IsActive, ct);
    }

    public async Task<Coupon?> GetCouponByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Coupons
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
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
        return await _context.CheckoutSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<CheckoutSession?> GetCheckoutSessionByIdempotencyKeyAsync(
        Guid organizationId,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        return await _context.CheckoutSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s => s.OrganizationId == organizationId && s.IdempotencyKey == idempotencyKey,
                ct);
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

    public async Task<Subscription?> GetNewestSubscriptionForClientAsync(
        Guid organizationId,
        Guid clientProfileId,
        CancellationToken ct = default)
    {
        return await _context.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.OrganizationId == organizationId && s.ClientProfileId == clientProfileId)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Orders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<CommerceTransactionLog?> GetTransactionLogByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.TransactionLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<CommerceTransactionLog>> GetTransactionLogsByCustomerEmailAsync(
        Guid organizationId,
        string customerEmail,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return Array.Empty<CommerceTransactionLog>();
        }

        var normalized = customerEmail.Trim().ToLowerInvariant();
        return await _context.TransactionLogs
            .IgnoreQueryFilters()
            .Where(t => t.OrganizationId == organizationId && t.CustomerEmail == normalized)
            .ToListAsync(ct);
    }

    public async Task<CommerceTransactionLog?> GetConfirmedTransactionLogByReferenceAsync(
        Guid organizationId,
        Guid subscriptionId,
        string externalReference,
        CancellationToken ct = default)
    {
        return await _context.TransactionLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.OrganizationId == organizationId
                    && t.SubscriptionId == subscriptionId
                    && t.ExternalReference == externalReference
                    && t.Status == CommerceTransactionLog.StatusConfirmed,
                ct);
    }

    public async Task<bool> HasActiveSubscriptionAsync(
        Guid organizationId,
        Guid clientProfileId,
        Guid productId,
        CancellationToken ct = default)
    {
        return await _context.Subscriptions
            .IgnoreQueryFilters()
            .AnyAsync(
                s => s.OrganizationId == organizationId
                    && s.ClientProfileId == clientProfileId
                    && s.ProductId == productId
                    && (s.Status == "ACTIVE" || s.Status == "TRIALING"),
                ct);
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
            .IgnoreQueryFilters()
            .AnyAsync(c => c.OrganizationId == organizationId, ct);
    }

    public async Task<bool> HasSubscriptionsAssignedToCampaignAsync(Guid campaignId, CancellationToken ct = default)
    {
        return await _context.Subscriptions
            .IgnoreQueryFilters()
            .AnyAsync(s => s.CurrentDunningCampaignId == campaignId && s.Status == "PAST_DUE", ct);
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
