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

    /// <summary>
    /// Resolves customer display for a Billing document. Prefers checkout-session or
    /// subscription CRM (TIN, company, address), then the transaction-log name/email.
    /// </summary>
    Task<CommerceCustomerDisplay?> GetCustomerForDocumentAsync(
        Guid organizationId,
        string referenceId,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>
    /// Subscription + product snapshot for Communications (failed-pay / portal mail).
    /// </summary>
    Task<CommerceSubscriptionCommsContext?> GetSubscriptionCommsContextAsync(
        Guid organizationId,
        Guid subscriptionId,
        CancellationToken ct = default);
}

public record CommerceCustomerDisplay(
    string Name,
    string Email,
    string? Tin = null,
    string? CompanyName = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? PostalCode = null,
    string? StateCode = null,
    string? CountryCode = null,
    string? IdType = null,
    string? IdValue = null);

public record CommerceSubscriptionCommsContext(
    Guid ClientProfileId,
    string Status,
    string? ProductName);

public record DraftCheckoutSessionDisplay(
    string CustomerName,
    string CustomerEmail,
    string? AdHocLineItemsJson,
    string? DocumentNumber = null);
