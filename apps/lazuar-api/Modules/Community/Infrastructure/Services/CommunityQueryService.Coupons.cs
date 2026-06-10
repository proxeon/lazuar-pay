using Dapper;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Community.Application.Queries;

namespace Modules.Community.Infrastructure.Services;

public partial class CommunityQueryService
{
    private record RawCouponDto(
        Guid Id, string Code, string DiscountType, decimal Amount, 
        int MaxUses, int UsedCount, DateTime? ExpiresAt);

    public async Task<IEnumerable<CouponDto>> GetCouponsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""Id"", ""Code"", ""DiscountType"", ""Amount"", ""MaxUses"", ""UsedCount"", ""ExpiresAt""
            FROM community.""Coupons""
            WHERE ""OrganizationId"" = @OrgId
            ORDER BY ""CreatedAt"" DESC";

        var rawCoupons = await connection.QueryAsync<RawCouponDto>(sql, new { OrgId = organizationId });

        return rawCoupons.Select(c => new CouponDto(
            c.Id.ToString(),
            c.Code,
            c.DiscountType,
            (double)c.Amount,
            c.MaxUses,
            c.UsedCount,
            c.ExpiresAt.HasValue ? new DateTimeOffset(c.ExpiresAt.Value) : null
        )).ToList();
    }
}
