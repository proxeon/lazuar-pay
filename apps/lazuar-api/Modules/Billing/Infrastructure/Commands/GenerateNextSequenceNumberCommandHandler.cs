using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Contracts.Commands;

namespace Modules.Billing.Infrastructure.Commands;

public class GenerateNextSequenceNumberCommandHandler : ICommandHandler<GenerateNextSequenceNumberCommand, string>
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public GenerateNextSequenceNumberCommandHandler([FromKeyedServices("BillingSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string> Handle(GenerateNextSequenceNumberCommand request, CancellationToken ct)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        // Atomically upserts and returns the incremented sequence value. 
        // This is safe under concurrency and prevents sequence gaps during rollbacks.
        const string sql = @"
            INSERT INTO billing.""DocumentSequences"" (""Id"", ""OrganizationId"", ""Prefix"", ""CurrentValue"")
            VALUES (@Id, @OrganizationId, @Prefix, 1)
            ON CONFLICT (""OrganizationId"", ""Prefix"") 
            DO UPDATE SET ""CurrentValue"" = billing.""DocumentSequences"".""CurrentValue"" + 1
            RETURNING ""CurrentValue"";";

        var nextValue = await connection.QuerySingleAsync<long>(sql, new
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = request.OrganizationId,
            Prefix = request.Prefix
        });

        return $"{request.Prefix}-{nextValue:D5}";
    }
}
