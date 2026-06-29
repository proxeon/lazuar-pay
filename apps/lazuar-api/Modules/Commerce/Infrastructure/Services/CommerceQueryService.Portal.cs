using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    private record RawPortalSubDto(Guid Id, Guid ProductId, string ProductName, string Status, DateTime? CurrentPeriodEnd);
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
            SELECT s.""Id"", s.""ProductId"", p.""Name"" as ProductName, s.""Status"", s.""CurrentPeriodEnd""
            FROM commerce.""Subscriptions"" s
            JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
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
            Subscriptions = subs.Select(s => new PortalSubscriptionDto
            {
                Id = s.Id.ToString(),
                Product_id = s.ProductId.ToString(),
                Product_name = s.ProductName,
                Status = s.Status,
                Current_period_end = s.CurrentPeriodEnd.HasValue ? new DateTimeOffset(s.CurrentPeriodEnd.Value) : null
            }).ToList(),
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
}
