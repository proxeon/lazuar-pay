// packages/api-spec/modules/billing/models.tsp
namespace LazuarApi.Billing;

model LedgerLineDto {
  id: string;
  ledger_entry_id: string;
  account_type: string;
  amount: float64;
  currency: string;
  base_currency_amount: float64;
  base_currency: string;
}

model LedgerEntryDto {
  id: string;
  timestamp: utcDateTime;
  reference_type: string;
  reference_id: string;
  description?: string;
  customer_type: string;
  tax_invoice_id?: string;
  lhdn_validation_status?: string;
  lines: LedgerLineDto[];
}

model FinancialSummaryDto {
  gross_revenue: float64;
  total_gateway_fees: float64;
  total_tax_liabilities: float64;
  net_revenue: float64;
  deferred_revenue: float64;
  recognized_revenue: float64;
  currency: string;
}

model NetProfitDto {
  period: string;
  gross_revenue: float64;
  gateway_fees: float64;
  refunds_issued: float64;
  net_profit: float64;
  currency: string;
}

model CreditTransactionDto {
  amount: int32;
  reference: string;
  created_at: utcDateTime;
}

model CreditBalanceDto {
  available_credits: int32;
  recent_transactions: CreditTransactionDto[];
}

model CreditPackageDto {
  amount_myr: float64;
  credits: int32;
}

model CreateTopUpRequestDto {
  amount_myr: float64;
  return_url: string;
}

model TopUpResponseDto {
  checkout_url: string;
}

model TenantBillingAddressDto {
  line1: string;
  line2?: string;
  line3?: string;
  city: string;
  postal_code: string;
  state_code: string;
  country_code: string;
}

model TenantBillingProfileDto {
  legal_name: string;
  tin: string;
  registration_number?: string;
  sst_registration_number?: string;
  logo_url?: string;
  address?: TenantBillingAddressDto;
}

model UpdateTenantBillingProfileRequestDto {
  legal_name: string;
  tin: string;
  registration_number?: string;
  sst_registration_number?: string;
  logo_url?: string;
  address?: TenantBillingAddressDto;
}

model DocumentDownloadUrlDto {
  url: string;
}
