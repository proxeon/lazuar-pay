/**
 * Local toy domain types — independent of Hub OpenAPI / @repo packages.
 */

export type OrderStatus =
  | "draft"
  | "checkout_open"
  | "paid"
  | "failed"
  | "cancelled";

export interface Order {
  id: string;
  amount: number;
  currency: string;
  description: string;
  customerEmail: string;
  status: OrderStatus;
  hubCheckoutId?: string;
  checkoutUrl?: string;
  paidAt?: string;
  lastDeliveryId?: string;
  lastEventId?: string;
  gatewayTransactionId?: string;
  metadata: Record<string, string>;
  createdAt: string;
  updatedAt: string;
}

/** Runtime Hub outbound webhook envelope (envelope + data). */
export interface HubWebhookEnvelope {
  id: string;
  event_type: string;
  created_at: string;
  data: PaymentWebhookData;
}

/** Payment fields live under data.* (not flat TypeSpec gap). */
export interface PaymentWebhookData {
  event_id?: string;
  checkout_id?: string;
  gateway?: string;
  gateway_transaction_id?: string;
  provider_session_id?: string;
  amount?: number;
  currency?: string;
  status?: string;
  metadata?: Record<string, string>;
  description?: string;
  customer_email?: string;
}

export interface CreateOrderInput {
  amount: number;
  currency?: string;
  description?: string;
  customerEmail: string;
}
