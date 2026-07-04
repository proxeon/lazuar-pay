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

model PaymentConfigDto {
  gateway_type: string;
  api_key?: string;
  merchant_id?: string;
  webhook_secret?: string;
  secret_key?: string;
  is_active: boolean;
  estimated_fee_percentage: float64;
  fixed_fee: float64;
  tax_rate: float64;
}

model SavePaymentConfigRequestDto {
  gateway_type: string;
  api_key?: string;
  collection_id?: string;
  webhook_secret?: string;
  secret_key?: string;
  is_active: boolean;
  estimated_fee_percentage?: float64;
  fixed_fee?: float64;
  tax_rate?: float64;
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

model MagicLinkRequestDto {
  email: string;
}

model CancelPortalRequest {
  subscription_id: string; // GUID
}

model BillingLinkResponseDto {
  url: string;
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
}

model ArrearsSummaryDto {
  product_name: string;
  amount: float64;
  currency: string;
  status: string;
}
