# Second-app proof harness (curl)

**Location:** `plans/006-sample/harness/second-app-proof.md` (living twin of the sample)  
**Do not** revive deleted singular `script/second-app-proof.md` as the primary path.  
**Runnable sample:** `examples/hub-cashier-next` (port **3020**)  
**Docs runbook:** `apps/lazuar-docs/docs/integrations/run-sample-app.md`

This harness is **curl + optional Python/Node** against Hub and the sample webhook handler. It does not require Aura.

---

## Prerequisites

| Need | Notes |
|------|--------|
| Hub API | `http://localhost:8080` → `$HUB=http://localhost:8080/api/v1` |
| Provision secret | `INTEGRATOR_PROVISION_SECRET` / `X-Lazuar-Provision-Key` |
| BYOK | Active gateway on workspace (Ops) for real create-checkout |
| Sample (handler tests) | Next on **3020** with matching `whsec_` in `.env.local` |

Real sandbox pay still needs a **public Hub** for hop 1 (gateway → Hub). Local hop 2 can use `http://127.0.0.1:3020/webhooks/hub/payments`.

---

## 1. Provision → store `sk_` / `whsec_`

```bash
export HUB=http://localhost:8080/api/v1
export INTEGRATOR_PROVISION_SECRET=…   # from Hub config — never commit

curl -sS -X POST "$HUB/one/integrations/workspaces/provision" \
  -H "Content-Type: application/json" \
  -H "X-Lazuar-Provision-Key: $INTEGRATOR_PROVISION_SECRET" \
  -d '{
    "external_product": "sample-shop",
    "external_org_id": "local-dev-1",
    "display_name": "Hub Cashier Sample",
    "is_test_mode": true,
    "webhook_url": "http://127.0.0.1:3020/webhooks/hub/payments"
  }' | tee /tmp/provision.json
```

Map once (secrets only on first materialization):

```bash
export SK_TEST_KEY=$(jq -r '.api_key.plain_key // empty' /tmp/provision.json)
export WHSEC=$(jq -r '.webhook.secret_key // empty' /tmp/provision.json)
# If re-provision returned null secrets, paste from your secret store.
```

**Redaction for evidence:** store as `sk_test_***` / `whsec_***` only — never paste full keys into git.

Copy into sample:

```bash
# examples/hub-cashier-next/.env.local
# LAZUAR_HUB_BASE_URL=http://localhost:8080/api/v1
# LAZUAR_SK_TEST_KEY=$SK_TEST_KEY
# LAZUAR_WEBHOOK_SECRET=$WHSEC
# NEXT_PUBLIC_APP_URL=http://127.0.0.1:3020
```

Then enable **BYOK** in Ops for the workspace.

---

## 2. Create checkout (Idempotency-Key + metadata.order_id)

```bash
export ORDER_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')
export IDEMPOTENCY_KEY="sample-$ORDER_ID"

curl -sS -X POST "$HUB/integrations/payments/checkouts" \
  -H "Authorization: Bearer $SK_TEST_KEY" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  -d "{
    \"amount\": 25.00,
    \"currency\": \"MYR\",
    \"description\": \"Harness order $ORDER_ID\",
    \"customer_email\": \"guest@example.com\",
    \"success_url\": \"http://127.0.0.1:3020/pay/success?order_id=$ORDER_ID\",
    \"cancel_url\": \"http://127.0.0.1:3020/pay/cancel?order_id=$ORDER_ID\",
    \"metadata\": {
      \"order_id\": \"$ORDER_ID\",
      \"type\": \"sample_order\",
      \"source\": \"second-app-proof-harness\"
    }
  }" | tee /tmp/checkout.json

export CHECKOUT_ID=$(jq -r '.checkout_id' /tmp/checkout.json)
echo "checkout_id=$CHECKOUT_ID order_id=$ORDER_ID"
# Redirect guest to checkout_url for real sandbox pay.
```

**Do not** treat browser `success_url` alone as paid.

---

## 3. Get checkout

```bash
curl -sS "$HUB/integrations/payments/checkouts/$CHECKOUT_ID" \
  -H "Authorization: Bearer $SK_TEST_KEY"
```

Scope: `payments.checkouts:read`.

---

## 4. Fake signed webhook (handler path)

Prefer the sample helper (matches envelope + headers):

```bash
# Sample must be running with LAZUAR_WEBHOOK_SECRET=$WHSEC
# Create a local order first (UI /pay or POST /api/orders), then:

cd examples/hub-cashier-next
ORDER_ID=<local-order-uuid> CHECKOUT_ID=<hub-or-placeholder> \
  LAZUAR_WEBHOOK_SECRET="$WHSEC" \
  node scripts/send-fake-webhook.mjs
```

### Python HMAC (portable)

```bash
export SAMPLE=http://127.0.0.1:3020
export WHSEC=whsec_…   # full string
export ORDER_ID=…      # must exist in sample .data/
export CHECKOUT_ID=…   # optional correlation
export DELIVERY_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')

python3 - <<'PY' | tee /tmp/signed-webhook.env
import hmac, hashlib, json, os, time, uuid
secret = os.environ["WHSEC"].encode("utf-8")
t = str(int(time.time()))
envelope = {
  "id": str(uuid.uuid4()),
  "event_type": "payment.completed",
  "created_at": "2026-08-10T00:00:00Z",
  "data": {
    "event_id": str(uuid.uuid4()),
    "checkout_id": os.environ.get("CHECKOUT_ID", "00000000-0000-0000-0000-000000000001"),
    "gateway": "BILLPLZ",
    "amount": 25.0,
    "currency": "MYR",
    "status": "completed",
    "metadata": {
      "order_id": os.environ["ORDER_ID"],
      "type": "sample_order",
      "source": "second-app-proof-harness",
    },
  },
}
body = json.dumps(envelope, separators=(",", ":"))
# Prefer compact stable body: sample accepts any valid JSON; sign exact bytes.
body = json.dumps(envelope)
msg = f"{t}.{body}".encode("utf-8")
v1 = hmac.new(secret, msg, hashlib.sha256).hexdigest()
print(f"export SIG='t={t},v1={v1}'")
print(f"export BODY={json.dumps(body)}")
print(f"export DELIVERY_ID={os.environ['DELIVERY_ID']}")
PY

# shell: source the exports carefully, or use node scripts/send-fake-webhook.mjs

curl -sS -X POST "$SAMPLE/webhooks/hub/payments" \
  -H "Content-Type: application/json" \
  -H "X-Lazuar-Signature: $SIG" \
  -H "X-Lazuar-Event: payment.completed" \
  -H "X-Lazuar-Delivery-Id: $DELIVERY_ID" \
  --data-binary "$BODY"
```

Use `--data-binary` so body bytes match the signature.

### Negative: bad signature → 401

```bash
curl -sS -o /dev/null -w "%{http_code}\n" -X POST "$SAMPLE/webhooks/hub/payments" \
  -H "Content-Type: application/json" \
  -H "X-Lazuar-Signature: t=1,v1=deadbeef" \
  -H "X-Lazuar-Event: payment.completed" \
  -H "X-Lazuar-Delivery-Id: bad-sig-$(date +%s)" \
  --data-binary '{"id":"x","event_type":"payment.completed","data":{}}'
# expect 401
```

### Replay: same delivery id → single unlock

```bash
# POST the same signed body + X-Lazuar-Delivery-Id twice
# second response: already: true; order remains paid once
```

Unit vectors (no HTTP server):

```bash
pnpm --filter @examples/hub-cashier-next test:webhook
```

---

## 5. Notes: real sandbox pay

| Hop | Direction | Local note |
|-----|-----------|------------|
| Hop 1 | Gateway → Hub | Needs public Hub (`App:ApiBaseUrl` / tunnel) |
| Hop 2 | Hub → sample | `http://127.0.0.1:3020/webhooks/hub/payments` if Hub on same machine |
| Browser | Guest → gateway | Not fulfillment |

Fake webhook proves **handler + unlock + idempotency**. Full multi-hop e2e needs sandbox + tunnel.

---

## Links

| Resource | Path |
|----------|------|
| Sample app | `examples/hub-cashier-next` |
| Sample README | `examples/hub-cashier-next/README.md` |
| VitePress runbook | Integrations → Run sample app (`/integrations/run-sample-app`) |
| Second-app checklist | `apps/lazuar-docs/docs/integrations/second-app-checklist.md` |
| Engineer quickstart | `docs/payments-integration-quickstart.md` |
| Evidence template | `plans/006-sample/evidence/local-e2e.md` |

---

## Redaction guidance (evidence)

- Record **date, branch, ports**, checkout id, delivery id (ok to store).  
- Redact keys: `sk_test_***`, `whsec_***`.  
- Do not commit `.env.local`, `/tmp/provision.json` with live secrets, or raw signed bodies containing secrets.  
- Prefer checklist rows pass/fail over pasting full JSON.
