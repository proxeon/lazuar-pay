using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts;

namespace Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Commerce-owned implementation of cross-schema reads used by Billing document generation.
/// </summary>
public class CommerceDocumentLookup : ICommerceDocumentLookup
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommerceDocumentLookup(
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommerceCustomerDisplay?> GetCustomerByGatewayTransactionAsync(
        Guid organizationId,
        string referenceId,
        CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""CustomerName"", ""CustomerEmail""
            FROM commerce.""TransactionLogs""
            WHERE ""OrganizationId"" = @OrgId AND (""ExternalReference"" = @RefId OR ""Id""::text = @RefId)
            LIMIT 1";

        var result = await connection.QuerySingleOrDefaultAsync(sql, new { OrgId = organizationId, RefId = referenceId });

        if (result == null) return null;

        return new CommerceCustomerDisplay(
            (string)(result.CustomerName ?? "Customer"),
            (string)(result.CustomerEmail ?? ""));
    }

    public async Task<DraftCheckoutSessionDisplay?> GetDraftCheckoutSessionAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT c.""AdHocLineItems"", cp.""FullName"" AS CustomerName, cp.""Email"" AS CustomerEmail
            FROM commerce.""CheckoutSessions"" c
            LEFT JOIN crm.""ClientProfiles"" cp ON c.""ClientProfileId"" = cp.""Id""
            WHERE c.""Id"" = @SessionId AND c.""OrganizationId"" = @OrgId
            LIMIT 1";

        var sessionData = await connection.QuerySingleOrDefaultAsync(sql, new { SessionId = sessionId, OrgId = organizationId });

        if (sessionData == null) return null;

        return new DraftCheckoutSessionDisplay(
            CustomerName: (string)(sessionData.CustomerName ?? "Customer"),
            CustomerEmail: (string)(sessionData.CustomerEmail ?? ""),
            AdHocLineItemsJson: (string?)sessionData.AdHocLineItems);
    }
}
