# Payments cashier quickstart (M2M integration)

**Audience:** Any server app integrating Lazuar Hub as a **BYOK cashier** (not Commerce catalog, not LHDN, not Paddle).  
**Repos:** Hub (`lazuar-hub`). Aura is a first-party consumer of this surface — see the consume/gap table in §8.  
**OpenAPI:** Scalar → Developers hub `/payments` · TypeSpec `packages/api-spec/docs-payments.tsp` · generated `packages/api-spec/dist/payments/openapi.yaml`.

---

## 0. Product lines (do not mix)

| Product | What it is | Primary path |
|---------|------------|--------------|
| **Payments (this guide)** | Ad-hoc amount + opaque metadata → gateway host page → `payment.*` webhooks | `POST /api/v1/integrations/payments/checkouts` |
| **Commerce** | Hub-native products, public buy links, subscription lifecycle | `/public/commerce/*` + `subscription.*` / `order.completed` |
| **LHDN** | Malaysian e-invoice | `/lhdn/*` + `invoice.*` |
| **Aura Plan / Paddle** | Salon SaaS subscription | **Not Hub** |

Email-provider configuration gates **Commerce** product activation in some flows. It does **not** block M2M Payments checkouts.

---

## 1. Prerequisites

1. Hub API base (examples):
   - Local: `http://localhost:8080/api/v1` (or your mapped port)
   - Prod: `https://hub.lazuar.com/api/v1`
2. **Provision secret** (`INTEGRATOR_PROVISION_SECRET` / `X-Lazuar-Provision-Key`) **or** SUPER_ADMIN JWT for workspace provision.
3. At least one **active BYOK gateway** on the workspace (Ops → Payment settings: Billplz / Stripe / …).
4. Public URL for **your** webhook receiver (Hub must POST to it).
5. For local provider callbacks: Hub inbound webhooks must also be public (tunnel) — see ops runbooks.

---

## 2. Integrator workspace provision

`POST /api/v1/one/integrations/workspaces/provision`

### Preferred (generic)

```bash
curl -sS -X POST "$HUB/one/integrations/workspaces/provision" \
  -H "Content-Type: application/json" \
  -H "X-Lazuar-Provision-Key: $PROVISION_SECRET" \
  -d '{
    "external_product": "demo-app",
    "external_org_id": "tenant-001",
    "display_name": "Demo App Tenant 001",
    "is_test_mode": true,
    "webhook_url": "https://your-app.example/webhooks/hub/payments",
    "owner_email": "you@example.com"
  }'
```

### Legacy / Aura example (still supported)

```json
{
  "aura_org_id": "11111111-1111-1111-1111-111111111111",
  "display_name": "Salon Melati",
  "is_test_mode": true,
  "webhook_url": "https://aura.example/api/v1/webhooks/hub/payments"
}
```

- Canonical body is `external_product` + `external_org_id`. Aura is **one example integrator**, not a prerequisite.
- Legacy: if the body has **only** `aura_org_id` and omits `external_product`, product defaults to `aura`.
- If the body has `external_org_id` and omits `external_product` → **400** `external_product_required`.
- `aura_org_id` aliases `external_org_id` when product is `aura`.
- Alias `aurabook` is accepted and stored as `aura` (same unique pair; do not send `aurabook` expecting a new workspace).
- For product `aura`, org id must be a **GUID**. Other products accept any stable non-empty string (max 128).
- Idempotent on `(external_product, external_org_id)`.
- Response includes `api_key.plain_key` and `webhook.secret_key` **only on first materialization**. Store them once.

Default bootstrap scopes: `payments.checkouts:write`, `payments.checkouts:read`, `webhooks.endpoints:manage`.

## Mint a Payments integrator key (AuraBook Guest payments)

1. Sign in to Lazuar Pay Ops → **Developer → API Keys**.
2. **Create Key**. Name is required (e.g. `AuraBook guest`).
3. Environment: **Test** (`sk_test_…`) or **Live** (`sk_live_…`).
4. Click preset **Payments integrator** (scopes: `payments.checkouts:write`, `payments.checkouts:read`, `webhooks.endpoints:manage`). Do not add LHDN scopes.
5. Create. Copy the secret **once**. It is a **Lazuar Pay** secret, not a Stripe secret — same `sk_` prefix, different system.
6. Paste into AuraBook **Guest payments → Lazuar Pay** (P04/P05). Aura will `GET /integrations/payments/me` then `POST /one/workspaces/{workspace_id}/webhooks`. You can probe first:

```bash
curl -sS "$HUB/integrations/payments/me" \
  -H "Authorization: Bearer $SK"
```

Expect `200` with `workspace_id` + `scopes`. The body must **not** contain `sk_` or `whsec_`.

---

## 3. Create a checkout

```bash
curl -sS -X POST "$HUB/integrations/payments/checkouts" \
  -H "Authorization: Bearer $SK_TEST_KEY" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: demo-order-42" \
  -d '{
    "amount": 25.00,
    "currency": "MYR",
    "description": "Demo order 42",
    "customer_email": "guest@example.com",
    "success_url": "https://your-app.example/pay/success",
    "cancel_url": "https://your-app.example/pay/cancel",
    "metadata": {
      "order_id": "ord_42",
      "type": "demo_order"
    }
  }'
```

- Redirect the guest to `checkout_url`.
- **Do not** treat browser redirect alone as paid — wait for signed webhook.
- Poll `GET /integrations/payments/checkouts/{checkout_id}` with `payments.checkouts:read` if needed.

### Metadata recommendations

- Treat as **opaque** string map. Keep domain IDs in your app (`order_id`, `invoice_ref`, …).
- Hub may stamp `hub_workspace_id`, `checkout_id`, `tenant_id`, `hub_checkout_kind=integration`.
- Do not expect Hub Payments adapters to branch on Aura-specific keys.

### Stable error codes (`ProblemDetails.extensions.code` / title)

| Code | Typical status |
|------|----------------|
| `PAYMENTS_NOT_CONFIGURED` | 422 |
| `AMOUNT_INVALID` / `AMOUNT_BELOW_MINIMUM` | 400 |
| `CURRENCY_INVALID` | 400 |
| `URLS_REQUIRED` | 400 |
| `METADATA_INVALID` | 400 |
| `IDEMPOTENCY_CONFLICT` | 409 |
| `GATEWAY_ERROR` | 502 |
| `CHECKOUT_NOT_FOUND` | 404 |
| `UNAUTHORIZED` / `FORBIDDEN` | 401 / 403 |
| `KEY_MODE_MISMATCH` | 409 (test K1 vs Stripe `sk_live_` K2, or the reverse) |

---

## 4. Verify webhooks

Events: **`payment.completed`**, **`payment.failed`**.

Headers (workspace fan-out):

- `X-Lazuar-Signature: t=<unix>,v1=<hex>`
- `X-Lazuar-Event`
- `X-Lazuar-Delivery-Id`
- `X-Lazuar-Webhook-Id`

### Signature algorithm

1. Read raw body bytes as UTF-8 string `body`.
2. Parse `t` and `v1` from `X-Lazuar-Signature`.
3. Reject if `|now - t|` is outside your skew window (e.g. 5 minutes).
4. Compute `HMAC-SHA256(secret, "{t}.{body}")` hex lowercase.
5. Compare `v1` with constant-time equality.

### Pseudo-code

```text
signed = t + "." + raw_body
expected = hex(hmac_sha256(whsec_secret, signed))
assert constant_time_eq(expected, v1)
```

### Test vector (shape)

Use your own `whsec_` and body in unit tests. Frozen hex digests for a public sample may be published under Developers → Webhooks; until then, generate fixtures with:

```bash
# illustrative only — replace secret and body
python3 - <<'PY'
import hmac, hashlib
secret = b"whsec_test_secret"
t = "1700000000"
body = b'{"event_type":"payment.completed","checkout_id":"00000000-0000-0000-0000-000000000001"}'
msg = t.encode() + b"." + body
print("v1=" + hmac.new(secret, msg, hashlib.sha256).hexdigest())
PY
```

Fulfillment rules:

- Unlock domain state only after valid signature + successful business rules.
- Idempotent on `event_id` / `checkout_id` so retries never double-fulfill.

---

## 5. Key lifecycle (honest)

| Action | Today |
|--------|--------|
| Mint | Ops UI or `POST /one/api-keys` (human OrgAdmin) |
| Reveal | Once at create / first provision |
| Revoke | `DELETE /one/api-keys/{id}` |
| Rotate | Mint new + swap app secret + revoke old (single-cut; dual-key window not productized) |
| Last used | **Not persisted yet** — backlog (see Phase 21 notes) |
| Never | Grant payment-config **write** to machine keys by default |

---

## 6. Versioning policy (v1)

- **Additive** JSON fields and new optional scopes are non-breaking.
- **Breaking** = rename/remove fields, change auth scheme, change signature algorithm, change idempotency semantics.
- Breaking changes require a new API major (`/api/v2/…`) or explicit deprecation window documented in Hub releases.
- OpenAPI/TypeSpec under `packages/api-spec` is the public SSoT for integrator paths.

---

## 7. Next steps

- Multi-app proof: [Second-app checklist](../apps/lazuar-docs/docs/integrations/second-app-checklist.md)
- Curl harness: [plans/006-sample/harness/second-app-proof.md](../plans/006-sample/harness/second-app-proof.md)
- Sample runbook: [Run sample app](../apps/lazuar-docs/docs/integrations/run-sample-app.md) · app `examples/hub-cashier-next` (port **3020**)
- Event catalog UI: Developers hub `/webhooks`
- Auth & scopes: Developers hub `/auth`

---

## 8. Aura consume / gap table (P00)

AuraBook consumes this public surface. Aura does **not** invent `/integrations/payments/*` shapes. Missing rows are **Pay** tickets (P02), not Aura-private RPCs.

| Concern | Path | Auth | Status for Aura |
|---------|------|------|-----------------|
| Create guest checkout | `POST /api/v1/integrations/payments/checkouts` | Bearer K1 + `payments.checkouts:write` | **Live** — Aura `HubPaymentsClient.CreateCheckoutAsync` |
| Get checkout (UX / reconcile only; **not** a money event) | `GET /api/v1/integrations/payments/checkouts/{id}` | Bearer K1 + `payments.checkouts:read` | **Live** |
| Provision Type-T workspace (K0 hatch) | `POST /api/v1/one/integrations/workspaces/provision` | `X-Lazuar-Provision-Key` (not salon K1) | **Live** — Aura Connect only |
| Introspect pasted K1 | `GET /api/v1/integrations/payments/me` | Bearer K1 + any `payments.*` scope | **Live** — `GET /api/v1/integrations/payments/me` |
| Payments ready? (vault live, no K2 material) | fields on `/me` | Bearer K1 (no `payments.config:read` required) | **Live on `/me`** — `has_active_gateway`, `gateway_names` (no K2) |
| Register guest webhook | after introspect, `POST /api/v1/one/workspaces/{workspace_id}/webhooks` | Bearer K1 + `webhooks.endpoints:manage` | **Live** — same POST; same-URL idempotent; rotate at `POST …/webhooks/{id}/rotate-secret`; DELETE disables; `whsec` encrypted at rest |
| Guest money events | signed `payment.completed` / `payment.failed` → Aura `/webhooks/hub/payments` | `X-Lazuar-Signature` | **Live** envelope `{ id, event_type, created_at, data }` (see §9 / CONTRACT-webhook-v1 amendment) |
| SaaS money events | second Aura URL | n/a this product | **Out of Payments M1** — Type-P / Commerce later |

### 8.1 `sk_` prefix collision (P00.33)

Pay mints K1 as `sk_test_` / `sk_live_` (`GenerateApiCredentialCommand`, provision key helper). Stripe merchant secrets (K2) use the **same prefixes**.

`Prefix decision = B`

Keep minting `sk_test_` / `sk_live_`. Aura P04 **must not** accept a key that only matches an `sk_live_` regex — it must `GET /me` and treat 401/403 as `PAY_KEY_INVALID`. If the pasted value looks like Stripe but Pay 401s: “This looks like a Stripe secret. Mint a Lazuar Pay key.”

### 8.2 Payments integrator preset includes webhook manage

Ops UI: `apps/lazuar-ops/src/modules/workspace/pages/ApiKeysPage.tsx`

```ts
const PRESET_PAYMENTS_INTEGRATOR = [
  "payments.checkouts:write",
  "payments.checkouts:read",
  "webhooks.endpoints:manage",
] as const;
```

Matches `PlatformApiScopes.DefaultAuraIntegratorScopes`. No LHDN scopes. `payments.config:read` is optional via catalog checkbox.

### 8.3 Billplz sandbox vs live (known gap)

Billplz sandbox vs live is **not** selected by the K1 prefix today. Hub calls `https://www.billplz-sandbox.com` unless `App:ApiBaseUrl` contains `lazuar.com`, in which case it calls production Billplz. A `sk_live_` integrator key against a non-prod Hub still hits Billplz **sandbox**. Aura may warn: “Billplz environment follows Hub base URL, not the key prefix.”

`KEY_MODE_MISMATCH` only compares Stripe-shaped K2 (`sk_test_` / `sk_live_` with the trailing underscore). Fixture keys like `sk_test` and Billplz/CHIP/Razorpay secrets are not gated.
