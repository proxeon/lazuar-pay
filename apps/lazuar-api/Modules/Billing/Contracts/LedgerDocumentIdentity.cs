using System;

namespace Modules.Billing.Contracts;

public sealed record LedgerDocumentIdentity(
    Guid Id,
    string ReferenceType,
    string ReferenceId,
    string? CustomerDocumentNumber,
    string? LhdnDocumentUuid,
    string? TaxInvoiceId,
    string CustomerType,
    string? LhdnValidationStatus,
    decimal Amount,
    string Currency,
    DateTime Timestamp);
