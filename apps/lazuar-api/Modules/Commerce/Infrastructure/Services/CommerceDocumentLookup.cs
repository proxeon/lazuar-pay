using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Commerce-owned implementation of cross-schema reads used by Billing document generation.
/// Commerce SQL stays commerce-only; customer name/email come from <see cref="ICrmQueryService"/>.
/// </summary>
public class CommerceDocumentLookup : ICommerceDocumentLookup
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICrmQueryService _crmQueryService;

    public CommerceDocumentLookup(
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        ICrmQueryService crmQueryService)
    {
        _connectionFactory = connectionFactory;
        _crmQueryService = crmQueryService;
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

        // L-05: commerce-owned SQL only; CRM name/email via ICrmQueryService (no crm JOIN).
        const string sql = @"
            SELECT c.""AdHocLineItems"", c.""ClientProfileId""
            FROM commerce.""CheckoutSessions"" c
            WHERE c.""Id"" = @SessionId AND c.""OrganizationId"" = @OrgId
            LIMIT 1";

        var sessionData = await connection.QuerySingleOrDefaultAsync(sql, new { SessionId = sessionId, OrgId = organizationId });

        if (sessionData == null) return null;

        Guid clientProfileId = sessionData.ClientProfileId;
        var profile = clientProfileId != Guid.Empty
            ? await _crmQueryService.GetClientProfileAsync(clientProfileId)
            : null;

        // Former LEFT JOIN semantics: missing profile → defaults.
        return new DraftCheckoutSessionDisplay(
            CustomerName: profile?.Full_name ?? "Customer",
            CustomerEmail: profile?.Email ?? "",
            AdHocLineItemsJson: (string?)sessionData.AdHocLineItems);
    }
}
