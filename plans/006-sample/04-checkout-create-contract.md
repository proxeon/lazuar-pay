# 04 — Checkout create contract (exact)

**Status:** analysis complete 2026-08-10  
**SSoT (runtime):** `IntegrationEndpoints.cs`, `CreateIntegrationCheckoutCommandHandler.cs`, `PaymentErrorCodes`  
**SSoT (contract):** `packages/api-spec/modules/payments/models.tsp`, `routes.tsp`  
**Engineer twin:** `docs/payments-integration-quickstart.md`

---

## 1. Endpoint

| Item | Value |
|------|--------|
| Method | `POST` |
| Path | `/api/v1/integrations/payments/checkouts` |
| Note | **No trailing slash** (OpenAPI + Minimal aligned) |
| Auth | `Authorization: Bearer sk_test_…` or `sk_live_…` |
| Scope | `payments.checkouts:write` |
| Policy name (host) | `IntegrationPaymentsCheckoutsWrite` |
| CORS | Group `.RequireCors()` — irrelevant for server-side sample |

### Get (poll)

| Item | Value |
|------|--------|
| Method | `GET` |
| Path | `/api/v1/integrations/payments/checkouts/{checkoutId}` |
| Scope | `payments.checkouts:read` (write implies read in product policy) |
| `checkoutId` | GUID |

Full URL examples:

```text
http://localhost:8080/api/v1/integrations/payments/checkouts
https://hub.lazuar.com/api/v1/integrations/payments/checkouts
```

---

## 2. Request — snake_case JSON

Matches TypeSpec `CreateIntegrationCheckoutRequestDto` and ASP.NET generated / dual DTO snake_case fields.

### Body fields

| Field | Type | Required | Rules (handler) |
|-------|------|----------|-----------------|
| `amount` | number (float64/decimal) | yes | Positive; gateway minimum enforced (`AMOUNT_BELOW_MINIMUM`) |
| `currency` | string | yes | Trimmed, uppercased; validated with amount rules |
| `description` | string | yes | Non-empty, max **200** chars |
| `customer_email` | string | yes | Must contain `@` (lightweight validation) |
| `customer_name` | string | no | Max **120** chars |
| `success_url` | string | yes | Absolute `http` or `https` |
| `cancel_url` | string | yes | Absolute `http` or `https` |
| `gateway_name` | string | no | One of `STRIPE`, `BILLPLZ`, `CHIP`, `RAZORPAY` (case-insensitive); else workspace default active |
| `setup_future_usage` | boolean | no | Default false |
| `idempotency_key` | string | no | Max **200**; prefer header instead |
| `metadata` | object string→string | no | Normalized/validated; size limits → `METADATA_INVALID` |

### Headers

| Header | Required | Notes |
|--------|----------|-------|
| `Authorization` | yes | `Bearer sk_…` |
| `Content-Type` | yes | `application/json` |
| `Idempotency-Key` | strongly recommended | **Wins over** body `idempotency_key` if both set |

Resolution (`IntegrationEndpoints.ResolveIdempotencyKey`):

1. Non-empty `Idempotency-Key` header  
2. Else body `idempotency_key`  
3. Else null (no idempotency — each call new session)

### Example request

```http
POST /api/v1/integrations/payments/checkouts HTTP/1.1
Host: localhost:8080
Authorization: Bearer sk_test_…
Content-Type: application/json
Idempotency-Key: order:11111111-1111-1111-1111-111111111111

{
  "amount": 25.0,
  "currency": "MYR",
  "description": "Demo order 42",
  "customer_email": "guest@example.com",
  "customer_name": "Guest",
  "success_url": "http://localhost:3005/pay/success?order_id=11111111-1111-1111-1111-111111111111",
  "cancel_url": "http://localhost:3005/pay/cancel?order_id=11111111-1111-1111-1111-111111111111",
  "metadata": {
    "order_id": "11111111-1111-1111-1111-111111111111",
    "type": "sample_order",
    "source": "hub-cashier-next"
  }
}
```

### curl

```bash
export HUB=http://localhost:8080/api/v1
export SK_TEST_KEY=sk_test_…

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

---

## 3. Response — snake_case JSON

TypeSpec `IntegrationCheckoutResponseDto` / endpoint `ToResponse`:

| Field | Type | Notes |
|-------|------|-------|
| `checkout_id` | string (GUID) | Hub session id |
| `checkout_url` | string? | Redirect guest here; may be null if failed mid-flight |
| `gateway` | string | Resolved gateway name |
| `status` | string | `open` \| `completed` \| `failed` \| `expired` (and any domain constants) |
| `amount` | number | Echo |
| `currency` | string | Echo normalized |
| `provider_session_id` | string? | Bill id / Stripe session id etc. |
| `gateway_transaction_id` | string? | Filled after money events typically |
| `expires_at` | string (ISO datetime) | UTC |
| `metadata` | object | Includes client keys + Hub stamps |

### Example 200

```json
{
  "checkout_id": "0193a0b0-0000-7000-8000-000000000001",
  "checkout_url": "https://www.billplz.com/bills/…",
  "gateway": "BILLPLZ",
  "status": "open",
  "amount": 25.0,
  "currency": "MYR",
  "provider_session_id": "…",
  "gateway_transaction_id": null,
  "expires_at": "2026-08-10T12:00:00+00:00",
  "metadata": {
    "order_id": "11111111-1111-1111-1111-111111111111",
    "type": "sample_order",
    "source": "hub-cashier-next",
    "checkout_id": "0193a0b0-0000-7000-8000-000000000001",
    "hub_workspace_id": "…",
    "hub_checkout_kind": "integration"
  }
}
```

Exact stamp keys depend on `IntegrationCheckoutMetadata.Stamp` — treat extra keys as additive.

---

## 4. Idempotency semantics

| Case | Result |
|------|--------|
| Same key + same fingerprint | Replay mapped session (200) |
| Same key + different fingerprint | **409** `IDEMPOTENCY_CONFLICT` |
| Race on unique (org, key) | Loser loads winner and replay/conflict |
| No key | Always new checkout id |

Fingerprint inputs (handler): amount, currency, success/cancel URLs, description, email, customer name, gateway preferred, setup_future_usage, client metadata (normalized).

**Sample recommendation:** `Idempotency-Key: order:{orderId}` so retries are safe.

---

## 5. Validation & gateway resolution order

1. `ValidateRequest` (amount/currency, description, email, absolute URLs)  
2. Normalize fields; validate gateway name allow-list  
3. Normalize metadata  
4. Idempotency lookup  
5. **`ResolveGatewayNameAsync` with `requireActiveGateway: true`** — unconfigured workspace never gets half-open if resolve fails first  
6. Insert session  
7. `GenerateAsync` against gateway  
8. `MarkProviderIssued` or `MarkFailed` + rethrow  

Allowed gateways: `STRIPE`, `BILLPLZ`, `CHIP`, `RAZORPAY`.

---

## 6. Error map (ProblemDetails)

Shape:

```json
{
  "status": 422,
  "title": "PAYMENTS_NOT_CONFIGURED",
  "detail": "No active payment gateway is configured for this workspace.",
  "code": "PAYMENTS_NOT_CONFIGURED"
}
```

(`title` = code; `extensions.code` also set.)

| Code | HTTP | When |
|------|------|------|
| `UNAUTHORIZED` | 401 | Missing tenant / bad auth at endpoint gate |
| `FORBIDDEN` | 403 | Auth middleware scope failure (policy) |
| `AMOUNT_INVALID` | 400 | Amount invalid |
| `AMOUNT_BELOW_MINIMUM` | 400 | Below min for currency/gateway rules |
| `CURRENCY_INVALID` | 400 | Bad currency |
| `URLS_REQUIRED` | 400 | success/cancel not absolute http(s) |
| `METADATA_INVALID` | 400 | Metadata validation |
| `INVALID_REQUEST` | 400 | description/email/gateway_name/idempotency length etc. |
| `IDEMPOTENCY_CONFLICT` | 409 | Key reuse different body |
| `PAYMENTS_NOT_CONFIGURED` | 422 | No active BYOK / disabled gateway |
| `GATEWAY_ERROR` | 502 | Provider create failed |
| `CHECKOUT_NOT_FOUND` | 404 | GET unknown id (wrong tenant → not found) |

### Sample app error mapping

| Hub code | UX |
|----------|-----|
| `PAYMENTS_NOT_CONFIGURED` | “Ask ops to enable Billplz/Stripe on this workspace.” |
| `IDEMPOTENCY_CONFLICT` | Dev error — do not change order fields under same key |
| `GATEWAY_ERROR` | Retry later |
| `UNAUTHORIZED` | Check `HUB_API_KEY` |
| default | Show status + detail in dev only |

---

## 7. Near-final Next.js Route Handler

`examples/hub-cashier-next/app/api/checkout/route.ts`:

```ts
import { NextRequest, NextResponse } from "next/server";
import { getOrder, updateOrder } from "@/lib/orders-store";
import { createIntegrationCheckout } from "@/lib/hub";

export const runtime = "nodejs";

export async function POST(req: NextRequest) {
  let body: { order_id?: string };
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: "invalid_json" }, { status: 400 });
  }

  const orderId = body.order_id?.trim();
  if (!orderId) {
    return NextResponse.json({ error: "order_id_required" }, { status: 400 });
  }

  const order = getOrder(orderId);
  if (!order) {
    return NextResponse.json({ error: "order_not_found" }, { status: 404 });
  }

  if (order.status === "paid") {
    return NextResponse.json(
      { error: "already_paid", checkout_id: order.hub_checkout_id },
      { status: 409 },
    );
  }

  const appBase = process.env.APP_BASE_URL?.replace(/\/$/, "");
  if (!appBase) {
    return NextResponse.json({ error: "APP_BASE_URL missing" }, { status: 500 });
  }

  try {
    const session = await createIntegrationCheckout({
      amount: order.amount,
      currency: order.currency,
      description: order.description,
      customer_email: order.customer_email,
      success_url: `${appBase}/pay/success?order_id=${encodeURIComponent(order.id)}`,
      cancel_url: `${appBase}/pay/cancel?order_id=${encodeURIComponent(order.id)}`,
      idempotency_key: `order:${order.id}`,
      metadata: {
        order_id: order.id,
        type: "sample_order",
        source: "hub-cashier-next",
      },
    });

    updateOrder(order.id, {
      status: "checkout_created",
      hub_checkout_id: session.checkout_id,
      hub_checkout_url: session.checkout_url,
    });

    return NextResponse.json({
      order_id: order.id,
      checkout_id: session.checkout_id,
      checkout_url: session.checkout_url,
      status: session.status,
      gateway: session.gateway,
    });
  } catch (e) {
    const message = e instanceof Error ? e.message : "checkout_failed";
    // Prefer parsing ProblemDetails code from Hub body in hub.ts
    return NextResponse.json({ error: "hub_error", detail: message }, { status: 502 });
  }
}
```

### Improved `createIntegrationCheckout` error parse

```ts
export class HubHttpError extends Error {
  constructor(
    public status: number,
    public code: string | undefined,
    public detail: string,
  ) {
    super(detail);
  }
}

// inside createIntegrationCheckout after res.text():
if (!res.ok) {
  let code: string | undefined;
  let detail = text;
  try {
    const pd = JSON.parse(text) as { title?: string; detail?: string; code?: string };
    code = pd.code ?? pd.title;
    detail = pd.detail ?? text;
  } catch { /* raw */ }
  throw new HubHttpError(res.status, code, detail);
}
```

Map `HubHttpError` → Next status:

| `code` | Next status to browser |
|--------|------------------------|
| `PAYMENTS_NOT_CONFIGURED` | 422 |
| `IDEMPOTENCY_CONFLICT` | 409 |
| `UNAUTHORIZED` / `FORBIDDEN` | 401 / 403 |
| else | 502 |

---

## 8. GET checkout (optional poll)

```ts
export async function getIntegrationCheckout(checkoutId: string) {
  const res = await fetch(hubUrl(`/integrations/payments/checkouts/${checkoutId}`), {
    headers: { Authorization: `Bearer ${process.env.HUB_API_KEY}` },
    cache: "no-store",
  });
  // … same error handling
}
```

Sample success page may poll **local** order status (updated by webhook) rather than Hub — simpler and teaches correct source of fulfillment truth (webhook → domain).

---

## 9. Auth scopes bootstrap

Default provision scopes (docs/runtime):

- `payments.checkouts:write`  
- `payments.checkouts:read`  
- `webhooks.endpoints:manage`  

Sample only needs write (+ read if polling Hub).

---

## 10. Contract honesty / drift risks

| Risk | Mitigation |
|------|------------|
| TypeSpec vs dual DTO casing | Sample uses snake_case only |
| Trailing slash clients | Document no trailing slash |
| `expires_at` null vs date | Treat as optional in TS |
| Metadata stamp key names | Do not hard-require stamp keys for fulfill; use your `order_id` |
| Amount as float JSON | Send decimals carefully; MYR use 2 dp |

---

## 11. Manual test cases (checkout only)

1. Happy path → 200 + `checkout_url`  
2. Replay same Idempotency-Key → same `checkout_id`  
3. Change amount same key → 409  
4. Empty BYOK workspace → 422  
5. Relative success_url → 400  
6. Missing Bearer → 401  
7. GET other workspace checkout → 404  

---

## 12. Implementation checklist

- [ ] `lib/hub.ts` create + error parse  
- [ ] `app/api/checkout/route.ts`  
- [ ] Wire UI button on order page  
- [ ] Document env in README  
- [ ] Cross-link docs create-checkout page  
