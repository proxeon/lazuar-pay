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
        return await _context.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Coupon?> GetCouponByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<CheckoutSession?> GetCheckoutSessionByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.CheckoutSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Subscription?> GetSubscriptionByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Check EF Core's active memory tracker first to support pre-save Domain Events
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

    public void AddProduct(Product product) => _context.Products.Add(product);

    public void AddSubscription(Subscription subscription) => _context.Subscriptions.Add(subscription);

    public void AddOrder(Order order) => _context.Orders.Add(order);

    public void AddChargeAttempt(ChargeAttemptLog log) => _context.ChargeAttemptLogs.Add(log);

    public void AddReminderSchedule(ReminderSchedule schedule) => _context.ReminderSchedules.Add(schedule);

    public void RemoveReminderSchedule(ReminderSchedule schedule) => _context.ReminderSchedules.Remove(schedule);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
