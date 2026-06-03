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
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"FullName\", \"Email\", \"Phone\" FROM crm.\"ClientProfiles\" WHERE \"Id\" = @Id LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<ClientProfileDto>(sql, new { Id = profileId });
    }

    public async Task<IEnumerable<ClientProfileDto>> GetClientProfilesAsync(IEnumerable<Guid> profileIds)
    {
        var ids = profileIds.Distinct().ToList();
        if (ids.Count == 0) return Enumerable.Empty<ClientProfileDto>();

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"FullName\", \"Email\", \"Phone\" FROM crm.\"ClientProfiles\" WHERE \"Id\" = ANY(@Ids)";
        return await connection.QueryAsync<ClientProfileDto>(sql, new { Ids = ids });
    }

    public async Task<ClientProfileDto?> GetClientProfileByEmailAsync(Guid organizationId, string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""Id"", ""FullName"", ""Email"", ""Phone"" 
            FROM crm.""ClientProfiles"" 
            WHERE ""OrganizationId"" = @OrgId AND ""Email"" = @Email 
            LIMIT 1";
            
        return await connection.QuerySingleOrDefaultAsync<ClientProfileDto>(sql, new 
        { 
            OrgId = organizationId, 
            Email = email.Trim().ToLowerInvariant() 
        });
    }
}
