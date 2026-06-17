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

model CreditBalanceDto {
  available_credits: int32;
}
