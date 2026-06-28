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

model ReminderScheduleDto {
  id: string; // GUID
  product_id?: string; // GUID
  product_name?: string;
  template_id: string; // GUID
  template_name: string;
  channel: string;
  days_relative_to_due: int32;
  time_of_day: string;
  is_enabled: boolean;
  created_at: utcDateTime;
}

model CreateReminderScheduleRequestDto {
  product_id?: string;
  template_id: string;
  channel: string;
  days_relative_to_due: int32;
  time_of_day: string;
  is_enabled: boolean;
}

model UpdateReminderScheduleRequestDto {
  product_id?: string;
  template_id?: string;
  channel?: string;
  days_relative_to_due?: int32;
  time_of_day?: string;
  is_enabled?: boolean;
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
