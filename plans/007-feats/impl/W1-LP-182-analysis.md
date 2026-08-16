# W1-LP-182 — Sandbox + test keys that match live/test

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 `LP-182`. Tracker: *Sandbox + test keys* — Lazuar **P**.  
**Not this ID:** Test clocks (`LP-141`). Full data-plane isolation (separate DBs). Scoped-key catalog (`LP-131`).

**Invariant:** `sk_test_` cannot create a **live** processor object. `sk_live_` cannot create a **sandbox** processor object. Billplz host is **not** inferred from Hub hostname alone.

---

## 0. Scope lock

In scope:

- K1 prefix → `IsTestMode` (already)
- Stripe (and CHIP/Razorpay if they have test prefixes) K2 vs K1 (`EnsureKeyModeMatchesGateway`)
- Billplz **sandbox vs www** selection
- Tenant payment-config **test/live** flag (one vault slot is not enough if host decides)
- Docs (`environments.md`) honesty
- LHDN: test key skips credits (already) — confirm MyInvois host is intentional

Out of scope:

- Dual `commerce.CheckoutSessions` tables
- Stripe-style multiple sandboxes
- Changing `sk_` prefix (decision B)

---

## 1. Verdict

Prefixes exist. **Environments do not match.**

| Check | Today |
|-------|--------|
| Mint `sk_test_` / `sk_live_` | **Y** — Ops + `Is_test_mode` |
| Middleware `IsTestMode` from prefix | **Y** |
| `/integrations/payments/me`.is_test_mode | **Y** |
| M2M Stripe: `sk_test_` vs `sk_live_` K2 | **Y** — `KEY_MODE_MISMATCH` 409 |
| M2M Billplz: K2 prefix | **Skipped** — Billplz keys are not `sk_*` |
| Billplz API host | **`BillplzPublicBase.IsProductionApi`**: `App:BillplzEnvironment` **or** Hub `App:ApiBaseUrl` host ∈ `{api,pay,hub}.lazuar.com` |
| Commerce hosted checkout `RequestIsTestMode` | **null** — no K1; host heuristic only |
| Dual BYOK vault (test + live Billplz) | **N** — one `TenantPaymentConfiguration` per gateway type |
| LHDN `IsTestMode` | **Y** on document; **credits skipped**; gateway `Lhdn:BaseUrl` default **preprod** for everyone |
| Docs | **Stale**: “contains `lazuar.com`” — would send `pay-local.lazuar.com` live; **code already fixed** the contains-bug; docs not updated |

A `sk_live_` against **staging** Hub still hits Billplz **sandbox** (host not in ProductionHosts). A `sk_test_` against **hub.lazuar.com** hits Billplz **production** with whatever collection id is in the vault. That is the P.

---

## 2. Current files

| Path | Role |
|------|------|
| `ApiKeyAuthenticationMiddleware.cs` | Prefix → claim |
| `CheckoutSessionCashier.EnsureKeyModeMatchesGateway` | Stripe-shaped K2 only |
| `CreateIntegrationCheckoutCommandHandler` | Passes `RequestIsTestMode` |
| `BillplzGatewayAdapter.cs` | `isProd ? ProductionApiUrl : SandboxApiUrl` |
| `BillplzPublicBase.cs` | Env override + host allowlist |
| `TenantPaymentConfiguration.cs` | No `IsTest` / environment field |
| `GetPaymentsMeQueryHandler.cs` | Echoes K1; lists **all** active gateways |
| `LhdnGatewayAdapter.cs` | Single `Lhdn:BaseUrl` |
| `apps/lazuar-docs/docs/integrations/environments.md` | Wrong “contains lazuar.com” sentence |
| `CreateIntegrationCheckoutTests` | Stripe mismatch + Billplz “plain K2 does not throw” |

---

## 3. Gaps

### G1 — Billplz host follows Hub, not K1 (P0)

M2M test key on production Hub → live Billplz. Inverse: live key on local/staging → sandbox (usually safe, still surprising).

### G2 — One vault row per gateway

Merchant cannot keep sandbox collection id **and** live collection id. Switching Hub host silently retargets the same secret.

### G3 — Hosted Commerce has no K1

Product links always use host heuristic. Fine if we add **per-config environment**.

### G4 — Docs lie about the heuristic

`Contains("lazuar.com")` was explicitly rejected in code comments.

### G5 — LHDN live vs preprod not tied to K1

Both test and live keys hit `Lhdn:BaseUrl` (default preprod). Live keys on preprod is OK for now; **do not** send test keys to production MyInvois. If `Lhdn:BaseUrl` is ever set to production in prod, test keys would hit live IRBM. Add a guard if cheap.

**Not gaps**

- Prefix collision with Stripe (documented).  
- Shared Postgres for test/live objects (acceptable if processor isolation holds).

---

## 4. Minimal changes

### 4.1 Must — config-owned Billplz environment

| File | Change |
|------|--------|
| `TenantPaymentConfiguration` + migration | `string Environment` = `test` \| `live` (default `test` for new rows; existing: backfill `live` if you refuse to surprise prod merchants, **or** `test` fail-closed — **prefer backfill `live` on existing production rows**, `test` for new). |
| Payment settings UI | Toggle “Billplz / CHIP sandbox vs live” next to the key. Stripe can infer from `sk_test_` / `sk_live_` of **K2**. |
| `BillplzGatewayAdapter` | Resolve host from **config.Environment**, not `IsProductionApi(ApiBaseUrl)`. Keep `App:BillplzEnvironment` as **ops override** only. |
| `CheckoutSessionCashier.GenerateAsync` | After decrypt: if `requestIsTestMode == true` and config.Environment == live → `KEY_MODE_MISMATCH`. Inverse for live K1 vs test config. When `requestIsTestMode` is null (hosted), **use the config flag only**. |
| `EnsureKeyModeMatchesGateway` | Keep Stripe prefix check; Billplz uses config flag. |

Do **not** infer Billplz from Hub hostname anymore (except override).

### 4.2 Must — docs

Rewrite `environments.md` § Billplz: sandbox host is the **workspace payment-config environment** (and K1 must match). Delete contains-`lazuar.com`.

### 4.3 Should

- Allow **two** Billplz rows (test + live) by unique (org, gateway, environment) instead of (org, gateway). Cashier picks the row matching K1. Hosted Commerce: pick `live` in production UI default, or a workspace “checkout environment” — simplest: hosted always uses the single row’s flag (merchant switches explicitly).  
- Dual rows are the clean Stripe-like story; if migration is heavy, **one row + explicit flag** is enough for Wave 1.

### 4.4 LHDN (small)

If `IsTestMode` and `Lhdn:BaseUrl` host is production MyInvois → 409. Else leave single preprod default.

### 4.5 Do not

- Split commerce tables.  
- Use Hub hostname as the Billplz switch.

---

## 5. Tests

| Case | Expect |
|------|--------|
| M2M `sk_test_` + Billplz config `live` | 409 `KEY_MODE_MISMATCH` |
| M2M `sk_test_` + config `test` | Calls `billplz-sandbox.com` |
| M2M `sk_live_` + config `test` | 409 |
| Hosted checkout (null K1) + config `test` | sandbox host even if `App:ApiBaseUrl` is `https://hub.lazuar.com` |
| Stripe `sk_test_` K1 + `sk_live_` K2 | 409 (existing) |
| `IsProductionApi` no longer used for bills | Adapter unit test |
| Docs grep `contains \`lazuar.com\`` | Zero in VitePress env page |

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Existing prod tenants backfilled `test` | Backfill `live` where `App:ApiBaseUrl` is a production host **or** where they have been charging |
| Two collections required | UI copy: paste sandbox collection on test, live on live |

---

## 7. Acceptance

1. `sk_test_` never POSTs `www.billplz.com`.  
2. `sk_live_` never POSTs `www.billplz-sandbox.com`.  
3. Production Hub hostname alone does not force live Billplz.  
4. Stripe prefix guard unchanged.  
5. VitePress env page matches code.  
6. Tests §5 pass.  
7. Tracker **P → Y**.

---

## 8. Implement order

1. Config field + adapter host  
2. K1 vs config mismatch  
3. Ops toggle + backfill  
4. Docs + tests  
