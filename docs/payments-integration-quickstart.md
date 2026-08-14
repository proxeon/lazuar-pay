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

## 2. Provision a workspace (multi-product)

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

### Aura-compatible (still supported)

```json
{
  "aura_org_id": "11111111-1111-1111-1111-111111111111",
  "display_name": "Salon Melati",
  "is_test_mode": true,
  "webhook_url": "https://aura.example/api/v1/webhooks/hub/payments"
}
```

- `external_product` defaults to **`aura`**.
- `external_org_id` aliases `aura_org_id` (either is accepted).
- For product `aura`, org id must be a **GUID**. Other products accept any stable non-empty string (max 128).
- Idempotent on `(external_product, external_org_id)`.
- Response includes `api_key.plain_key` and `webhook.secret_key` **only on first materialization**. Store them once.

Default bootstrap scopes: `payments.checkouts:write`, `payments.checkouts:read`, `webhooks.endpoints:manage`.

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
| Introspect pasted K1 | `GET /api/v1/integrations/payments/me` | Bearer K1 | **Missing** — TypeSpec **draft** P00.31; implement P02. Aura must not trust a typed workspace id (L11) |
| Payments ready? (vault live, no K2 material) | fields on `/me` or future `GET /integrations/payments/config` | Bearer K1; optional `payments.config:read` | **Missing** — P02.40. Do not treat “row has workspace id” as Ready (L15) |
| Register guest webhook | intended: after introspect, `POST /api/v1/one/workspaces/{workspace_id}/webhooks` | Bearer K1 + `webhooks.endpoints:manage` | **Partial** — route **exists** for humans / machine keys **with** manage scope. Create is **not** same-URL idempotent today (always new row + new `whsec_`). P02.32 must add idempotent same-URL + no re-reveal. Aura P00 client does **not** call this yet |
| Guest money events | signed `payment.completed` / `payment.failed` → Aura `/webhooks/hub/payments` | `X-Lazuar-Signature` | **Live** envelope `{ id, event_type, created_at, data }` (see §9 / CONTRACT-webhook-v1 amendment) |
| SaaS money events | second Aura URL | n/a this product | **Out of Payments M1** — Type-P / Commerce later |

### 8.1 `sk_` prefix collision (P00.33)

Pay mints K1 as `sk_test_` / `sk_live_` (`GenerateApiCredentialCommand`, provision key helper). Stripe merchant secrets (K2) use the **same prefixes**.

Aura **must not** accept a pasted key on regex alone (P04). Decision **deferred to P02.20**:

| Option | Meaning |
|--------|---------|
| **A** | New prefixes `lpk_test_` / `lpk_live_` for newly minted keys; old `sk_*` still verify |
| **B** (recommended now) | Keep `sk_*`; Aura rejects keys that fail Pay introspect even if they look like Stripe |

Tick in P02: `Prefix decision = ________`

### 8.2 `PRESET_AURA` omits webhook manage (P00.34)

Ops UI: `apps/lazuar-ops/src/modules/workspace/pages/ApiKeysPage.tsx`

```ts
const PRESET_AURA = ["payments.checkouts:write", "payments.checkouts:read"] as const;
```

The scope **exists** in `SCOPE_CATALOG` and in `PlatformApiScopes.WebhooksEndpointsManage`. Provision bootstrap (`DefaultAuraIntegratorScopes`) **includes** `webhooks.endpoints:manage`. A salon who clicks **Aura payments** in the create-key dialog gets a K1 that can checkout but **cannot** `POST /one/workspaces/{id}/webhooks`.

P02.01 will add `webhooks.endpoints:manage` (and optionally `payments.config:read`) to that preset. **Do not change the preset in P00.**
