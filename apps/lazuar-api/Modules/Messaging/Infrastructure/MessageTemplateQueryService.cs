using Dapper;
using System.Data;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Messaging.Contracts;

namespace Modules.Messaging.Infrastructure;

public class MessageTemplateQueryService : IMessageTemplateQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public MessageTemplateQueryService([FromKeyedServices("MessagingSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<MessageTemplateDto>> GetTemplatesAsync(IEnumerable<Guid> templateIds)
    {
        var ids = templateIds.Distinct().ToList();
        if (ids.Count == 0) return Enumerable.Empty<MessageTemplateDto>();

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"Name\", \"Subject\", \"Body\" FROM messaging.\"MessageTemplates\" WHERE \"Id\" = ANY(@Ids)";
        return await connection.QueryAsync<MessageTemplateDto>(sql, new { Ids = ids });
    }

    public async Task<MessageTemplateDto?> GetTemplateByNameAsync(Guid organizationId, string name)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""Id"", ""Name"", ""Subject"", ""Body"" 
            FROM messaging.""MessageTemplates"" 
            WHERE ""OrganizationId"" = @OrgId AND ""Name"" = @Name 
            LIMIT 1";
            
        return await connection.QuerySingleOrDefaultAsync<MessageTemplateDto>(sql, new { OrgId = organizationId, Name = name });
    }
}
