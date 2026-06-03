using Dapper;
using System.Data;
using Modules.Tenant.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Modules.Tenant.Infrastructure;

public class TenantQueryService : ITenantQueryService
{
    private readonly DbContext _dbContext;

    public TenantQueryService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantSnapshotDto?> GetTenantByIdAsync(Guid tenantId)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();

        const string sql = "SELECT \"Id\", \"Name\", \"Slug\", \"IsActive\" FROM tenant.Organizations WHERE \"Id\" = @Id LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<TenantSnapshotDto>(sql, new { Id = tenantId });
    }

    public async Task<TenantSnapshotDto?> GetTenantBySlugAsync(string slug)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();

        const string sql = "SELECT \"Id\", \"Name\", \"Slug\", \"IsActive\" FROM tenant.Organizations WHERE \"Slug\" = @Slug LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<TenantSnapshotDto>(sql, new { Slug = slug.ToLower().Trim() });
    }
}
