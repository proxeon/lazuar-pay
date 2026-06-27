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

    private record RawPortalSubDto(Guid Id, Guid ProductId, string ProductName, string Status, DateTime? CurrentPeriodEnd);
    private record RawPortalOrderDto(Guid Id, Guid ProductId, string ProductName, string Status, DateTime CreatedAt);

    public async Task<AggregatedPortalDataResponse?> GetPortalDataAsync(Guid organizationId, Guid referenceSubscriptionId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        // 1. Resolve the ClientProfileId securely from the reference subscription (decoded from Magic Link)
        const string clientProfileSql = @"
            SELECT ""ClientProfileId"" FROM commerce.""Subscriptions"" 
            WHERE ""Id"" = @SubId AND ""OrganizationId"" = @OrgId LIMIT 1";

        var clientProfileId = await connection.QuerySingleOrDefaultAsync<Guid?>(clientProfileSql, new { SubId = referenceSubscriptionId, OrgId = organizationId });

        if (clientProfileId == null) return null;

        // 2. Fetch all Subscriptions for this client profile
        const string subsSql = @"
            SELECT s.""Id"", s.""ProductId"", p.""Name"" as ProductName, s.""Status"", s.""CurrentPeriodEnd""
            FROM commerce.""Subscriptions"" s
            JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
            WHERE s.""ClientProfileId"" = @ProfileId AND s.""OrganizationId"" = @OrgId AND s.""Status"" != 'PENDING'
            ORDER BY s.""CreatedAt"" DESC";

        var subs = await connection.QueryAsync<RawPortalSubDto>(subsSql, new { ProfileId = clientProfileId.Value, OrgId = organizationId });

        // 3. Fetch all Orders for this client profile
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

    private record RawReminderScheduleDto(
        Guid Id, Guid? ProductId, string? ProductName, Guid TemplateId,
        string Channel, int DaysRelativeToDue, string TimeOfDay, bool IsEnabled, DateTime CreatedAt);

    public async Task<IEnumerable<ReminderScheduleDto>> GetReminderSchedulesAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT
                r.""Id"", r.""ProductId"", p.""Name"" as ProductName, r.""TemplateId"",
                r.""Channel"", r.""DaysRelativeToDue"", r.""TimeOfDay"", r.""IsEnabled"", r.""CreatedAt""
            FROM commerce.""ReminderSchedules"" r
            LEFT JOIN commerce.""Products"" p ON r.""ProductId"" = p.""Id""
            WHERE r.""OrganizationId"" = @OrgId
            ORDER BY r.""DaysRelativeToDue"", r.""TimeOfDay""";

        var rawSchedules = await connection.QueryAsync<RawReminderScheduleDto>(sql, new { OrgId = organizationId });

        // Phase 1 Dunning: We retrieve the template name manually or via a separate cross-module query. 
        // For simplicity in the generic catalog, we will return "Assigned Template" unless we hydrate it via the Communications module.
        // The frontend uses the ID to manage it.
        return rawSchedules.Select(r => new ReminderScheduleDto
        {
            Id = r.Id.ToString(),
            Product_id = r.ProductId?.ToString(),
            Product_name = r.ProductName,
            Template_id = r.TemplateId.ToString(),
            Template_name = "Assigned Template", // Decoupled from Communications schema
            Channel = r.Channel,
            Days_relative_to_due = r.DaysRelativeToDue,
            Time_of_day = r.TimeOfDay,
            Is_enabled = r.IsEnabled,
            Created_at = new DateTimeOffset(r.CreatedAt)
        }).ToList();
    }
}
