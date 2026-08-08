# Second-app proof — curl-only cashier harness (no Aura)

**Plan:** 663 Phase 22  
**Goal:** Prove a second product can complete **provision → checkout → (sandbox pay) → webhook** using only Hub public APIs and env config — **zero** imports from the Aura monorepo.  
**Status:** Engineering harness + documented flow. Live sandbox pay + inbound gateway webhook still require env/tunnel (ops).

---

## What “proof” means

| Criterion | Bar |
|-----------|-----|
| No Aura code | This doc + curl / your own server only |
| Generic provision | `external_product` ≠ `aura` |
| Scoped key | Bootstrap `sk_test_` with `payments.checkouts:*` |
| Checkout create | `POST /integrations/payments/checkouts` returns `checkout_url` |
| Webhook path | Endpoint registered; signature algorithm documented |
| Domain unlock | **Your** app marks order paid on `payment.completed` (sample below) |

Marketing claim “any app” is allowed only after this path is exercised in a real env with gateway + delivery SUCCESS (see evidence note at bottom).

---

## Env

```bash
export HUB="${HUB:-http://localhost:8080/api/v1}"
export PROVISION_SECRET="${PROVISION_SECRET:?set INTEGRATOR_PROVISION_SECRET}"
export EXTERNAL_PRODUCT="${EXTERNAL_PRODUCT:-demo-app}"
export EXTERNAL_ORG_ID="${EXTERNAL_ORG_ID:-tenant-$(date +%s)}"
export WEBHOOK_URL="${WEBHOOK_URL:-https://webhook.site/your-uuid}"  # or local requestbin + tunnel
```

Optional: `jq` for JSON parsing.

---

## Step 1 — Provision (generic product)

```bash
PROVISION_RESP=$(curl -sS -X POST "$HUB/one/integrations/workspaces/provision" \
  -H "Content-Type: application/json" \
  -H "X-Lazuar-Provision-Key: $PROVISION_SECRET" \
  -d "{
    \"external_product\": \"$EXTERNAL_PRODUCT\",
    \"external_org_id\": \"$EXTERNAL_ORG_ID\",
    \"display_name\": \"Second App $EXTERNAL_ORG_ID\",
    \"is_test_mode\": true,
    \"webhook_url\": \"$WEBHOOK_URL\"
  }")

echo "$PROVISION_RESP" | jq .

export SK=$(echo "$PROVISION_RESP" | jq -r '.api_key.plain_key // empty')
export WHSEC=$(echo "$PROVISION_RESP" | jq -r '.webhook.secret_key // empty')
export WORKSPACE_ID=$(echo "$PROVISION_RESP" | jq -r '.workspace_id')

test -n "$SK" && test "$SK" != "null" || echo "WARN: no plain_key (idempotent re-run?) — set SK manually"
```

**Expect:** `created: true` first time; `external_product` / `external_org_id` echoed; scopes include `payments.checkouts:write`.

**Idempotent re-run:** same product+org → `created: false`, no new `plain_key`.

---

## Step 2 — Gateway BYOK (human once)

Bootstrap keys cannot write payment config (by design).

1. Open Hub Ops for `$WORKSPACE_ID`.
2. Configure Billplz sandbox or Stripe test keys.
3. Confirm gateway active.

Without this, checkout returns `PAYMENTS_NOT_CONFIGURED` (422).

---

## Step 3 — Create checkout

```bash
CHECKOUT_RESP=$(curl -sS -X POST "$HUB/integrations/payments/checkouts" \
  -H "Authorization: Bearer $SK" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: second-app-$(date +%s)" \
  -d '{
    "amount": 5.00,
    "currency": "MYR",
    "description": "Second-app harness order",
    "customer_email": "guest@example.com",
    "success_url": "https://example.com/success",
    "cancel_url": "https://example.com/cancel",
    "metadata": {
      "order_id": "harness-1",
      "type": "second_app_demo"
    }
  }')

echo "$CHECKOUT_RESP" | jq .
export CHECKOUT_ID=$(echo "$CHECKOUT_RESP" | jq -r '.checkout_id')
export CHECKOUT_URL=$(echo "$CHECKOUT_RESP" | jq -r '.checkout_url')
echo "Open: $CHECKOUT_URL"
```

---

## Step 4 — Pay (sandbox) + inbound webhook

1. Open `$CHECKOUT_URL` in a browser; complete Billplz/Stripe sandbox pay.
2. Provider must reach Hub: `POST /api/v1/webhooks/payments/{gateway}/{tenantId}` (public Hub base).
3. Hub emits outbound `payment.completed` to `$WEBHOOK_URL`.

Local tip: tunnel Hub API; set public bases per issue **003** runbooks. Browser open alone ≠ fulfillment.

---

## Step 5 — Verify signature in your app (sample)

Minimal Python verifier (stdlib only — still **not** Aura code):

```python
#!/usr/bin/env python3
"""stdin: raw body; env: WHSEC, SIG_HEADER (X-Lazuar-Signature value)."""
import hashlib, hmac, os, sys

secret = os.environ["WHSEC"].encode()
header = os.environ["SIG_HEADER"]  # t=…,v1=…
body = sys.stdin.buffer.read()

parts = dict(p.split("=", 1) for p in header.split(",") if "=" in p)
t, v1 = parts["t"], parts["v1"]
msg = f"{t}.".encode() + body
expected = hmac.new(secret, msg, hashlib.sha256).hexdigest()
assert hmac.compare_digest(expected, v1.lower()), "bad signature"
print("ok", t)
```

Usage after capturing a delivery body:

```bash
export SIG_HEADER='t=1700000000,v1=...'
python3 verify.py < delivery.json
```

Your domain unlock (pseudo):

```text
if event_type == payment.completed and signature ok and order not yet paid:
  mark order paid (idempotent on checkout_id / event_id)
```

---

## Step 6 — Poll status (optional)

```bash
curl -sS "$HUB/integrations/payments/checkouts/$CHECKOUT_ID" \
  -H "Authorization: Bearer $SK" | jq .
```

---

## Isolation check (optional)

Provision a second `external_org_id` under the same product. Confirm:

- Different `workspace_id` and key.
- Key A cannot read checkout of workspace B (404 / forbidden).

---

## Evidence log (fill when run live)

| Field | Value |
|-------|--------|
| Date | |
| Hub base | |
| external_product / org | |
| workspace_id | |
| checkout_id | |
| Gateway | billplz / stripe sandbox |
| Outbound delivery | SUCCESS / FAIL |
| Signature verified | Y/N |
| Notes | |

Copy filled table into Aura `idea/022-remaining/evidence/PHASE22-second-app.md` when live evidence exists.

---

## Explicit non-claims until live evidence

- This markdown alone does **not** close issue **009** production bar.
- It **does** ship the integrator contract + harness so a second app team can finish without reading Aura sources.
