namespace LazuarApi.Commerce;

model CashFlowTrendDto {
  month: string;
  amount: float64;
}

model PaymentMethodDto {
  method: string;
  count: int32;
  total_amount: float64;
}

model CommerceStatsDto {
  mrr: float64;
  /** Annualized committed recurring (MRR × 12). Not cash. */
  arr?: float64;
  active_subscribers: int32;
  past_due_subscribers: int32;
  cancelled_subscribers: int32;
  net_new_last_30_days: int32;
  churn_rate_percentage: float64;
  average_revenue_per_user: float64;
  total_revenue_collected: float64;
  /** Lifetime SUM of commerce.DunningCampaigns.RecoveredRevenue (not monthly). */
  recovered_revenue: float64;
  /** Lifetime SUM of commerce.DunningCampaigns.SavedSubscriptions. */
  saved_subscriptions: int32;
  cash_flow_trend: CashFlowTrendDto[];
  payment_methods: PaymentMethodDto[];
}
