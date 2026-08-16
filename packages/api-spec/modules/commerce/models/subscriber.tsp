namespace LazuarApi.Commerce;

@summary("Ops offline payment against an existing subscriber.")
model RecordPaymentRequestDto {
  amount: float64;
  payment_method: string;
  reference_number?: string;
  next_billing_date?: utcDateTime;
}

model RecordRefundRequestDto {
  amount?: float64;
  gateway_name?: string;
  subscription_id?: string;
  tax_amount?: float64;
  mark_refunded?: boolean;
  reason?: string;
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
  is_reminder_only: boolean;
  @doc("Hosted pay-this-cycle checkout when minted for the current due date; null when cleared.")
  current_renewal_checkout_url?: string;
  cancel_at_period_end: boolean;
  dunning_campaign_name?: string;
  current_dunning_step?: int32;
  dunning_paused_until?: utcDateTime;
  created_at: utcDateTime;
  quantity?: int32;
  pending_quantity?: int32;
  pending_product_id?: string;
  pending_product_name?: string;
  unit_amount?: float64;
  trial_ends_at?: utcDateTime;
  collection_paused_until?: utcDateTime;
}

model ChangePlanRequestDto {
  product_id?: string;
  prorate?: boolean;
  apply?: string;
}

model SetQuantityRequestDto {
  quantity: int32;
  prorate?: boolean;
  apply?: string;
}

model PauseCollectionRequestDto {
  resume_on: utcDateTime;
}

model PlanChangePreviewDto {
  current_product_id: string;
  current_amount: float64;
  currency: string;
  interval: string;
  next_product_id: string;
  next_amount: float64;
  effective_at?: utcDateTime;
  amount_due_now: float64;
  policy: string;
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
  gateway_name?: string;
  refunded_amount: float64;
  remaining_amount: float64;
  supports_api_refund: boolean;
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

@summary("Ops cancel. Omitted / empty body is immediate. Period-end only when at_period_end is true and a paid period remains.")
model CancelSubscriberRequest {
  at_period_end?: boolean = false;
}
