#!/usr/bin/env node
/**
 * Dev helper: sign + POST a fake Hub payment webhook to the local sample.
 * Does NOT replace real sandbox pay for full e2e (Hop 1 gateway→Hub).
 *
 * Usage:
 *   LAZUAR_WEBHOOK_SECRET=whsec_… ORDER_ID=… CHECKOUT_ID=… \
 *     node scripts/send-fake-webhook.mjs
 *
 * Optional env:
 *   SAMPLE_URL (default http://127.0.0.1:3020)
 *   EVENT_TYPE (default payment.completed)
 *   DELIVERY_ID (default random)
 *   AMOUNT (default 25)
 */
import { createHmac, randomUUID } from "node:crypto";
import { readFileSync, existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));

function loadDotEnvLocal() {
  const p = join(__dirname, "..", ".env.local");
  if (!existsSync(p)) return;
  const text = readFileSync(p, "utf8");
  for (const line of text.split("\n")) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;
    const eq = trimmed.indexOf("=");
    if (eq <= 0) continue;
    const key = trimmed.slice(0, eq).trim();
    let val = trimmed.slice(eq + 1).trim();
    if (
      (val.startsWith('"') && val.endsWith('"')) ||
      (val.startsWith("'") && val.endsWith("'"))
    ) {
      val = val.slice(1, -1);
    }
    if (process.env[key] === undefined) process.env[key] = val;
  }
}

loadDotEnvLocal();

const secret =
  process.env.LAZUAR_WEBHOOK_SECRET || process.env.HUB_WEBHOOK_SECRET;
if (!secret) {
  console.error(
    "Set LAZUAR_WEBHOOK_SECRET (full whsec_… string) or put it in .env.local",
  );
  process.exit(1);
}

const orderId = process.env.ORDER_ID;
const checkoutId =
  process.env.CHECKOUT_ID || "00000000-0000-0000-0000-000000000001";
if (!orderId) {
  console.error(
    "Set ORDER_ID to a local order id (create via /pay or POST /api/orders).",
  );
  process.exit(1);
}

const sampleUrl = (
  process.env.SAMPLE_URL ||
  process.env.NEXT_PUBLIC_APP_URL ||
  "http://127.0.0.1:3020"
).replace(/\/$/, "");
const eventType = process.env.EVENT_TYPE || "payment.completed";
const deliveryId = process.env.DELIVERY_ID || randomUUID();
const amount = Number(process.env.AMOUNT || "25");
const t = Math.floor(Date.now() / 1000);

const envelope = {
  id: randomUUID(),
  event_type: eventType,
  created_at: new Date().toISOString(),
  data: {
    event_id: randomUUID(),
    checkout_id: checkoutId,
    gateway: "BILLPLZ",
    gateway_transaction_id: process.env.GATEWAY_TX || randomUUID(),
    amount,
    currency: "MYR",
    status: eventType === "payment.completed" ? "completed" : "failed",
    metadata: {
      order_id: orderId,
      type: "sample_order",
      source: "hub-cashier-next",
    },
    description: "Fake webhook (dev)",
    customer_email: "guest@example.com",
  },
};

const body = JSON.stringify(envelope);
const signedPayload = `${t}.${body}`;
const v1 = createHmac("sha256", Buffer.from(secret, "utf8"))
  .update(signedPayload, "utf8")
  .digest("hex");
const signature = `t=${t},v1=${v1}`;

const url = `${sampleUrl}/webhooks/hub/payments`;
console.log("POST", url);
console.log("event:", eventType, "order:", orderId, "delivery:", deliveryId);

const res = await fetch(url, {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
    "X-Lazuar-Signature": signature,
    "X-Lazuar-Event": eventType,
    "X-Lazuar-Delivery-Id": deliveryId,
    "X-Lazuar-Webhook-Id": process.env.WEBHOOK_ID || randomUUID(),
  },
  body, // raw — must match signed bytes
});

const text = await res.text();
console.log("status:", res.status);
console.log(text);

if (!res.ok) process.exit(1);
