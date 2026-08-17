using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Lazuar.ApiTypes;
using Modules.Commerce.Application;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    private record RawCustomCheckoutDto(
        Guid Id,
        Guid ClientProfileId,
        string Status,
        DateTime ExpiresAt,
        DateTime? DueAt,
        bool IsB2bRequired,
        string AdHocLineItems,
        DateTime CreatedAt,
        string? DocumentNumber,
        int TotalCount
    );

    public async Task<PaginatedResponse<CustomCheckoutDto>> GetCustomCheckoutsAsync(Guid organizationId, int page, int limit)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;

        const string sql = @"
            SELECT 
                c.""Id"", c.""ClientProfileId"", c.""Status"", c.""ExpiresAt"", c.""DueAt"", c.""IsB2bRequired"", c.""AdHocLineItems"", c.""CreatedAt"", c.""DocumentNumber"",
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
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
            _billingQueryService, organizationId);

        var dtos = rawCheckouts.Select(c =>
        {
            profileMap.TryGetValue(c.ClientProfileId, out var profile);
            
            var lineItems = string.IsNullOrWhiteSpace(c.AdHocLineItems) 
                ? new List<CustomLineItemDto>() 
                : JsonSerializer.Deserialize<List<CustomLineItemDto>>(c.AdHocLineItems, jsonOptions) ?? new List<CustomLineItemDto>();

            var totalAmount = CustomQuotePayable(lineItems, merchantHasSst);

            return new CustomCheckoutDto
            {
                Id = c.Id.ToString(),
                Client_profile_id = c.ClientProfileId.ToString(),
                Client_name = profile?.Full_name,
                Client_email = profile?.Email,
                Status = c.Status,
                Expires_at = new DateTimeOffset(c.ExpiresAt),
                Due_at = c.DueAt.HasValue ? new DateTimeOffset(c.DueAt.Value) : null,
                Is_b2b_required = c.IsB2bRequired,
                Line_items = lineItems,
                Total_amount = (double)totalAmount,
                Created_at = new DateTimeOffset(c.CreatedAt),
                Document_number = c.DocumentNumber
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
                c.""Id"", c.""ClientProfileId"", c.""Status"", c.""ExpiresAt"", c.""DueAt"", c.""IsB2bRequired"", c.""AdHocLineItems"", c.""CreatedAt"", c.""DocumentNumber"", 1 AS ""TotalCount""
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

        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
            _billingQueryService, organizationId);
        var totalAmount = CustomQuotePayable(lineItems, merchantHasSst);

        return new CustomCheckoutDto
        {
            Id = rawCheckout.Id.ToString(),
            Client_profile_id = rawCheckout.ClientProfileId.ToString(),
            Client_name = profile?.Full_name,
            Client_email = profile?.Email,
            Status = rawCheckout.Status,
            Expires_at = new DateTimeOffset(rawCheckout.ExpiresAt),
            Due_at = rawCheckout.DueAt.HasValue ? new DateTimeOffset(rawCheckout.DueAt.Value) : null,
            Is_b2b_required = rawCheckout.IsB2bRequired,
            Line_items = lineItems,
            Total_amount = (double)totalAmount,
            Created_at = new DateTimeOffset(rawCheckout.CreatedAt),
            Document_number = rawCheckout.DocumentNumber
        };
    }

    public async Task<PaginatedResponse<CommerceDisputeDto>> GetDisputesAsync(Guid organizationId, int page, int limit)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;
        const string sql = @"
            SELECT
                d.""Id"", d.""GatewayTransactionId"", d.""Amount"", d.""Currency"", d.""Status"",
                d.""SubscriptionId"", d.""CheckoutSessionId"", d.""CreatedAt"",
                (COUNT(*) OVER())::int AS ""TotalCount""
            FROM commerce.""Disputes"" d
            WHERE d.""OrganizationId"" = @OrgId
            ORDER BY d.""CreatedAt"" DESC
            LIMIT @Limit OFFSET @Offset;";

        var rows = (await connection.QueryAsync<RawDisputeDto>(sql, new { OrgId = organizationId, Limit = limit, Offset = offset })).ToList();
        if (!rows.Any())
        {
            return new PaginatedResponse<CommerceDisputeDto>(Enumerable.Empty<CommerceDisputeDto>(), 0, page, limit);
        }

        var dtos = rows.Select(r => new CommerceDisputeDto
        {
            Id = r.Id.ToString(),
            Gateway_transaction_id = r.GatewayTransactionId,
            Amount = (double)r.Amount,
            Currency = r.Currency,
            Status = r.Status,
            Subscription_id = r.SubscriptionId?.ToString(),
            Checkout_session_id = r.CheckoutSessionId?.ToString(),
            Created_at = new DateTimeOffset(r.CreatedAt)
        }).ToList();

        return new PaginatedResponse<CommerceDisputeDto>(dtos, rows[0].TotalCount, page, limit);
    }

    private static decimal CustomQuotePayable(IEnumerable<CustomLineItemDto> lineItems, bool merchantHasSst)
    {
        var net = lineItems.Sum(li => (decimal)li.Unit_price * li.Quantity);
        return SubscriptionBillingAmount.CustomQuoteBreakdown(net, merchantHasSst).Gross;
    }

    private record RawDisputeDto(
        Guid Id,
        string GatewayTransactionId,
        decimal Amount,
        string Currency,
        string Status,
        Guid? SubscriptionId,
        Guid? CheckoutSessionId,
        DateTime CreatedAt,
        int TotalCount);
}
