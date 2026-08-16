# W1-LP-173 — Portal update payment method as first-class

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 `LP-173`. Tracker: *Update payment method* — Lazuar **P**. Note: “route exists for dunning.”  
**Not this ID:** Magic update-payment **link in dunning email** (`LP-075` **Y**). Plan change (`LP-174`). Variable resolution (`LP-153`). Do not loosen PAST_DUE recovery.

**Invariant:** An **ACTIVE** (or PAST_DUE) buyer in the magic-link portal can start “update payment method” without being told they are in good standing and blocked. Hosted `/update-payment/{subId}` is a first-class portal action, not only a dunning landing page.

---

## 0. Scope lock

In scope:

- Portal subscription card CTA
- `POST /public/commerce/checkout/{subId}/update-payment` accepting `ACTIVE` (and existing `PAST_DUE` / `SUSPENDED`)
- Honest copy for Billplz reminder-only vs Stripe/CHIP vault
- Success return still `{clientUrl}/{slug}/portal` (no fake Order Complete — LP-024)

Out of scope:

- Changing arrears amount / dunning campaign
- $0 Stripe SetupIntent-only session (nice; only if adapter already supports amount 0 + `setup_future_usage`)
- Cancel-at-period-end
- Guest without magic token enumerating subs

---

## 1. Verdict

The **URL works for dunning**. The **product** “change card from portal” does not.

| Layer | Today |
|-------|--------|
| `GET …/arrears` | Any status; used to choose UI |
| `POST …/update-payment` | **400** unless `PAST_DUE` or `SUSPENDED`: *“currently active and does not require a payment update.”* |
| Portal page `/update-payment/[subId]` | ACTIVE → “Account in Good Standing” + dashboard; no charge |
| Buyer portal `/{slug}/portal` | Cancel only — **no** update-payment link |
| Dunning / failed-pay email | `{{update_payment_link}}` → that page (LP-151/153) |
| `GenerateCheckoutSessionQuery` already passes `setupFutureUsage: true` on arrears | Vaults on Stripe/CHIP when they **pay the due amount** |

Tracker **P**: dunning path **Y**; first-class portal **N**.

---

## 2. Current files

| Path | Role |
|------|------|
| `apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` | Status gate in UI |
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` | Cancel form only |
| `Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs` | Server 400 on ACTIVE |
| `packages/api-spec/modules/commerce/public-routes.tsp` | Docs say “past-due” |
| `MessageTemplateHydrator.cs` | Builds `/{slug}/update-payment/{subId}` |
| `RenewalCheckoutIssuer.cs` | Cancel URL = same page |

Arrears POST already: CRM email → One slug → metadata `type=commerce_subscription` → `setupFutureUsage: true` → product gateway.

---

## 3. Gaps

### G1 — ACTIVE is rejected (P0)

Pre-dunning (−3) and “update card while healthy” hit the good-standing wall. LP-153 analysis called this out and deferred it **here**.

### G2 — Portal has no CTA

Even PAST_DUE buyers who open the dashboard (not the email link) cannot start recovery without knowing the URL.

### G3 — Charging ACTIVE the full price is the wrong verb

If we only flip the status gate and reuse arrears, an ACTIVE buyer pays **another period** to “change card.” That is a support incident.

**Design lock for ACTIVE:**

- If gateway **can vault** (Stripe / CHIP / Razorpay): create checkout with `setupFutureUsage: true`. Prefer **minimum charge the adapter allows** or existing setup mode. If the cashier **requires** a positive amount, charge the **smallest currency unit** (e.g. RM 1) and document “authorization / verification charge”, **or** skip charge if `GenerateCheckoutSession` + Stripe Checkout `mode=setup` is already reachable. **Do not** bill `product.Price` again for ACTIVE.  
- If gateway is **reminder-only** (Billplz): do **not** open a new bill for the full price. Show copy: “This plan is paid by invoice each cycle. We’ll email the next Billplz link. No card on file.” CTA hidden or info-only.

Reuse `PaymentGatewayCapabilities.IsReminderOnlyGateway`.

### G4 — Unauthenticated GET arrears leaks product name/amount/status

Pre-existing. Do not expand; optional later: require token for ACTIVE update.

---

## 4. Minimal changes

### 4.1 Must

| File | Change |
|------|--------|
| `PublicArrearsEndpoints.cs` | Allow `ACTIVE` **in addition to** PAST_DUE/SUSPENDED. Branch: reminder-only ACTIVE → 400 with a **stable** code/message (`REMINDER_ONLY` / “no vaulted method”). Vault ACTIVE → generate session with `setupFutureUsage: true` and **amount = verification policy** (see G3), metadata `type=commerce_subscription` + `update_payment=1` so the completed handler **updates vault ids** without `RecoverFromPayment` advancing dates. PAST_DUE/SUSPENDED path **unchanged** (full price + recover). |
| `GatewayPaymentCompletedIntegrationEventHandler` subscription branch | If metadata `update_payment=1` and status already ACTIVE: update vault customer/token only; **do not** extend period. |
| Portal `page.tsx` (buyer dashboard) | Button “Update payment method” → `/{tenant}/update-payment/{sub.id}` for ACTIVE (vault) and PAST_DUE/SUSPENDED. Hide for CANCELED / reminder-only ACTIVE. |
| `update-payment/[subId]/page.tsx` | ACTIVE vault: “Update how you pay {product}” + submit. ACTIVE reminder-only: explanation, no form. Keep PAST_DUE “Action Required”. |
| TypeSpec | Doc: not only past-due. |

### 4.2 Should

- Require magic `token` query on POST for ACTIVE (portal already has token). Dunning email links stay open (today’s threat model). If adding token for ACTIVE only is messy, leave open + UUID opacity.  
- Copy on −3 template can stay `update_payment_link` — page will work.

### 4.3 Do not

- Charge ACTIVE `product.Price`.  
- Show “Order Complete” on return.  
- Change PAST_DUE amount logic.

---

## 5. Tests

Extend `PublicArrearsEndpointsBoundaryTests` + recovery tests:

| Case | Expect |
|------|--------|
| PAST_DUE POST | URL; full price (existing) |
| ACTIVE + Stripe product | 200 URL; amount **≠** full price (or setup session) |
| ACTIVE + Billplz | 400 `REMINDER_ONLY` (or 200 with no charge — if you choose info-only at API, portal hides button) |
| ACTIVE completed webhook with `update_payment=1` | Vault ids change; `NextBillingDate` unchanged |
| CANCELED POST | 400 |
| Portal markup | Button present for ACTIVE vault (component test if any; else manual) |

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Accidental double period | Metadata flag + handler guard |
| Stripe setup mode missing | RM 1 verification + refund policy later — document |
| Open GET/POST by sub GUID | Existing; UUIDv7 |

---

## 7. Acceptance

1. Portal shows Update payment for ACTIVE vaulted subs and for PAST_DUE.  
2. ACTIVE Billplz does not charge a new full bill.  
3. ACTIVE Stripe/CHIP updates the stored method without moving `NextBillingDate`.  
4. PAST_DUE recovery unchanged.  
5. Success URL is portal, not checkout success.  
6. Tests §5 pass.  
7. Tracker **P → Y**.

---

## 8. Implement order

1. API status + amount/setup branch + webhook guard  
2. Portal CTA + page copy  
3. Tests  
