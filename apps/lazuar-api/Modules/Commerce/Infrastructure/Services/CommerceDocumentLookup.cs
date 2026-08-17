using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.EntityFrameworkCore;
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
    private readonly CommerceDbContext _dbContext;

    public CommerceDocumentLookup(
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        ICrmQueryService crmQueryService,
        CommerceDbContext dbContext)
    {
        _connectionFactory = connectionFactory;
        _crmQueryService = crmQueryService;
        _dbContext = dbContext;
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
            (string)(result.CustomerEmail ?? ""),
            null,
            null);
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
            SELECT c.""AdHocLineItems"", c.""ClientProfileId"", c.""DocumentNumber""
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
            AdHocLineItemsJson: (string?)sessionData.AdHocLineItems,
            DocumentNumber: (string?)sessionData.DocumentNumber);
    }

    public async Task<CommerceCustomerDisplay?> GetCustomerForDocumentAsync(
        Guid organizationId,
        string referenceId,
        string? correlationId,
        CancellationToken ct = default)
    {
        var fromLog = await FindCustomerOnTransactionLogAsync(organizationId, referenceId, ct);

        foreach (var candidate in DistinctGuidCandidates(correlationId, referenceId))
        {
            var fromSession = await FindCustomerOnCheckoutSessionAsync(organizationId, candidate, ct);
            if (fromSession != null && !string.IsNullOrWhiteSpace(fromSession.Email))
            {
                return fromSession;
            }

            var fromSubscription = await FindCustomerOnSubscriptionAsync(organizationId, candidate, ct);
            if (fromSubscription != null && !string.IsNullOrWhiteSpace(fromSubscription.Email))
            {
                return fromSubscription;
            }
        }

        return fromLog;
    }

    public async Task<CommerceSubscriptionCommsContext?> GetSubscriptionCommsContextAsync(
        Guid organizationId,
        Guid subscriptionId,
        CancellationToken ct = default)
    {
        var sub = await _dbContext.Subscriptions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == organizationId, ct);

        if (sub == null) return null;

        var product = await _dbContext.Products
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);

        return new CommerceSubscriptionCommsContext(sub.ClientProfileId, sub.Status, product?.Name);
    }

    private async Task<CommerceCustomerDisplay?> FindCustomerOnTransactionLogAsync(
        Guid organizationId,
        string referenceId,
        CancellationToken ct)
    {
        var log = await _dbContext.TransactionLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.OrganizationId == organizationId
                     && (t.ExternalReference == referenceId || t.Id.ToString() == referenceId),
                ct);

        if (log == null) return null;

        return new CommerceCustomerDisplay(
            string.IsNullOrWhiteSpace(log.CustomerName) ? "Customer" : log.CustomerName,
            log.CustomerEmail ?? "",
            null,
            null);
    }

    private async Task<CommerceCustomerDisplay?> FindCustomerOnCheckoutSessionAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken ct)
    {
        var session = await _dbContext.CheckoutSessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.OrganizationId == organizationId, ct);

        if (session == null) return null;

        return await FromCrmAsync(session.ClientProfileId);
    }

    private async Task<CommerceCustomerDisplay?> FindCustomerOnSubscriptionAsync(
        Guid organizationId,
        Guid subscriptionId,
        CancellationToken ct)
    {
        var sub = await _dbContext.Subscriptions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == organizationId, ct);

        if (sub == null) return null;

        return await FromCrmAsync(sub.ClientProfileId);
    }

    private async Task<CommerceCustomerDisplay?> FromCrmAsync(Guid clientProfileId)
    {
        if (clientProfileId == Guid.Empty) return null;

        var profile = await _crmQueryService.GetClientProfileAsync(clientProfileId);
        if (profile == null) return null;

        return new CommerceCustomerDisplay(
            string.IsNullOrWhiteSpace(profile.Full_name) ? "Customer" : profile.Full_name,
            profile.Email ?? "",
            profile.Tin,
            profile.Company_name,
            profile.Billing_address?.Line1,
            profile.Billing_address?.Line2,
            profile.Billing_address?.City,
            profile.Billing_address?.Postal_code,
            profile.Billing_address?.State_code,
            profile.Billing_address?.Country_code,
            profile.Id_type,
            profile.Id_value);
    }

    private static IEnumerable<Guid> DistinctGuidCandidates(string? correlationId, string referenceId)
    {
        var seen = new HashSet<Guid>();
        if (Guid.TryParse(correlationId, out var correlation) && seen.Add(correlation))
        {
            yield return correlation;
        }

        if (Guid.TryParse(referenceId, out var reference) && seen.Add(reference))
        {
            yield return reference;
        }
    }
}
