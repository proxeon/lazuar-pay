using System;
using System.Collections.Generic;
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
        Guid Id, 
        Guid ClientProfileId, 
        Guid ProductId, 
        string ProductName, 
        decimal ProductPrice,
        string Status, 
        DateTime? CurrentPeriodEnd, 
        DateTime? NextBillingDate, 
        DateTime CreatedAt,
        string? VaultedCustomerId, 
        string? VaultedTokenId
    );

    public async Task<PaginatedResponse<CommerceSubscriptionDto>> GetSubscribersAsync(
        Guid organizationId, 
        int page, 
        int limit, 
        string? searchTerm = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        // Query only from local commerce tables to preserve schema boundaries
        const string sql = @"
            SELECT
                s.""Id"", s.""ClientProfileId"", s.""ProductId"",
                p.""Name"" as ProductName, p.""Price"" as ProductPrice,
                s.""Status"", s.""CurrentPeriodEnd"", s.""NextBillingDate"", s.""CreatedAt"",
                s.""VaultedCustomerId"", s.""VaultedTokenId""
            FROM commerce.""Subscriptions"" s
            JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
            WHERE s.""OrganizationId"" = @OrgId 
            AND s.""Status"" != 'PENDING'
            ORDER BY s.""CreatedAt"" DESC;";

        var rawSubs = (await connection.QueryAsync<RawSubDto>(sql, new { OrgId = organizationId })).ToList();
        if (!rawSubs.Any())
        {
            return new PaginatedResponse<CommerceSubscriptionDto>(Enumerable.Empty<CommerceSubscriptionDto>(), 0, page, limit);
        }

        // Batch resolve client details asynchronously from the CRM module
        var profileIds = rawSubs.Select(s => s.ClientProfileId).Distinct().ToList();
        var profiles = await _crmQueryService.GetClientProfilesAsync(profileIds);
        var profileMap = profiles.ToDictionary(p => Guid.Parse(p.Id), p => p);

        var now = DateTime.UtcNow;
        var dtos = rawSubs.Select(s =>
        {
            profileMap.TryGetValue(s.ClientProfileId, out var profile);
            
            var daysOverdue = (s.Status is "PAST_DUE" or "CANCELED") && s.NextBillingDate.HasValue
                ? Math.Max(0, (int)(now - s.NextBillingDate.Value).TotalDays)
                : (int?)null;

            return new CommerceSubscriptionDto
            {
                Id = s.Id.ToString(),
                Client_profile_id = s.ClientProfileId.ToString(),
                Customer_name = profile?.Full_name ?? "Unknown",
                Customer_email = profile?.Email ?? string.Empty,
                Customer_phone = profile?.Phone ?? string.Empty,
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

        // Apply search filtering in-memory
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            dtos = dtos.Where(d => 
                d.Customer_name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                d.Customer_email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            );
        }

        var filteredList = dtos.ToList();
        var totalCount = filteredList.Count;
        var paginatedData = filteredList.Skip((page - 1) * limit).Take(limit);

        return new PaginatedResponse<CommerceSubscriptionDto>(paginatedData, totalCount, page, limit);
    }
}
