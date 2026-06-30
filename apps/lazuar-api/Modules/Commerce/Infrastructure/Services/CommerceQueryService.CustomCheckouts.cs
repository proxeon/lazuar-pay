using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    private record RawCustomCheckoutDto(
        Guid Id,
        Guid ClientProfileId,
        string Status,
        DateTime ExpiresAt,
        bool IsB2bRequired,
        string AdHocLineItems,
        DateTime CreatedAt,
        int TotalCount
    );

    public async Task<PaginatedResponse<CustomCheckoutDto>> GetCustomCheckoutsAsync(Guid organizationId, int page, int limit)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;

        const string sql = @"
            SELECT 
                c.""Id"", c.""ClientProfileId"", c.""Status"", c.""ExpiresAt"", c.""IsB2bRequired"", c.""AdHocLineItems"", c.""CreatedAt"",
                (COUNT(*) OVER())::int AS ""TotalCount""
            FROM commerce.""CheckoutSessions"" c
            WHERE c.""OrganizationId"" = @OrgId AND c.""ProductId"" IS NULL
            ORDER BY c.""CreatedAt"" DESC
            LIMIT @Limit OFFSET @Offset;";

        var rawCheckouts = (await connection.QueryAsync<RawCustomCheckoutDto>(sql, new { OrgId = organizationId, Limit = limit, Offset = offset })).ToList();

        if (!rawCheckouts.Any())
        {
            return new PaginatedResponse<CustomCheckoutDto>(Enumerable.Empty<CustomCheckoutDto>(), 0, page, limit);
        }

        int totalCount = rawCheckouts.First().TotalCount;

        var profileIds = rawCheckouts.Select(c => c.ClientProfileId).Distinct().ToList();
        var profiles = await _crmQueryService.GetClientProfilesAsync(profileIds);
        var profileMap = profiles.ToDictionary(p => Guid.Parse(p.Id), p => p);

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        var dtos = rawCheckouts.Select(c =>
        {
            profileMap.TryGetValue(c.ClientProfileId, out var profile);
            
            var lineItems = string.IsNullOrWhiteSpace(c.AdHocLineItems) 
                ? new List<CustomLineItemDto>() 
                : JsonSerializer.Deserialize<List<CustomLineItemDto>>(c.AdHocLineItems, jsonOptions) ?? new List<CustomLineItemDto>();

            var totalAmount = lineItems.Sum(li => li.Unit_price * li.Quantity);

            return new CustomCheckoutDto
            {
                Id = c.Id.ToString(),
                Client_profile_id = c.ClientProfileId.ToString(),
                Client_name = profile?.Full_name,
                Client_email = profile?.Email,
                Status = c.Status,
                Expires_at = new DateTimeOffset(c.ExpiresAt),
                Is_b2b_required = c.IsB2bRequired,
                Line_items = lineItems,
                Total_amount = (double)totalAmount,
                Created_at = new DateTimeOffset(c.CreatedAt)
            };
        }).ToList();

        return new PaginatedResponse<CustomCheckoutDto>(dtos, totalCount, page, limit);
    }

    public async Task<CustomCheckoutDto?> GetCustomCheckoutBySessionIdAsync(Guid organizationId, Guid sessionId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 
                c.""Id"", c.""ClientProfileId"", c.""Status"", c.""ExpiresAt"", c.""IsB2bRequired"", c.""AdHocLineItems"", c.""CreatedAt"", 1 AS ""TotalCount""
            FROM commerce.""CheckoutSessions"" c
            WHERE c.""OrganizationId"" = @OrgId AND c.""Id"" = @SessionId AND c.""ProductId"" IS NULL
            LIMIT 1;";

        var rawCheckout = await connection.QuerySingleOrDefaultAsync<RawCustomCheckoutDto>(sql, new { OrgId = organizationId, SessionId = sessionId });

        if (rawCheckout == null) return null;

        var profile = await _crmQueryService.GetClientProfileAsync(rawCheckout.ClientProfileId);

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var lineItems = string.IsNullOrWhiteSpace(rawCheckout.AdHocLineItems) 
            ? new List<CustomLineItemDto>() 
            : JsonSerializer.Deserialize<List<CustomLineItemDto>>(rawCheckout.AdHocLineItems, jsonOptions) ?? new List<CustomLineItemDto>();

        var totalAmount = lineItems.Sum(li => li.Unit_price * li.Quantity);

        return new CustomCheckoutDto
        {
            Id = rawCheckout.Id.ToString(),
            Client_profile_id = rawCheckout.ClientProfileId.ToString(),
            Client_name = profile?.Full_name,
            Client_email = profile?.Email,
            Status = rawCheckout.Status,
            Expires_at = new DateTimeOffset(rawCheckout.ExpiresAt),
            Is_b2b_required = rawCheckout.IsB2bRequired,
            Line_items = lineItems,
            Total_amount = (double)totalAmount,
            Created_at = new DateTimeOffset(rawCheckout.CreatedAt)
        };
    }
}
