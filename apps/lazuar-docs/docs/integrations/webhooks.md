# Webhooks

Hub notifies **your server** when a payment finishes (or fails). This is the fulfillment signal.

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

## Signature verification

1. Read **raw body** as UTF-8 string `body`.  
2. Parse `t` and `v1` from `X-Lazuar-Signature`.  
3. Reject if `|now - t|` outside skew (e.g. **300 seconds**).  
4. Compute `HMAC-SHA256(secret, "{t}.{body}")` as lowercase hex.  
5. Compare `v1` with **constant-time** equality.  

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
body = b'{"event_type":"payment.completed","checkout_id":"00000000-0000-0000-0000-000000000001"}'
msg = t.encode() + b"." + body
print("v1=" + hmac.new(secret, msg, hashlib.sha256).hexdigest())
PY
```

## HTTP responses from your app

| Status | Meaning to Hub |
|--------|----------------|
| **2xx** | ACK — stop retrying this delivery |
| **401** | Bad signature — fix secret; Hub may still retry |
| **422** | Unprocessable (missing ids) — fix mapping; avoid silent 200 |
| **5xx** | Transient — Hub retries with backoff |

## Idempotency

Hub may deliver more than once. Your handler must:

- Dedupe by event id / delivery id **and** gateway transaction id  
- Treat “already paid” as success (no double credit)

## Fulfillment rules

1. Verify signature first.  
2. Load your domain object from `metadata` / checkout id.  
3. Assert workspace / tenant isolation.  
4. Apply domain transition once.  
5. **Never** unlock only because the browser hit `success_url`.

## Delivery logs

Hub Ops (workspace) shows outbound delivery attempts. Use them when “paid at gateway, unpaid in app.”

## Next

[Environments & public URLs](/integrations/environments) — make processor and Hub reach each other.
