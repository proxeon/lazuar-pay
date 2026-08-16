namespace Modules.Billing.Domain;

/// <summary>
/// Chart-of-accounts codes used on <see cref="Entities.LedgerLine.AccountType"/>.
/// Prefer these constants over magic strings in handlers, workers, and SQL.
/// </summary>
public static class AccountTypes
{
    public const string AssetCash = "ASSET_CASH";
    public const string AssetAccountsReceivable = "ASSET_ACCOUNTS_RECEIVABLE";

    public const string LiabilityTaxPayable = "LIABILITY_TAX_PAYABLE";
    public const string LiabilityDeferredRevenue = "LIABILITY_DEFERRED_REVENUE";
    public const string LiabilityAffiliatePayable = "LIABILITY_AFFILIATE_PAYABLE";

    public const string RevenueGross = "REVENUE_GROSS";
    public const string RevenueRecognized = "REVENUE_RECOGNIZED";
    public const string ContraRevenueRefunds = "CONTRA_REVENUE_REFUNDS";

    public const string ExpenseGatewayFee = "EXPENSE_GATEWAY_FEE";
    public const string ExpenseDiscount = "EXPENSE_DISCOUNT";
    public const string ExpenseCommission = "EXPENSE_COMMISSION";
    public const string ExpenseSoftwareSubscription = "EXPENSE_SOFTWARE_SUBSCRIPTION";
}

/// <summary>
/// B2C consolidation lifecycle on <c>LedgerEntries.ConsolidationStatus</c>.
/// Separate from LHDN validation status.
/// </summary>
public static class ConsolidationStatuses
{
    public const string Pending = "PENDING";
    public const string Consolidated = "CONSOLIDATED";
    public const string NotRequired = "NOT_REQUIRED";
    public const string Ignored = "IGNORED";
}

/// <summary>
/// LHDN / local receipt lifecycle values on <c>LedgerEntries.LhdnValidationStatus</c>.
/// </summary>
public static class LhdnValidationStatuses
{
    public const string B2cReceipt = "B2C_RECEIPT";
    public const string ConsolidatedPending = "CONSOLIDATED_PENDING";
    public const string Valid = "VALID";
    public const string Invalid = "INVALID";
    public const string Cancelled = "CANCELLED";
    public const string NeedsBuyerTin = "NEEDS_BUYER_TIN";
    public const string IgnoredNoRevenue = "IGNORED_NO_REVENUE";
}

/// <summary>
/// Ledger entry reference types used for idempotency keys.
/// </summary>
public static class LedgerReferenceTypes
{
    public const string GatewayPayment = "GATEWAY_PAYMENT";
    public const string GatewayRefund = "GATEWAY_REFUND";
    public const string ManualEnrollment = "MANUAL_ENROLLMENT";
    public const string SystemCreditTopup = "SYSTEM_CREDIT_TOPUP";
    public const string SystemCreditChargeback = "SYSTEM_CREDIT_CHARGEBACK";
    public const string SystemSaasFee = "SYSTEM_SAAS_FEE";
    public const string LhdnCancellation = "LHDN_CANCELLATION";
    public const string InvoiceIssued = "INVOICE_ISSUED";
    public const string ZeroAmountCheckout = "ZERO_AMOUNT_CHECKOUT";
    public const string CommissionAccrued = "COMMISSION_ACCRUED";
}
