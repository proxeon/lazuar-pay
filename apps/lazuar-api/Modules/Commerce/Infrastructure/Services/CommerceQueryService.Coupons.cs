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
                DiscountType = c.DiscountType,
                Amount = (double)c.Amount,
                MaxUses = c.MaxUses,
                UsedCount = c.UsedCount,
                ReservedCount = c.ReservedCount,
                MinimumOriginalPrice = (double)c.MinimumOriginalPrice,
                ExpiresAt = c.ExpiresAt.HasValue ? new DateTimeOffset(c.ExpiresAt.Value) : null,
                ApplicableProductIds = productIds,
                IsActive = c.IsActive
            };
        }).ToList();
    }
}
