using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Queries.Agent;

namespace Modules.Lhdn.Infrastructure.Services;

public class LhdnQueryService : ILhdnQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public LhdnQueryService([FromKeyedServices("LhdnSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<AgentLhdnSubmissionResult>> GetRecentSubmissionsAsync(Guid organizationId, int limit, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        var safeLimit = limit > 100 ? 100 : limit;

        const string sql = @"
            SELECT 
                ""Id"" as DocumentId, 
                ""InternalReferenceId"" as InternalReference, 
                ""ValidationStatus"" as Status, 
                ""LhdnUuid"", 
                ""LongId"",
                ""ErrorMessage"", 
                ""CreatedAt""
            FROM lhdn.""TaxDocuments""
            WHERE ""OrganizationId"" = @OrgId
            ORDER BY ""CreatedAt"" DESC
            LIMIT @Limit";

        var results = await connection.QueryAsync<dynamic>(sql, new { OrgId = organizationId, Limit = safeLimit });

        return results.Select(r => new AgentLhdnSubmissionResult(
            r.DocumentId.ToString(),
            r.InternalReference,
            r.Status,
            r.LhdnUuid,
            r.LongId,
            r.ErrorMessage,
            ((DateTime)r.CreatedAt).ToString("yyyy-MM-dd HH:mm:ss")
        )).ToList();
    }
}
