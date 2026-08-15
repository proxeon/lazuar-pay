# W0-LP-024 — Success page only after payment truth

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 0 row `LP-024` (“Success page only after payment truth”). Tracker label in [00-checklist-tracker.md](../00-checklist-tracker.md) is “Success page + fulfillment hook” (`Ours = P`).  
**Not this ID:** [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) and [18-pricing-onboarding-trust.md](../18-pricing-onboarding-trust.md) reuse `LP-024` for unrelated rows (proration / TOS). Ignore those meanings.

**Invariant:** Buyer success UI and fulfillment must not unlock because the browser landed on a gateway `success_url` / `redirect_url`. Paid is true only when platform state written after a verified money event (or an explicit server-side settle) says so.

---

## 0. Scope lock

In scope:

- Hosted product checkout success page (`lazuar-portal`)
- Public checkout session status poll
- Writers of `commerce.CheckoutSessions.Status = COMPLETED`
- Proof that redirect query params cannot flip paid / fulfill

Out of scope (do not expand this ticket):

- Un-hiding custom quotes (`/{tenant}/pay/{id}` is `[MVP-HIDE]` / `notFound()`)
- Minting portal magic tokens on the status poll
- Update-payment / dunning return URL (lands on `/{tenant}/portal`, no “Order Complete” claim)
- M2M cashier success page (sample already honest; see §3.4)
- Checkout branding, BM/EN, quantity UI, PWYW display, TIN
- Joining `billing` ledger from the public status query (see §2)
- Outbound webhook delivery/redrive, inbound signature work beyond what already exists

**Dependency (do not implement here):** inbound gateway verify + event/business-key idempotency already lives on `ProcessGatewayWebhookCommandHandler`. Status `COMPLETED` for paid product checkout is a downstream consumer of that path. If verify/idempotency regresses, this page lies.

---

## 1. What “payment truth” means here

Two planes, do not mix:

| Plane | Who | Paid signal | Used by buyer success? |
|-------|-----|-------------|------------------------|
| Gateway redirect | Browser | Landed on `success_url` (Billplz `redirect_url`, Stripe `SuccessUrl`, CHIP `success_redirect`) | **Never** |
| Commerce session | Platform | `CheckoutSession.Status == "COMPLETED"` | **Yes — this is the poll gate** |
| Commerce fulfillment | Platform | `Order` / `Subscription` + `order.completed` / `subscription.activated` | Written in the same handler as `Complete()`, not on the page |
| Billing ledger | Platform | `LedgerEntry` on `GatewayPayment` or `ZeroAmountCheckout` | **No — parallel book** |
| Integrator unlock | Merchant app | Signed outbound `payment.completed` (M2M) or `order.completed` / `subscription.activated` | Not the portal page |

**Design lock — do not join Billing ledger on `GET …/status`.**

- Ledger is a separate inbox consumer of the same `GatewayPaymentCompletedIntegrationEvent` (`GatewayPaymentCompletedHandler`).
- Zero-amount posts a *different* ledger type (`ZeroAmountCheckoutHandler` on `ZeroAmountCheckoutCompletedIntegrationEvent`).
- Requiring a ledger row would: cross-schema query from a public commerce endpoint; delay success until Billing inbox runs; fail or special-case zero-amount and offline mark-paid.
- Commerce `COMPLETED` is already written only by server-side settle paths (webhook open-session handler, zero-amount command, ops offline mark-paid). That is the buyer-facing truth for this ticket.

“Fulfillment hook” in the tracker is **not** a new success-page callback. It already exists: webhook/zero/offline handlers create Order/Subscription and publish lifecycle events. Communications then emails; One fans out workspace webhooks. The success page must only **observe** that state.

---

## 2. Payment-truth model (current writers)

`CheckoutSession` statuses in domain (`CheckoutSession.cs`): `OPEN` → `COMPLETED` | `EXPIRED`. There is no `FAILED` / `ACTIVE`.

| Writer | File | Function | When | Money? |
|--------|------|----------|------|--------|
| Webhook (product / custom open session) | `…/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` | `HandleOpenCheckoutSessionAsync` → `session.Complete()` | After Payments verified webhook published `GatewayPaymentCompleted` and Commerce handler found `Status == "OPEN"` | Yes (gateway) |
| Zero-amount | `…/Commands/ProcessZeroAmountCheckoutCommand.cs` | `ProcessZeroAmountCheckoutCommandHandler.Handle` | `InitiateCheckout` net amount `== 0` (100% coupon). Completes **in the initiate request**, before any browser redirect | No gateway; ledger via `ZeroAmountCheckoutCompletedIntegrationEvent` |
| Offline mark-paid | `…/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` | `HandleProductSessionAsync` / `HandleCustomSessionAsync` | Ops staff | Manual; not a buyer redirect |

`Complete()` itself does not guard `OPEN`. Callers do:

- Webhook: only if `session != null && session.Status == "OPEN"`
- Zero-amount: throws unless `OPEN`
- Offline: throws unless `OPEN`

Redirect adapters only **receive** the success URL as a string. They never call `Complete()`.

---

## 3. Current files

### 3.1 Portal success page

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/success/page.tsx` | SSR product fetch; renders `CheckoutSuccessView` in `Suspense`. Does **not** read payment query flags. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutSuccessView.tsx` | Client poller. Query `sub_id` = checkout **session** id (name is leftover). States: `VERIFYING` → `SUCCESS` \| `TIMEOUT` \| `ERROR`. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/lib/api.ts` | `getCheckoutStatus` → `GET /public/commerce/{tenantSlug}/checkout/{sessionId}/status` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` | `handleSuccessZeroAmount` → `router.push(…/success)` **without** `?sub_id=` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | On `is_zero_amount_bypass` calls `onSuccessZeroAmount()`; else `window.location.href = result.url` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | Hop-1 form. `?cancelled=true` only. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` | Custom link — `notFound()`. Out of scope. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` | Orphaned. Treats `checkout.status === "COMPLETED"` from **server GET**, not redirect. Out of scope. |

Poller (`CheckoutSuccessView`):

- Missing `sub_id` → `ERROR` “Invalid Session” (does **not** show paid)
- 10 attempts × 2.5 s (`GET` status)
- Treats `response.status === "ACTIVE" \|\| "COMPLETED"` as success
- Swallows fetch errors and retries
- `SUCCESS` → “Order Complete!” + dashboard link
- `TIMEOUT` → “Processing Payment” + email hope + dashboard link
- `token` from API is always null → dashboard is `/{tenant}/portal` with no magic link

The page does **not** read `payment`, `paid`, Billplz `paid`/`x_signature`, Stripe `session_id`, or CHIP query flags.

### 3.2 Checkout session status endpoints

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/public-routes.tsp` | `getCheckoutStatus`, `getCheckoutStatusLegacy` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/models/checkout.tsp` | `CheckoutResponse` (`url`, `is_zero_amount_bypass?`); `CheckoutStatusResponse` (`status`, deprecated `token?`) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs` | POST checkout; both GET status maps. Always `Token = null`. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Checkout.cs` | `GetCheckoutStatusAsync` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Queries/ICommerceQueryService.cs` | `CheckoutStatusDto(string Status, string? Token)` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicEndpoints.cs` | Composer |

Status query (Dapper, org-bound):

```sql
SELECT "Status", "ClientProfileId", "ProductId"
FROM commerce."CheckoutSessions"
WHERE "Id" = @SessionId AND "OrganizationId" = @OrgId
```

Mapping:

- row missing → `null` → HTTP 404
- `Status == "COMPLETED"` → `{ status: "COMPLETED", token: null }`
- anything else (`OPEN`, `EXPIRED`) → `{ status: "PENDING", token: null }`

Legacy `GET /public/commerce/checkout/{subId}/status?tenant_slug=` is the same mapping; missing `tenant_slug` → 400.

Unauthenticated by design. Knowing tenant slug + session GUID reveals only `COMPLETED`/`PENDING`. No PII, no token.

### 3.3 Initiate + session + fulfillment writers

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | Creates `OPEN` session; zero-amount command **or** gateway URL with `…/success?sub_id={session.Id}` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Contracts/Commands/InitiateCheckoutCommand.cs` | `CheckoutResultDto(string Url, bool IsZeroAmountBypass)` — **no session id** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/CheckoutSession.cs` | `Complete()` / `Expire()` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/CheckoutSessionExpiryJob.cs` | `OPEN` past `ExpiresAt` → `EXPIRED` + coupon release |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/CommerceCheckoutMetadata.cs` | Stamps gateway metadata `type=commerce_subscription`, `subscription_id={session.Id}` (session id, not Subscription id) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Services/CheckoutSessionCashier.cs` | Shared hop-2 generate |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` | `redirect_url` = success URL (same URL even if buyer abandons on Billplz, depending on collection) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | `SuccessUrl` / `CancelUrl` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` | `success_redirect` / failure / cancel |

Zero-amount today:

```csharp
return new CheckoutResultDto(string.Empty, true);
```

Paid product path success URL is already the poller handle:

```csharp
var successUrl = $"{clientUrl}/{tenantSlug}/checkout/{productSlug}/success?sub_id={session.Id}";
```

Custom initiate still emits `/{tenant}/checkout/custom/success?sub_id=…` (no portal route). Out of scope; 404 is not a false unlock.

### 3.4 Webhook → event → commerce / ledger / M2M

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs` | `POST /webhooks/payments/{gatewayType}/{tenantId}` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | Verify → event-id + business-key idempotency → persist log → publish `GatewayPaymentCompleted` / Failed / Dispute |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.Idempotency.cs` | `BuildBusinessKey`, unique-violation treat-as-duplicate |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.Metadata.cs` | Merge M2M session metadata (Billplz stripped body). Commerce product checkout metadata usually already on the adapter payload |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Contracts/Events/GatewayPaymentCompletedIntegrationEvent.cs` | Money event |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxEventBus.cs` | Payments outbox write |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxPublisherJob.cs` | Outbox → `InMemoryEventBus` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/InMemoryEventBus.cs` | **New DI scope per publish** — Commerce handler SaveChanges is its own `CommerceDbContext` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` | Filter type; resolve `subscription_id` / `receipt`; OPEN session → open-checkout; else subscription recovery |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` | Coupon confirm; `Complete()`; Order or Subscription + lifecycle events; or `payment_link.paid` for custom |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Helpers.cs` | Correlation + tx log |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | Looks up **Subscription** by metadata id. First-checkout session id will not match a Subscription → session stays `OPEN` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` | Double-entry `GatewayPayment` (skips `utility_credit_topup`) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ZeroAmountCheckoutHandler.cs` | Ledger for 100% coupon |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/EventHandlers/OrderCompletedIntegrationEventHandler.cs` | Outbound `order.completed` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs` | Outbound `subscription.activated` etc. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/OrderCompletedDigitalDeliveryHandler.cs` | Email if template exists |

M2M (reference only — already honest):

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs` | `GET /integrations/payments/checkouts/{id}` is **key-auth**, not a public buyer poll |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs` | Marks integration session completed **only** on verified money event; outbound `payment.completed` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next/app/pay/success/page.tsx` | Polls **local** order; copy: `success_url` is not confirmation |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next/app/webhooks/hub/payments/route.ts` | HMAC verify; unlock only on `payment.completed` |

`lazuar-portal` has **no** test runner (`package.json` scripts: `dev` / `build` / `lint` only).

---

## 4. End-to-end (paid product checkout)

```
Buyer POST /public/commerce/checkout
  → CheckoutSession OPEN (24h)
  → gateway URL (Billplz/Stripe/CHIP)
Buyer pays on hop 2
  → gateway POST /webhooks/payments/{gw}/{tenantId}
  → verify + idempotency + PaymentWebhookLog
  → Payments outbox GatewayPaymentCompleted
  → InMemoryEventBus (new scope)
      → Commerce: OPEN session → Complete + Order/Sub + events
      → Billing: LedgerEntry GatewayPayment
      → Payments: IntegrationCheckout no-op (no M2M row)
Buyer browser hits /{tenant}/checkout/{slug}/success?sub_id={sessionId}
  → poll GET …/status until COMPLETED or 25s
```

Latency: Payments outbox poll is 5 s idle. Buyer can land on success **before** Commerce `SaveChanges`. Poller is the intended wait. TIMEOUT must stay “not paid.”

`InMemoryEventBus.CreateScope()` means an in-memory `Complete()` that never reaches `SaveChanges` is discarded (product-missing throw, org-mismatch `return`). Status cannot flip `COMPLETED` without the handler’s explicit save. Analyzed; not a gap.

---

## 5. What is already correct

1. **Redirect is not paid.** Success page ignores gateway query flags. Only `sub_id` is a poll handle.
2. **Paid hop-2 return already includes the handle.** `InitiateCheckoutCommandHandler` paid branch sets `success?sub_id={CheckoutSession.Id}`.
3. **Status SSoT is the session row**, org-bound, no token mint. Comments + TypeSpec match the code.
4. **`COMPLETED` is not written by HTTP GET/POST success.** Only webhook open-session, zero-amount command, offline mark-paid.
5. **Fulfillment is not on the page.** Order/Sub + outbound events fire in Commerce handlers. Sample cashier unlocks only on signed `payment.completed`.
6. **Missing `sub_id` is fail-closed** (Invalid Session), not “Order Complete.”
7. **TIMEOUT copy does not claim paid.**
8. **Idempotent inbound money events** prevent double `GatewayPaymentCompleted` from Stripe dual events (business key). Duplicate webhook after `COMPLETED` falls through to subscription lookup by session id → no-op.
9. **Cancel path is separate.** `?cancelled=true` on the form, not on success.

Tracker `Ours = P` is right: philosophy shipped; two product-checkout holes remain (zero-amount handle, status/UI honesty). The paid-redirect unlock bug is **not** present.

---

## 6. Exact gaps

### G1 — Zero-amount cannot show success after truth (false negative)

Server already fulfills in `ProcessZeroAmountCheckoutCommandHandler` (session `COMPLETED`, Order/Sub, `ZeroAmountCheckoutCompleted` → ledger). Browser is sent to `/success` **without** `sub_id` because:

- `CheckoutResultDto` / `CheckoutResponse` have no session id
- zero-amount returns `Url = ""`
- `CheckoutView.handleSuccessZeroAmount` pushes `/success` with no query

Buyer who used a 100% coupon sees **Invalid Session** even though payment truth exists. This is the only hosted-product path that breaks “success page after payment truth.”

### G2 — Status collapses `EXPIRED` to `PENDING`

`GetCheckoutStatusAsync` maps every non-`COMPLETED` row to `PENDING`. After expiry job, the poller keeps “Verifying…” then “Processing Payment.” Honest (not paid), but the session is dead, not in flight.

### G3 — Frontend treats `ACTIVE` as paid

Commerce never returns `ACTIVE`. Harmless today. A future status typo / copy-paste could unlock. Success must be **`COMPLETED` only**.

### G4 — No automated tests for the invariant

No tests call `GetCheckoutStatusAsync` or the public status routes. Existing coverage (`CommerceProductCompletenessTests.GatewayPaymentCompleted_ConfirmsCouponReservation_OnPaidCheckout`, webhook handler tests) proves writers, not the poller contract.

### G5 — 25 s poll vs outbox (UX, not unlock)

10 × 2.5 s can lose to a slow Payments outbox. TIMEOUT is correct. Buyers who actually paid see “still processing” until they refresh (refresh with same `sub_id` is safe).

### G6 — SUCCESS copy over-claims fulfillment

“receipt, digital downloads, and community access links” is shown for every `COMPLETED` product. Not an unlock bug. Community/Vault internals are gone. Keep copy factual.

**Not gaps for this ticket**

| Observation | Why not LP-024 |
|-------------|----------------|
| `token` always null; dashboard is a login wall | Do not mint tokens. Access is email magic link. |
| Custom success URL 404 | Hidden product; 404 ≠ paid. |
| Update-payment `success_url` = portal | No “Order Complete.” Recovery is webhook-only. |
| First-checkout `PAYMENT_FAILED` leaves session `OPEN` | Fail-closed. Adding `FAILED` is optional later. |
| Knowing a completed session GUID shows “Order Complete” | Same as knowing the poll handle the gateway already put in the URL. |

---

## 7. Minimal code changes

Prefer filling existing `CheckoutResponse.url` over a TypeSpec field. Zero-amount already has `url: string`; empty string is the lie.

### 7.1 Must change

| File | Function | Change |
|------|----------|--------|
| `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | `Handle` zero-amount branch (~164–169) | After `ProcessZeroAmountCheckoutCommand`, return `CheckoutResultDto(successUrlWithSubId, true)` using the **same** URL shape as the paid branch: `{clientUrl}/{tenantSlug}/checkout/{productSlug}/success?sub_id={session.Id}`. Do not return empty `Url`. |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | `handleSubmit` | If `result.is_zero_amount_bypass`, navigate to `result.url` when non-empty (`window.location.assign` or `router.push`). Do **not** call a bare `/success`. Keep gateway redirect for the non-bypass path. |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` | `handleSuccessZeroAmount` | Either delete and let the form navigate, or change the callback to accept `url: string` and push that. Stop pushing `/success` with no query. |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutSuccessView.tsx` | `verifyPayment` | Treat **only** `response.status === "COMPLETED"` as success. Do not treat `ACTIVE`, `PENDING`, `EXPIRED`, or HTTP errors as paid. Optional: if API later returns `EXPIRED`, map to a distinct non-paid UI (or keep TIMEOUT). |

No new routes. No token mint. No ledger join. No webhook handler rewrite.

### 7.2 Should change (same ticket, small)

| File | Function | Change |
|------|----------|--------|
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Checkout.cs` | `GetCheckoutStatusAsync` | If `session.Status == "EXPIRED"`, return `CheckoutStatusDto("EXPIRED", null)` instead of `PENDING`. Leave `OPEN` → `PENDING`. Still never `COMPLETED` unless the row is `COMPLETED`. |
| `CheckoutSuccessView.tsx` | poll loop | Increase wait to ~60 s (e.g. 20 × 3 s) so Payments outbox + Commerce handler can finish. TIMEOUT remains “not paid.” Optional “Check again” that restarts the same poll (same `sub_id`). |
| `CheckoutSuccessView.tsx` | SUCCESS copy | Drop “digital downloads, and community access links.” Confirm order; tell them to check email. Dashboard CTA may stay; it does not grant access. |

### 7.3 Do not change

- `PublicCheckoutEndpoints` token-null behavior
- `ProcessGatewayWebhookCommandHandler` (unless a test-only seam)
- `GatewayPaymentCompletedIntegrationEventHandler` fulfillment (already the truth writer)
- TypeSpec `token` field (keep deprecated)
- Sample `hub-cashier-next` (already the teaching copy)
- Custom checkout URLs / `pay/[sessionId]`
- `PublicArrearsEndpoints` success URL

### 7.4 Optional later (not required to close LP-024)

- Add `session_id` to `CheckoutResponse` + `CheckoutResultDto` if clients want an explicit handle. Filling `url` is enough.
- `CheckoutSession.Fail()` on first-checkout `PAYMENT_FAILED` so the page can stop saying “processing.”
- Move `session.Complete()` to immediately before `SaveChanges` in `HandleOpenCheckoutSessionAsync` (hygiene only; dirty state is not saved today).

---

## 8. Tests to add

Portal has no unit/e2e harness. Put the invariant in **API module tests**. Manual smoke for the zero-amount URL.

### 8.1 New: `GetCheckoutStatus` / query service

File: `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GetCheckoutStatusTests.cs`  
(or extend `CommerceProductCompletenessTests` if you want one fixture)

Against `CommerceQueryService.GetCheckoutStatusAsync` (in-memory or SQL fixture consistent with other Commerce query tests):

| Case | Expect |
|------|--------|
| Unknown session or wrong `organizationId` | `null` |
| `OPEN` | `Status == "PENDING"`, `Token == null` |
| `COMPLETED` | `Status == "COMPLETED"`, `Token == null` |
| `EXPIRED` | After G2: `"EXPIRED"`; before G2: `"PENDING"` — never `"COMPLETED"` |
| Token | Always `null` even when `COMPLETED` |

### 8.2 Initiate zero-amount returns poll URL

Extend `CommerceProductCompletenessTests` (already constructs `InitiateCheckoutCommandHandler`):

| Case | Expect |
|------|--------|
| Net 0 coupon | `IsZeroAmountBypass == true` |
| | `Url` contains `/checkout/{slug}/success?sub_id={session.Id}` |
| | Session row `COMPLETED` |
| Paid path (mock gateway URL) | Session stays `OPEN`; `Url` is gateway, not treated as paid |

Do **not** assert that returning from a fake redirect completes the session.

### 8.3 Webhook writer still the only paid flip

Already: `GatewayPaymentCompleted_ConfirmsCouponReservation_OnPaidCheckout` asserts session `COMPLETED`. Add:

| Case | Expect |
|------|--------|
| Same event twice (handler) | Still one Order/Sub; session stays `COMPLETED` |
| Event with `type` not commerce / custom | Session stays `OPEN` |
| Metadata `subscription_id` = session id but session already `COMPLETED` | No second Order/Sub (`HandleSubscriptionPaymentAsync` no-op) |

Webhook pipeline tests already live in `ProcessGatewayWebhookCommandHandlerTests` (verify fail, idempotency, no event). Do not duplicate LP-090; one assertion is enough: unverified parse ⇒ no `GatewayPaymentCompleted` ⇒ session would remain `OPEN`.

### 8.4 Endpoint mapping (optional)

If there is an existing WebApplicationFactory for public commerce, assert:

- `GET /public/commerce/{slug}/checkout/{sessionId}/status` → 404 unknown tenant / bad GUID
- 200 body `token` is null
- Legacy path without `tenant_slug` → 400

Skip spinning a new host just for this.

### 8.5 Manual (portal, no test runner)

1. Paid Billplz/Stripe sandbox: land on success **before** webhook → Verifying → Order Complete only after webhook. Kill webhook → never Order Complete.
2. Open success URL with no query → Invalid Session.
3. Open success with `sub_id` of an `OPEN` session → Processing, not Complete.
4. 100% coupon → success URL includes `sub_id` → Order Complete on first poll.

---

## 9. Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Outbox slower than poll | Low (honesty) | Longer poll + refresh; never flip SUCCESS on timeout |
| Filling zero-amount `url` with a relative/wrong host | Med | Reuse paid-branch `App:ClientUrl` + slug + `session.Id` |
| Frontend still calls `onSuccessZeroAmount()` without URL (old bundle / QuoteView) | Low | Form must not navigate to bare `/success`. QuoteView is unrouted. |
| Returning `EXPIRED` breaks a client that only knows PENDING/COMPLETED | Low | Portal is the only caller; teach it. TypeSpec `status` is already a free string. |
| Treating ledger as required | High if done | Do not. Breaks zero-amount timing and module boundaries. |
| Re-minting `token` on status | High | Do not. Anonymous poll would become an access oracle. |
| Session GUID enumeration | Low | UUIDv7; response has no PII. Acceptable for a poller. |
| Billplz always redirects to `redirect_url` | None if poller holds | Unpaid lander stays PENDING → TIMEOUT. That is the feature. |

---

## 10. Acceptance criteria

Close LP-024 when all of the following are true:

1. Visiting `/{tenant}/checkout/{slug}/success` with **no** `sub_id` never shows “Order Complete.”
2. Visiting that page with `sub_id` of an `OPEN` or `EXPIRED` session never shows “Order Complete,” including if the URL has any gateway query flags (`paid`, `payment`, Stripe session id, Billplz `x_signature`).
3. “Order Complete” appears only after `GET /public/commerce/{tenant}/checkout/{sessionId}/status` returns `status: "COMPLETED"` (and `token` is null).
4. `COMPLETED` is still written only by: verified `GatewayPaymentCompleted` open-session handler, `ProcessZeroAmountCheckoutCommandHandler`, or `MarkCheckoutAsPaidOfflineCommandHandler`.
5. 100% coupon / zero-amount: initiate response `url` includes `sub_id`; success page shows Complete after that session is `COMPLETED` (already true server-side before navigation).
6. Fulfillment (Order/Subscription, outbound `order.completed` / `subscription.activated`, digital-delivery email) still does **not** run in the portal or in `PublicCheckoutEndpoints` GET status.
7. Tests in §8.1–8.3 exist and pass.
8. Sample cashier remains “success_url never unlocks” (no required edits).

---

## 11. Suggested implement order

1. Backend: zero-amount `CheckoutResultDto` URL (G1) + tests §8.2  
2. Portal: navigate to that URL; `COMPLETED`-only success (G1, G3)  
3. Status: return `EXPIRED` (G2) + tests §8.1  
4. Poll window + copy (G5, G6)  
5. Manual smoke §8.5  

That is the whole ticket.
