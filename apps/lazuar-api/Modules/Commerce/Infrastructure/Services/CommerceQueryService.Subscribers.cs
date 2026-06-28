using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    private record RawSubDto(
        Guid Id, Guid ClientProfileId, Guid ProductId, string ProductName, decimal ProductPrice,
        string Status, DateTime? CurrentPeriodEnd, DateTime? NextBillingDate, DateTime CreatedAt,
        string? VaultedCustomerId, string? VaultedTokenId, string CustomerName, string CustomerEmail, string CustomerPhone);

    public async Task<PaginatedResponse<CommerceSubscriptionDto>> GetSubscribersAsync(Guid organizationId, int page, int limit, string? searchTerm = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;
        var searchPattern = string.IsNullOrWhiteSpace(searchTerm) ? null : $"%{searchTerm}%";

        const string sql = @"
            SELECT COUNT(*)::int
            FROM commerce.""Subscriptions"" s
            JOIN crm.""ClientProfiles"" cp ON s.""ClientProfileId"" = cp.""Id""
            WHERE s.""OrganizationId"" = @OrgId 
            AND s.""Status"" != 'PENDING'
            AND (@SearchTerm IS NULL OR cp.""FullName"" ILIKE @SearchTerm OR cp.""Email"" ILIKE @SearchTerm);

            SELECT
                s.""Id"", s.""ClientProfileId"", s.""ProductId"",
                p.""Name"" as ProductName, p.""Price"" as ProductPrice,
                s.""Status"", s.""CurrentPeriodEnd"", s.""NextBillingDate"", s.""CreatedAt"",
                s.""VaultedCustomerId"", s.""VaultedTokenId"",
                cp.""FullName"" as CustomerName, cp.""Email"" as CustomerEmail, cp.""Phone"" as CustomerPhone
            FROM commerce.""Subscriptions"" s
            JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
            JOIN crm.""ClientProfiles"" cp ON s.""ClientProfileId"" = cp.""Id""
            WHERE s.""OrganizationId"" = @OrgId 
            AND s.""Status"" != 'PENDING'
            AND (@SearchTerm IS NULL OR cp.""FullName"" ILIKE @SearchTerm OR cp.""Email"" ILIKE @SearchTerm)
            ORDER BY s.""CreatedAt"" DESC
            LIMIT @Limit OFFSET @Offset;";

        using var multi = await connection.QueryMultipleAsync(sql, new { OrgId = organizationId, Limit = limit, Offset = offset, SearchTerm = searchPattern });
        var totalCount = await multi.ReadFirstAsync<int>();
        var rawSubs = (await multi.ReadAsync<RawSubDto>()).ToList();

        if (totalCount == 0) return new PaginatedResponse<CommerceSubscriptionDto>(Enumerable.Empty<CommerceSubscriptionDto>(), 0, page, limit);

        var now = DateTime.UtcNow;
        var dtos = rawSubs.Select(s =>
        {
            var daysOverdue = (s.Status is "PAST_DUE" or "CANCELED") && s.NextBillingDate.HasValue
                ? Math.Max(0, (int)(now - s.NextBillingDate.Value).TotalDays)
                : (int?)null;

            return new CommerceSubscriptionDto
            {
                Id = s.Id.ToString(),
                Client_profile_id = s.ClientProfileId.ToString(),
                Customer_name = s.CustomerName ?? "Unknown",
                Customer_email = s.CustomerEmail ?? "",
                Customer_phone = s.CustomerPhone ?? "",
                Product_id = s.ProductId.ToString(),
                Product_name = s.ProductName,
                Product_price = (double)s.ProductPrice,
                Status = s.Status,
                Current_period_end = s.CurrentPeriodEnd.HasValue ? new DateTimeOffset(s.CurrentPeriodEnd.Value) : null,
                Next_billing_date = s.NextBillingDate.HasValue ? new DateTimeOffset(s.NextBillingDate.Value) : null,
                Days_overdue = daysOverdue,
                Vaulted_customer_id = s.VaultedCustomerId,
                Vaulted_token_id = s.VaultedTokenId,
                Created_at = new DateTimeOffset(s.CreatedAt)
            };
        });

        return new PaginatedResponse<CommerceSubscriptionDto>(dtos, totalCount, page, limit);
    }
}
