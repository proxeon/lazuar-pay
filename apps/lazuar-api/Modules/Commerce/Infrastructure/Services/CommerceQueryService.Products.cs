using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    internal record RawProductDto(
        Guid Id, string Slug, string Name, decimal Price, string PricingModel, decimal MinimumPrice, string Currency, string Interval,
        bool RequiresAddress, bool RequiresTaxId, bool RequiresPhone,
        string? FulfillmentTargets, bool IsActive, string GatewayName,
        string? SstTaxType = null, decimal SstRatePercent = 0, int TrialDays = 0);

    public async Task<IEnumerable<ProductDto>> GetProductsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 
                ""Id"", ""Slug"", ""Name"", ""Price"", ""PricingModel"", ""MinimumPrice"", ""Currency"", ""Interval"",
                ""RequiresAddress"", ""RequiresTaxId"", ""RequiresPhone"",
                ""FulfillmentTargets""::text, ""IsActive"", ""GatewayName"",
                ""SstTaxType"", ""SstRatePercent"", ""TrialDays""
            FROM commerce.""Products""
            WHERE ""OrganizationId"" = @OrgId
            ORDER BY ""CreatedAt"" DESC";

        var rawProducts = (await connection.QueryAsync<RawProductDto>(sql, new { OrgId = organizationId })).ToList();
        var prices = await LoadPricesAsync(connection, rawProducts.Select(p => p.Id));
        return rawProducts.Select(p => MapToDto(p, prices));
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid organizationId, Guid productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 
                ""Id"", ""Slug"", ""Name"", ""Price"", ""PricingModel"", ""MinimumPrice"", ""Currency"", ""Interval"",
                ""RequiresAddress"", ""RequiresTaxId"", ""RequiresPhone"",
                ""FulfillmentTargets""::text, ""IsActive"", ""GatewayName"",
                ""SstTaxType"", ""SstRatePercent"", ""TrialDays""
            FROM commerce.""Products""
            WHERE ""OrganizationId"" = @OrgId AND ""Id"" = @ProductId
            LIMIT 1";

        var rawProduct = await connection.QuerySingleOrDefaultAsync<RawProductDto>(sql, new { OrgId = organizationId, ProductId = productId });

        if (rawProduct == null) return null;

        var prices = await LoadPricesAsync(connection, new[] { rawProduct.Id });
        return MapToDto(rawProduct, prices);
    }

    private static async Task<ILookup<Guid, ProductPriceDto>> LoadPricesAsync(IDbConnection connection, IEnumerable<Guid> productIds)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Array.Empty<ProductPriceDto>().ToLookup(_ => Guid.Empty);
        }

        const string sql = @"
            SELECT ""ProductId"", ""Id"", ""Interval"", ""Amount"", ""IsDefault""
            FROM commerce.""ProductPrices""
            WHERE ""ProductId"" = ANY(@Ids)";

        try
        {
            var rows = await connection.QueryAsync<(Guid ProductId, Guid Id, string Interval, decimal Amount, bool IsDefault)>(
                sql, new { Ids = ids.ToArray() });
            return rows.ToLookup(
                r => r.ProductId,
                r => new ProductPriceDto
                {
                    Id = r.Id.ToString(),
                    Interval = r.Interval,
                    Amount = (double)r.Amount,
                    Is_default = r.IsDefault
                });
        }
        catch
        {
            return Array.Empty<ProductPriceDto>().ToLookup(_ => Guid.Empty);
        }
    }

    internal static ProductDto MapToDto(RawProductDto raw) => MapToDto(raw, null);

    internal static ProductDto MapToDto(RawProductDto raw, ILookup<Guid, ProductPriceDto>? prices)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var fulfillmentTargets = new List<string>();

        if (!string.IsNullOrWhiteSpace(raw.FulfillmentTargets))
        {
            try
            {
                fulfillmentTargets = JsonSerializer.Deserialize<List<string>>(raw.FulfillmentTargets, jsonOptions) ?? new List<string>();
            }
            catch
            {
                fulfillmentTargets = new List<string>();
            }
        }

        return new ProductDto
        {
            Id = raw.Id.ToString(),
            Slug = raw.Slug,
            Name = raw.Name,
            Price = (double)raw.Price,
            Pricing_model = raw.PricingModel,
            Minimum_price = (double)raw.MinimumPrice,
            Currency = raw.Currency,
            Interval = raw.Interval,
            Is_active = raw.IsActive,
            Gateway_name = raw.GatewayName,
            Supports_off_session = Modules.Payments.Contracts.PaymentGatewayCapabilities.SupportsOffSession(raw.GatewayName),
            Checkout_configuration = new CheckoutConfigurationDto
            {
                Requires_address = raw.RequiresAddress,
                Requires_phone = raw.RequiresPhone,
                Requires_tax_id = raw.RequiresTaxId
            },
            Fulfillment_targets = fulfillmentTargets,
            Sst_tax_type = string.IsNullOrWhiteSpace(raw.SstTaxType) ? "06" : raw.SstTaxType,
            Sst_rate_percent = (double)raw.SstRatePercent,
            Trial_days = raw.TrialDays,
            Prices = prices?[raw.Id].ToList() ?? new List<ProductPriceDto>()
        };
    }
}
