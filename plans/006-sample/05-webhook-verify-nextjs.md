# 05 — Webhook verification for Next.js (match Hub)

**Status:** analysis complete 2026-08-10  
**SSoT (sign):** `apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs`  
**SSoT (headers/dispatch):** `OutboundWebhookDispatcherJob.cs`  
**SSoT (envelope):** `OutboundWebhookEventHandlers.cs`  
**SSoT (payment data):** `IntegrationCheckoutGatewayEventsHandler.cs`  
**Unit tests (C#):** `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OutboundWebhookTests.cs`

---

## 1. What Hub sends

### HTTP

```http
POST {your_webhook_url}
Content-Type: application/json
X-Lazuar-Signature: t=1700000000,v1=abcdef…
X-Lazuar-Event: payment.completed
X-Lazuar-Delivery-Id: {guid}
X-Lazuar-Webhook-Id: {endpoint-guid}
```

From `OutboundWebhookDispatcherJob`:

```csharp
request.Headers.TryAddWithoutValidation("X-Lazuar-Signature", signature);
request.Headers.TryAddWithoutValidation("X-Lazuar-Event", delivery.EventType);
request.Headers.TryAddWithoutValidation("X-Lazuar-Delivery-Id", delivery.Id.ToString());
request.Headers.TryAddWithoutValidation("X-Lazuar-Webhook-Id", endpoint.Id.ToString());
request.Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json");
```

### Signature algorithm (exact)

`OutboundWebhookSignature.ComputeHeaderValue`:

1. `signedPayload = $"{unixTimestampSeconds}.{body}"`  
2. `HMACSHA256(UTF8(secret), UTF8(signedPayload))`  
3. Hex digest **lowercase** (`Convert.ToHexString(hash).ToLowerInvariant()`)  
4. Header value: `t={unixTimestampSeconds},v1={hex}`

`TryVerify`:

1. Reject empty secret or header  
2. Parse `t` and `v1` from comma-separated parts (`t=…`, `v1=…`, case-insensitive keys)  
3. If `toleranceSeconds > 0` (default **300**): reject when `|now - t| > tolerance`  
4. Recompute expected header at timestamp `t`  
5. Compare hex with **fixed-time** equality (after lowercasing)

**Not** Stripe’s `whsec_` base64 key decoding — Hub uses the secret string **as UTF-8 raw key material** (including the `whsec_` prefix characters if present in the stored secret).

---

## 2. Runtime JSON envelope (critical honesty)

One wraps the payment payload:

```csharp
var jsonPayload = JsonSerializer.Serialize(new
{
    id = Guid.CreateVersion7().ToString(),
    event_type = @event.EventType,
    created_at = DateTime.UtcNow,
    data = @event.Payload
}, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
```

### Outer envelope

```json
{
  "id": "0193…",
  "event_type": "payment.completed",
  "created_at": "2026-08-10T10:00:00Z",
  "data": { }
}
```

### Inner `data` for M2M payment events

Built by `IntegrationCheckoutGatewayEventsHandler.BuildPayload`:

```json
{
  "event_id": "…",
  "checkout_id": "…",
  "gateway": "BILLPLZ",
  "gateway_transaction_id": "…",
  "provider_session_id": "…",
  "amount": 25.0,
  "currency": "MYR",
  "status": "completed",
  "metadata": {
    "order_id": "…",
    "type": "sample_order"
  },
  "description": "…",
  "customer_email": "…"
}
```

### TypeSpec gap

`PaymentWebhookPayloadDto` in `models.tsp` is **flat** (`event_id`, `event_type`, `checkout_id`, …). Runtime is **envelope + data**. Sample and docs must implement/document **runtime**. Track TypeSpec honesty as follow-up; do not break sample on flat model.

### Events

| `event_type` / header | Meaning for sample |
|-----------------------|--------------------|
| `payment.completed` | Mark order paid |
| `payment.failed` | Mark failed; no unlock |
| other | 200 ignore or 204 |

---

## 3. Full TypeScript verify helper

`examples/hub-cashier-next/lib/webhook-verify.ts`:

```ts
import { createHmac, timingSafeEqual } from "node:crypto";

export type ParsedSignature = { t: number; v1: string };

/**
 * Parse Standard Webhooks–style header: t=<unix>,v1=<hex>
 * Matches OutboundWebhookSignature.TryParseHeader
 */
export function parseLazuarSignatureHeader(headerValue: string): ParsedSignature | null {
  let t: number | undefined;
  let v1: string | undefined;

  for (const part of headerValue.split(",")) {
    const trimmed = part.trim();
    const eq = trimmed.indexOf("=");
    if (eq <= 0) continue;
    const key = trimmed.slice(0, eq);
    const value = trimmed.slice(eq + 1);
    if (key.toLowerCase() === "t") {
      const n = Number(value);
      if (Number.isFinite(n)) t = n;
    } else if (key.toLowerCase() === "v1") {
      v1 = value;
    }
  }

  if (t === undefined || !v1) return null;
  return { t, v1 };
}

export function computeLazuarSignatureHeader(
  secret: string,
  body: string,
  unixTimestampSeconds: number,
): string {
  const signedPayload = `${unixTimestampSeconds}.${body}`;
  const hex = createHmac("sha256", Buffer.from(secret, "utf8"))
    .update(signedPayload, "utf8")
    .digest("hex"); // node digest("hex") is lowercase
  return `t=${unixTimestampSeconds},v1=${hex}`;
}

function fixedTimeEqualHex(a: string, b: string): boolean {
  const left = Buffer.from(a.toLowerCase(), "utf8");
  const right = Buffer.from(b.toLowerCase(), "utf8");
  if (left.length !== right.length) return false;
  return timingSafeEqual(left, right);
}

/**
 * Matches OutboundWebhookSignature.TryVerify
 * @param toleranceSeconds default 300; pass 0 to skip skew check
 */
export function verifyLazuarSignature(
  secret: string,
  body: string,
  headerValue: string | null | undefined,
  options?: { toleranceSeconds?: number; nowUnixSeconds?: number },
): boolean {
  if (!secret || !headerValue) return false;

  const parsed = parseLazuarSignatureHeader(headerValue);
  if (!parsed) return false;

  const tolerance = options?.toleranceSeconds ?? 300;
  if (tolerance > 0) {
    const now = options?.nowUnixSeconds ?? Math.floor(Date.now() / 1000);
    if (Math.abs(now - parsed.t) > tolerance) return false;
  }

  const expectedHeader = computeLazuarSignatureHeader(secret, body, parsed.t);
  const expected = parseLazuarSignatureHeader(expectedHeader);
  if (!expected) return false;

  return fixedTimeEqualHex(parsed.v1, expected.v1);
}
```

---

## 4. Full webhook route handler

`examples/hub-cashier-next/app/api/webhooks/hub/route.ts`:

```ts
import { NextRequest, NextResponse } from "next/server";
import { verifyLazuarSignature } from "@/lib/webhook-verify";
import { getOrder, getOrderByCheckoutId, updateOrder } from "@/lib/orders-store";

export const runtime = "nodejs";

// Ensure we never cache webhook responses
export const dynamic = "force-dynamic";

type HubEnvelope = {
  id?: string;
  event_type?: string;
  created_at?: string;
  data?: {
    event_id?: string;
    checkout_id?: string;
    status?: string;
    metadata?: Record<string, string>;
    gateway_transaction_id?: string;
  };
};

export async function POST(req: NextRequest) {
  const secret = process.env.HUB_WEBHOOK_SECRET;
  if (!secret) {
    console.error("HUB_WEBHOOK_SECRET missing");
    return NextResponse.json({ error: "misconfigured" }, { status: 500 });
  }

  // CRITICAL: raw body for HMAC — do not request.json() first
  const rawBody = await req.text();
  const signature = req.headers.get("x-lazuar-signature");
  // Node fetch headers are case-insensitive; Hub sends X-Lazuar-Signature

  if (!verifyLazuarSignature(secret, rawBody, signature)) {
    return NextResponse.json({ error: "invalid_signature" }, { status: 401 });
  }

  let envelope: HubEnvelope;
  try {
    envelope = JSON.parse(rawBody) as HubEnvelope;
  } catch {
    return NextResponse.json({ error: "invalid_json" }, { status: 400 });
  }

  const eventType =
    req.headers.get("x-lazuar-event") ?? envelope.event_type ?? "";
  const deliveryId = req.headers.get("x-lazuar-delivery-id") ?? envelope.id;
  const data = envelope.data ?? {};

  // Prefer payment.* from header/envelope
  if (eventType === "payment.failed") {
    await markFailed(data);
    return NextResponse.json({ ok: true, delivery_id: deliveryId });
  }

  if (eventType !== "payment.completed") {
    // ACK unknown events so Hub stops retrying if we are not subscribed
    return NextResponse.json({ ok: true, ignored: eventType });
  }

  const orderId = data.metadata?.order_id;
  let order = orderId ? getOrder(orderId) : undefined;
  if (!order && data.checkout_id) {
    order = getOrderByCheckoutId(data.checkout_id);
  }

  if (!order) {
    // 422 signals mapping bug — Hub may retry; for demo 200+log also acceptable
    console.warn("webhook order not found", { deliveryId, checkout_id: data.checkout_id });
    return NextResponse.json({ error: "order_not_found" }, { status: 422 });
  }

  // Idempotent fulfill
  if (order.status === "paid") {
    return NextResponse.json({ ok: true, already: true, delivery_id: deliveryId });
  }

  updateOrder(order.id, {
    status: "paid",
    paid_at: new Date().toISOString(),
    last_event_id: data.event_id ?? deliveryId ?? undefined,
    hub_checkout_id: data.checkout_id ?? order.hub_checkout_id,
  });

  return NextResponse.json({ ok: true, order_id: order.id, delivery_id: deliveryId });
}

function markFailed(data: HubEnvelope["data"]) {
  const orderId = data?.metadata?.order_id;
  let order = orderId ? getOrder(orderId) : undefined;
  if (!order && data?.checkout_id) order = getOrderByCheckoutId(data.checkout_id);
  if (!order || order.status === "paid") return;
  updateOrder(order.id, { status: "failed" });
}
```

### HTTP status contract (docs alignment)

| Status | Hub interpretation |
|--------|--------------------|
| 2xx | Success — stop retry |
| 401 | Bad signature |
| 422 | Unprocessable mapping |
| 5xx | Transient — retry |

---

## 5. Next.js pitfalls checklist

| Pitfall | Fix |
|---------|-----|
| `await request.json()` then re-stringify | Use `request.text()` first |
| Middleware parsing body | Exclude `/api/webhooks/*` |
| Edge runtime crypto differences | `export const runtime = "nodejs"` |
| Secret in `NEXT_PUBLIC_` | Never |
| Clock skew in local VM | Keep 300s; injectable `now` in tests |
| Assuming flat payload | Read `envelope.data` |
| Double fulfill | Check `order.status === "paid"` |
| Case of headers | Use `.get("x-lazuar-signature")` |

---

## 6. Local unit test for verify (optional Vitest/Node assert)

```ts
import assert from "node:assert/strict";
import {
  computeLazuarSignatureHeader,
  verifyLazuarSignature,
} from "../lib/webhook-verify";

const secret = "whsec_test_secret";
const body =
  '{"id":"1","event_type":"payment.completed","data":{"checkout_id":"00000000-0000-0000-0000-000000000001"}}';
const t = 1700000000;
const header = computeLazuarSignatureHeader(secret, body, t);

assert.equal(verifyLazuarSignature(secret, body, header, { nowUnixSeconds: t }), true);
assert.equal(verifyLazuarSignature(secret, body + "x", header, { nowUnixSeconds: t }), false);
assert.equal(verifyLazuarSignature("whsec_other", body, header, { nowUnixSeconds: t }), false);
assert.equal(
  verifyLazuarSignature(secret, body, header, {
    toleranceSeconds: 30,
    nowUnixSeconds: t + 120,
  }),
  false,
);
```

Align vectors with C# `OutboundWebhookTests` when possible.

---

## 7. Python fixture generator (docs + local)

Same as existing docs `webhooks.md` / quickstart:

```bash
python3 - <<'PY'
import hmac, hashlib
secret = b"whsec_test_secret"
t = "1700000000"
body = b'{"id":"1","event_type":"payment.completed","created_at":"2026-01-01T00:00:00Z","data":{"event_id":"e1","checkout_id":"00000000-0000-0000-0000-000000000001","status":"completed","metadata":{"order_id":"ord_1"}}}'
msg = t.encode() + b"." + body
print("X-Lazuar-Signature: t=" + t + ",v1=" + hmac.new(secret, msg, hashlib.sha256).hexdigest())
print(body.decode())
PY
```

### curl against sample

```bash
# after running the python snippet, export SIG and BODY
export SAMPLE=http://localhost:3005
export SIG='t=1700000000,v1=…'
export BODY='…'

curl -sS -X POST "$SAMPLE/api/webhooks/hub" \
  -H "Content-Type: application/json" \
  -H "X-Lazuar-Signature: $SIG" \
  -H "X-Lazuar-Event: payment.completed" \
  -H "X-Lazuar-Delivery-Id: 00000000-0000-0000-0000-000000000099" \
  -H "X-Lazuar-Webhook-Id: 00000000-0000-0000-0000-000000000088" \
  --data-binary "$BODY"
```

Use `--data-binary` so body bytes match signature.

### Negative tests

```bash
# tampered body
curl -sS -o /dev/null -w "%{http_code}\n" -X POST "$SAMPLE/api/webhooks/hub" \
  -H "X-Lazuar-Signature: $SIG" \
  -H "Content-Type: application/json" \
  --data-binary "${BODY}tamper"
# expect 401
```

---

## 8. End-to-end with real Hub (preferred)

1. Sample running; `HUB_WEBHOOK_SECRET` matches provision.  
2. Register webhook URL: `http://host.docker.internal:3005/api/webhooks/hub` or tunnel if Hub remote.  
3. Create checkout + pay sandbox.  
4. Watch sample logs for delivery id.  
5. Confirm order `paid`.  
6. Re-deliver from Ops logs if available → still paid once.

If Hub and sample on same machine: `http://127.0.0.1:3005/api/webhooks/hub` works for Hop 2. Hop 1 (gateway→Hub) still needs public Hub URL.

---

## 9. Docs updates required

| Page | Add |
|------|-----|
| `integrations/webhooks.md` | Envelope example (outer+data); raw body warning for Next |
| `reference/events.md` | Note envelope wrapper |
| `run-sample-app.md` | curl/python local inject |
| TypeSpec (later) | Align `PaymentWebhookPayloadDto` or document as `data` model |

---

## 10. Implementation checklist

- [ ] `lib/webhook-verify.ts` with tests  
- [ ] `app/api/webhooks/hub/route.ts` raw body  
- [ ] Store `delivery_id` / `event_id` for idempotency logs  
- [ ] README section “simulate webhook”  
- [ ] Never document DIY Billplz verify as required for Hub path  
