# 013 — Implementation checklists (Bar B)

**Date:** 21 August 2026  
**Style:** Many **small** phase files. One phase ≈ one commit (or a tightly scoped PR).  
**How-to evidence:** parent [`../01`](../01-production-ready-bar.md)–[`../10`](../10-ci-observability-decommission.md). Do not treat this folder as a substitute for those papers.  
**Freeze:** [`decisions.md`](./decisions.md) (locked in B00).

**This program is Bar B:** the 011 dogfood sentence, lived on `apps/lazuar-pay` **8081**, `lazuar-pay-merchant` **5178**, `lazuar-pay-checkout` **5179**. It is not Hub parity, not Hub dark, not Bar C (renewals, magic-link portal, second rail).

012 C99 (whoami / org ready / fixture checkout) is **already closed**. This folder **unparks** 012 P10 (merchant OIDC), P50 (money), P20/P30 (keys + One HMAC) **only as far as Bar B**. 012 P60 (ops/portal on 8081) stays **refused**.

## Rule: not one mega-PR

| Do | Don’t |
|----|--------|
| One intent per phase | OIDC + Postgres + Stripe + journal in one tip |
| Fake One / fake PSP in `task pay:test` | Require Zitadel, CHIP, or Hub compose in CI |
| Merchant SPA public PKCE | Pay password form, Hub cookie, `id_token` as Bearer |
| Buyer origin with **no** One login | OIDC on `:5179` |
| One Pay DB, one schema, one migrator | Nine `*DbContext`, MediatR, Hub module schemas |
| Same HTTP request: verify webhook → fulfill | `GatewayPaymentCompleted` event bus |
| Keep listen **8081** | Bind 8080 or `task dev` Hub |
| CORS 5178 + 5179 only | Add `:3003` / `:3004` “to demo” |

## Track map

```text
B00 Align & freeze
  │
  ├─ Track M Merchant OIDC (serial)     M10 → … → M27
  │
  ├─ Track D Database (after B00; D17 after D12)
  │     D10 → D16 process
  │     D17 → D29 tables (one table-or-concern each)
  │
  ├─ Track CAT Catalog (after D18 + M24)
  │
  ├─ Track K Buyer pay page (after D17; public resource before UI)
  │
  ├─ Track G Rails (after D20 + CAT; G18 before F)
  │
  ├─ Track F Fulfillment (after G21; one handler)
  │
  └─ Track O One extras (after M26; HMAC before live charges)
Q10 → Q15 can start after M23 / Isolation already exists
B99 Bar B definition of done

Parked (do not start in this program):
  HUB cutover phases B–D    BARC product v1 extras    P60 ops/portal retarget
```

**Serial inside M, and inside F.** D process (D10–D16) may run **in parallel** with M10–M16. Catalog, K public resource, and G keys need the matching D tables. Fulfillment must not land before webhook verify + idempotency exist. One HMAC (`O14`–`O17`) must exist **before** you call a live charge production-ready (paper 06 plane A vs B).

## Phase index

### Program

| ID | File | Intent |
|----|------|--------|
| B00 | [b00-align-freeze.md](./b00-align-freeze.md) | Lock Bar B, paths, anti-goals. No product code. |
| B99 | [b99-bar-b-done.md](./b99-bar-b-done.md) | Honest close of Bar B (not Hub dark, not Bar C) |

### Track M — Merchant OIDC (`:5178`)

| ID | File | Intent |
|----|------|--------|
| M10 | [m10-spa-register.md](./m10-spa-register.md) | One `POST /tenants/{id}/apps` (or One seed). Not Console-only. |
| M11 | [m11-merchant-oidc-env.md](./m11-merchant-oidc-env.md) | `VITE_ZITADEL_*` + `VITE_PAY_API_URL`. Public `client_id` only. |
| M12 | [m12-bearer-picker.md](./m12-bearer-picker.md) | Copy `pickApiBearerToken`. Never `id_token`. |
| M13 | [m13-oidc-pkce.md](./m13-oidc-pkce.md) | `oidc-client-ts` / `react-oidc-context` like `lazuar-app`. |
| M14 | [m14-callback-route.md](./m14-callback-route.md) | `http://localhost:5178/callback`. |
| M15 | [m15-login-host.md](./m15-login-host.md) | Sign-in via `:5175`. Homepage is `:5178`. Not `:3005`/`:5173`. |
| M16 | [m16-whoami-from-spa.md](./m16-whoami-from-spa.md) | After callback, `GET /v1/whoami` with Bearer. |
| M17 | [m17-tenant-list.md](./m17-tenant-list.md) | Render `tenants[]`. Empty state: create/pick, no Pay org table. |
| M18 | [m18-pick-org.md](./m18-pick-org.md) | Active org in path/hint. Header is not authz. |
| M19 | [m19-create-workspace.md](./m19-create-workspace.md) | `POST` One `/tenants` (or deep-link `lazuar-app`). |
| M20 | [m20-no-password-form.md](./m20-no-password-form.md) | Grep/lock: no Pay password, no `/one/auth/login`. |
| M21 | [m21-no-id-token.md](./m21-no-id-token.md) | Tests: picker never returns `id_token`. |
| M22 | [m22-session-storage.md](./m22-session-storage.md) | `sessionStorage`; no `credentials: include` on localhost. |
| M23 | [m23-no-hub-types.md](./m23-no-hub-types.md) | Merchant `package.json` has no `@repo/api-types-ts`. |
| M24 | [m24-role-chrome.md](./m24-role-chrome.md) | `owner`/`admin` write money; `member` read-only. Not `check(member)` as VIEWER. |
| M25 | [m25-allowlist-cors.md](./m25-allowlist-cors.md) | Login `REDIRECT_ALLOWLIST` + One CORS for `:5178`. Pay CORS already. |
| M26 | [m26-merchant-runbook.md](./m26-merchant-runbook.md) | Ada: `:5178` → `:5175` → whoami. Hub off. |
| M27 | [m27-not-ops.md](./m27-not-ops.md) | Do not retarget ops. Do not copy ops modules. |

### Track D — Database (greenfield)

| ID | File | Intent |
|----|------|--------|
| D10 | [d10-migrator.md](./d10-migrator.md) | One migrator, one history table. SQL or one `PayDbContext`. |
| D11 | [d11-postgres-5435.md](./d11-postgres-5435.md) | Local Postgres published **5435**. Not 5432. Not One’s `lazuar`. |
| D12 | [d12-connection-string.md](./d12-connection-string.md) | One name `ConnectionStrings:Pay`. |
| D13 | [d13-ready-probe.md](./d13-ready-probe.md) | `/health` liveness; ready = Postgres only, never One. |
| D14 | [d14-one-schema.md](./d14-one-schema.md) | `public` (or single `pay`). No `commerce`/`billing` schemas. |
| D15 | [d15-ban-org-users.md](./d15-ban-org-users.md) | No `organizations` / `users` tables. Test. |
| D16 | [d16-migrate-task.md](./d16-migrate-task.md) | `task pay:db:migrate` before traffic. Not nine contexts at boot. |
| D17 | [d17-checkouts-table.md](./d17-checkouts-table.md) | Replace in-memory `CheckoutStore` ids. |
| D18 | [d18-idempotency-keys.md](./d18-idempotency-keys.md) | `(org_id, key)` survives restart. |
| D19 | [d19-org-settings.md](./d19-org-settings.md) | Thin row keyed by One tenant id. Not membership. |
| D20 | [d20-products.md](./d20-products.md) | `products`. |
| D21 | [d21-prices.md](./d21-prices.md) | `prices`. MYR. |
| D22 | [d22-gateway-credentials.md](./d22-gateway-credentials.md) | Encrypted BYOK column. |
| D23 | [d23-psp-webhook-events.md](./d23-psp-webhook-events.md) | Unique `(org_id, provider, event_id)`. |
| D24 | [d24-charges.md](./d24-charges.md) | `charges`. |
| D25 | [d25-subscriptions.md](./d25-subscriptions.md) | `subscriptions`. Not Stripe Billing SoT. |
| D26 | [d26-journal.md](./d26-journal.md) | `journal_entries` + `journal_lines`. |
| D27 | [d27-receipts.md](./d27-receipts.md) | `documents` / sequences. `RCPT-` not `INV-`. |
| D28 | [d28-payers.md](./d28-payers.md) | Payer profile in Pay. Not Zitadel. |
| D29 | [d29-audit-mail.md](./d29-audit-mail.md) | `audit_events` (+ optional `mail_outbox`). Same DB. |

### Track CAT — Catalog

| ID | File | Intent |
|----|------|--------|
| CAT10 | [cat10-product-create.md](./cat10-product-create.md) | `POST` product for `org_id`. Member gate. |
| CAT11 | [cat11-price-myr.md](./cat11-price-myr.md) | At least one price, currency MYR. |
| CAT12 | [cat12-product-list.md](./cat12-product-list.md) | `GET` list for merchant. |
| CAT13 | [cat13-merchant-product-ui.md](./cat13-merchant-product-ui.md) | `:5178` create/list. Not ops. |
| CAT14 | [cat14-pay-spec-catalog.md](./cat14-pay-spec-catalog.md) | `pay-spec` products/prices only. |
| CAT15 | [cat15-catalog-tests.md](./cat15-catalog-tests.md) | Hermetic 201/403/401. |

### Track K — Buyer page (`:5179`)

| ID | File | Intent |
|----|------|--------|
| K10 | [k10-public-pay-get.md](./k10-public-pay-get.md) | `GET /v1/pay/{token}`. Do not ungated merchant GET. |
| K11 | [k11-public-dto.md](./k11-public-dto.md) | Buyer DTO: amount, currency, status, merchant display. No org internals. |
| K12 | [k12-pay-start.md](./k12-pay-start.md) | `POST /v1/pay/{token}/start` → `{ redirect_url }`. |
| K13 | [k13-public-404.md](./k13-public-404.md) | Unknown token → 404, not 401/403. |
| K14 | [k14-cors-pay.md](./k14-cors-pay.md) | CORS 5179 on public pay, including OPTIONS. Still deny 3004. |
| K15 | [k15-checkout-route.md](./k15-checkout-route.md) | Vite `/c/{token}`. |
| K16 | [k16-page-states.md](./k16-page-states.md) | open / paid / expired / missing / verifying. |
| K17 | [k17-no-oidc-checkout.md](./k17-no-oidc-checkout.md) | No Zitadel on 5179. Fail if login appears. |
| K18 | [k18-payer-fields.md](./k18-payer-fields.md) | Name + email on session. Not TIN-as-legal. |
| K19 | [k19-success-honesty.md](./k19-success-honesty.md) | `success_url` is not paid. Poll public status. |
| K20 | [k20-pay-spec-public.md](./k20-pay-spec-public.md) | `pay-spec` public pay ops. |
| K21 | [k21-checkout-no-hub-types.md](./k21-checkout-no-hub-types.md) | Checkout `package.json` has no `@repo/api-types-ts`. |
| K22 | [k22-checkout-runbook.md](./k22-checkout-runbook.md) | Open a pay link without a One account. |

### Track G — Rails

| ID | File | Intent |
|----|------|--------|
| G10 | [g10-pick-rail.md](./g10-pick-rail.md) | Stripe **XOR** CHIP for first dogfood. Write into decisions. |
| G11 | [g11-encrypt-keys.md](./g11-encrypt-keys.md) | Encrypt at rest. Master key in Pay env, not Vite. |
| G12 | [g12-put-keys.md](./g12-put-keys.md) | Merchant `PUT` keys. `owner`/`admin` + `authz`. |
| G13 | [g13-get-keys-metadata.md](./g13-get-keys-metadata.md) | List last4 / provider. Never raw secret. |
| G14 | [g14-member-cannot-keys.md](./g14-member-cannot-keys.md) | `member` 403 on write. |
| G15 | [g15-wrap-rails-label.md](./g15-wrap-rails-label.md) | Honest copy: hosted vs vaulted. No silent debit. |
| G16 | [g16-psp-hosted-session.md](./g16-psp-hosted-session.md) | Pay creates PSP hosted session. `mode=payment` (Stripe). |
| G17 | [g17-redirect-url.md](./g17-redirect-url.md) | Store PSP URL; `start` returns it. |
| G18 | [g18-webhook-route.md](./g18-webhook-route.md) | `POST /v1/webhooks/{provider}/{orgId}`. |
| G19 | [g19-webhook-signature.md](./g19-webhook-signature.md) | Verify signature. Unknown sig → 4xx. |
| G20 | [g20-empty-body-400.md](./g20-empty-body-400.md) | Empty body → 400. |
| G21 | [g21-webhook-idempotency.md](./g21-webhook-idempotency.md) | Unique `(org_id, provider, event_id)`. |
| G22 | [g22-setup-not-paid.md](./g22-setup-not-paid.md) | Setup / amount≤0 does not fulfill. |
| G23 | [g23-no-stripe-billing-sot.md](./g23-no-stripe-billing-sot.md) | Do not listen `customer.subscription.*` as SoT. |
| G24 | [g24-one-adapter.md](./g24-one-adapter.md) | One live adapter. Not Razorpay+Xendit+Billplz+CHIP+Stripe. |
| G25 | [g25-webhook-tests.md](./g25-webhook-tests.md) | Hermetic verify / 400 / replay. |
| G26 | [g26-pay-spec-webhooks.md](./g26-pay-spec-webhooks.md) | `pay-spec` webhook op. |

### Track F — Fulfillment

| ID | File | Intent |
|----|------|--------|
| F10 | [f10-same-handler.md](./f10-same-handler.md) | Webhook HTTP calls fulfill() in-process. No MediatR event. |
| F11 | [f11-open-to-paid.md](./f11-open-to-paid.md) | CAS: only `open` → `paid`. |
| F12 | [f12-seat-or-oneoff.md](./f12-seat-or-oneoff.md) | Insert subscription **or** complete one-off. Buyer access = Pay row. |
| F13 | [f13-balanced-journal.md](./f13-balanced-journal.md) | Journal + lines same TX. Steal `ValidateBalanced` **judgment**. |
| F14 | [f14-rcpt-number.md](./f14-rcpt-number.md) | `RCPT-{MYT year}-#####`. Never UUID. Missing = `PENDING`. |
| F15 | [f15-not-tax-invoice.md](./f15-not-tax-invoice.md) | Title Official Receipt. No VALID. |
| F16 | [f16-audit-same-tx.md](./f16-audit-same-tx.md) | Audit row in the same transaction. |
| F17 | [f17-zero-amount.md](./f17-zero-amount.md) | Amount≤0 does not mint `RCPT-` or ACTIVE. |
| F18 | [f18-sst-fail-closed.md](./f18-sst-fail-closed.md) | Unknown SST registration: do not undercharge. Qty=1 still honest. |
| F19 | [f19-list-payments.md](./f19-list-payments.md) | Merchant `GET` payments for org. |
| F20 | [f20-get-receipt.md](./f20-get-receipt.md) | Merchant open receipt. |
| F21 | [f21-merchant-receipt-ui.md](./f21-merchant-receipt-ui.md) | `:5178` shows payment + `RCPT-`. |
| F22 | [f22-webhook-replay.md](./f22-webhook-replay.md) | Second POST same event_id: no second journal. |
| F23 | [f23-pay-spec-fulfillment.md](./f23-pay-spec-fulfillment.md) | `pay-spec` payments/receipts. |

### Track O — One extras (Bar B)

| ID | File | Intent |
|----|------|--------|
| O10 | [o10-invite-copy-link.md](./o10-invite-copy-link.md) | Second engineer via One copy-link. No homemade SMTP. |
| O11 | [o11-accept-non-email.md](./o11-accept-non-email.md) | Keep non-email accept (app deep-link). |
| O12 | [o12-member-sees-ops.md](./o12-member-sees-ops.md) | Invited `member` sees payments, cannot paste keys. |
| O13 | [o13-lzr-sk.md](./o13-lzr-sk.md) | Pay accepts `lzr_sk_` as Bearer via One. Explicit scopes. |
| O14 | [o14-one-hmac-route.md](./o14-one-hmac-route.md) | Pay `POST` One-webhook door. Different from PSP. |
| O15 | [o15-one-hmac-verify.md](./o15-one-hmac-verify.md) | Verify HMAC. Pay holds webhook secret, not Zitadel PAT. |
| O16 | [o16-tenant-suspended.md](./o16-tenant-suspended.md) | `tenant.suspended` → stop **new** charges. Money already captured stays. |
| O17 | [o17-one-webhook-events-table.md](./o17-one-webhook-events-table.md) | Table `one_webhook_events`. Not `psp_webhook_events`. |

### Track Q — CI / isolation

| ID | File | Intent |
|----|------|--------|
| Q10 | [q10-isolation-vite.md](./q10-isolation-vite.md) | IsolationTests scan merchant/checkout package.json. |
| Q11 | [q11-ci-pay-test.md](./q11-ci-pay-test.md) | GitHub runs `Lazuar.Pay.slnx` tests. |
| Q12 | [q12-ci-vite-build.md](./q12-ci-vite-build.md) | CI builds merchant + checkout. |
| Q13 | [q13-pay-spec-not-hub-gen.md](./q13-pay-spec-not-hub-gen.md) | Do not add pay-spec to `task gen` / Hub honesty. |
| Q14 | [q14-readme-dx.md](./q14-readme-dx.md) | Dogfood DX is `pay:dev` + merchant + checkout + One. Not `task dev`. |
| Q15 | [q15-cors-still-denies-ops.md](./q15-cors-still-denies-ops.md) | CorsTests still fail `:3003` and do not add `:3004`. |

### Parked

| ID | File | Intent |
|----|------|--------|
| HUB | [parked-hub-cutover.md](./parked-hub-cutover.md) | Paper 02 phases B–D. After Bar B is boring. |
| BARC | [parked-bar-c.md](./parked-bar-c.md) | Renew, refunds, magic-link portal, SST×seats, second rail. |
| P60 | [parked-p60-old-frontends.md](./parked-p60-old-frontends.md) | Never retarget ops/portal. Pointer to 012 P60. |

## How to execute

1. Complete **B00** and fill [`decisions.md`](./decisions.md) (especially **rail pick** and **public pay token**).
2. M10→M27 in order. D10→D16 may overlap M10→M16.
3. D17+ as needed by CAT / K / G / F / O.
4. K10–K14 before K15 UI. G18–G22 before F10.
5. O14–O16 before calling a **live** charge Bar B.
6. B99 only when M, D (tables used), CAT, K, G, F, O, Q checklists for Bar B are done.
7. Flip [011/11](../../011-new-lazuar-pay/11-checklist.md) only for IDs listed in each phase **Exit**, and only when a human can do the job on the new stack.
8. Do not start HUB / BARC / P60 in the same PR as Bar B phases.
