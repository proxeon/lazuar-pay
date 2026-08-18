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
    internal record RawPortalSubDto(
        Guid Id,
        Guid ProductId,
        string ProductName,
        string Status,
        DateTime? CurrentPeriodEnd,
        bool CancelAtPeriodEnd,
        bool IsReminderOnly,
        int Quantity = 1,
        Guid? PendingProductId = null,
        string? PendingProductName = null,
        DateTime? TrialEndsAt = null);
    private record RawPortalOrderDto(Guid Id, Guid ProductId, string ProductName, string Status, DateTime CreatedAt);

    public async Task<AggregatedPortalDataResponse?> GetPortalDataAsync(Guid organizationId, Guid referenceSubscriptionId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string clientProfileSql = @"
            SELECT ""ClientProfileId"" FROM commerce.""Subscriptions"" 
            WHERE ""Id"" = @SubId AND ""OrganizationId"" = @OrgId LIMIT 1";

        var clientProfileId = await connection.QuerySingleOrDefaultAsync<Guid?>(clientProfileSql, new { SubId = referenceSubscriptionId, OrgId = organizationId });

        if (clientProfileId == null) return null;

        const string subsSql = @"
            SELECT s.""Id"", s.""ProductId"", p.""Name"" as ProductName, s.""Status"",
                   s.""CurrentPeriodEnd"", s.""CancelAtPeriodEnd"",
                   s.""IsReminderOnly"", s.""Quantity"", s.""PendingProductId"",
                   pp.""Name"" as PendingProductName, s.""TrialEndsAt""
            FROM commerce.""Subscriptions"" s
            JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
            LEFT JOIN commerce.""Products"" pp ON s.""PendingProductId"" = pp.""Id""
            WHERE s.""ClientProfileId"" = @ProfileId AND s.""OrganizationId"" = @OrgId AND s.""Status"" != 'PENDING'
            ORDER BY s.""CreatedAt"" DESC";

        var subs = await connection.QueryAsync<RawPortalSubDto>(subsSql, new { ProfileId = clientProfileId.Value, OrgId = organizationId });

        const string ordersSql = @"
            SELECT o.""Id"", o.""ProductId"", p.""Name"" as ProductName, o.""Status"", o.""CreatedAt""
            FROM commerce.""Orders"" o
            JOIN commerce.""Products"" p ON o.""ProductId"" = p.""Id""
            WHERE o.""ClientProfileId"" = @ProfileId AND o.""OrganizationId"" = @OrgId AND o.""Status"" != 'PENDING'
            ORDER BY o.""CreatedAt"" DESC";

        var orders = await connection.QueryAsync<RawPortalOrderDto>(ordersSql, new { ProfileId = clientProfileId.Value, OrgId = organizationId });

        return new AggregatedPortalDataResponse
        {
            Subscriptions = subs.Select(MapPortalSubscription).ToList(),
            Orders = orders.Select(o => new PortalOrderDto
            {
                Id = o.Id.ToString(),
                Product_id = o.ProductId.ToString(),
                Product_name = o.ProductName,
                Status = o.Status,
                Created_at = new DateTimeOffset(o.CreatedAt)
            }).ToList()
        };
    }

    internal static PortalSubscriptionDto MapPortalSubscription(RawPortalSubDto s) => new()
    {
        Id = s.Id.ToString(),
        Product_id = s.ProductId.ToString(),
        Product_name = s.ProductName,
        Status = s.Status,
        Current_period_end = s.CurrentPeriodEnd.HasValue ? new DateTimeOffset(s.CurrentPeriodEnd.Value) : null,
        Cancel_at_period_end = s.CancelAtPeriodEnd,
        Is_reminder_only = s.IsReminderOnly,
        Quantity = s.Quantity < 1 ? 1 : s.Quantity,
        Pending_product_id = s.PendingProductId?.ToString(),
        Pending_product_name = s.PendingProductName,
        Trial_ends_at = s.TrialEndsAt.HasValue ? new DateTimeOffset(s.TrialEndsAt.Value) : null
    };

    public async Task<IReadOnlyList<PortalPlanDto>> GetPortalPlansAsync(Guid organizationId, Guid subscriptionId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string subSql = @"
            SELECT s.""ProductId"", p.""GatewayName"", p.""Currency""
            FROM commerce.""Subscriptions"" s
            JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
            WHERE s.""Id"" = @SubId AND s.""OrganizationId"" = @OrgId
            LIMIT 1";

        var current = await connection.QuerySingleOrDefaultAsync<(Guid ProductId, string GatewayName, string Currency)>(
            subSql, new { SubId = subscriptionId, OrgId = organizationId });
        if (current.ProductId == Guid.Empty)
        {
            return Array.Empty<PortalPlanDto>();
        }

        const string plansSql = @"
            SELECT p.""Id"", p.""Name"", p.""Interval"", p.""Price"" as Amount, p.""Currency""
            FROM commerce.""Products"" p
            WHERE p.""OrganizationId"" = @OrgId
              AND p.""IsActive"" = true
              AND p.""Interval"" IN ('mo', 'yr')
              AND p.""GatewayName"" = @Gateway
              AND p.""Currency"" = @Currency
              AND p.""Id"" <> @CurrentId
            ORDER BY p.""Price""";

        var rows = await connection.QueryAsync<(Guid Id, string Name, string Interval, decimal Amount, string Currency)>(
            plansSql,
            new
            {
                OrgId = organizationId,
                Gateway = current.GatewayName,
                Currency = current.Currency,
                CurrentId = current.ProductId
            });

        return rows.Select(r => new PortalPlanDto
        {
            Id = r.Id.ToString(),
            Name = r.Name,
            Interval = r.Interval,
            Amount = (double)r.Amount,
            Currency = r.Currency
        }).ToList();
    }
}
