using Dapper;
using System.Data;
using Modules.Tenant.Contracts;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Tenant.Infrastructure;

public class TenantQueryService : ITenantQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TenantQueryService([FromKeyedServices("TenantSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<TenantSnapshotDto?> GetTenantByIdAsync(Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) 
        {
            connection.Open();
        }

        // Added double quotes around "Organizations" to preserve case-sensitivity
        const string sql = "SELECT \"Id\", \"Name\", \"Slug\", \"IsActive\" FROM tenant.\"Organizations\" WHERE \"Id\" = @Id LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<TenantSnapshotDto>(sql, new { Id = tenantId });
    }

    public async Task<TenantSnapshotDto?> GetTenantBySlugAsync(string slug)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) 
        {
            connection.Open();
        }

        // Added double quotes around "Organizations" to preserve case-sensitivity
        const string sql = "SELECT \"Id\", \"Name\", \"Slug\", \"IsActive\" FROM tenant.\"Organizations\" WHERE \"Slug\" = @Slug LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<TenantSnapshotDto>(sql, new { Slug = slug.ToLower().Trim() });
    }
}
