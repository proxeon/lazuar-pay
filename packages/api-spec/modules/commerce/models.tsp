namespace LazuarApi.Commerce;

model CheckoutConfigurationDto {
  requires_address: boolean;
  requires_tax_id: boolean;
  requires_phone: boolean;
}

model ProductDto {
  id: string; // GUID
  slug: string;
  name: string;
  price: float64;
  pricing_model: string;
  minimum_price: float64;
  currency: string;
  interval: string;
  is_active: boolean;
  gateway_name: string;
  fulfillment_targets: string[];
  checkout_configuration: CheckoutConfigurationDto;
}

model CreateProductRequestDto {
  name: string;
  slug: string;
  price: float64;
  pricing_model: string;
  minimum_price: float64;
  currency: string;
  interval: string;
  gateway_name: string;
  requires_address: boolean;
  requires_tax_id: boolean;
  requires_phone: boolean;
  fulfillment_targets: string[];
}

model UpdateProductRequestDto {
  name: string;
  slug: string;
  price: float64;
  pricing_model: string;
  minimum_price: float64;
  currency: string;
  interval: string;
  is_active: boolean;
  gateway_name: string;
  requires_address: boolean;
  requires_tax_id: boolean;
  requires_phone: boolean;
  fulfillment_targets: string[];
}

model PublicCheckoutRequestDto {
  tenant_slug: string;
  product_slug: string;
  session_id?: string; // GUID
  name: string;
  email: string;
  phone?: string;
  tax_id?: string;
  company_name?: string;
  address_line1?: string;
  city?: string;
  postal_code?: string;
  state_code?: string;
  country_code?: string;
  quantity?: int32;
  is_guest_checkout?: boolean;
  coupon_code?: string;
}

model PortalSubscriptionDto {
  id: string; // GUID
  product_id: string; // GUID
  product_name: string;
  status: string;
  current_period_end?: utcDateTime;
}

model PortalOrderDto {
  id: string; // GUID
  product_id: string; // GUID
  product_name: string;
  status: string;
  created_at: utcDateTime;
}

model AggregatedPortalDataResponse {
  subscriptions: PortalSubscriptionDto[];
  orders: PortalOrderDto[];
}

model DunningStepDto {
  day_offset: int32;
  action_type: string;
  subject?: string;
  email_body?: string;
  whatsapp_body?: string;
}

model DunningCampaignDto {
  id: string; // GUID
  name: string;
  is_active: boolean;
  final_action: string;
  grace_period_days: int32;
  priority_order: int32;
  recovered_revenue: float64;
  saved_subscriptions: int32;
  churned_subscriptions: int32;
  target_product_ids: string[]; // GUID array
  target_payment_methods: string[];
  steps: DunningStepDto[];
  created_at: utcDateTime;
}

model CreateDunningCampaignRequestDto {
  name: string;
  final_action: string;
  grace_period_days: int32;
  priority_order?: int32 = 0;
  target_product_ids?: string[]; // GUID array
  target_payment_methods?: string[];
  steps: DunningStepDto[];
}

model UpdateDunningCampaignRequestDto {
  name: string;
  final_action: string;
  grace_period_days: int32;
  priority_order?: int32 = 0;
  target_product_ids?: string[]; // GUID array
  target_payment_methods?: string[];
  steps: DunningStepDto[];
  is_active?: boolean;
}

model PauseDunningRequestDto {
  pause_until: utcDateTime;
}

@summary("Ops offline payment against an existing subscriber.")
model RecordPaymentRequestDto {
  amount: float64;
  payment_method: string;
  reference_number?: string;
}

model GenerateCustomerPortalRequestDto {
  customer_email: string;
  return_url: string;
}

model GenerateCustomerPortalResponseDto {
  url: string;
}

model RecordRefundRequestDto {
  amount?: float64;
  gateway_name?: string;
  subscription_id?: string;
  tax_amount?: float64;
}

model PaymentConfigDto {
  gateway_type: string;
  /** @deprecated Never populated; use has_api_key + api_key_hint. */
  api_key?: string;
  merchant_id?: string;
  /** @deprecated Never populated; use has_webhook_secret + webhook_secret_hint. */
  webhook_secret?: string;
  /** @deprecated Never populated; use has_secret_key + secret_key_hint. */
  secret_key?: string;
  /** When false, credentials are retained but gateway is not used for new checkouts/charges. */
  is_active: boolean;
  /** True when an API key is stored (encrypted). Full key is never returned. */
  has_api_key: boolean;
  /** Masked hint, e.g. last 4 characters of the plaintext key. */
  api_key_hint?: string;
  /** True when a webhook secret is stored (encrypted). */
  has_webhook_secret: boolean;
  /** Masked hint for the webhook secret. */
  webhook_secret_hint?: string;
  /** True when a Stripe-style secret key is stored (same column as api_key). */
  has_secret_key: boolean;
  /** Masked hint for the secret key. */
  secret_key_hint?: string;
}

model SavePaymentConfigRequestDto {
  gateway_type: string;
  /** Omit or empty to keep existing encrypted key. */
  api_key?: string;
  collection_id?: string;
  /** Omit or empty to keep existing webhook secret. */
  webhook_secret?: string;
  /** Stripe secret key; omit or empty to keep existing. */
  secret_key?: string;
  /** Soft-disable without deleting credentials. Defaults to true on create. */
  is_active?: boolean;
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

model PaymentRecordDto {
  id: string; // GUID
  amount: float64;
  currency: string;
  payment_method: string;
  status: string;
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

model CouponDto {
  id: string; // GUID
  code: string;
  discount_type: string;
  amount: float64;
  max_uses: int32;
  used_count: int32;
  reserved_count: int32;
  minimum_original_price: float64;
  expires_at?: utcDateTime;
  applicable_product_ids?: string[]; // GUID array
  is_active: boolean;
}

model CreateCouponRequestDto {
  code: string;
  discount_type: string;
  amount: float64;
  max_uses: int32;
  minimum_original_price?: float64;
  expires_at?: utcDateTime;
  applicable_product_ids?: string[]; // GUID array
}

model UpdateCouponRequestDto {
  code?: string;
  discount_type?: string;
  amount?: float64;
  max_uses?: int32;
  minimum_original_price?: float64;
  expires_at?: utcDateTime;
  applicable_product_ids?: string[]; // GUID array
  is_active?: boolean;
}

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
  active_subscribers: int32;
  past_due_subscribers: int32;
  cancelled_subscribers: int32;
  net_new_last_30_days: int32;
  churn_rate_percentage: float64;
  average_revenue_per_user: float64;
  total_revenue_collected: float64;
  cash_flow_trend: CashFlowTrendDto[];
  payment_methods: PaymentMethodDto[];
}

model CancelPortalRequest {
  subscription_id: string; // GUID
}

model CheckoutResponse {
  url: string;
  is_zero_amount_bypass?: boolean;
}

model ValidateCouponResponseDto {
  is_valid: boolean;
  discount_amount: float64;
  final_price: float64;
  error_message?: string;
}

model CheckoutStatusResponse {
  status: string;
  /**
   * Deprecated: anonymous status poll no longer mints portal magic tokens.
   * Use email magic links or authenticated portal access instead.
   */
  token?: string;
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

model CustomLineItemDto {
  description: string;
  quantity: int32;
  unit_price: float64;
}

model CreateCustomCheckoutRequestDto {
  client_email: string;
  client_name: string;
  line_items: CustomLineItemDto[];
  expires_at?: utcDateTime;
  is_b2b_required: boolean;
  /** Preferred payment gateway (e.g. STRIPE, CHIP). Defaults to first configured tenant gateway. */
  gateway_name?: string;
}

model CustomCheckoutDto {
  id: string; // GUID
  client_profile_id: string; // GUID
  client_name?: string;
  client_email?: string;
  status: string;
  expires_at: utcDateTime;
  is_b2b_required: boolean;
  line_items: CustomLineItemDto[];
  total_amount: float64;
  created_at: utcDateTime;
  /** HMAC-signed public URL for draft proforma PDF (sig+exp). */
  draft_pdf_url?: string;
}

model ArrearsSummaryDto {
  product_name: string;
  amount: float64;
  currency: string;
  status: string;
}
