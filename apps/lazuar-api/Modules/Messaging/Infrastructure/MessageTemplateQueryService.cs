using Dapper;
using System.Data;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Messaging.Contracts;
using Lazuar.ApiTypes;

namespace Modules.Messaging.Infrastructure;

public class MessageTemplateQueryService : IMessageTemplateQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private record RawMessageTemplate(Guid Id, string Name, string Subject, string Body, bool IsDefault, DateTime UpdatedAt);

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

        const string sql = "SELECT \"Id\", \"Name\", \"Subject\", \"Body\", \"IsDefault\", \"UpdatedAt\" FROM messaging.\"MessageTemplates\" WHERE \"Id\" = ANY(@Ids)";
        var rawTemplates = await connection.QueryAsync<RawMessageTemplate>(sql, new { Ids = ids });
        
        return rawTemplates.Select(MapToDto);
    }

    public async Task<MessageTemplateDto?> GetTemplateByNameAsync(Guid organizationId, string name)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""Id"", ""Name"", ""Subject"", ""Body"", ""IsDefault"", ""UpdatedAt""
            FROM messaging.""MessageTemplates""
            WHERE ""OrganizationId"" = @OrgId AND ""Name"" = @Name
            LIMIT 1";
            
        var rawTemplate = await connection.QuerySingleOrDefaultAsync<RawMessageTemplate>(sql, new { OrgId = organizationId, Name = name });
        return rawTemplate != null ? MapToDto(rawTemplate) : null;
    }

    public async Task<IEnumerable<MessageTemplateDto>> GetAllTemplatesAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""Id"", ""Name"", ""Subject"", ""Body"", ""IsDefault"", ""UpdatedAt""
            FROM messaging.""MessageTemplates""
            WHERE ""OrganizationId"" = @OrgId
            ORDER BY ""Name""";
            
        var rawTemplates = await connection.QueryAsync<RawMessageTemplate>(sql, new { OrgId = organizationId });
        return rawTemplates.Select(MapToDto);
    }

    private static MessageTemplateDto MapToDto(RawMessageTemplate raw)
    {
        return new MessageTemplateDto
        {
            Id = raw.Id.ToString(),
            Name = raw.Name,
            Subject = raw.Subject,
            Body = raw.Body,
            Is_default = raw.IsDefault,
            Updated_at = new DateTimeOffset(raw.UpdatedAt)
        };
    }
}
