using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Infrastructure.Repositories;

public class CommerceRepository : ICommerceRepository
{
    private readonly CommerceDbContext _context;
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommerceRepository(
        CommerceDbContext context,
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _context = context;
        _connectionFactory = connectionFactory;
    }

    public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
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
            .Include(s => s.ReminderLogs)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<bool> HasChargeAttemptAsync(Guid subscriptionId, DateTime targetDate, CancellationToken ct = default)
    {
        return await _context.ChargeAttemptLogs
            .AnyAsync(l => l.SubscriptionId == subscriptionId && l.TargetBillingDate.Date == targetDate.Date, ct);
    }

    public async Task<ReminderSchedule?> GetReminderScheduleByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ReminderSchedules.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<Dictionary<string, Guid>> GetDefaultTemplateIdsAsync(Guid organizationId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string query = @"
            SELECT ""Id"", ""Name"" 
            FROM communications.""MessageTemplates"" 
            WHERE ""OrganizationId"" = @TenantId 
              AND ""Name"" IN ('Subscription Renewal (3 Days)', 'Subscription Renewal Due Today', 'Subscription Renewal Overdue')";

        var templates = await connection.QueryAsync<(Guid Id, string Name)>(
            new CommandDefinition(query, new { TenantId = organizationId }, cancellationToken: ct));
        
        var templateDict = new Dictionary<string, Guid>();
        foreach (var t in templates)
        {
            templateDict[t.Name] = t.Id;
        }

        return templateDict;
    }

    public void AddProduct(Product product) => _context.Products.Add(product);

    public void AddSubscription(Subscription subscription) => _context.Subscriptions.Add(subscription);

    public void AddOrder(Order order) => _context.Orders.Add(order);

    public void AddChargeAttempt(ChargeAttemptLog log) => _context.ChargeAttemptLogs.Add(log);

    public void AddReminderSchedule(ReminderSchedule schedule) => _context.ReminderSchedules.Add(schedule);

    public void RemoveReminderSchedule(ReminderSchedule schedule) => _context.ReminderSchedules.Remove(schedule);

    public void AddCheckoutSession(CheckoutSession session) => _context.CheckoutSessions.Add(session);

    public void AddCoupon(Coupon coupon) => _context.Coupons.Add(coupon);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
