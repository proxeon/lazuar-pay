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

    private record RawGlobalTxDto(
        Guid Id, decimal Amount, string Currency, string Status, DateTime CreatedAt, 
        string CustomerName, string CustomerEmail, string PaymentMethod);

    public async Task<PaginatedResponse<TransactionLogDto>> GetTransactionsAsync(Guid organizationId, int page, int limit, string? status, string? paymentMethod, string? searchTerm = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;
        var searchPattern = string.IsNullOrWhiteSpace(searchTerm) ? null : $"%{searchTerm}%";

        var sql = @"
            SELECT COUNT(*)::int 
            FROM billing.""LedgerEntries"" le
            JOIN billing.""LedgerLines"" ll ON le.""Id"" = ll.""LedgerEntryId""
            LEFT JOIN crm.""ClientProfiles"" cp ON le.""ReferenceId"" = cp.""Id""::text
            WHERE le.""OrganizationId"" = @OrgId AND ll.""AccountType"" = 'ASSET_CASH'
            AND (@Status IS NULL OR (@Status = 'CONFIRMED' AND ll.""Amount"" > 0) OR (@Status = 'REFUNDED' AND ll.""Amount"" < 0))
            AND (@SearchTerm IS NULL OR cp.""FullName"" ILIKE @SearchTerm OR cp.""Email"" ILIKE @SearchTerm);

            SELECT 
                le.""Id"", 
                ABS(ll.""Amount"") as Amount, 
                ll.""Currency"", 
                CASE WHEN ll.""Amount"" > 0 THEN 'CONFIRMED' ELSE 'REFUNDED' END as Status, 
                le.""Timestamp"" as CreatedAt, 
                COALESCE(cp.""FullName"", 'Unknown') as CustomerName, 
                COALESCE(cp.""Email"", 'Unknown') as CustomerEmail,
                'GATEWAY' as PaymentMethod
            FROM billing.""LedgerEntries"" le
            JOIN billing.""LedgerLines"" ll ON le.""Id"" = ll.""LedgerEntryId""
            LEFT JOIN crm.""ClientProfiles"" cp ON le.""ReferenceId"" = cp.""Id""::text
            WHERE le.""OrganizationId"" = @OrgId AND ll.""AccountType"" = 'ASSET_CASH'
            AND (@Status IS NULL OR (@Status = 'CONFIRMED' AND ll.""Amount"" > 0) OR (@Status = 'REFUNDED' AND ll.""Amount"" < 0))
            AND (@SearchTerm IS NULL OR cp.""FullName"" ILIKE @SearchTerm OR cp.""Email"" ILIKE @SearchTerm)
            ORDER BY le.""Timestamp"" DESC
            LIMIT @Limit OFFSET @Offset;";

        using var multi = await connection.QueryMultipleAsync(sql, new { OrgId = organizationId, Limit = limit, Offset = offset, SearchTerm = searchPattern, Status = status });

        var totalCount = await multi.ReadFirstAsync<int>();
        var rawTx = (await multi.ReadAsync<RawGlobalTxDto>()).ToList();

        if (totalCount == 0) return new PaginatedResponse<TransactionLogDto>(Enumerable.Empty<TransactionLogDto>(), 0, page, limit);

        var dtos = rawTx.Select(t => new TransactionLogDto
        {
            Id = t.Id.ToString(),
            Amount = (double)t.Amount,
            Currency = t.Currency,
            Status = t.Status,
            Created_at = new DateTimeOffset(t.CreatedAt),
            Customer_name = t.CustomerName,
            Customer_email = t.CustomerEmail,
            Payment_method = t.PaymentMethod
        });

        return new PaginatedResponse<TransactionLogDto>(dtos, totalCount, page, limit);
    }

    private record RawCouponDto(
        Guid Id, string Code, string DiscountType, decimal Amount,
        int MaxUses, int UsedCount, int ReservedCount, decimal MinimumOriginalPrice, DateTime? ExpiresAt,
        string? ApplicableProductIds, bool IsActive);

    public async Task<IEnumerable<CouponDto>> GetCouponsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""Id"", ""Code"", ""DiscountType"", ""Amount"", ""MaxUses"", ""UsedCount"", ""ReservedCount"", ""MinimumOriginalPrice"", ""ExpiresAt"", ""ApplicableProductIds""::text, ""IsActive""
            FROM commerce.""Coupons""
            WHERE ""OrganizationId"" = @OrgId
            ORDER BY ""CreatedAt"" DESC";

        var rawCoupons = await connection.QueryAsync<RawCouponDto>(sql, new { OrgId = organizationId });

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };

        return rawCoupons.Select(c => 
        {
            var productIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(c.ApplicableProductIds))
            {
                try { productIds = JsonSerializer.Deserialize<List<string>>(c.ApplicableProductIds, jsonOptions) ?? new List<string>(); }
                catch { productIds = new List<string>(); }
            }

            return new CouponDto
            {
                Id = c.Id.ToString(),
                Code = c.Code,
                Discount_type = c.DiscountType,
                Amount = (double)c.Amount,
                Max_uses = c.MaxUses,
                Used_count = c.UsedCount,
                Reserved_count = c.ReservedCount,
                Minimum_original_price = (double)c.MinimumOriginalPrice,
                Expires_at = c.ExpiresAt.HasValue ? new DateTimeOffset(c.ExpiresAt.Value) : null,
                Applicable_product_ids = productIds,
                Is_active = c.IsActive
            };
        }).ToList();
    }

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
            Active_subscribers = activeSubs.Count,
            Past_due_subscribers = subs.Count(s => s.Status == "PAST_DUE"),
            Cancelled_subscribers = subs.Count(s => s.Status == "CANCELED"),
            Net_new_last_30_days = newActiveLast30 - cancelledLast30,
            Churn_rate_percentage = churnRate,
            Average_revenue_per_user = arpu,
            Total_revenue_collected = 0, 
            Cash_flow_trend = new List<CashFlowTrendDto>(),
            Payment_methods = new List<PaymentMethodDto>()
        };
    }
}
