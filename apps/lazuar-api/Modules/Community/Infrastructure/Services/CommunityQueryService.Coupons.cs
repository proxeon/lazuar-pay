using Dapper;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Community.Application.Queries;

namespace Modules.Community.Infrastructure.Services;

public partial class CommunityQueryService
{
    private record RawCouponDto(
        Guid Id, string Code, string DiscountType, decimal Amount,
        int MaxUses, int UsedCount, int ReservedCount, decimal MinimumOriginalPrice, DateTime? ExpiresAt,
        string? ApplicablePlanIds);

    public async Task<IEnumerable<CouponDto>> GetCouponsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""Id"", ""Code"", ""DiscountType"", ""Amount"", ""MaxUses"", ""UsedCount"", ""ReservedCount"", ""MinimumOriginalPrice"", ""ExpiresAt"", ""ApplicablePlanIds""::text
            FROM community.""Coupons""
            WHERE ""OrganizationId"" = @OrgId
            ORDER BY ""CreatedAt"" DESC";

        var rawCoupons = await connection.QueryAsync<RawCouponDto>(sql, new { OrgId = organizationId });

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        return rawCoupons.Select(c => 
        {
            var planIds = string.IsNullOrWhiteSpace(c.ApplicablePlanIds) 
                ? new List<string>() 
                : JsonSerializer.Deserialize<List<string>>(c.ApplicablePlanIds, jsonOptions) ?? new List<string>();

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
                Applicable_plan_ids = planIds
            };
        }).ToList();
    }
}
