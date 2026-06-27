using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Lazuar.ApiTypes;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application.Queries;

namespace Modules.Commerce.Infrastructure.Services;

public class CommerceQueryService : ICommerceQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommerceQueryService([FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private record RawProductDto(
        Guid Id, string Slug, string Name, decimal Price, string Currency, string Interval,
        bool RequiresAddress, bool RequiresTaxId, bool RequiresPhone,
        string? FulfillmentTargets, bool IsActive);

    public async Task<IEnumerable<ProductDto>> GetProductsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 
                ""Id"", ""Slug"", ""Name"", ""Price"", ""Currency"", ""Interval"",
                ""RequiresAddress"", ""RequiresTaxId"", ""RequiresPhone"",
                ""FulfillmentTargets""::text, ""IsActive""
            FROM commerce.""Products""
            WHERE ""OrganizationId"" = @OrgId
            ORDER BY ""CreatedAt"" DESC";

        var rawProducts = await connection.QueryAsync<RawProductDto>(sql, new { OrgId = organizationId });

        return rawProducts.Select(MapToDto);
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid organizationId, Guid productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 
                ""Id"", ""Slug"", ""Name"", ""Price"", ""Currency"", ""Interval"",
                ""RequiresAddress"", ""RequiresTaxId"", ""RequiresPhone"",
                ""FulfillmentTargets""::text, ""IsActive""
            FROM commerce.""Products""
            WHERE ""OrganizationId"" = @OrgId AND ""Id"" = @ProductId
            LIMIT 1";

        var rawProduct = await connection.QuerySingleOrDefaultAsync<RawProductDto>(sql, new { OrgId = organizationId, ProductId = productId });

        if (rawProduct == null) return null;

        return MapToDto(rawProduct);
    }

    private static ProductDto MapToDto(RawProductDto raw)
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
                // Graceful fallback for invalid JSON strings
                fulfillmentTargets = new List<string>();
            }
        }

        return new ProductDto
        {
            Id = raw.Id.ToString(),
            Slug = raw.Slug,
            Name = raw.Name,
            Price = (double)raw.Price,
            Currency = raw.Currency,
            Interval = raw.Interval,
            Is_active = raw.IsActive,
            Checkout_configuration = new CheckoutConfigurationDto
            {
                Requires_address = raw.RequiresAddress,
                Requires_phone = raw.RequiresPhone,
                Requires_tax_id = raw.RequiresTaxId
            },
            Fulfillment_targets = fulfillmentTargets
        };
    }
}
