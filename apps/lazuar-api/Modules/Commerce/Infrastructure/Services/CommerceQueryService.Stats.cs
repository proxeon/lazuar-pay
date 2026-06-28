using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    private record SubStatsDto(string Status, DateTime CreatedAt, DateTime UpdatedAt, decimal Price, string Interval);

    public async Task<CommerceStatsDto> GetStatsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string subSql = @"
            SELECT 
                s.""Status"" as Status, s.""CreatedAt"" as CreatedAt, s.""UpdatedAt"" as UpdatedAt, 
                p.""Price"" as Price, p.""Interval"" as Interval
            FROM commerce.""Subscriptions"" s
            JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND s.""Status"" != 'PENDING'";

        var subs = (await connection.QueryAsync<SubStatsDto>(subSql, new { OrgId = organizationId })).ToList();

        var activeSubs = subs.Where(s => s.Status == "ACTIVE" || s.Status == "PAST_DUE").ToList();
        var mrr = activeSubs.Sum(s => s.Interval == "yr" ? s.Price / 12m : s.Price);

        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var cancelledLast30 = subs.Count(s => s.Status == "CANCELED" && s.UpdatedAt >= thirtyDaysAgo);
        var newActiveLast30 = activeSubs.Count(s => s.CreatedAt >= thirtyDaysAgo);
        var active30DaysAgo = activeSubs.Count + cancelledLast30 - newActiveLast30;
        
        double churnRate = active30DaysAgo > 0 ? Math.Round((double)cancelledLast30 / active30DaysAgo * 100, 2) : 0;
        double arpu = activeSubs.Count > 0 ? (double)(mrr / activeSubs.Count) : 0;

        return new CommerceStatsDto
        {
            Mrr = (double)mrr,
            ActiveSubscribers = activeSubs.Count,
            PastDueSubscribers = subs.Count(s => s.Status == "PAST_DUE"),
            CancelledSubscribers = subs.Count(s => s.Status == "CANCELED"),
            NetNewLast30Days = newActiveLast30 - cancelledLast30,
            ChurnRatePercentage = churnRate,
            AverageRevenuePerUser = arpu,
            TotalRevenueCollected = 0, 
            CashFlowTrend = new List<CashFlowTrendDto>(),
            PaymentMethods = new List<PaymentMethodDto>()
        };
    }
}
