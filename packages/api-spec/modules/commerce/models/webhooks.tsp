// Frozen P09 SaaS outbound webhook contract (doc model — not a POST body).
// Runtime dispatcher wraps `data` in { id, event_type, created_at, data }.
// Same signer as guest: X-Lazuar-Signature t.{rawBody}. Do not flatten.
// Do not emit subscription.updated.

namespace LazuarApi.Commerce;

/** Five Commerce lifecycle types Aura applies on POST /webhooks/hub/saas. */
union CommerceSubscriptionEventType {
  "subscription.activated",
  "subscription.resumed",
  "subscription.past_due",
  "subscription.canceled",
  "subscription.suspended",
}

/**
 * Outbound envelope posted to workspace webhook URLs.
 * `id` is the dispatcher envelope id (Aura claim key), not commerce.Subscriptions.Id.
 */
model CommerceWebhookEnvelope {
  id: string;
  event_type: CommerceSubscriptionEventType;
  created_at: utcDateTime;
  data: SubscriptionWebhookData;
}

/**
 * `data` object for subscription.* events.
 * current_period_end is the paid-through instant (NextBillingDate).
 * customer_id is ClientProfileId; client_profile_id is a backward-compat alias.
 */
model SubscriptionWebhookData {
  subscription_id: string;
  status: "ACTIVE" | "PAST_DUE" | "CANCELED" | "SUSPENDED";
  current_period_end?: utcDateTime;
  customer_id: string;
  client_profile_id?: string;
  customer_email?: string;
  product_id: string;
  amount?: float64;
  currency?: string;
  interval?: "mo" | "yr";
  /** Present on subscription.activated only. */
  is_first_payment?: boolean;
  metadata?: Record<string>;
}
