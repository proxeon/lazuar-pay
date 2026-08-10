# 03 — Sample app architecture (Next.js cashier client)

**Status:** analysis complete 2026-08-10  
**Goal:** Specify a minimal, teachable Next.js App Router sample that proves Hub as multi-app cashier **without** Aura, Billplz SDKs, or monorepo API types packages.

---

## 1. Placement decision

### Recommendation: `examples/hub-cashier-next`

| Option | Path | Verdict |
|--------|------|---------|
| **A (recommended)** | `examples/hub-cashier-next/` | Clear product name; one package = one lesson |
| B | `examples/sample-apps/hub-cashier-next/` | Extra nesting; use only if multiple samples soon |
| C | `apps/lazuar-sample-*` | **Reject** — confuses product apps (ops/portal/admin) and pulls into default app mental model |

**Package name:** `hub-cashier-next` (pnpm filter).  
**Display title:** “Hub Cashier Sample (Next.js)”.

### Workspace membership

Today `pnpm-workspace.yaml`:

```yaml
packages:
  - "apps/*"
  - "packages/*"
```

**Change (D03):**

```yaml
packages:
  - "apps/*"
  - "packages/*"
  - "examples/*"
```

See `07-monorepo-packaging.md` for turbo/CI exclusion rules.

---

## 2. Product story (what the sample simulates)

A **toy merchant backend**:

1. Creates local **orders** (in-memory or SQLite/JSON file — prefer **in-memory Map** for zero ops).  
2. Calls Hub to create a hosted checkout for `order.amount`.  
3. Redirects browser to `checkout_url`.  
4. Receives Hub webhook → marks order `paid`.  
5. Success page polls or revalidates order status (“Payment received” vs “Processing…”).

Domain is intentionally boring so attention stays on Hub integration.

---

## 3. Stack choices

| Choice | Version / approach | Rationale |
|--------|--------------------|-----------|
| Next.js | **16.x** (align with `lazuar-portal` 16.2.x / `lazuar-developers` 16.2.x) | Monorepo familiarity; App Router |
| React | 19.x | Matches portal |
| Language | TypeScript strict | Matches repo |
| CSS | Minimal — one global CSS or Tailwind optional | Prefer **zero** UI kit to reduce noise; plain CSS is OK |
| HTTP client | **plain `fetch`** | No `@repo/api-types-ts`, no openapi-fetch |
| Auth to Hub | Server-only env `HUB_API_KEY` | Never `NEXT_PUBLIC_HUB_API_KEY` |
| Data store | `globalThis` Map in dev / module singleton | Enough for demo; document multi-instance caveat |
| Tests | Optional later unit tests for signature helper only | Manual e2e is DoD |

### Explicitly out of stack

- `@repo/api-types-ts`, `@repo/ui`
- Billplz / Stripe / CHIP SDKs
- Prisma / Postgres (unless later expansion)
- Docker
- Shared monorepo eslint-config **optional** — may use local minimal eslint to avoid coupling

---

## 4. App Router structure

```text
examples/hub-cashier-next/
  package.json
  tsconfig.json
  next.config.ts
  next-env.d.ts
  .env.example
  README.md
  app/
    layout.tsx
    page.tsx                      # Landing: create demo order form
    globals.css
    orders/
      [orderId]/
        page.tsx                  # Order status + pay button / paid badge
    pay/
      success/page.tsx            # success_url target — processing UX
      cancel/page.tsx             # cancel_url target
    api/
      orders/
        route.ts                  # POST create order (local)
        [orderId]/route.ts        # GET order JSON
      checkout/
        route.ts                  # POST create Hub checkout for orderId
      webhooks/
        hub/
          route.ts                # POST Hub outbound receiver
  lib/
    hub.ts                        # base URL helpers, createCheckout()
    orders-store.ts               # in-memory orders
    webhook-verify.ts             # HMAC verify (match C#)
    types.ts                      # local types (not generated)
  scripts/
    provision-and-print-env.sh    # optional outline (06)
```

### Route map

| Method | Path | Role |
|--------|------|------|
| `GET` | `/` | Form: amount, email, description → create order |
| `GET` | `/orders/[orderId]` | Status UI; button “Pay with Hub” |
| `GET` | `/pay/success` | Post-redirect UX; show polling note |
| `GET` | `/pay/cancel` | Cancelled UX |
| `POST` | `/api/orders` | Create local order `{ amount, currency, customer_email, description }` |
| `GET` | `/api/orders/[orderId]` | JSON status for polling |
| `POST` | `/api/checkout` | Body `{ order_id }` → Hub create checkout → `{ checkout_url }` |
| `POST` | `/api/webhooks/hub` | Verify signature → fulfill order |

**Do not** expose a public “mark paid” button in production-shaped sample; optional **dev-only** simulate endpoint is acceptable behind `NODE_ENV=development` for offline demos without gateway.

---

## 5. Environment variables

```bash
# .env.example — never commit real secrets

# Hub API root including /api/v1
HUB_API_BASE_URL=http://localhost:8080/api/v1

# Machine key (from provision or Ops)
HUB_API_KEY=sk_test_replace_me

# Outbound webhook secret (from provision)
HUB_WEBHOOK_SECRET=whsec_replace_me

# Public base of THIS sample (absolute URLs for success/cancel + webhook registration)
# Local: http://localhost:3005
# Use tunnel URL when Hub is remote
APP_BASE_URL=http://localhost:3005

# Optional: default currency
DEFAULT_CURRENCY=MYR
```

### Port recommendation

| Service | Port | Notes |
|---------|------|-------|
| Hub API | **8080** | Canonical monorepo |
| VitePress docs | 5180 | Unrelated |
| Sample Next | **3005** | Avoid clash with developers 3002, portal 3004, admin vite, ops vite |

Add `http://localhost:3005` to Hub `App:CorsOrigins` only if browser calls Hub **directly**. Preferred design: **browser only talks to sample**; sample server talks to Hub → **no CORS change required**.

---

## 6. In-scope vs out-of-scope

### In scope (MVP sample)

- [x] Create local order  
- [x] Server-side Hub checkout create with Idempotency-Key = `order:{id}`  
- [x] Redirect to hosted page  
- [x] Webhook verify + mark paid  
- [x] Success page that does **not** claim paid without status  
- [x] README with curl provision pointer  
- [x] Document raw body requirement  

### Out of scope

- Multi-tenant key mapping UI  
- Ops BYOK configuration automation  
- Refunds / `payment.refunded`  
- Commerce products / subscriptions  
- LHDN  
- User login / sessions beyond demo  
- Production hardening (rate limits, durable DB)  
- Dockerfile / K8s  
- Using provision secret from browser  

---

## 7. Data model (local)

```ts
// lib/types.ts
export type OrderStatus = "pending" | "checkout_created" | "paid" | "failed" | "cancelled";

export interface Order {
  id: string;                 // uuid
  amount: number;             // major units
  currency: string;           // MYR
  description: string;
  customer_email: string;
  status: OrderStatus;
  hub_checkout_id?: string;
  hub_checkout_url?: string;
  paid_at?: string;
  last_event_id?: string;
  created_at: string;
}
```

Metadata sent to Hub:

```json
{
  "order_id": "<local uuid>",
  "type": "sample_order",
  "source": "hub-cashier-next"
}
```

Fulfillment resolution order:

1. `data.metadata.order_id`  
2. Else map `data.checkout_id` → order via stored `hub_checkout_id`  
3. Else 422

---

## 8. Server module sketches

### 8.1 `lib/hub.ts`

```ts
export function hubUrl(path: string): string {
  const base = process.env.HUB_API_BASE_URL?.replace(/\/$/, "");
  if (!base) throw new Error("HUB_API_BASE_URL missing");
  return `${base}${path.startsWith("/") ? path : `/${path}`}`;
}

export async function createIntegrationCheckout(input: {
  amount: number;
  currency: string;
  description: string;
  customer_email: string;
  success_url: string;
  cancel_url: string;
  metadata: Record<string, string>;
  idempotency_key: string;
}) {
  const key = process.env.HUB_API_KEY;
  if (!key) throw new Error("HUB_API_KEY missing");

  const res = await fetch(hubUrl("/integrations/payments/checkouts"), {
    method: "POST",
    headers: {
      Authorization: `Bearer ${key}`,
      "Content-Type": "application/json",
      "Idempotency-Key": input.idempotency_key,
    },
    body: JSON.stringify({
      amount: input.amount,
      currency: input.currency,
      description: input.description,
      customer_email: input.customer_email,
      success_url: input.success_url,
      cancel_url: input.cancel_url,
      metadata: input.metadata,
    }),
  });

  const text = await res.text();
  if (!res.ok) {
    throw new Error(`Hub checkout failed ${res.status}: ${text}`);
  }
  return JSON.parse(text) as {
    checkout_id: string;
    checkout_url?: string;
    status: string;
    gateway: string;
    // …
  };
}
```

Near-final route code: `04-checkout-create-contract.md`.

### 8.2 Orders store caveat

In-memory store **breaks** on:

- Multiple Node workers  
- Serverless cold starts (Vercel)  
- Hot reload clearing module state  

**Document:** “Local single-process demo only.” For longer demos, swap to SQLite file later — not MVP.

---

## 9. Raw body risk (critical)

Next.js App Router **must not**:

```ts
// WRONG — body re-serialized may not match HMAC
const json = await request.json();
verify(JSON.stringify(json), header);
```

**Correct pattern:**

```ts
const rawBody = await request.text();
const ok = verifyHubSignature(rawBody, request.headers.get("x-lazuar-signature"), secret);
const payload = JSON.parse(rawBody);
```

### Middleware risk

If global `middleware.ts` consumes body — avoid. No body-reading middleware on webhook path.

### Body size

Default Next limits are fine for small JSON webhooks.

### Config

Do **not** enable body parsers that alter bytes. App Router Route Handlers use Web Request — good.

Full algorithm: `05-webhook-verify-nextjs.md`.

---

## 10. Security checklist for sample

| Item | Requirement |
|------|-------------|
| Secrets | Server-only |
| Webhook | Signature + skew + constant-time |
| Checkout API | No open redirect to attacker-controlled Hub (fixed `HUB_API_BASE_URL`) |
| success_url | Built from `APP_BASE_URL` only, not client-supplied host |
| Logging | Log order id + delivery id; never secrets |
| CSRF | Server-to-server webhook has no CSRF; form POST to `/api/orders` can use simple same-origin |

---

## 11. UX copy requirements (teaching)

| Screen | Must say |
|--------|----------|
| Success | “Do not treat this page as payment confirmation. Waiting for Hub webhook…” until status paid |
| Order paid | “Unlocked via signed `payment.completed`” |
| README | Link matrices M1/M2/M7 |
| README | “No Billplz/Stripe SDK in this app” |

---

## 12. Relationship to Aura reference client

| Aura | Sample |
|------|--------|
| Real salon domain | Toy order |
| Encrypted key store | `.env` |
| Dual-run legacy gateways | Hub-only |
| Many metadata types | Single `sample_order` |
| Production ops | Local demo |

Docs should say: **pattern** from Aura reference page; **runnable proof** is this sample.

---

## 13. Implementation phases mapping

| Phase | Work |
|-------|------|
| D03 | Scaffold package, env, empty routes, README skeleton |
| D04 | Orders + checkout + UI |
| D05 | Webhook verify + fulfill |
| D06 | Runbook docs page + provision script outline |

---

## 14. Acceptance criteria (architecture-level)

- [ ] Package lives under `examples/hub-cashier-next`  
- [ ] Browser never holds Hub secrets  
- [ ] Webhook uses raw body  
- [ ] Fulfillment only on verified `payment.completed`  
- [ ] Dependencies exclude payment processor SDKs  
- [ ] Plain fetch to Hub  
- [ ] Documented single-process store limitation  
