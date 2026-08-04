using System;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Commerce.Contracts;

/// <summary>
/// Cross-module read port for Billing document generation.
/// Keeps commerce/crm SQL out of the Billing module (no cross-schema Dapper from Billing).
/// </summary>
public interface ICommerceDocumentLookup
{
    /// <summary>
    /// Resolves customer display fields for a final receipt/tax invoice from commerce transaction logs
    /// (matched by gateway external reference or transaction id text).
    /// </summary>
    Task<CommerceCustomerDisplay?> GetCustomerByGatewayTransactionAsync(
        Guid organizationId,
        string referenceId,
        CancellationToken ct = default);

    /// <summary>
    /// Loads draft checkout session line items + customer for proforma PDF generation.
    /// </summary>
    Task<DraftCheckoutSessionDisplay?> GetDraftCheckoutSessionAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken ct = default);
}

public record CommerceCustomerDisplay(string Name, string Email);

public record DraftCheckoutSessionDisplay(
    string CustomerName,
    string CustomerEmail,
    string? AdHocLineItemsJson);
