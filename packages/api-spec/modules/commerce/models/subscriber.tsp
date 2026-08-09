namespace LazuarApi.Commerce;

@summary("Ops offline payment against an existing subscriber.")
model RecordPaymentRequestDto {
  amount: float64;
  payment_method: string;
  reference_number?: string;
}

model RecordRefundRequestDto {
  amount?: float64;
  gateway_name?: string;
  subscription_id?: string;
  tax_amount?: float64;
}

model CommerceSubscriptionDto {
  id: string; // GUID
  client_profile_id: string; // GUID
  customer_name: string;
  customer_email: string;
  customer_phone: string;
  product_id: string; // GUID
  product_name: string;
  product_price: float64;
  status: string;
  current_period_end?: utcDateTime;
  next_billing_date?: utcDateTime;
  days_overdue?: int32;
  vaulted_customer_id?: string;
  vaulted_token_id?: string;
  dunning_campaign_name?: string;
  current_dunning_step?: int32;
  dunning_paused_until?: utcDateTime;
  created_at: utcDateTime;
}

model TransactionLogDto {
  id: string; // GUID
  amount: float64;
  fee_amount: float64;
  net_amount: float64;
  currency: string;
  status: string;
  created_at: utcDateTime;
  customer_name: string;
  customer_email: string;
  product_name?: string;
  recorded_by_name: string;
  external_reference?: string;
}

@summary("Input details required to manually enroll a subscriber via admin dashboard.")
model CreateManualSubscriberDto {
  name: string;
  email: string;
  phone: string;
  product_id: string; // GUID
  payment_method: string;
  amount_paid: float64;
  reference_number?: string;
  send_welcome_email?: boolean = true;
  start_date?: utcDateTime;
  next_billing_date?: utcDateTime;
}
