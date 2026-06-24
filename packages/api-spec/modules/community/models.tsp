namespace LazuarApi.Community;

model FaqItemDto {
  id: string; // GUID
  question: string;
  answer: string;
}

model CommunityPlanDto {
  id: string; // GUID
  slug: string;
  name: string;
  audience: string;
  short_description: string;
  long_description: string;
  price: float64;
  interval: string;
  features: string[];
  methodology: string;
  faq: FaqItemDto[];
  is_active: boolean;
  display_order: int32;
  max_capacity?: int32;
  grace_period_days: int32;
  telegram_invite_link?: string;
  weekly_meeting_link?: string;
  enrolled_count: int32;
  spots_remaining?: int32;
  is_full: boolean;
}

model CommunitySubscriptionDto {
  id: string; // GUID
  client_profile_id: string; // GUID
  customer_name: string;
  customer_email: string;
  customer_phone: string;
  plan_id: string; // GUID
  plan_name: string;
  plan_price: float64;
  status: string;
  source: string;
  is_reminder_only: boolean;
  preferred_channel?: string;
  admin_notes?: string;
  reminders_paused_until?: utcDateTime;
  current_period_end?: utcDateTime;
  next_billing_date?: utcDateTime;
  days_overdue?: int32;
  vaulted_customer_id?: string;
  vaulted_token_id?: string;
  created_at: utcDateTime;
}

model PaymentRecordDto {
  id: string; // GUID
  system_reference: string;
  amount: float64;
  currency: string;
  payment_method: string;
  reference_number?: string;
  receipt_url?: string;
  recorded_by?: string;
  period_start: utcDateTime;
  period_end: utcDateTime;
  status: string;
  notes?: string;
  created_at: utcDateTime;
}

model TransactionLogDto {
  id: string; // GUID
  system_reference: string;
  amount: float64;
  currency: string;
  payment_method: string;
  status: string;
  created_at: utcDateTime;
  customer_name: string;
  customer_email: string;
  recorded_by_name: string;
  external_reference?: string;
}

model DeliveryHistoryItemDto {
  id: string; // GUID
  channel: string;
  recipient: string;
  template_name?: string;
  subject?: string;
  status: string;
  error_message?: string;
  created_at: utcDateTime;
}

model CommunityReminderScheduleDto {
  id: string; // GUID
  plan_id?: string; // GUID
  plan_name?: string;
  template_id: string; // GUID
  template_name: string;
  channel: string;
  days_relative_to_due: int32;
  time_of_day: string;
  is_enabled: boolean;
  created_at: utcDateTime;
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

model CommunitySubscriberStatsDto {
  mrr: float64;
  active_subscribers: int32;
  past_due_subscribers: int32;
  cancelled_subscribers: int32;
  net_new_last_30_days: int32;
  churn_rate_percentage: float64;
  average_revenue_per_user: float64;
  reminder_effectiveness_percentage: float64;
  total_revenue_collected: float64;
  cash_flow_trend: CashFlowTrendDto[];
  payment_methods: PaymentMethodDto[];
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

model CreatePlanRequestDto {
  slug: string;
  name: string;
  audience: string;
  short_description: string;
  long_description: string;
  price: float64;
  interval: string;
  features: string[];
  methodology: string;
  faq: FaqItemDto[];
  display_order: int32;
  max_capacity?: int32;
  grace_period_days: int32;
  telegram_invite_link?: string;
  weekly_meeting_link?: string;
}

model UpdatePlanRequestDto {
  slug?: string;
  name?: string;
  audience?: string;
  short_description?: string;
  long_description?: string;
  price?: float64;
  interval?: string;
  features?: string[];
  methodology?: string;
  faq?: FaqItemDto[];
  is_active?: boolean;
  display_order?: int32;
  max_capacity?: int32;
  grace_period_days?: int32;
  telegram_invite_link?: string;
  weekly_meeting_link?: string;
}

model CreateSubscriberRequestDto {
  name: string;
  email: string;
  phone: string;
  plan_id: string; // GUID
  source?: string;
  is_reminder_only?: boolean;
  preferred_channel?: string;
  amount_paid?: float64;
  payment_method?: string;
  reference_number?: string;
  notes?: string;
  start_date?: utcDateTime;
  next_billing_date?: utcDateTime;
  send_welcome_email?: boolean;
}

model UpdateSubscriberProfileRequestDto {
  is_reminder_only: boolean;
  preferred_channel?: string;
  admin_notes?: string;
  next_renewal_date?: utcDateTime;
}

model RecordPaymentRequestDto {
  amount: float64;
  payment_method: string;
  reference_number?: string;
  receipt_file?: string;
}

model ExtendGraceRequestDto {
  days: int32;
}

model PauseRemindersRequestDto {
  pause_until?: utcDateTime;
}

model SendOneOffReminderRequestDto {
  template_id?: string; // GUID
  custom_message?: string;
  channel?: string;
}

model ScheduleOneOffRequestDto {
  subscriber_id: string; // GUID
  template_id?: string; // GUID
  custom_message?: string;
  channel?: string;
  scheduled_at: utcDateTime;
}

model CreateReminderScheduleRequestDto {
  plan_id?: string; // GUID
  template_id: string; // GUID
  channel: string;
  days_relative_to_due: int32;
  time_of_day: string;
  is_enabled: boolean;
}

model UpdateReminderScheduleRequestDto {
  plan_id?: string; // GUID
  template_id?: string; // GUID
  channel?: string;
  days_relative_to_due?: int32;
  time_of_day?: string;
  is_enabled?: boolean;
}

model PublicCheckoutRequestDto {
  tenant_slug: string;
  plan_slug: string;
  name: string;
  email: string;
  phone: string;
  is_guest_checkout?: boolean;
  coupon_code?: string;
}

model MagicLinkRequestDto {
  email: string;
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

model PortalDataResponse {
  subscription: CommunitySubscriptionDto;
}

model CancelPortalRequest {
  subscription_id: string; // GUID
}

model MessageTemplateDto {
  id: string; // GUID
  name: string;
  channel: string;
  subject: string;
  body: string;
  is_default: boolean;
  required_variables: string[];
  optional_variables: string[];
  updated_at: utcDateTime;
}

model CreateTemplateRequestDto {
  name: string;
  subject: string;
  body: string;
  channel: string;
  required_variables: string[];
  optional_variables: string[];
}

model UpdateTemplateRequestDto {
  subject: string;
  body: string;
}

model TestReminderRequestDto {
  template_name: string;
  channel?: string;
}

model TestReminderResponse {
  success: boolean;
  sent_to: string;
}

model ChangePlanRequestDto {
  new_plan_id: string; // GUID
}

model RefundRequestDto {
  payment_record_id: string; // GUID
  reason?: string;
}

model BillingLinkResponseDto {
  url: string;
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
  applicable_plan_ids?: string[]; // GUID array
  is_active: boolean;
}

model CreateCouponRequestDto {
  code: string;
  discount_type: string;
  amount: float64;
  max_uses: int32;
  minimum_original_price?: float64;
  expires_at?: utcDateTime;
  applicable_plan_ids?: string[]; // GUID array
}

model UpdateCouponRequestDto {
  code?: string;
  discount_type?: string;
  amount?: float64;
  max_uses?: int32;
  minimum_original_price?: float64;
  expires_at?: utcDateTime;
  applicable_plan_ids?: string[]; // GUID array
  is_active?: boolean;
}

model TemplateVariableDto {
  tag: string;
  description: string;
}

model TemplateVariableCategoryDto {
  title: string;
  items: TemplateVariableDto[];
}

model TemplatePreviewRequestDto {
  subject: string;
  body: string;
}

model TemplatePreviewResponseDto {
  html_content: string;
  subject_content: string;
}

model CreateBroadcastRequestDto {
  subject: string;
  body: string;
  channel: string;
  target_plan_id?: string; // GUID
  target_status?: string;
  target_is_reminder_only?: boolean;
}
