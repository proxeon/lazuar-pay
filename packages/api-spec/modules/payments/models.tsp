// packages/api-spec/modules/payments/models.tsp
// M2M cashier / integration checkout contracts (Track B)

namespace LazuarApi.Payments;

/** Request body for POST /integrations/payments/checkouts */
model CreateIntegrationCheckoutRequestDto {
  /** Positive amount in major currency units (e.g. 10.00 MYR). */
  amount: float64;
  /** ISO 4217 currency code (e.g. MYR, USD). */
  currency: string;
  /** Human-readable description shown on gateway / receipts when supported. */
  description: string;
  customer_email: string;
  customer_name?: string;
  /** Absolute success redirect URL after guest pay. */
  success_url: string;
  /** Absolute cancel redirect URL. */
  cancel_url: string;
  /** Optional gateway preference (billplz, stripe, …). Omit → workspace default active gateway. */
  gateway_name?: string;
  /** When true, request setup for future off-session charges if gateway supports it. */
  setup_future_usage?: boolean;
  /**
   * Optional idempotency key (also accepted via Idempotency-Key header).
   * Same key + same fingerprint → same session; same key + different body → 409.
   */
  idempotency_key?: string;
  /**
   * Opaque string map. Hub stamps hub_workspace_id / checkout_id / hub_checkout_kind.
   * Domain ids (booking_id, order_id, …) stay in the integrator app — do not special-case in Hub.
   */
  metadata?: Record<string>;
}

model IntegrationCheckoutResponseDto {
  checkout_id: string; // GUID
  checkout_url?: string;
  gateway: string;
  /** open | completed | failed | expired */
  status: string;
  amount: float64;
  currency: string;
  provider_session_id?: string;
  gateway_transaction_id?: string;
  expires_at: utcDateTime;
  metadata: Record<string>;
}

/**
 * Outbound payment.* webhook envelope (snake_case JSON body).
 * Signed with X-Lazuar-Signature: t=…,v1=… (HMAC-SHA256 of "{t}.{rawBody}").
 */
model PaymentWebhookPayloadDto {
  event_id: string;
  event_type: "payment.completed" | "payment.failed";
  checkout_id: string;
  workspace_id: string;
  amount: float64;
  currency: string;
  status: string;
  gateway?: string;
  gateway_transaction_id?: string;
  metadata?: Record<string>;
  occurred_at?: utcDateTime;
}
