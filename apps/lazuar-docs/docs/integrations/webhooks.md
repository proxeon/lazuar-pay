# Webhooks

Hub notifies **your server** when a payment finishes (or fails). This is the fulfillment signal.

**Product:** Payments (M2M). Commerce lifecycle events are a different surface.

<!-- source: OutboundWebhookDispatcherJob + OutboundWebhookEventHandlers -->

## Dual hops (do not conflate)

There are **two** network legs. Browser `checkout_url` is **not** a webhook hop.

```text
Hop 1 — inbound (provider → Hub)
  Gateway  -->  Hub public processor URL (App:ApiBaseUrl)
                Hub verifies provider signature
                IntegrationCheckoutGatewayEventsHandler
                open → completed | failed
                enqueue WebhookDeliveryOutbox

Hop 2 — outbound (Hub → your app)
  Hub  -->  POST your webhook_url
            Headers:
              X-Lazuar-Signature: t=<unix>,v1=<hex>
              X-Lazuar-Event: payment.completed | payment.failed
              X-Lazuar-Delivery-Id: …
              X-Lazuar-Webhook-Id: …
            Your app: raw body → HMAC → unlock domain
```

```text
Gateway ----hop1----> Hub ----hop2----> Your webhook receiver
Guest browser ------> checkout_url on gateway   (NOT a hop)
```

**Summary:** Processors call Hub (hop 1). Hub signs and posts to your registered URL (hop 2). Configure public Hub base and your reachable webhook independently — see [Environments](/integrations/environments). Full cashier: [Payment flow](/integrations/payment-flow).

## Handler sequence

```text
Receive POST
   |
   v
Buffer RAW body as UTF-8 bytes/string
   |
   v
Parse X-Lazuar-Signature → t and v1
   |
   v
|now - t| ≤ ~300s ?  --no-->  401 Reject
   |
  yes
   v
signed = "{t}." + raw_body
v1_expected = hex_lower(HMAC-SHA256(full_whsec_secret, signed))
   |
   v
constant-time eq(v1, v1_expected) ?  --no-->  401
   |
  yes
   v
Parse JSON envelope { id, event_type, created_at, data }
   |
   v
Dedupe by event id / delivery id (+ gateway txn)
   |
   +-- payment.completed --> unlock domain once --> 2xx ACK
   +-- payment.failed    --> mark failed / no unlock --> 2xx ACK
   +-- mapping error     --> 422
   +-- transient error   --> 5xx (Hub retries)
```

### HTTP responses from your app

| Status | Meaning to Hub |
|--------|----------------|
| **2xx** | ACK — stop retrying this delivery |
| **401** | Bad signature — fix secret; Hub may still retry |
| **422** | Unprocessable (missing ids) — fix mapping; avoid silent 200 |
| **5xx** | Transient — Hub retries with backoff |

## Envelope honesty (runtime body)

Outbound Payments events are an **envelope** plus nested `data` — not a flat top-level payment object.

```json
{
  "id": "evt_…",
  "event_type": "payment.completed",
  "created_at": "2026-01-15T12:00:00Z",
  "data": {
    "checkout_id": "…",
    "status": "completed",
    "amount": 25.0,
    "currency": "MYR",
    "metadata": {
      "order_id": "ord_42"
    }
  }
}
```

| Field | Notes |
|-------|--------|
| `id` | Event id — use for dedupe |
| `event_type` | `payment.completed` / `payment.failed` (refunds maturing) |
| `created_at` | Event timestamp |
| `data.*` | Payment / checkout fields |
| Order correlation | Prefer `data.metadata.order_id` (or your keys) — **not** a top-level `order_id` |

Header `X-Lazuar-Event` mirrors the event type for quick routing; still parse the body.

## Events (Payments M2M)

| Event | Meaning |
|-------|---------|
| `payment.completed` | Money captured / paid — unlock domain |
| `payment.failed` | Payment failed — do **not** unlock |
| `payment.refunded` | Maturing — do not assume full support for M2M yet |

Commerce lifecycle events (`subscription.*`, `order.completed`) are a **different** product surface.

## Registration

### At provision (recommended)

Pass `webhook_url` when provisioning — see [Provision](/integrations/provision).

### Companion API

```http
POST /api/v1/one/workspaces/{workspaceId}/webhooks
```

Auth: OrgAdmin session **or** machine key with `webhooks.endpoints:manage` (workspace must match key tenant).

Body (conceptually):

```json
{
  "url": "https://your-app.example/webhooks/hub/payments",
  "is_active": true,
  "enabled_events": ["payment.completed", "payment.failed"]
}
```

`secret_key` / signing secret returned **once**.

## Request to your app

```http
POST https://your-app.example/webhooks/hub/payments
Content-Type: application/json
X-Lazuar-Signature: t=<unix>,v1=<hex>
X-Lazuar-Event: payment.completed
X-Lazuar-Delivery-Id: …
X-Lazuar-Webhook-Id: …
```

## Signature algorithm

1. Read **raw body** as UTF-8 string `body` (do not re-serialize JSON before verify).  
2. Parse `t` and `v1` from `X-Lazuar-Signature`.  
3. Reject if `|now - t|` outside skew (e.g. **300 seconds**).  
4. Compute:

```text
signed = "{t}." + raw_body
v1 = hex_lower(HMAC-SHA256(full_whsec_secret, signed))
```

5. Compare `v1` with **constant-time** equality.  
6. Use the **full** `whsec_…` secret string — **do not strip** the `whsec_` prefix.

### Pseudo-code

```text
signed = t + "." + raw_body
expected = hex(hmac_sha256(whsec_secret, signed))
assert constant_time_eq(expected, v1)
```

### Generate a local fixture

```bash
python3 - <<'PY'
import hmac, hashlib
secret = b"whsec_test_secret"
t = "1700000000"
body = b'{"id":"evt_demo","event_type":"payment.completed","created_at":"2026-01-01T00:00:00Z","data":{"checkout_id":"00000000-0000-0000-0000-000000000001","status":"completed"}}'
msg = t.encode() + b"." + body
print("v1=" + hmac.new(secret, msg, hashlib.sha256).hexdigest())
PY
```

## Idempotency

Hub may deliver more than once. Your handler must:

- Dedupe by event id / delivery id **and** gateway transaction id  
- Treat “already paid” as success (no double credit)

## Fulfillment state (app-owned)

```text
pending  --verified payment.completed-->  unlocked
pending  --payment.failed--------------->  failed (no unlock)
unlocked --replay delivery-------------->  still unlocked (no double credit)

success_url alone  -->  NEVER unlock
```

Rules:

1. Verify signature first.  
2. Load your domain object from `data.metadata` / `data.checkout_id`.  
3. Assert workspace / tenant isolation.  
4. Apply domain transition once.  
5. **Never** unlock only because the browser hit `success_url`.

## Delivery logs

Hub Ops (workspace) shows outbound delivery attempts. Use them when “paid at gateway, unpaid in app.”

## Next

[Environments & public URLs](/integrations/environments) — make processor and Hub reach each other.  
[Payment flow](/integrations/payment-flow) — end-to-end SSoT.
