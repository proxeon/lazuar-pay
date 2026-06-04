import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";

using TypeSpec.Http;

@service({
  title: "Lazuar Platform API",
})
@server("http://localhost:8080/api/v1", "Local development server")
namespace LazuarApi;

// ==========================================
// 1. Standard Responses & Errors
// ==========================================

namespace Models.Core {
  @summary("RFC 7807 Standardized Error Response")
  model ProblemDetails {
    type?: string;
    title?: string;
    status?: int32;
    detail?: string;
    instance?: string;
  }

  @summary("RFC 7807 Extended Validation Error Response")
  model ValidationProblemDetails extends ProblemDetails {
    errors?: Record<string[]>;
  }

  model IdResponse {
    id: string; // GUID
  }

  model StatusResponse {
    status: string;
  }
}

@error
model ProblemDetailsResponse {
  @statusCode statusCode: 400 | 401 | 403 | 404 | 500;
  @body body: Models.Core.ProblemDetails;
}

// ==========================================
// 2. Domain Models (DTOs)
// ==========================================

namespace Models.Community {
  // --- Read Models ---
  model FaqItemDto {
    id: string;
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
    created_at: utcDateTime;
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
  }

  // --- Write Models (Requests) ---
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

  model SavePaymentConfigRequestDto {
    gateway_type: string;
    api_key?: string;
    collection_id?: string; // Mapped to MerchantId
    webhook_secret?: string;
    secret_key?: string;
    is_active: boolean;
  }

  model PublicCheckoutRequestDto {
    tenant_slug: string;
    plan_slug: string;
    name: string;
    email: string;
    phone: string;
  }

  model MagicLinkRequestDto {
    email: string;
  }
}

namespace Models.Messaging {
  model MessageTemplateDto {
    id: string; // GUID
    name: string;
    subject: string;
    body: string;
    is_default: boolean;
    updated_at: utcDateTime;
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
}

namespace Models.Auth {
  model LoginRequest {
    email: string;
    password?: string;
  }

  model LoginResponse {
    token: string;
    user: AuthUser;
  }

  model AuthUser {
    email: string;
    name: string;
    role: string;
  }
}

// ==========================================
// 3. API Endpoints
// ==========================================

@route("/platform/auth")
namespace AuthApi {
  @post
  @route("/login")
  op login(
    @body body: Models.Auth.LoginRequest,
  ): Models.Auth.LoginResponse | ProblemDetailsResponse;

  @useAuth(BearerAuth) // Built-in TypeSpec auth mechanism
  @get
  @route("/me")
  op getMe(): Models.Auth.AuthUser | ProblemDetailsResponse;
}

@useAuth(BearerAuth)
@route("/admin/community")
namespace AdminCommunityApi {
  // --- Plans ---
  @get
  @route("/plans")
  op getPlans(): Models.Community.CommunityPlanDto[] | ProblemDetailsResponse;

  @get
  @route("/plans/{id}")
  op getPlanById(
    @path id: string,
  ): Models.Community.CommunityPlanDto | ProblemDetailsResponse;

  @post
  @route("/plans")
  op createPlan(
    @body body: Models.Community.CreatePlanRequestDto,
  ): Models.Core.IdResponse | ProblemDetailsResponse;

  @put
  @route("/plans/{id}")
  op updatePlan(
    @path id: string,
    @body body: Models.Community.UpdatePlanRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @delete
  @route("/plans/{id}")
  op archivePlan(
    @path id: string,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  // --- Subscribers ---
  @get
  @route("/subscribers")
  op getSubscribers(
  ): Models.Community.CommunitySubscriptionDto[] | ProblemDetailsResponse;

  @get
  @route("/subscribers/export")
  op exportSubscribers(): {
    @header contentType: "text/csv";
    @body content: bytes;
  } | ProblemDetailsResponse;

  @get
  @route("/subscribers/{id}/reminders")
  op getReminderHistory(
    @path id: string,
  ): Models.Community.DeliveryHistoryItemDto[] | ProblemDetailsResponse;

  @post
  @route("/subscribers")
  op createSubscriber(
    @body body: Models.Community.CreateSubscriberRequestDto,
  ): Models.Core.IdResponse | ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/payments")
  op recordPaymentViaPayments(
    @path id: string,
    @body body: Models.Community.RecordPaymentRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/record-payment")
  op recordPayment(
    @path id: string,
    @body body: Models.Community.RecordPaymentRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/cancel")
  op cancelSubscription(
    @path id: string,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @put
  @route("/subscribers/{id}")
  op updateSubscriberProfile(
    @path id: string,
    @body body: Models.Community.UpdateSubscriberProfileRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/extend-grace")
  op extendGrace(
    @path id: string,
    @body body: Models.Community.ExtendGraceRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/pause-reminders")
  op pauseReminders(
    @path id: string,
    @body body: Models.Community.PauseRemindersRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @post
  @route("/subscribers/{id}/send-reminder")
  op sendReminder(
    @path id: string,
    @body body: Models.Community.SendOneOffReminderRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @post
  @route("/reminders/schedule-one-off")
  op scheduleOneOffReminder(
    @body body: Models.Community.ScheduleOneOffRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  // --- Reminder Schedules ---
  @get
  @route("/reminder-schedules")
  op getReminderSchedules(
  ): Models.Community.CommunityReminderScheduleDto[] | ProblemDetailsResponse;

  @post
  @route("/reminder-schedules")
  op createReminderSchedule(
    @body body: Models.Community.CreateReminderScheduleRequestDto,
  ): Models.Core.IdResponse | ProblemDetailsResponse;

  @put
  @route("/reminder-schedules/{id}")
  op updateReminderSchedule(
    @path id: string,
    @body body: Models.Community.UpdateReminderScheduleRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @delete
  @route("/reminder-schedules/{id}")
  op deleteReminderSchedule(
    @path id: string,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  // --- Settings & Stats ---
  @get
  @route("/stats")
  op getStats(
  ): Models.Community.CommunitySubscriberStatsDto | ProblemDetailsResponse;

  @get
  @route("/payment-config")
  op getPaymentConfig(
  ): Models.Community.PaymentConfigDto | ProblemDetailsResponse;

  @put
  @route("/payment-config")
  op savePaymentConfig(
    @body body: Models.Community.SavePaymentConfigRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  // --- Templates (Messaging Context) ---
  @get
  @route("/templates")
  op getTemplates(
  ): Models.Messaging.MessageTemplateDto[] | ProblemDetailsResponse;

  @put
  @route("/templates/{id}")
  op updateTemplate(
    @path id: string,
    @body body: Models.Messaging.UpdateTemplateRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @delete
  @route("/templates/{id}")
  op resetTemplate(
    @path id: string,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  @post
  @route("/reminders/test")
  op testReminder(
    @body body: Models.Messaging.TestReminderRequestDto,
  ): Models.Messaging.TestReminderResponse | ProblemDetailsResponse;
}

@route("/public/community")
namespace PublicCommunityApi {
  @get
  @route("/{tenantSlug}/plans")
  op getPublicPlans(
    @path tenantSlug: string,
  ): Models.Community.CommunityPlanDto[] | ProblemDetailsResponse;

  @get
  @route("/{tenantSlug}/plans/{slug}")
  op getPlanBySlug(
    @path tenantSlug: string,
    @path slug: string,
  ): Models.Community.CommunityPlanDto | ProblemDetailsResponse;

  model CheckoutResponse {
    url: string;
  }

  @post
  @route("/checkout")
  op initiateCheckout(
    @body body: Models.Community.PublicCheckoutRequestDto,
  ): CheckoutResponse | ProblemDetailsResponse;

  @post
  @route("/{tenantSlug}/portal/magic-link")
  op requestMagicLink(
    @path tenantSlug: string,
    @body body: Models.Community.MagicLinkRequestDto,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;

  model PortalDataResponse {
    subscription: Models.Community.CommunitySubscriptionDto;
  }

  @get
  @route("/{tenantSlug}/portal")
  op getPortalData(
    @path tenantSlug: string,
    @query token: string,
  ): PortalDataResponse | ProblemDetailsResponse;

  model CancelPortalRequest {
    subscription_id: string;
  }

  @post
  @route("/{tenantSlug}/portal/cancel")
  op cancelPortalSubscription(
    @path tenantSlug: string,
    @query token: string,
    @body body: CancelPortalRequest,
  ): Models.Core.StatusResponse | ProblemDetailsResponse;
}
