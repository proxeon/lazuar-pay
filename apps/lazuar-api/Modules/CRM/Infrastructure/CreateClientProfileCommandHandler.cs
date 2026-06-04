using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Modules.CRM.Contracts;

namespace Modules.CRM.Infrastructure;

public class CreateClientProfileCommandHandler : ICommandHandler<CreateClientProfileCommand, Guid>
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CreateClientProfileCommandHandler([FromKeyedServices("CrmSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> Handle(CreateClientProfileCommand request, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) 
        {
            connection.Open();
        }

        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var phoneNormalized = NormalizePhone(request.Phone);

        // 1. Look up existing profile by Email or Phone to ensure idempotency
        const string selectSql = @"
            SELECT ""Id"" FROM crm.""ClientProfiles"" 
            WHERE ""OrganizationId"" = @OrgId 
              AND (""Email"" = @Email OR ""Phone"" = @Phone) 
            LIMIT 1";

        var existingId = await connection.QuerySingleOrDefaultAsync<Guid?>(selectSql, new
        {
            OrgId = request.OrganizationId,
            Email = emailNormalized,
            Phone = phoneNormalized
        });

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        // 2. Insert new profile if not found
        var newId = Guid.CreateVersion7();
        const string insertSql = @"
            INSERT INTO crm.""ClientProfiles"" (""Id"", ""OrganizationId"", ""FullName"", ""Email"", ""Phone"", ""ConsentedToMarketing"")
            VALUES (@Id, @OrgId, @FullName, @Email, @Phone, true)";

        await connection.ExecuteAsync(insertSql, new
        {
            Id = newId,
            OrgId = request.OrganizationId,
            FullName = request.FullName.Trim(),
            Email = emailNormalized,
            Phone = phoneNormalized
        });

        return newId;
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        
        var normalized = phone
            .Replace("+", "")
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "");

        if (normalized.StartsWith('0'))
        {
            normalized = "60" + normalized[1..];
        }

        return normalized;
    }
}
