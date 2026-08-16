# W4-LP-045 — Xendit adapter (BYOK wrap)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-045`. Tracker: *Xendit adapter* — Lazuar **N**.  
**Not this ID:** xenPlatform / marketplace split (`LP-203` refuse). Becoming an acquirer. Fiuu. HitPay as a fifth K2 (only if a tenant cannot reach methods via Xendit). Individual rails (`LP-032`–`036`) **consume** this adapter.

**Invariant:** Xendit is a **new `IPaymentGatewayAdapter`**, same port as Stripe/Billplz/CHIP/Razorpay. Tenant BYOK keys. Money settles on **their** Xendit account. We do not rebuild wallets or e-mandate — we request Xendit’s hosted invoice / payment method codes.

---

## 0. Scope lock

In scope:

- `XenditGatewayAdapter` + factory + webhook route `{gatewayType}=xendit`  
- Ops / admin payment-config dropdown  
- Checkout, webhook verify, refund, off-session **if** their token API is used  
- Capability flags: off-session, refund, reminder-only fallback  
- M2M allow-list

Out of scope:

- Implementing DuitNow QR / TnG pixels ourselves  
- xenInvoice product clone  
- Multi-country FX desk (`LP-096`)  
- Shipping this before Waves 0–2 are sellable

**Unlocks:** LP-032 (FPX e-mandate), LP-033–036 (methods Xendit already lists in MY).

---

## 1. Verdict

`PaymentGatewayFactory` resolves `STRIPE|BILLPLZ|CHIP|RAZORPAY` only. README still lists Xendit as a local gateway — that is a **claim**. Wave 4 exists to wrap, not to keep lying.

Do not start this adapter until a paying tenant needs SEA methods we cannot get from CHIP/Billplz hosted defaults **or** we are ready to sell e-mandate (LP-032).

---

## 2. Current files

| Path | Role |
|------|------|
| `PaymentGatewayFactory` | Four types |
| `IPaymentGatewayAdapter` | Port to implement |
| `PaymentGatewayCapabilities` | Off-session = Stripe/CHIP only |
| Ops `PaymentSettingsPage` | Four labels |
| Webhook endpoints | Unknown type → 400 |
| No `Xendit*` sources | |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No adapter |
| G2 | Capabilities / UI / allow-lists omit XENDIT |
| G3 | README names Xendit as current |

---

## 4. Recommended model

Copy CHIP’s shape (hosted purchase + webhook HMAC/token + refund POST):

| Port method | Xendit analogue (confirm against current docs at impl time) |
|-------------|--------------------------------------------------------------|
| `GenerateCheckoutAsync` | Invoice / Payment Request create; redirect URL |
| `ParseWebhookAsync` | `x-callback-token` (or current signing); map `PAID` / `EXPIRED` / `FAILED` |
| `IssueRefundAsync` | Refund API; fail closed if amount missing |
| `ChargeOffSessionAsync` | Cards with stored token **only**; else `false` + reminder-only |
| `GenerateCustomerPortalAsync` | Throw (like CHIP) |

`PaymentGatewayCapabilities`: off-session true **only** after a passing card-recurring soak. Until then `IsReminderOnlyGateway("XENDIT")` if we only ship invoices.

Do not set `SupportsOffSession` from marketing copy.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| New `XenditGatewayAdapter.cs` | Port |
| DI + factory | Register |
| Webhook map | `/webhooks/payments/xendit/{tenantId}` |
| Capabilities + ops + admin dropdown | `XENDIT` |
| M2M allow-list | Same four→five |
| README / docs | Move Xendit from “have” to “optional BYOK” until soak |
| Tests | Verify fail-closed; paid parse; unknown event passthrough |

Must not: new module; clone xenPlatform; request every ASEAN method on day one.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Bad callback token | `Verified=false` |
| Paid invoice | `PAYMENT_COMPLETED` + amount/currency |
| Failed/expired | `PAYMENT_FAILED` published |
| Factory `XENDIT` | Resolves |
| Factory still 400 on `FIUU` | |

No live Xendit soak in CI (operator residual).

---

## 7. Acceptance

1. Tenant pastes Xendit keys; hop 2 is Xendit’s host; webhook fulfills Commerce.  
2. Refund API works or ops shows mark-refunded honestly.  
3. README does not list Xendit unless this adapter is registered.  
4. Wallets / e-mandate are **not** claimed until LP-032–036 pass their own flags.

Tracker **N → W** (wrap), not **Y**.

---

## 8. Order

1. Adapter + webhook + tests  
2. Ops BYOK  
3. Honesty copy  
4. Then LP-032 / 033–036 as **method allow-lists** on this adapter  

Do **not** implement from this file.
