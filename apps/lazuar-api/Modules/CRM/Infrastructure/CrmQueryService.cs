using Dapper;
using System.Data;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.CRM.Contracts;

namespace Modules.CRM.Infrastructure;

public class CrmQueryService : ICrmQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CrmQueryService([FromKeyedServices("CrmSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ClientProfileDto?> GetClientProfileAsync(Guid profileId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) 
        {
            connection.Open();
        }

        // We use Dapper for fast, read-only queries from the CRM schema
        const string sql = "SELECT \"Id\", \"FullName\", \"Email\", \"Phone\" FROM \"ClientProfiles\" WHERE \"Id\" = @Id LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<ClientProfileDto>(sql, new { Id = profileId });
    }
}
