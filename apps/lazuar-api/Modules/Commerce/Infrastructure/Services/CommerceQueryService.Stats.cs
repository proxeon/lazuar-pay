using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Lazuar.ApiTypes;
using Modules.Commerce.Application;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    private record SubStatsDto(
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        decimal Price,
        string Interval,
        decimal UnitAmount = 0,
        int Quantity = 1,
        DateTime? CollectionPausedUntil = null);

    private record TxRevenueDto(decimal Amount, string Status, DateTime CreatedAt, string RecordedByName);

    private record DunningRecoveryStatsDto(decimal RecoveredRevenue, int SavedSubscriptions);

    public async Task<CommerceStatsDto> GetStatsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string subSql = @"
            SELECT 
                s.""Status"" as Status, s.""CreatedAt"" as CreatedAt, s.""UpdatedAt"" as UpdatedAt, 
                p.""Price"" as Price,
                COALESCE(NULLIF(BTRIM(s.""BillingInterval""), ''), p.""Interval"") as Interval,
                s.""UnitAmount"" as UnitAmount, s.""Quantity"" as Quantity,
                s.""CollectionPausedUntil"" as CollectionPausedUntil
            FROM commerce.""Subscriptions"" s
            JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND s.""Status"" != 'PENDING'";

        var subs = (await connection.QueryAsync<SubStatsDto>(subSql, new { OrgId = organizationId })).ToList();

        var now = DateTime.UtcNow;
        var activeSubs = subs.Where(s => s.Status == "ACTIVE" || s.Status == "PAST_DUE").ToList();
        var mrr = subs.Sum(s => CommerceMrr.MonthlyEquivalent(
            s.Status,
            s.CollectionPausedUntil,
            now,
            s.Interval,
            s.UnitAmount,
            s.Quantity,
            s.Price));
        var thirtyDaysAgo = now.AddDays(-30);
        var cancelledLast30 = subs.Count(s => s.Status == "CANCELED" && s.UpdatedAt >= thirtyDaysAgo);
        var newActiveLast30 = activeSubs.Count(s => s.CreatedAt >= thirtyDaysAgo);
        var active30DaysAgo = activeSubs.Count + cancelledLast30 - newActiveLast30;
        
        double churnRate = active30DaysAgo > 0 ? Math.Round((double)cancelledLast30 / active30DaysAgo * 100, 2) : 0;
        var mrrSeats = subs.Count(s => CommerceMrr.ContributesToMrr(
            s.Status, s.CollectionPausedUntil, now, s.Interval));
        double arpu = CommerceMrr.Arpu(mrr, mrrSeats);

        // Revenue KPIs from TransactionLogs (honest ops dashboard — not stubbed zeros).
        const string txSql = @"
            SELECT t.""Amount"" as Amount, t.""Status"" as Status, t.""CreatedAt"" as CreatedAt, t.""RecordedByName"" as RecordedByName
            FROM commerce.""TransactionLogs"" t
            WHERE t.""OrganizationId"" = @OrgId";

        var txs = (await connection.QueryAsync<TxRevenueDto>(txSql, new { OrgId = organizationId })).ToList();
        var confirmed = txs.Where(t => string.Equals(t.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase)).ToList();

        var totalRevenue = confirmed.Sum(t => t.Amount);

        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var cashFlowTrend = confirmed
            .Where(t => t.CreatedAt >= sixMonthsAgo)
            .GroupBy(t => new DateTime(t.CreatedAt.Year, t.CreatedAt.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new CashFlowTrendDto
            {
                Month = g.Key.ToString("yyyy-MM"),
                Amount = (double)g.Sum(x => x.Amount)
            })
            .ToList();

        // Fill missing months so charts don't look broken with sparse data.
        for (var i = 0; i < 6; i++)
        {
            var month = sixMonthsAgo.AddMonths(i).ToString("yyyy-MM");
            if (cashFlowTrend.All(c => c.Month != month))
            {
                cashFlowTrend.Add(new CashFlowTrendDto { Month = month, Amount = 0 });
            }
        }
        cashFlowTrend = cashFlowTrend.OrderBy(c => c.Month).ToList();

        var paymentMethods = confirmed
            .GroupBy(t => string.IsNullOrWhiteSpace(t.RecordedByName) ? "UNKNOWN" : t.RecordedByName.ToUpperInvariant())
            .Select(g => new PaymentMethodDto
            {
                Method = g.Key,
                Count = g.Count(),
                Total_amount = (double)g.Sum(x => x.Amount)
            })
            .OrderByDescending(p => p.Total_amount)
            .ToList();

        const string recoverySql = @"
            SELECT COALESCE(SUM(""RecoveredRevenue""), 0) AS ""RecoveredRevenue"",
                   CAST(COALESCE(SUM(""SavedSubscriptions""), 0) AS integer) AS ""SavedSubscriptions""
            FROM commerce.""DunningCampaigns""
            WHERE ""OrganizationId"" = @OrgId";

        var recovery = await connection.QuerySingleAsync<DunningRecoveryStatsDto>(
            recoverySql,
            new { OrgId = organizationId });

        return new CommerceStatsDto
        {
            Mrr = (double)mrr,
            Arr = (double)(mrr * 12m),
            Active_subscribers = activeSubs.Count,
            Past_due_subscribers = subs.Count(s => s.Status == "PAST_DUE"),
            Cancelled_subscribers = subs.Count(s => s.Status == "CANCELED"),
            Net_new_last_30_days = newActiveLast30 - cancelledLast30,
            Churn_rate_percentage = churnRate,
            Average_revenue_per_user = arpu,
            Total_revenue_collected = (double)totalRevenue,
            Recovered_revenue = (double)recovery.RecoveredRevenue,
            Saved_subscriptions = recovery.SavedSubscriptions,
            Cash_flow_trend = cashFlowTrend,
            Payment_methods = paymentMethods
        };
    }
}
