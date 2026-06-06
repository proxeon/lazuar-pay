using Dapper;
using System.Data;
using Modules.One.Contracts;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.One.Infrastructure.Services;

public class OneQueryService : IOneQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public OneQueryService([FromKeyedServices("OneSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<WorkspaceSnapshotDto?> GetWorkspaceByIdAsync(Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"Name\", \"Slug\", \"IsActive\" FROM one.\"Organizations\" WHERE \"Id\" = @Id LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<WorkspaceSnapshotDto>(sql, new { Id = tenantId });
    }

    public async Task<WorkspaceSnapshotDto?> GetWorkspaceBySlugAsync(string slug)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"Name\", \"Slug\", \"IsActive\" FROM one.\"Organizations\" WHERE \"Slug\" = @Slug LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<WorkspaceSnapshotDto>(sql, new { Slug = slug.ToLower().Trim() });
    }
}
