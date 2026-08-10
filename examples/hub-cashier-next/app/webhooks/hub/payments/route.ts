import { NextRequest, NextResponse } from "next/server";
import { getWebhookSecret } from "@/lib/env";
import {
  findByCheckoutId,
  getOrder,
  hasSeenDelivery,
  markDeliverySeen,
  updateOrder,
} from "@/lib/orders-store";
import type { HubWebhookEnvelope, PaymentWebhookData } from "@/lib/types";
import { verifySignature } from "@/lib/webhook-verify";

/**
 * Hub outbound webhook receiver.
 * Path: POST /webhooks/hub/payments
 * (must match provision webhook_url)
 *
 * CRITICAL: raw body via request.text() before verify — never request.json() first.
 */
export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(req: NextRequest) {
  const secret = getWebhookSecret();
  if (!secret) {
    console.error(
      "[webhook] LAZUAR_WEBHOOK_SECRET missing — refuse deliveries",
    );
    return NextResponse.json({ error: "misconfigured" }, { status: 500 });
  }

  // Raw body for HMAC — bytes must match what Hub signed
  const rawBody = await req.text();
  const signature = req.headers.get("x-lazuar-signature");

  if (!verifySignature(secret, rawBody, signature)) {
    return NextResponse.json({ error: "invalid_signature" }, { status: 401 });
  }

  let envelope: HubWebhookEnvelope;
  try {
    envelope = JSON.parse(rawBody) as HubWebhookEnvelope;
  } catch {
    return NextResponse.json({ error: "invalid_json" }, { status: 400 });
  }

  const eventType =
    req.headers.get("x-lazuar-event") ?? envelope.event_type ?? "";
  const deliveryId =
    req.headers.get("x-lazuar-delivery-id") ?? envelope.id ?? "";
  const data: PaymentWebhookData = envelope.data ?? {};

  // Delivery-id dedupe (file Set; multi-instance caveat)
  if (deliveryId && hasSeenDelivery(deliveryId)) {
    return NextResponse.json({
      ok: true,
      already: true,
      delivery_id: deliveryId,
    });
  }

  if (eventType === "payment.failed") {
    await fulfillFailed(data, deliveryId);
    if (deliveryId) markDeliverySeen(deliveryId);
    return NextResponse.json({ ok: true, delivery_id: deliveryId });
  }

  if (eventType !== "payment.completed") {
    // ACK unknown events so Hub stops retrying
    if (deliveryId) markDeliverySeen(deliveryId);
    return NextResponse.json({ ok: true, ignored: eventType });
  }

  const orderId = data.metadata?.order_id;
  let order = orderId ? getOrder(orderId) : undefined;
  if (!order && data.checkout_id) {
    order = findByCheckoutId(data.checkout_id);
  }

  if (!order) {
    console.warn("[webhook] order not found", {
      deliveryId,
      checkout_id: data.checkout_id,
      // do not log secrets
    });
    return NextResponse.json({ error: "order_not_found" }, { status: 422 });
  }

  // Business idempotency: already paid / same gateway_transaction_id → 200 no-op
  if (order.status === "paid") {
    if (deliveryId) markDeliverySeen(deliveryId);
    return NextResponse.json({
      ok: true,
      already: true,
      order_id: order.id,
      delivery_id: deliveryId,
    });
  }

  if (
    data.gateway_transaction_id &&
    order.gatewayTransactionId &&
    order.gatewayTransactionId === data.gateway_transaction_id
  ) {
    // Same processor txn seen again while not yet flipped to paid in an earlier write race
    if (deliveryId) markDeliverySeen(deliveryId);
    return NextResponse.json({
      ok: true,
      already: true,
      order_id: order.id,
      delivery_id: deliveryId,
    });
  }

  updateOrder(order.id, {
    status: "paid",
    paidAt: new Date().toISOString(),
    lastDeliveryId: deliveryId || undefined,
    lastEventId: data.event_id ?? envelope.id,
    hubCheckoutId: data.checkout_id ?? order.hubCheckoutId,
    gatewayTransactionId: data.gateway_transaction_id,
  });

  if (deliveryId) markDeliverySeen(deliveryId);

  console.info("[webhook] order paid", {
    order_id: order.id,
    delivery_id: deliveryId,
    checkout_id: data.checkout_id,
  });

  return NextResponse.json({
    ok: true,
    order_id: order.id,
    delivery_id: deliveryId,
  });
}

function fulfillFailed(data: PaymentWebhookData, deliveryId: string) {
  const orderId = data.metadata?.order_id;
  let order = orderId ? getOrder(orderId) : undefined;
  if (!order && data.checkout_id) {
    order = findByCheckoutId(data.checkout_id);
  }
  // Never unlock; never overwrite paid
  if (!order || order.status === "paid") return;
  updateOrder(order.id, {
    status: "failed",
    lastDeliveryId: deliveryId || undefined,
    lastEventId: data.event_id,
  });
}
