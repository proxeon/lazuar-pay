using System;
using Dapper;
using System.Data;
using System.Linq;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Community.Infrastructure.Services;

public partial class CommunityQueryService
{
    private record SubStatsDto(string Status, bool IsReminderOnly, DateTime CreatedAt, DateTime UpdatedAt, decimal Price, string Interval);
    private record RawCashFlowTrendDto(string Month, decimal Amount);
    private record RawPaymentMethodDto(string Method, int Count, decimal TotalAmount);

    [Obsolete("MRR and financial stats are now managed by the Billing module. Use IBillingQueryService for accurate ledger-based financials.")]
    public async Task<CommunitySubscriberStatsDto> GetSubscriberStatsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string subSql = @"
            SELECT 
                s.""Status"" as Status, s.""IsReminderOnly"" as IsReminderOnly, 
                s.""CreatedAt"" as CreatedAt, s.""UpdatedAt"" as UpdatedAt, 
                p.""Price"" as Price, p.""Interval"" as Interval
            FROM community.""Subscriptions"" s
            JOIN community.""Plans"" p ON s.""PlanId"" = p.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND s.""Status"" != 'PENDING'";

        var subs = (await connection.QueryAsync<SubStatsDto>(subSql, new { OrgId = organizationId })).ToList();

        const string revSql = @"
            SELECT COALESCE(SUM(pr.""Amount""), 0.0)
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND pr.""Status"" = 'CONFIRMED'";
        
        var totalRevenue = await connection.ExecuteScalarAsync<decimal>(revSql, new { OrgId = organizationId });

        const string trendSql = @"
            SELECT 
                to_char(pr.""CreatedAt"", 'Mon YYYY') as Month, 
                SUM(pr.""Amount"") as Amount
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND pr.""Status"" = 'CONFIRMED'
            GROUP BY to_char(pr.""CreatedAt"", 'Mon YYYY'), to_char(pr.""CreatedAt"", 'YYYY-MM')
            ORDER BY to_char(pr.""CreatedAt"", 'YYYY-MM') DESC
            LIMIT 6";
        
        var rawTrend = await connection.QueryAsync<RawCashFlowTrendDto>(trendSql, new { OrgId = organizationId });
        var cashFlowTrend = rawTrend.Select(r => new CashFlowTrendDto { Month = r.Month, Amount = (double)r.Amount }).Reverse().ToList();

        const string methodsSql = @"
            SELECT 
                pr.""PaymentMethod"" as Method, 
                COUNT(*)::int as Count, 
                SUM(pr.""Amount"") as TotalAmount
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND pr.""Status"" = 'CONFIRMED'
            GROUP BY pr.""PaymentMethod""";
        
        var rawMethods = await connection.QueryAsync<RawPaymentMethodDto>(methodsSql, new { OrgId = organizationId });
        var paymentMethods = rawMethods.Select(r => new PaymentMethodDto { Method = r.Method, Count = r.Count, Total_amount = (double)r.TotalAmount }).ToList();

        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var activeSubs = subs.Where(s => s.Status == "ACTIVE" || s.Status == "PAST_DUE").ToList();
        
        var mrr = activeSubs
            .Where(s => !s.IsReminderOnly)
            .Sum(s => s.Interval == "yr" ? s.Price / 12m : s.Price);

        var cancelledLast30 = subs.Count(s => s.Status == "CANCELLED" && s.UpdatedAt >= thirtyDaysAgo);
        var newActiveLast30 = activeSubs.Count(s => s.CreatedAt >= thirtyDaysAgo);
        var netNewSubscribers = newActiveLast30 - cancelledLast30;
        var active30DaysAgo = activeSubs.Count + cancelledLast30 - newActiveLast30;
        
        double churnRate = active30DaysAgo > 0 ? Math.Round((double)cancelledLast30 / active30DaysAgo * 100, 2) : 0;
        var truePlatformActive = activeSubs.Count(s => !s.IsReminderOnly);
        double arpu = truePlatformActive > 0 ? (double)(mrr / truePlatformActive) : 0;

        return new CommunitySubscriberStatsDto
        {
            Mrr = (double)mrr,
            Active_subscribers = activeSubs.Count,
            Past_due_subscribers = subs.Count(s => s.Status == "PAST_DUE"),
            Cancelled_subscribers = subs.Count(s => s.Status == "CANCELLED"),
            Net_new_last_30_days = netNewSubscribers,
            Churn_rate_percentage = churnRate,
            Average_revenue_per_user = arpu,
            Reminder_effectiveness_percentage = 85.0,
            Total_revenue_collected = (double)totalRevenue,
            Cash_flow_trend = cashFlowTrend,
            Payment_methods = paymentMethods
        };
    }
}
