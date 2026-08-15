# W0-LP-047 — Implementation analysis: Honest vault / off-session

**Date:** 16 August 2026  
**ID:** LP-047 (Wave 0 — close loops)  
**Status:** analysis only. Do not implement from this file.  
**Canonical name:** Honest vault / off-session (Stripe/CHIP can vault; Billplz is reminder-only)

Tracker rows:

- [00-implement-ids.md](../00-implement-ids.md) — `LP-047 | Honest vault / off-session (Stripe/CHIP vs Billplz reminder-only)`
- [00-checklist-tracker.md](../00-checklist-tracker.md) — Wave 0 `Saved card / tokenization / off-session` and backlog “Honest vault story”

**ID collision (ignore for this ticket):** [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) later remaps `LP-047` to “Quotes / tax invoices / credit notes UI”. That is a different numbering scheme. Wave 2 IDs for invoicing UI are `LP-102`–`LP-106`.

Related but **not** this ticket:

| ID | Why adjacent, not this work |
|----|-----------------------------|
| LP-052 | Automatic renewal actually runs (vaulted Stripe/CHIP loop) |
| LP-053 | First-class “send link each cycle” product (Wave 1) |
| LP-065 | Offline / manual enroll polish (Wave 1) |
| LP-072 | Off-session AUTO_CHARGE retry intelligence |
| LP-044 | Finish Razorpay / Curlec |
| LP-076 | Hard vs soft decline |
| LP-079 | Campaign snapshot |

---

## 1. Product contract (what “done” means)

Lazuar must stop implying silent auto-debit on rails that cannot vault.

1. **Stripe and CHIP Collect can vault** and may run off-session renewals / dunning `AUTO_CHARGE`.
2. **Billplz is reminder-only.** Hosted checkout each cycle. No card on file. No silent charge.
3. **Product / ops / dunning must not offer `AUTO_CHARGE` as a working action on Billplz.**
4. **Reminder-only is first-class and honest** in the subscription flag, billing/dunning engines, API DTOs, and ops UI — not an accidental “no token → PAST_DUE” side effect.

Recurring Billplz products remain legal. They are a collection mode, not a broken Stripe clone.

---

## 2. Current adapters

Registered in [apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/DependencyInjection.cs) via `IPaymentGatewayFactory` → [PaymentGatewayFactory.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs). Port: [IPaymentGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs).

There is **no capability flag**. Every adapter must implement `ChargeOffSessionAsync`. Commerce infers “can auto-charge” only from `Subscription.VaultedTokenId`.

| Adapter | `GatewayType` | Checkout | Vault on first paid checkout | `ChargeOffSessionAsync` | Refund | Portal |
|---------|---------------|----------|------------------------------|-------------------------|--------|--------|
| [StripeGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs) | `STRIPE` | Checkout Session `mode=payment`; `setupFutureUsage` sets `PaymentIntentData.SetupFutureUsage = "off_session"` | Parses `session.CustomerId` + PI `PaymentMethodId` (or PI path) | Creates confirmed off-session PI; `true` if `succeeded` or `processing`; StripeException → `false` | Yes | Yes |
| [ChipCollectGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs) | `CHIP` | `force_recurring` when `setupFutureUsage` | Token only if root `is_recurring_token`; **`GatewayCustomerId` always `null`** | Fetch old purchase by token → new purchase → `charge/` with `recurring_token`; `paid` or `pending_charge` → `true` | Yes | Throws (no portal) |
| [BillplzGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs) | `BILLPLZ` | Hosted bill; **ignores `setupFutureUsage`** | Never returns customer/token | **`throw new NotSupportedException(...)`** | `false` | Throws |
| [RazorpayGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs) | `RAZORPAY` | Payment link; subscription_registration when `setupFutureUsage` | Parses customer + token when present | Recurring payment with **hardcoded** `billing@lazuar.com` / `0000000000` | Yes | n/a |

Webhook allow-list: `STRIPE`, `BILLPLZ`, `RAZORPAY`, `CHIP` in [Payments/Infrastructure/Endpoints.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs). M2M checkout allow-list is the same set in [CreateIntegrationCheckoutCommandHandler.cs](../../../apps/lazuar-api/Modules/Payments/Application/Commands/CreateIntegrationCheckoutCommandHandler.cs).

### 2.1 Off-session handler

[ExecuteOffSessionChargeIntegrationEventHandler.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs)

- Loads tenant config by `@event.GatewayName`.
- Missing/inactive config → `GatewayPaymentFailed` (`failure_reason=gateway_not_configured`).
- Else calls `adapter.ChargeOffSessionAsync`. `false` → failed event (`charge_declined`).
- **Does not catch `NotSupportedException`.** Billplz throw leaves the `ChargeAttemptLog` `PENDING` and never publishes failed → never `PAST_DUE` via this path.

Event: [ExecuteOffSessionChargeIntegrationEvent.cs](../../../apps/lazuar-api/Modules/Payments/Contracts/Events/ExecuteOffSessionChargeIntegrationEvent.cs). Default `GatewayName = "STRIPE"`. Billing/dunning do pass `product.GatewayName`. The old Commerce-contracts twin (no `GatewayName`) is **gone**.

### 2.2 Stripe vault honesty (adapter)

[StripeGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs) lines 32–66:

- Payment mode + `CustomerEmail`.
- **No `CustomerCreation = "always"`.**
- `setup_future_usage` without a Customer often yields no reusable PM / null `session.CustomerId`.

Then Commerce only stores a vault when **both** customer and token are non-empty ([OpenCheckout.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs) 89–92). Stripe recurring can complete paid and still be reminder-only in practice.

### 2.3 CHIP vault honesty (adapter)

[ChipCollectGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs) 196–217:

- Token = purchase id if `is_recurring_token` on **root** (not `purchase.*` / `recurring_token`).
- `GatewayCustomerId: null` always.

`ChargeOffSessionAsync` does **not** use `customerId` — only `tokenId`. Persistence still requires both IDs. CHIP can theoretically off-session but Commerce often never stores the token.

### 2.4 Billplz (adapter)

- Checkout works; `setupFutureUsage` unused.
- Webhook never sets customer/token.
- Off-session **throws** (lines 246–251).
- Tests ([BillplzGatewayAdapterTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/BillplzGatewayAdapterTests.cs)) cover webhook/checkout only — **no `ChargeOffSession` test**.

### 2.5 Razorpay

Wave 4 (`LP-044`). Treat off-session as **not demoable**. Do not expand in LP-047 except “do not throw / do not offer as Billplz-class honesty.”

### 2.6 Capability matrix (intended)

| Gateway | Hosted checkout | Persist vault | Off-session | LP-047 collection mode |
|---------|-----------------|---------------|-------------|------------------------|
| `STRIPE` | Yes | Yes (must actually persist) | Yes | Vaulted |
| `CHIP` | Yes | Yes (must actually persist) | Yes | Vaulted |
| `BILLPLZ` | Yes | No | No | Reminder-only |
| `RAZORPAY` | Partial | Partial | Stub | Out of scope |
| unknown / empty | — | No | No | Reminder-only |

No `SupportsOffSession` / `PaymentGatewayCapabilities` type exists today.

---

## 3. `Product.GatewayName`

Domain: [Product.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs)

- Required non-empty; stored `Trim().ToUpperInvariant()`.
- **No whitelist** (`STRIPE`/`CHIP`/`BILLPLZ`/`RAZORPAY`).
- **No `SupportsOffSession` / `CollectionMode` field.** Reminder-only is not a product column.

Commands: [CreateProductCommand.cs](../../../apps/lazuar-api/Modules/Commerce/Contracts/Commands/CreateProductCommand.cs), [UpdateProductCommand.cs](../../../apps/lazuar-api/Modules/Commerce/Contracts/Commands/UpdateProductCommand.cs). Handlers pass the string through. Recurring + `BILLPLZ` is allowed (correct).

DTO: [packages/api-spec/modules/commerce/models/product.tsp](../../../packages/api-spec/modules/commerce/models/product.tsp) — `gateway_name: string` only. Mapping: [CommerceQueryService.Products.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Products.cs).

### Where `GatewayName` is used

| Path | Behavior |
|------|----------|
| [InitiateCheckoutCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs) 181–198 | `SetupFutureUsage = product.Interval != "one_time"` even for Billplz; preferred gateway = `product.GatewayName` |
| [BillingEngineJob.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs) 190 | Passed on off-session event **only if vault ids exist** — gateway capability not checked |
| [DunningEngineJob.PastDue.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs) 165 | Same |
| [PublicArrearsEndpoints.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs) 88–109 | Update-payment uses product gateway; `SetupFutureUsage=true` even for Billplz (harmless — Billplz ignores it; becomes another hosted bill) |
| [CheckoutSessionCashier.cs](../../../apps/lazuar-api/Modules/Payments/Application/Services/CheckoutSessionCashier.cs) | Preferred → first active config → **legacy `"BILLPLZ"`** when `requireActiveGateway=false` (Commerce string query) |

Blank `GatewayName` (legacy rows): checkout falls through to first active / BILLPLZ. New products cannot be blank (`ThrowIfNullOrWhiteSpace`).

---

## 4. `Subscription.IsReminderOnly`

Domain: [Subscription.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs)

| Method | Effect on flag |
|--------|----------------|
| ctor | `false` |
| `Activate(..., isReminderOnly = false)` | Sets the flag to the argument (default **false**) |
| `StoreVaultedToken` | Sets `false` |
| `RecoverFromPayment` / `Resume` / `ClearDunning` | **Does not touch** the flag |

Schema: column exists, default `false` ([CommerceDbContext.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs) 157). Migration `20260629072336_AddIsReminderOnlyToSubscription`.

### Who sets it

| Writer | `isReminderOnly` |
|--------|------------------|
| [CreateManualSubscriberCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs) 61 | `true` |
| [MarkCheckoutAsPaidOfflineCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs) 114 | `true` |
| [GatewayPaymentCompleted…OpenCheckout.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs) 86–92 | `Activate()` **default false**, then vault only if both ids present |
| [ProcessZeroAmountCheckoutCommand.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs) 76 | `Activate()` default **false**, never vaults |
| [RecordSubscriberPaymentCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs) 85 | Preserves existing flag on ACTIVE path |
| Failed-renewal / billing PAST_DUE | Does not set the flag |

**Billplz (and any no-token) paid recurring checkout therefore stays `IsReminderOnly=false`.** Docs in [11-subscriptions-lifecycle.md](../11-subscriptions-lifecycle.md) (“reminder-only subscription after first paid period”) describe intent, not the row.

### Who reads it

**Nobody in the engines.** Grep of workers: billing and dunning never consult `IsReminderOnly`. They only look at vault ids.

[CommerceQueryService.Subscribers.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Subscribers.cs) does **not** SELECT the column. [subscriber.tsp](../../../packages/api-spec/modules/commerce/models/subscriber.tsp) has `vaulted_*` only.

Stale sanitization note ([apps/lazuar-api/docs/003-data-sanitization-domain-rule-alignment.md](../../../apps/lazuar-api/docs/003-data-sanitization-domain-rule-alignment.md) Rule B): “reminder-only must never go `EXPIRED`/`SUSPENDED`.” `EXPIRED` is not a live status. Dunning **can** CANCEL/SUSPEND reminder-only after grace. **Do not implement Rule B in LP-047** (that would fight LP-078). Leave as historical.

---

## 5. Dunning `AUTO_CHARGE`

### Domain / API

- [DunningStep.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Entities/DunningStep.cs): `ActionType` is a free string (`ToUpperInvariant`). Comment lists EMAIL / WHATSAPP / AUTO_CHARGE. Engine also accepts `AUTOCHARGE` and comms `ALL`.
- [DunningCampaign.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs) `AddStep`: no validation against product gateway.
- Create/update handlers: [DunningCampaignCommandHandlers.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs) — **persist any action type**.
- TypeSpec [dunning.tsp](../../../packages/api-spec/modules/commerce/models/dunning.tsp): `action_type: string`.
- Default seed: EMAIL −3, EMAIL 0, WHATSAPP +3, grace 7, CANCEL — **no AUTO_CHARGE** (honest default).

### Targeting (not gateway-aware)

```text
inferredPaymentMethod = string.IsNullOrEmpty(sub.VaultedTokenId) ? "MANUAL" : "ONLINE_GATEWAY"
```

Used in [DunningEngineJob.PastDue.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs), [PreDunning.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PreDunning.cs), [GatewayPaymentFailedIntegrationEventHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs).

Ops checkbox label **“Online Gateways (Cards/FPX)”** ([CampaignSettingsPanel.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/dunning/CampaignSettingsPanel.tsx) 74–76) maps to `ONLINE_GATEWAY`. Typical Billplz FPX has no vault → **`MANUAL`**. An “FPX recovery” campaign targeted `ONLINE_GATEWAY` will **not** attach.

`IsReminderOnly` is unused for targeting.

### Engine

**Pre-dunning** ([DunningEngineJob.PreDunning.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PreDunning.cs) 37–40): only `EMAIL` / `WHATSAPP` / `ALL`. AUTO_CHARGE before due is already excluded. Good.

**Past-due** ([DunningEngineJob.PastDue.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs) 127–192):

1. If `AUTOCHARGE` / `AUTO_CHARGE` and (attempts ≥ 4 or missing vault) → log skip, **do not publish**.
2. Else insert `ChargeAttemptLog` + `ExecuteOffSessionCharge` with `product.GatewayName`.
3. **Then always** `RecordReminderDispatched` — skipped AUTO_CHARGE still consumes the DayOffset.
4. **Does not** check `product.GatewayName` or `IsReminderOnly`.

If a Billplz product ever has vault ids (bad data / future bug), step 2 publishes and Billplz **throws** (gap 2.1).

Normal Billplz path: no vault → skip charge → step marked done → emails (if any) still run. Engine is *accidentally* safe; it is not honest (AUTO_CHARGE looks executed).

Billing attempt 1: [BillingEngineJob.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs) 166–226. Vault present → off-session regardless of gateway. No vault → `MarkAsPastDue` + `subscription.past_due`. `IsReminderOnly` unused.

Limits: [ChargeAttemptLimits.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/ChargeAttemptLimits.cs) — billing owns 1, dunning 2–4.

---

## 6. Ops / admin UI (what is already honest vs not)

### Already honest (keep)

| Surface | Copy |
|---------|------|
| [ProductForm.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx) 135–139 | Amber: Billplz cannot vault / silent auto-charge / dunning retries. Use Stripe or CHIP. |
| [CreateProductForm.tsx](../../../apps/lazuar-ops/src/components/forms/CreateProductForm.tsx) 103–107 | Same paragraph |
| [PaymentSettingsPage.tsx](../../../apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx) 259–265 | Billplz “Offline / hosted checkout only…” |
| [PlatformPaymentSettingsPage.tsx](../../../apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx) 263 | Duplicate of ops payment-settings banner |
| [DunningStepEditor.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx) 168–180 | AUTO_CHARGE blue card + “Billplz does not support off-session” |
| [CreateSubscriberModal.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/CreateSubscriberModal.tsx) 136–141 | Manual enroll = Reminder Only, no auto-debit |

### Still dishonest / incomplete

| Surface | Gap |
|---------|-----|
| Product forms | Warning only when `gatewayName === "BILLPLZ"`. Recurring+Billplz still submits as a normal plan. No “this product is reminder-only” confirmation. No CHIP/Stripe “auto-renew will vault” positive copy. |
| [ProductDetailPanel.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/ProductDetailPanel.tsx) 209–212 | Shows raw `gateway_name`. No collection-mode badge. |
| [ProductsPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/ProductsPage.tsx) | No per-product gateway / reminder-only column. |
| [DunningStepEditor.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx) 149–151 | **Always offers** `AUTO_CHARGE` (“Auto-Retry Card”). Banner is not a disable. |
| [CampaignBuilderPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/CampaignBuilderPage.tsx) | Save does not reject AUTO_CHARGE on Billplz-only product targets. |
| [CampaignSettingsPanel.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/dunning/CampaignSettingsPanel.tsx) | Product list has no gateway hint. “Cards/FPX” ≠ Billplz FPX. |
| [SubscribersPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx) 241, 326 | Zap / “Auto-Debit Active” only if `vaulted_token_id`. No Reminder-only badge. Billplz paid subs look like “Auto-Debit: None” with no explanation. |
| Subscriber API | `is_reminder_only` not exposed — UI cannot be first-class without a DTO change. |
| [update-payment page](../../../apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx) 65–68 | Always “Your payment … **failed**.” Billplz reminder-only due is **no charge attempted**. |

Frontend hardcodes the string `"BILLPLZ"`. Backend has no capability API for the forms to share.

---

## 7. Exact gaps (priority)

### P0 — engine must not lie or throw

1. **Billplz `ChargeOffSessionAsync` throws.** Handler does not catch. Attempt stays PENDING; no `GatewayPaymentFailed`.
2. **Billing/dunning publish off-session from vault ids only**, ignoring `Product.GatewayName` and `IsReminderOnly`. Capability is not a first-class predicate.
3. **Paid Billplz (and any no-token) recurring checkout does not set `IsReminderOnly=true`.** Flag is unused by jobs. Reminder-only is not first-class in the engine.
4. **CHIP (and often Stripe) fail to persist vault** → “Stripe/CHIP can vault” is false in production even when the merchant picked those rails.

### P1 — product / dunning must not offer AUTO_CHARGE on Billplz

5. Campaign create/update accept AUTO_CHARGE with no product-gateway check.
6. Ops builder always shows AUTO_CHARGE; warning is not a gate.
7. Targeting + labels treat Billplz FPX as if it were a vaulted card rail.

### P2 — UI / API honesty

8. `CommerceSubscriptionDto` omits `is_reminder_only`; subscriber list cannot label the mode.
9. `ProductDto` has no `supports_off_session` / collection mode.
10. Product detail / list do not state reminder-only vs auto-renew.
11. Update-payment copy assumes a failed auto-debit.
12. Zero-amount recurring checkout is `IsReminderOnly=false` with no vault.

### Explicitly not a gap for LP-047

- Recurring + Billplz product create (allowed; reminder-only mode).
- Default campaign without AUTO_CHARGE (already honest).
- Pre-dunning excluding AUTO_CHARGE (already correct).
- Manual enroll reminder-only copy (already honest).
- Payment-settings Billplz banner (already honest).
- Send-link-each-cycle automation (LP-053).
- Hard/soft decline, Razorpay contact, campaign snapshot.

---

## 8. Minimal changes

One capability helper + persist vault for Stripe/CHIP + set/read `IsReminderOnly` + stop offering AUTO_CHARGE on Billplz. **No new tables. No new adapters. No campaign snapshot. No send-link job.**

### 8.1 Shared capability (single source of truth)

Add `Modules.Payments.Contracts.PaymentGatewayCapabilities` (Commerce already references Payments contracts):

```csharp
public static bool SupportsOffSession(string? gatewayName)
{
    var g = (gatewayName ?? "").Trim().ToUpperInvariant();
    return g is "STRIPE" or "CHIP"; // RAZORPAY: not demoable; leave false for LP-047
}

public static bool IsReminderOnlyGateway(string? gatewayName) => !SupportsOffSession(gatewayName);
```

Use from Commerce workers, campaign validation, and query DTO mapping. Frontend may keep a `"BILLPLZ"` warning but should prefer `supports_off_session` once on `ProductDto`.

Do **not** add `Product.IsReminderOnly` column. Derive product mode from `GatewayName`.

### 8.2 Adapters / handler (vault actually works; Billplz never throws)

| File | Change |
|------|--------|
| [BillplzGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs) | `ChargeOffSessionAsync` → `return Task.FromResult(false)` (log warning). Never throw. |
| [ExecuteOffSessionChargeIntegrationEventHandler.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs) | `try/catch` around adapter: `NotSupportedException` → failed event `off_session_not_supported`. Optional: if `!SupportsOffSession(gateway)` short-circuit before adapter. |
| [StripeGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs) | When `setupFutureUsage`: `CustomerCreation = "always"` so Checkout creates a Customer the PM can attach to. |
| [ChipCollectGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs) | Parse `client.id` (root or `purchase.client`) into `GatewayCustomerId`. Read `is_recurring_token` / recurring token from root **or** purchase node. Fallback: if token present and customer missing, set customer = token (charge path only needs token). |

Optional later (not required): `bool SupportsOffSession { get; }` on `IPaymentGatewayAdapter` mirroring the static helper.

### 8.3 Commerce: set and honor `IsReminderOnly`

**Open checkout** ([OpenCheckout.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs)):

```text
hasVault = both customer + token non-empty
  (CHIP: token-only is enough if 8.2 fallback lands)
Activate(..., isReminderOnly: !hasVault)
if (hasVault) StoreVaultedToken(...)
```

If `product.GatewayName` is reminder-only, force `isReminderOnly: true` even if junk tokens appear.

**Zero-amount recurring:** `Activate(..., isReminderOnly: true)`.

**Subscription payment path** ([Subscription.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs)): keep `StoreVaultedToken` when tokens present (update-payment converting reminder-only → vaulted is allowed and already clears the flag). Do not clear reminder-only on Billplz pay-again.

**BillingEngineJob** (`ProcessOneSubscriptionAsync`):

```text
canCharge = SupportsOffSession(product.GatewayName)
         && !sub.IsReminderOnly
         && vault customer + token present
if (canCharge) → attempt 1 + event
else → MarkAsPastDue + subscription.past_due
```

**Dunning PastDue AUTO_CHARGE:**

```text
if (!SupportsOffSession(product.GatewayName) || sub.IsReminderOnly || missing vault || at max)
  skip publish (keep RecordReminderDispatched so the timeline advances)
else
  existing publish
```

Never publish `ExecuteOffSessionCharge` for `BILLPLZ`.

**Campaign save** ([DunningCampaignCommandHandlers.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs)):

- If any step is AUTO_CHARGE / AUTOCHARGE **and** `TargetProductIds` is non-empty **and** every targeted product is `!SupportsOffSession` → `BusinessRuleValidationException` (“AUTO_CHARGE is not available for Billplz / reminder-only products”).
- If `TargetPaymentMethods` is only `MANUAL` and AUTO_CHARGE present → same reject.
- Empty product targets (org-wide): **allow** (mixed catalog). Engine skips Billplz rows. UI must warn.

Do not change default seed.

### 8.4 API honesty

| Contract | Add |
|----------|-----|
| [product.tsp](../../../packages/api-spec/modules/commerce/models/product.tsp) `ProductDto` | `supports_off_session: boolean` (computed; do not persist) |
| [subscriber.tsp](../../../packages/api-spec/modules/commerce/models/subscriber.tsp) | `is_reminder_only: boolean` |
| Query services | Map from `GatewayName` / `IsReminderOnly` |
| `task gen` | Regen TS + C# clients |

No new endpoints.

### 8.5 Ops UI (minimal)

| File | Change |
|------|--------|
| [ProductForm.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx) + [CreateProductForm.tsx](../../../apps/lazuar-ops/src/components/forms/CreateProductForm.tsx) | Keep Billplz amber. If `interval !== "one_time"` && Billplz: title it **Reminder-only renewals** (pay link each cycle; AUTO_CHARGE will not run). Prefer `supports_off_session` when generated. |
| [ProductDetailPanel.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/ProductDetailPanel.tsx) | Badge: Reminder-only vs Auto-renew, next to gateway. |
| [DunningStepEditor.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx) | Accept `allowAutoCharge`. If false: hide/disable AUTO_CHARGE option; if an old step is AUTO_CHARGE, show blocked state. Keep Stripe/CHIP copy when allowed. |
| [CampaignBuilderPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/CampaignBuilderPage.tsx) | `allowAutoCharge =` selected products empty ? (any product `supports_off_session`, else true for mixed/unknown) : some selected product supports off-session. Client-side block + rely on 8.3 API reject. |
| [CampaignSettingsPanel.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/dunning/CampaignSettingsPanel.tsx) | Show gateway next to product name. Relabel “Online Gateways (Cards/FPX)” → **“Vaulted auto-debit (Stripe / CHIP)”**. Relabel MANUAL → **“Reminder-only / offline (incl. Billplz)”**. |
| [SubscribersPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx) | If `is_reminder_only`: badge “Reminder-only”. If vaulted: keep Zap. Detail: “Reminder-only (pay link / record payment)” vs “Auto-debit active”. |
| Payment settings | Keep existing Billplz banner. No change required. |
| [update-payment page](../../../apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx) | Neutral copy: “Payment is due” / “Complete payment” — not “failed” unless we later pass a failed-charge flag (out of scope if arrears DTO has no flag; then just drop “failed”). |

Admin [PlatformPaymentSettingsPage.tsx](../../../apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx) already matches ops; leave unless copy is edited (keep both in sync).

### 8.6 What not to change

- No `send_invoice` / link-each-cycle worker (LP-053).
- No grace-period special case that blocks CANCEL/SUSPEND for reminder-only.
- No Razorpay contact rewrite.
- No decline-code matrix (LP-076 / LP-072).
- No new Product column, no migration unless DTO-only (no migration).
- Do not delete AUTO_CHARGE from the enum globally — it stays for Stripe/CHIP.

---

## 9. Tests

There is **no** `DunningEngineJob` worker test fixture today. Add one rather than only asserting domain matching in [DunningCampaignDomainTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/DunningCampaignDomainTests.cs).

### 9.1 Unit / module (must)

| Test | File (existing or new) | Assert |
|------|------------------------|--------|
| Billplz off-session does not throw; returns false | `BillplzGatewayAdapterTests` | `ChargeOffSessionAsync` → `false` |
| Handler + Billplz (or `NotSupportedException`) publishes failed, not unhandled | `ExecuteOffSessionChargeIntegrationEventHandlerTests` | `GatewayPaymentFailed` `failure_reason` `off_session_not_supported` or `charge_declined`; no throw |
| Handler still passes Stripe/CHIP success path | existing handler tests | unchanged |
| Capabilities | new `PaymentGatewayCapabilitiesTests` | STRIPE/CHIP true; BILLPLZ/empty/RAZORPAY false |
| Open checkout Billplz (no tokens) → `IsReminderOnly` | new or `CommerceProductCompletenessTests` | flag true, no vault |
| Open checkout Stripe/CHIP with both ids → vaulted, flag false | same | `StoreVaultedToken` semantics |
| Open checkout CHIP token-only (after 8.2) → vaulted | same | not reminder-only |
| Zero-amount recurring → reminder-only | completeness tests | flag true |
| Manual / offline still reminder-only | existing completeness tests | add `IsReminderOnly.Should().BeTrue()` to [MarkCheckoutAsPaidOffline](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs) (today asserts ACTIVE only) |
| Billing: Billplz or `IsReminderOnly` or no vault → PAST_DUE, **no** off-session event | extend [BillingEngineJobTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs) | capture keyed `IEventBus` |
| Billing: Stripe + vault + not reminder-only → one `ExecuteOffSessionCharge` with `GatewayName=STRIPE` | same | attempt 1 |
| Dunning AUTO_CHARGE + Billplz product + fake vault → **no** publish | new `DunningEngineJobTests` | skip |
| Dunning AUTO_CHARGE + reminder-only + no vault → no publish, reminder log recorded | same | |
| Dunning AUTO_CHARGE + Stripe vault → publish `GatewayName=STRIPE` | same | |
| Campaign create AUTO_CHARGE targeting only Billplz products → business rule | new handler tests | |
| Campaign create AUTO_CHARGE targeting Stripe product → ok | same | |
| Campaign create AUTO_CHARGE, no product filter → ok | same | |
| `RecoverFromPayment` preserves `IsReminderOnly` | [SubscriptionRecoveryTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionRecoveryTests.cs) | set true, recover, still true |
| Subscriber query includes `is_reminder_only` | query/mapping test if one exists; else handler-level | |

### 9.2 Adapter-focused (should)

- Stripe checkout options: when `setupFutureUsage`, session uses customer creation always (mock Stripe or inspect constructed options if tests already stub the client).
- CHIP webhook parse: `is_recurring_token` + `client.id` → both gateway ids set.

### 9.3 Not required for LP-047

- Ops component tests (no runner). Manual click-through on acceptance.
- Razorpay recurring contact.
- Full sandbox e2e Stripe+CHIP (LP-052). Mention as residual.

---

## 10. Acceptance

A reviewer can tick these without implementing LP-052/053/072.

### Engine

- [ ] Recurring **Billplz** checkout activates `IsReminderOnly=true`, no vault ids.
- [ ] Billing tick on that sub → `PAST_DUE` + `subscription.past_due`; **zero** `ExecuteOffSessionCharge`.
- [ ] Dunning campaign with AUTO_CHARGE + EMAIL: Billplz sub does **not** call a gateway; EMAIL still sends (if Resend configured).
- [ ] `ChargeOffSession` on Billplz adapter never throws; a stray publish yields failed event + failed attempt, not a hung PENDING + unhandled exception.
- [ ] Recurring **Stripe** (and **CHIP**) checkout with real customer+token → `IsReminderOnly=false`, vault stored.
- [ ] Billing tick on that vaulted sub → attempt 1 off-session with `product.GatewayName` (not default STRIPE on a CHIP product).
- [ ] Manual enroll and offline mark-paid remain reminder-only; record-payment / Billplz update-payment **does not** clear the flag; Stripe/CHIP update-payment **may** vault and clear it.

### Product / dunning must not offer AUTO_CHARGE on Billplz

- [ ] POST/PUT campaign: AUTO_CHARGE + only Billplz (or only MANUAL) targets → **4xx** with explicit message.
- [ ] Ops builder: selecting only Billplz products hides/disables Auto-Retry Card.
- [ ] Ops builder: org-wide or Stripe/CHIP targets still offer AUTO_CHARGE; Billplz limitation banner remains.
- [ ] Product create/edit recurring+Billplz: warning states **reminder-only**, not merely “cannot vault.”
- [ ] Product detail shows Reminder-only vs Auto-renew.

### UI first-class reminder-only

- [ ] GET subscribers includes `is_reminder_only`.
- [ ] GET products includes `supports_off_session`.
- [ ] Subscriber row/detail: Reminder-only vs Auto-debit. No Zap on Billplz paid members.
- [ ] Targeting labels do not call Billplz FPX a vaulted “online card” method.
- [ ] Update-payment page does not say the payment “failed” for a reminder-only due.

### Honesty regression

- [ ] Payment-settings Billplz banner still present (ops + admin).
- [ ] Default dunning seed still has no AUTO_CHARGE.
- [ ] Create-subscriber modal still says Reminder Only.
- [ ] README / Wave 0 copy: Billplz renewals = pay link; Stripe/CHIP = vault. Do not claim “subscriptions auto-renew” without naming the rail.

---

## 11. Suggested implementation order

1. `PaymentGatewayCapabilities` + Billplz return false + handler catch (unblocks throw).
2. Stripe `customer_creation` + CHIP token/customer parse + OpenCheckout `IsReminderOnly` / token-only vault.
3. Billing + dunning predicates.
4. Campaign save reject.
5. TypeSpec + query mapping + `task gen`.
6. Ops forms / dunning builder / subscribers / update-payment copy.
7. Tests in 9.1.

Estimate: one focused PR. Commerce workers + two adapters + DTO/UI. No migration.

---

## 12. File index

### Payments

- [IPaymentGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs)
- [PaymentGatewayFactory.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs)
- [StripeGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs)
- [ChipCollectGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs)
- [BillplzGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs)
- [RazorpayGatewayAdapter.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs)
- [ExecuteOffSessionChargeIntegrationEventHandler.cs](../../../apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs)
- [ExecuteOffSessionChargeIntegrationEvent.cs](../../../apps/lazuar-api/Modules/Payments/Contracts/Events/ExecuteOffSessionChargeIntegrationEvent.cs)
- [CheckoutSessionCashier.cs](../../../apps/lazuar-api/Modules/Payments/Application/Services/CheckoutSessionCashier.cs)
- [ProcessGatewayWebhookCommandHandler.cs](../../../apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs) (forwards customer/token)

### Commerce domain / application

- [Product.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs)
- [Subscription.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs)
- [DunningCampaign.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs)
- [DunningStep.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Entities/DunningStep.cs)
- [ChargeAttemptLimits.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/ChargeAttemptLimits.cs)
- [InitiateCheckoutCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs)
- [CreateProductCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/CreateProductCommandHandler.cs)
- [UpdateProductCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/UpdateProductCommandHandler.cs)
- [DunningCampaignCommandHandlers.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs)
- [CreateManualSubscriberCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs)
- [MarkCheckoutAsPaidOfflineCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs)
- [ProcessZeroAmountCheckoutCommand.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs)
- [RecordSubscriberPaymentCommandHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs)

### Commerce workers / events / queries

- [BillingEngineJob.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs)
- [DunningEngineJob.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs) + PastDue / PreDunning / Dispatch
- [GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs)
- [GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs)
- [GatewayPaymentFailedIntegrationEventHandler.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs)
- [PublicArrearsEndpoints.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs)
- [CommerceQueryService.Products.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Products.cs)
- [CommerceQueryService.Subscribers.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Subscribers.cs)

### Contracts / UI

- [packages/api-spec/modules/commerce/models/product.tsp](../../../packages/api-spec/modules/commerce/models/product.tsp)
- [packages/api-spec/modules/commerce/models/subscriber.tsp](../../../packages/api-spec/modules/commerce/models/subscriber.tsp)
- [packages/api-spec/modules/commerce/models/dunning.tsp](../../../packages/api-spec/modules/commerce/models/dunning.tsp)
- [ProductForm.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx)
- [CreateProductForm.tsx](../../../apps/lazuar-ops/src/components/forms/CreateProductForm.tsx)
- [ProductDetailPanel.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/ProductDetailPanel.tsx)
- [DunningStepEditor.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx)
- [CampaignBuilderPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/CampaignBuilderPage.tsx)
- [CampaignSettingsPanel.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/dunning/CampaignSettingsPanel.tsx)
- [SubscribersPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx)
- [CreateSubscriberModal.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/CreateSubscriberModal.tsx)
- [PaymentSettingsPage.tsx](../../../apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx)
- [PlatformPaymentSettingsPage.tsx](../../../apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx)
- [update-payment page](../../../apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx)

### Existing tests to extend

- [BillplzGatewayAdapterTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/BillplzGatewayAdapterTests.cs)
- [ExecuteOffSessionChargeIntegrationEventHandlerTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ExecuteOffSessionChargeIntegrationEventHandlerTests.cs)
- [BillingEngineJobTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs)
- [CommerceProductCompletenessTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs)
- [SubscriptionRecoveryTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionRecoveryTests.cs)
- [DunningCampaignDomainTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/DunningCampaignDomainTests.cs)

### Prior research (do not re-litigate)

- [12-dunning-and-recovery.md](../12-dunning-and-recovery.md) § entry conditions, DN-019
- [11-subscriptions-lifecycle.md](../11-subscriptions-lifecycle.md) § interval/gateway coupling
- [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) §§ 5–6
- [docs/001-gaps/06-payments-module.md](../../../docs/001-gaps/06-payments-module.md), [07-commerce-module.md](../../../docs/001-gaps/07-commerce-module.md)
