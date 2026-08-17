# 01 — Commerce first payment: hop-1 / hop-2 checkout, coupons, $0 and trial vaulting, SST on first charge, CheckoutSession lifecycle, SubscriptionActivation, ProcessZeroAmount, offline mark-paid, custom quotes at first create, quantity, idempotency

**Date:** 17 August 2026  
**Branch:** `feat/007-waves-1-4-implement`  
**HEAD:** `297ba98` (`fix(one): add /accept-invite on ops and mint invite URLs there`)  
**Slice lock:** Commerce first payment only. Hop-1 public checkout, hop-2 gateway mint, coupons (reserve / confirm / release), $0 catalog and 100% coupon, trial vaulting, SST on the first charge, `CheckoutSession` OPEN/COMPLETED/EXPIRED, `SubscriptionActivation`, `ProcessZeroAmount`, clerk mark-paid, custom quotes at first create, quantity, idempotency.  
**Out of scope (other 009 reports own these):** billing-engine renewals and collection-pause reclaim, dunning / arrears tokens / update-payment, payment adapter internals except where they prove a Commerce first-charge bug, ledger journal, LHDN XML, One identity, frontends except as they prove a checkout bug, TypeSpec.

This is not a rewrite of `plans/007-feats` or `plans/008-evals`. Code on this tree wins. A Wave `*-done.md` that says a cell is Y is not evidence. 008 named several P0/P1s that later commits on this branch claimed to close; those are re-read below, not assumed closed.

Recently fixed (do not re-open unless the live code is still broken):

- `$0` Stripe/CHIP recurring now mints hop-2 + Stripe `mode=setup` (`8b3567d`). `ProcessZeroAmount` is still reminder-only for one-time / reminder-only rails. **Re-verified: still fixed on initiate. The command itself is still hard-coded reminder-only if you call it directly.**
- Hop-1 SST now uses `SubscriptionBillingAmount.GrossBreakdown` (`eba0741`). **Re-verified: hop-1 product path does this. Custom quotes and offline mark-paid do not.**
- `type=trial` hop-2 may still be ignored by `GatewayPaymentCompleted` if `IsCommerceSubscriptionType` does not accept `"trial"`. **Re-verified: still a live bug. Filed as B01-C01.**

---

## 2. Files table — every file opened, what it owns

Paths are under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/` unless noted.

### Application (first-charge policy)

| File | What it owns |
|------|----------------|
| `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` | Public product + custom hop-2 mint. Idempotency replay. Coupon reserve. SST GrossBreakdown. Trial vs coupon $0 fork. Vaulting hop-2 vs ProcessZeroAmount. |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs` | Completes $0 / 100% coupon / non-vaulting trial. Always `reminderOnly: true`. Re-discounts `product.Price`. |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` | Clerk mark-paid for product or ad-hoc session. Always reminder-only. Discount on `product.Price`. No SST. |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/CreateCustomCheckoutCommandHandler.cs` | Quote create: CRM resolve, ad-hoc lines, `QT-` number, due_at / terms, gateway preference. |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/CouponCommandHandlers.cs` | Coupon CRUD. Archive-on-delete. |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs` | Ops enroll (adjacent). Reminder-only, qty 1, `product.Price`. |
| `apps/lazuar-api/Modules/Commerce/Application/Commands/CreateProductCommandHandler.cs` | Catalog write: SST, trial, yearly, archive if no email. |
| `apps/lazuar-api/Modules/Commerce/Application/SubscriptionActivation.cs` | `IsTrialOffer` + `Start` → `ActivateTrial` vs `Activate`. |
| `apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs` | Exclusive SST. `02` only if merchant has SST ID and rate > 0 and net > 0. |
| `apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs` | Unit × seats, GrossBreakdown, `MerchantHasSstAsync` (null billing → false), `AdvanceFrom`. |
| `apps/lazuar-api/Modules/Commerce/Application/CommerceCheckoutMetadata.cs` | Gateway merge, persistence map, `IsCommerceSubscriptionType`. |
| `apps/lazuar-api/Modules/Commerce/Application/CommerceCheckoutIdempotency.cs` | Key normalize (max 200) + SHA-256 fingerprint. |
| `apps/lazuar-api/Modules/Commerce/Application/CommerceCheckoutQuantity.cs` | 1–99 FIXED one-time / mo / yr. PWYW stays 1. |
| `apps/lazuar-api/Modules/Commerce/Application/ICommerceRepository.cs` | Persistence port used by initiate / zero / offline. |
| `apps/lazuar-api/Modules/Commerce/Application/Queries/ValidateCouponQuery.cs` | Public validate query (slug + code only). |
| `apps/lazuar-api/Modules/Commerce/Application/Queries/ValidateCouponQueryHandler.cs` | Discount against `product.Price`, not the selected price row. |
| `apps/lazuar-api/Modules/Commerce/Application/OfflinePaymentMethods.cs` | `BANK_TRANSFER` / `CASH` / `COMPED`. |
| `apps/lazuar-api/Modules/Commerce/Application/CommerceWebhookPayload.cs` | `subscription.*` payload. First-activate amount uses Gross when billing is present. |
| `apps/lazuar-api/Modules/Commerce/Application/EventHandlers/OrderCompletedIntegrationEventHandler.cs` | Outbox `order.completed` after a one-time order. |
| `apps/lazuar-api/Modules/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs` | Outbox `subscription.activated` etc. Optional billing for SST on payload. |
| `apps/lazuar-api/Modules/Commerce/Application/DependencyInjection.cs` | MediatR marker only. |

### Domain

| File | What it owns |
|------|----------------|
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/CheckoutSession.cs` | OPEN/COMPLETED/EXPIRED. Qty, PriceId, coupon, metadata, idempotency, gateway URL, ad-hoc lines, quote number, due date. `Complete()` is unguarded. |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Coupon.cs` | PERCENTAGE / FIXED. Reserve / Confirm / Release. MaxUses includes reserved. |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs` | Catalog + SST + TrialDays + Prices. `SetTrialDays` rejects one-time. |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Order.cs` | One-time entitlement. AmountPaid + Quantity. |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` | Activate / ActivateTrial / vault / snapshot. First-charge writes status here. |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/ProductPrice.cs` | `mo` / `yr` / `one_time` price rows. |
| `apps/lazuar-api/Modules/Commerce/Domain/ValueObjects/AdHocLineItem.cs` | Quote line. No qty/price validation. |

### Infrastructure (HTTP, persistence, workers, first-charge webhook)

| File | What it owns |
|------|----------------|
| `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` | Type filter + tenant_id check + session vs subscription dispatch. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Helpers.cs` | Correlation id, tx log, vault id rules, metadata copy. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` | First paid / setup: coupon confirm, Complete, Order or SubscriptionActivation. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` | Renewal / recover / update-payment (read only to see that a completed session falls here and no-ops). |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | Fail attempt → PAST_DUE. Does **not** look at OPEN checkout sessions. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/CheckoutSessionExpiryJob.cs` | Expire OPEN past `ExpiresAt`, `ReleaseReservation`. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` | EF loads. Coupon `FOR UPDATE`. Session-by-id **without** `IgnoreQueryFilters`. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs` | Unique `(OrganizationId, IdempotencyKey)` filtered index. No row version on sessions. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Migrations/20260818110000_AddCheckoutSessionIdempotency.cs` | Idempotency columns + unique index. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/DependencyInjection.cs` | CommerceEventBus = outbox. Inbox/outbox jobs. Handler registration. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints.cs` | Admin custom-checkout create + mark-paid. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs` | `POST /public/commerce/checkout` + status pollers. Idempotency-Key header. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicProductEndpoints.cs` | Public product GET + validate-coupon. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCustomCheckoutEndpoints.cs` | Public quote GET + draft PDF URL. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs` | Manual enroll HTTP. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Checkout.cs` | Org-bound status map: COMPLETED / EXPIRED / else PENDING. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.CustomCheckouts.cs` | Quote list/get. Total = line sum, no SST. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Products.cs` | Public product DTO including SST + trial + `supports_off_session`. |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/CommerceInboxConsumerJob.cs` | Inbox consumer for GatewayPaymentCompleted. |

### Contracts / Payments (only as they prove a Commerce first-charge bug)

| File | What it owns |
|------|----------------|
| `apps/lazuar-api/Modules/Commerce/Contracts/Commands/InitiateCheckoutCommand.cs` | Command + `CheckoutResultDto`. No PWYW amount field. |
| `apps/lazuar-api/Modules/Commerce/Contracts/Commands/CreateCustomCheckoutCommand.cs` | Quote command. Line items unconstrained. |
| `apps/lazuar-api/Modules/Payments/Contracts/Queries/GenerateCheckoutSessionQuery.cs` | Amount = unit; Quantity multiplies in the adapter. |
| `apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` | Off-session = Stripe/CHIP only. |
| `apps/lazuar-api/Modules/Payments/Application/Queries/GenerateCheckoutSessionQueryHandler.cs` | Thin cashier wrapper. |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | `$0` + setupFutureUsage → `Mode = "setup"`. Webhook emits `PAYMENT_COMPLETED` with session metadata. |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` | `$0` + force_recurring → `skip_capture`. Multiplies unit × qty. |
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` | Minor-unit multiply. |
| `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.Idempotency.cs` | Dual-event business key (adapter-side). |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` | Ledger tax from `sst_tax_amount` metadata (proves hop-1 stamps matter). |
| `apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs` | Registers `IBillingQueryService` in the monolith. |
| `apps/lazuar-api/BuildingBlocks/Infrastructure/PlatformDbContext.cs` | Fail-closed tenant filter. |
| `apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxEventBus.cs` | Publish = insert outbox row in the same DbContext. |
| `apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxPublisherJob.cs` | Later fan-out to InMemoryEventBus. |
| `apps/lazuar-api/BuildingBlocks/Infrastructure/InboxConsumerJob.cs` | Batch FOR UPDATE SKIP LOCKED. |

### Portal (only as they prove a checkout bug)

| File | What it owns |
|------|----------------|
| `apps/lazuar-portal/src/modules/checkout/lib/api.ts` | `Idempotency-Key` from sessionStorage keyed by tenant+product slug only. |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` | Interval toggle, qty, trial $0 display, coupon ratio against `product.price`. |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | Submit payload. No SST. No PWYW amount. No 409-specific recovery. |
| `apps/lazuar-portal/src/modules/checkout/components/OrderSummaryCard.tsx` | Total = pre-tax currentPrice. No SST line. |
| `apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` | Custom pay. Calls `submitCheckout` with `product_slug: "custom"`. |
| `apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` | Quote page. |

### Tests

| File | What it owns |
|------|----------------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs` | Coupon confirm, expiry release, mark-paid, $0 Stripe hop-2, Billplz bypass, quantity, replay, type filter, vault rules, ProcessZeroAmount reminder-only. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceCheckoutMetadataTests.cs` | Merge / persist / `IsCommerceSubscriptionType` (commerce + saas only). |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceCheckoutIdempotencyTests.cs` | Normalize + fingerprint length. No replay / EXPIRED / race. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceCheckoutQuantityTests.cs` | 1–99 FIXED; PWYW N≠1 throws. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SstTaxMathTests.cs` | 06=0; 02+reg=8; 02 no-reg=0. No initiate test. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionBillingAmountTests.cs` | Gross 108 / 324. `billing: null` → 100 (pins optional skip). |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionTrialTests.cs` | Domain ActivateTrial / SetTrialDays. No hop-2. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CouponLifecycleTests.cs` | Reserve / confirm / release / max uses. No concurrency. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CreateCustomCheckoutAndInitiateSessionTests.cs` | QT- number, net_30 due, custom B2B TIN, completed session throw. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CheckoutB2bIdentityTests.cs` | Product requires_tax_id + custom is_b2b_required stamp. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GetCheckoutStatusTests.cs` | OPEN→PENDING, COMPLETED, EXPIRED. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CreateManualSubscriberCommandHandlerTests.cs` | Manual enroll reminder-only. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/TenantIsolation/TenantIsolationHardeningTests.cs` | Cross-tenant GatewayPaymentCompleted is a no-op. |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/PaymentGatewayCapabilitiesTests.cs` | Off-session matrix. |

### Plans (context only; code wins)

| File | What it owns |
|------|----------------|
| `plans/009-bugs/README.md` | Slice map. This file is report 01. |
| `plans/008-evals/01-commerce-subscriptions-checkout.md` | Prior eval. Several of its P0s were later committed; trial type filter was mis-described. |

---

## 3. How this slice is supposed to work (mechanics, not marketing)

A first payment is a `CheckoutSession` that starts `OPEN` and ends `COMPLETED` (or `EXPIRED`). There is no `PAID` status. The public poller maps `OPEN` → `PENDING`, `COMPLETED` → `COMPLETED`, `EXPIRED` → `EXPIRED`. Success URLs carry `sub_id={session.Id}`. That handle is a **session** id, not a `Subscription` id, even on product checkouts.

There are two hop-1 shapes on the same `POST /public/commerce/checkout`:

**A. Custom quote.** `SessionId` is set. The session already exists (created by `CreateCustomCheckoutCommandHandler`). Initiate sums `AdHocLineItem.UnitPrice * Quantity`, optionally resolves CRM when `IsB2bRequired`, and mints hop-2 with `type=custom_payment_link`, `subscription_id=session.Id`, amount = **line total**, `Quantity = 1`. No Commerce `Subscription` is ever created. Webhook `HandleOpenCheckoutSessionAsync` completes the session, writes a transaction log, and emits `payment_link.paid`.

**B. Product slug.** Resolve workspace by slug (email provider is a hard gate). Normalize quantity. Resolve price (`price_id` wins, else `interval`, else catalog default). Reject trial + one-time price. If `TrialDays > 0` and both intervals are `mo`/`yr`, coupons are skipped and first charge is 0. Else a coupon may be locked, validated against the **resolved unit**, reserved, and applied. SST is exclusive via `GrossBreakdown` only if Billing returns an SST registration number. A new `CheckoutSession` is persisted with qty, `PriceId`, metadata, and optional idempotency.

Then the money fork:

1. `lineGross == 0` **and** product gateway is Stripe or CHIP **and** interval is `mo`/`yr` → mint hop-2 with amount `0`, `SetupFutureUsage: true`. Session stays `OPEN`. Stripe adapter uses `Mode = "setup"` because a $0 PaymentIntent is invalid. CHIP uses `force_recurring` + `skip_capture`. Metadata `type` is `"trial"` if trial, else `"commerce_subscription"`.
2. `lineGross == 0` otherwise (Billplz / Xendit / Razorpay / blank, or one-time 100% coupon) → `ProcessZeroAmountCheckoutCommand`. Completes the session in-process. Recurring rows are always `reminderOnly: true`. One-time writes an `Order` with `AmountPaid = 0`.
3. Else mint hop-2 with **unit gross** (net + SST) and `Quantity: N`. Adapters multiply. `SetupFutureUsage` is true iff interval ≠ `one_time`. Session stays `OPEN` until the webhook.

Idempotency is optional. Header `Idempotency-Key` (max 200). Fingerprint is SHA-256 of tenant, slug, email, coupon, qty, session id, interval, price id. Same key + different fingerprint → HTTP 409. Same key + same fingerprint + stored URL → replay that URL. Unique index `(OrganizationId, IdempotencyKey)` WHERE key IS NOT NULL.

Coupons: `Reserve` on initiate, `ConfirmReservation` on paid webhook / zero-amount / mark-paid, `ReleaseReservation` on the 5-minute expiry job when `OPEN && ExpiresAt < now`. `Validate` treats `UsedCount + ReservedCount >= MaxUses` as exhausted. Confirm without a reserve throws.

`SubscriptionActivation.Start` is the only first-activate router. If `product.TrialDays > 0` and `product.Interval` is `mo`/`yr`, it calls `ActivateTrial(now+TrialDays)` and parks `NextBillingDate` at trial end. Otherwise `Activate(now, now+interval)`. Vaulting is a separate step: the open-checkout webhook stores tokens only when the product gateway is not reminder-only and a token id is present.

Quantity: FIXED one-time / mo / yr may be 1–99. PWYW and anything else must be 1. Product hop-2 sends unit × N. Custom hop-2 sends pre-summed line and N=1. That contract is written on `GenerateCheckoutSessionQuery` and is what the quantity tests pin.

SST on first charge is exclusive, 2 dp away-from-zero, only tax type `02`, only when the merchant billing profile has `Sst_registration_number`. Hop-1 stamps `sst_tax_type` / `sst_tax_amount` / `sst_rate_percent` on gateway metadata so Billing can journal tax. The buyer-facing hop-1 summary does not display those fields.

Offline mark-paid is the clerk bypass: product sessions become Order or reminder-only Subscription; custom sessions just complete + tx log + ledger event. No card is vaulted. That is correct for cash. The amount must still be the amount the merchant believes was paid.

The inbox is the only Commerce consumer of `GatewayPaymentCompletedIntegrationEvent`. It refuses any `type` that is not `commerce_subscription`, `saas_subscription`, or `custom_payment_link`. That filter is the load-bearing gate for every first paid / setup event. `type=trial` is not on the list.

---

## 4. Walk the live handlers line-by-line

### 4.1 `InitiateCheckoutCommandHandler.Handle`

Workspace + email gate. No tenant slug on the path; slug lookup then `HasValidEmailConfigAsync`. Missing Resend disables checkout for Stripe and Billplz alike. That is a product rule, not a bug.

```48:58:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        var tenantId = await _oneQueryService.GetTenantIdBySlugAsync(request.TenantSlug);
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException($"Workspace with slug '{request.TenantSlug}' not found.");
        }

        var hasEmailConfig = await _communicationsQueryService.HasValidEmailConfigAsync(tenantId.Value);
        if (!hasEmailConfig)
        {
            throw new InvalidOperationException("This workspace has not configured an active email provider. Checkout is temporarily disabled.");
        }
```

Idempotency runs **before** the custom-vs-product split. Fingerprint includes session id, so two quotes are different payloads. Replay requires a stored `GatewayCheckoutUrl`. Status is not consulted. An `EXPIRED` row with a URL is returned as if it were still payable.

```60:88:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        var idempotencyKey = CommerceCheckoutIdempotency.NormalizeKey(request.IdempotencyKey);
        var fingerprint = CommerceCheckoutIdempotency.Fingerprint(
            tenantId.Value,
            request.ProductSlug,
            request.Email,
            request.CouponCode,
            request.Quantity,
            request.SessionId,
            request.Interval,
            request.PriceId);

        if (idempotencyKey != null)
        {
            var existing = await _repository.GetCheckoutSessionByIdempotencyKeyAsync(
                tenantId.Value, idempotencyKey, ct);
            if (existing != null)
            {
                if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("IDEMPOTENCY_CONFLICT: Idempotency-Key was reused with a different checkout payload.");
                }

                if (!string.IsNullOrWhiteSpace(existing.GatewayCheckoutUrl))
                {
                    return new CheckoutResultDto(
                        existing.GatewayCheckoutUrl,
                        existing.Status == "COMPLETED");
                }
            }
        }
```

If the key matches and the URL is empty, execution **falls through** and will try to insert a second session with the same key (product path) or remint (custom path). See B01-C04.

Custom branch: OPEN + same org only. No `ExpiresAt` check. Amount is the jsonb line sum. Currency is the string `"MYR"`. Quantity sent to Payments is 1. `SetupFutureUsage` is false. B2B without TIN throws. Gateway URL is overwritten every call. Idempotency is **not** written onto the quote session.

```101:163:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
            decimal customTotalAmount = existingSession.AdHocLineItems.Sum(x => x.UnitPrice * x.Quantity);
            // ...
            var customGatewayQuery = new GenerateCheckoutSessionQuery(
                tenantId.Value,
                customTotalAmount,
                "MYR",
                "Custom Payment Request",
                request.Email,
                customSuccessUrl,
                customCancelUrl,
                customMetadata,
                false,
                1,
                existingSession.GatewayName
            );

            var customCheckoutUrl = await _mediator.Send(customGatewayQuery, ct);
            existingSession.SetGatewayCheckoutUrl(customCheckoutUrl);
            await _repository.SaveChangesAsync(ct);
            return new CheckoutResultDto(customCheckoutUrl, false);
```

Product branch: quantity, price resolve, trial+one-time reject, `isTrial` conjunction, checkout-config enforce, CRM resolve.

```172:181:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        var quantity = CommerceCheckoutQuantity.NormalizeOrThrow(request.Quantity, product);
        var resolved = ResolveCheckoutPrice(product, request.PriceId, request.Interval);

        if (product.TrialDays > 0 && string.Equals(resolved.Interval, "one_time", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Free trial is not available on one-time products.");
        }

        var isTrial = SubscriptionActivation.IsTrialOffer(product)
            && resolved.Interval is "mo" or "yr";
```

Coupon is skipped on trial. Otherwise lock + validate + reserve against **resolved.Amount**.

```215:227:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        if (!isTrial && !string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = await _repository.GetCouponByCodeWithLockAsync(tenantId.Value, request.CouponCode, ct);
            if (coupon == null)
            {
                throw new InvalidOperationException($"Coupon with code '{request.CouponCode}' is invalid or expired.");
            }

            coupon.Validate(resolved.Amount, product.Id);
            unitDiscount = coupon.CalculateDiscount(resolved.Amount);
            coupon.Reserve();
            couponId = coupon.Id;
        }
```

SST: optional `_billingQueryService`. Null → `MerchantHasSstAsync` returns false → tax 0. Then `GrossBreakdown(unitNet, quantity, ...)`. The local name `lineNet` is actually **gross including tax**. The $0 fork uses that number, so a 100% coupon still goes $0 even if the product is SST `02` (tax on 0 is 0). Paid path sends `unitGross` + `quantity`, not the pre-multiplied line.

```229:237:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        var unitNet = isTrial ? 0m : Math.Max(0, resolved.Amount - unitDiscount);
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
            _billingQueryService, tenantId.Value);
        var breakdown = SubscriptionBillingAmount.GrossBreakdown(
            unitNet, quantity, product.SstTaxType, product.SstRatePercent, merchantHasSst);
        var sstType = breakdown.TaxType;
        var unitTax = breakdown.UnitTax;
        var unitGross = breakdown.UnitGross;
        var lineNet = breakdown.Gross;
```

Session persist, then unique-key race handler. Race only returns if the winner already has a URL.

$0 vaulting fork — this is the 8b3567d fix, and also where `type=trial` is stamped:

```286:316:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        if (lineNet == 0)
        {
            var vaultingRecurring = PaymentGatewayCapabilities.SupportsOffSession(product.GatewayName)
                && resolved.Interval is "mo" or "yr";
            if (vaultingRecurring)
            {
                // ...
                vaultMetadata["type"] = isTrial ? "trial" : "commerce_subscription";
                var vaultQuery = new GenerateCheckoutSessionQuery(
                    tenantId.Value,
                    0m,
                    product.Currency,
                    product.Name,
                    request.Email,
                    successUrl,
                    cancelUrl,
                    vaultMetadata,
                    true,
                    quantity,
                    string.IsNullOrWhiteSpace(product.GatewayName) ? null : product.GatewayName
                );
```

Paid fork stamps SST metadata only when `unitTax > 0`, sends unit gross, `SetupFutureUsage = resolved.Interval != "one_time"`.

`ResolveCheckoutPrice` is honest: unknown `price_id` throws; unknown interval throws unless it matches the catalog default.

`EnforceCheckoutConfiguration` requires phone / TIN+id type+id value+company / full address when the product flags say so. `is_b2b_required` on the session is **not** those flags; it is “buyer typed a TaxId”.

### 4.2 `ProcessZeroAmountCheckoutCommandHandler.Handle`

Reload session. Must be OPEN + same org. Product by id (no org check). Coupon discount is `CalculateDiscount(product.Price)`, not the chosen price row. Trial short-circuits the payable check to 0. Recurring `Start(..., reminderOnly: true)` **always**, even if `product.GatewayName` is STRIPE. Events go to the Commerce outbox, then `SaveChanges`.

```41:65:apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs
        var quantity = Math.Max(1, session.Quantity);
        var unitDiscount = 0m;
        var couponCode = "NONE";

        if (session.CouponId.HasValue)
        {
            var coupon = await _repository.GetCouponByIdAsync(session.CouponId.Value, ct);
            if (coupon != null)
            {
                unitDiscount = coupon.CalculateDiscount(product.Price);
                couponCode = coupon.Code;
                coupon.ConfirmReservation();
            }
        }

        var chosen = product.Prices.FirstOrDefault(p => p.Id == session.PriceId);
        var unitAmount = chosen?.Amount ?? product.Price;
        var lineGross = unitAmount * quantity;
        var lineDiscount = unitDiscount * quantity;
        var isTrial = SubscriptionActivation.IsTrialOffer(product);
        var finalPrice = isTrial ? 0m : Math.Max(0, lineGross - lineDiscount);
        if (finalPrice > 0)
        {
            throw new InvalidOperationException("This checkout session requires payment and cannot bypass the gateway.");
        }
```

`ConfirmReservation` throws if `ReservedCount <= 0`. A session whose reservation was released (expiry job) cannot be zero-completed.

### 4.3 `SubscriptionActivation`

```8:32:apps/lazuar-api/Modules/Commerce/Application/SubscriptionActivation.cs
    public static bool IsTrialOffer(Product product) =>
        product.TrialDays > 0 && product.Interval is "mo" or "yr";

    public static void Start(...)
    {
        var instant = now ?? DateTime.UtcNow;
        var interval = string.IsNullOrWhiteSpace(billingInterval) ? product.Interval : billingInterval;

        if (IsTrialOffer(product))
        {
            subscription.ActivateTrial(instant.AddDays(product.TrialDays), reminderOnly, quantity, unitAmount);
        }
        else
        {
            var next = SubscriptionBillingAmount.AdvanceFrom(instant, interval);
            subscription.Activate(instant, next, reminderOnly, quantity, unitAmount);
        }
```

`IsTrialOffer` keys off **catalog** `product.Interval`, not the chosen price interval. Initiate already forbids trial + resolved one-time. Yearly checkout of a monthly-default trial product still trials. That matches hop-1 UI (`trialDays` follows selected interval ≠ one_time).

### 4.4 SST helpers

```14:23:apps/lazuar-api/Modules/Commerce/Application/SstTaxMath.cs
        if (!merchantHasSstRegistration
            || !string.Equals(requestedType, ServiceTax, StringComparison.OrdinalIgnoreCase)
            || ratePercent <= 0
            || netAmount <= 0)
        {
            return (NotApplicable, 0m);
        }

        var tax = Math.Round(netAmount * ratePercent / 100m, 2, MidpointRounding.AwayFromZero);
        return (ServiceTax, tax);
```

`GrossBreakdown` taxes the **unit**, then multiplies:

```41:44:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
        seats = Math.Max(1, seats);
        var (taxType, unitTax) = SstTaxMath.Compute(sstTaxType, sstRatePercent, unitNet, merchantHasSst);
        var unitGross = unitNet + unitTax;
        return new Breakdown(unitNet, unitTax, unitGross, seats, unitGross * seats, taxType);
```

`MerchantHasSstAsync(null, _)` is `false`. That is the optional-DI skip.

### 4.5 Metadata, quantity, idempotency helpers

`IsCommerceSubscriptionType` is two strings. Not `trial`. Not `custom_payment_link`.

```33:35:apps/lazuar-api/Modules/Commerce/Application/CommerceCheckoutMetadata.cs
    public static bool IsCommerceSubscriptionType(string? type) =>
        string.Equals(type, TypeCommerce, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, TypeSaas, StringComparison.OrdinalIgnoreCase);
```

`MergeClientIntoGateway` forces `commerce_subscription` unless the client sent `saas_subscription`. Initiate then **overwrites** `type` to `trial` on the vaulting path. Persistence `ForPersistence` never stores `trial`; a trial session’s `MetadataJson` is still `commerce_subscription` unless the client sent saas.

Quantity helper is strict and is called **before** CRM resolve / persist. Out-of-range never creates a session. Tests cover that.

Fingerprint material is listed in §3. It does not include name, TIN, address, or client metadata (`aura_org_id`).

### 4.6 Domain: session, coupon, product, order, subscription

`CheckoutSession.Complete()` / `Expire()` assign the string and bump `UpdatedAt`. There is no guard, no original-status check, no row version.

```134:144:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/CheckoutSession.cs
    public void Complete()
    {
        Status = "COMPLETED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        Status = "EXPIRED";
        UpdatedAt = DateTime.UtcNow;
    }
```

Coupon confirm is fail-closed; release is a no-op at zero. `Validate` includes reservations in the cap. `CalculateDiscount` caps at original price.

`Product.SetTrialDays` rejects one-time. `SetSst` coerces non-`02` / non-positive rate to `06`/0.

`Order` stores `AmountPaid` as given. Zero-amount writes 0. Offline writes discounted catalog line. Webhook writes `@event.AmountPaid` (processor gross, which includes hop-1 SST if hop-1 charged it).

`Subscription.ActivateTrial` requires a future end, sets `TRIALING`, parks all three clocks on that end.

### 4.7 `GatewayPaymentCompletedIntegrationEventHandler`

Entry:

```31:65:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs
        var type = @event.Metadata.GetValueOrDefault("type");
        if (!CommerceCheckoutMetadata.IsCommerceSubscriptionType(type) && type != "custom_payment_link")
        {
            return;
        }
        // tenant_id mismatch → return
        // missing subscription_id/receipt Guid → return
        // load session IgnoreQueryFilters by (Id, OrganizationId)
        if (session != null && session.Status == "OPEN")
        {
            await HandleOpenCheckoutSessionAsync(@event, session, type!);
            return;
        }
        await HandleSubscriptionPaymentAsync(@event, correlationId);
```

`type=trial` hits the first `return`. Session stays OPEN. No coupon confirm. No subscription. No order. No tx log. The Payments module has already treated Stripe `checkout.session.completed` (setup mode) as `PAYMENT_COMPLETED` and copied session metadata through. Commerce is the module that drops it.

`HandleOpenCheckoutSessionAsync`: confirm coupon if `ReservedCount > 0` (skip, not throw, if already 0). `session.Complete()` **before** product load. Custom type: log + `payment_link.paid` + save + return. Product: load with Prices. Recurring if `product.Interval != "one_time"` (catalog interval, not `session.PriceId`). `Start` then maybe `StoreVaultedToken`. One-time: `Order(@event.AmountPaid, session.Quantity)`.

`TryVaultIds` refuses reminder-only gateways even if junk tokens are present. Tests pin Billplz junk → reminder-only, Stripe/CHIP tokens → vault.

If the session is not OPEN, `HandleSubscriptionPaymentAsync` looks up a **Subscription** with that id. A checkout session id will not match. Duplicate webhook after COMPLETED is a no-op. That is what `GatewayPaymentCompleted_SameEventTwice_DoesNotCreateSecondSubscription` proves — **sequential** replay, same DbContext.

### 4.8 Mark-paid, custom create, expiry, validate-coupon

Mark-paid: OPEN + org. Product path mirrors zero-amount money math (`product.Price` discount, chosen price for snapshot) and always `reminderOnly: true`. Custom path sums lines, currency `MYR`, no product. Ledger event only if `totalAmount > 0`.

Custom create: CRM resolve with empty phone. ExpiresAt default now+30d. Due terms `due_on_receipt` / `net_7` / `net_15` / `net_30`. Link expiry is raised to due+14d. `QT-` number allocated once. Line items are passed through `AdHocLineItem` with no numeric guard.

Expiry job: every 5 minutes, all OPEN past `ExpiresAt`, `Expire()` + `ReleaseReservation`. No `FOR UPDATE` on the session rows. No skip of sessions that a webhook is currently completing.

Validate-coupon: product by slug, coupon by code, `Validate(product.Price, product.Id)`, discount on `product.Price`. No `interval` / `price_id` / quantity.

### 4.9 HTTP + portal that prove first-charge behaviour

`PublicCheckoutEndpoints` reads `Idempotency-Key`, maps 409 on `IDEMPOTENCY_CONFLICT`, and **swallows every other exception as HTTP 400** (`ex.InnerException?.Message ?? ex.Message`). A unique-constraint leak from the idempotency race becomes a BadRequest string.

Portal `checkoutIdempotencyKey(tenantSlug, productSlug)` stores one UUID per tab per product. Quantity, coupon, email, interval, and `session_id` are not part of the key. `QuoteView` calls the same helper with `product_slug: "custom"`. `OrderSummaryCard` has no SST. `CheckoutView` applies coupon as `discount_amount / product.price` times the **selected** line.

### 4.10 Stripe setup mode (Commerce-relevant only)

```454:472:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
        if (amount == 0 && setupFutureUsage)
        {
            var setupOptions = new SessionCreateOptions
            {
                Mode = "setup",
                // ...
                Metadata = metadata,
                SetupIntentData = new SessionSetupIntentDataOptions
                {
                    Metadata = metadata
                },
```

Parse path: `checkout.session.completed` with no PaymentIntent calls `ReadSetupSessionVaultIds` and still returns `EventType: "PAYMENT_COMPLETED"` with `session.Metadata`. That is how a $0 coupon hop-2 (`type=commerce_subscription`) activates. It is also how a trial hop-2 (`type=trial`) arrives at Commerce and is dropped.

---

## 5. Bug catalog

### B01-C01 — `type=trial` hop-2 is dropped; Stripe/CHIP trials never activate

**Severity:** P0  
**One-sentence fault:** Initiate stamps vaulting trial hop-2 as `type=trial`; `GatewayPaymentCompleted` returns before looking at the OPEN session because `IsCommerceSubscriptionType` does not accept `"trial"`.

**Evidence.**

Initiate, after `MergeClientIntoGateway` (which would have set `commerce_subscription`):

```299:299:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
                vaultMetadata["type"] = isTrial ? "trial" : "commerce_subscription";
```

Filter:

```33:36:apps/lazuar-api/Modules/Commerce/Application/CommerceCheckoutMetadata.cs
    public static bool IsCommerceSubscriptionType(string? type) =>
        string.Equals(type, TypeCommerce, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, TypeSaas, StringComparison.OrdinalIgnoreCase);
```

```33:37:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs
        var type = @event.Metadata.GetValueOrDefault("type");
        if (!CommerceCheckoutMetadata.IsCommerceSubscriptionType(type) && type != "custom_payment_link")
        {
            return;
        }
```

Stripe will emit `PAYMENT_COMPLETED` for setup-mode `checkout.session.completed` and will pass the metadata through (`StripeGatewayAdapter.ParseWebhookAsync`, `EventType: "PAYMENT_COMPLETED"`, `Metadata: meta`). Commerce never reaches `HandleOpenCheckoutSessionAsync`. `SubscriptionActivation.Start` → `ActivateTrial` is dead on the vaulting path.

008 (`plans/008-evals/01-commerce-subscriptions-checkout.md` §6 Trial) claimed: “Vaulting gateways: $0 setup-future hop 2, `type=trial`. Webhook `HandleOpenCheckoutSessionAsync` then `SubscriptionActivation.Start`.” That paragraph describes the **intended** wire. The type filter was not re-read. The intention is not the live behaviour.

**Reproduction in words.** Merchant creates a monthly Stripe product with `TrialDays = 14`. Buyer submits hop-1. Initiate persists an OPEN session, mints a Stripe Checkout session in `mode=setup`, returns that URL, `IsZeroAmountBypass = false`. Buyer completes card setup. Stripe fires `checkout.session.completed`. Payments publishes `GatewayPaymentCompletedIntegrationEvent` with `type=trial`, `subscription_id={sessionId}`, customer + payment method ids. Commerce handler returns. Session remains OPEN. Public status poller stays `PENDING`. Success page spins. No `Subscription` row. No `TRIALING`. No `SubscriptionActivatedIntegrationEvent`. After 24 hours the expiry job marks EXPIRED. The card is a Stripe Customer + PaymentMethod with no Commerce row to hang it on.

Billplz trial does **not** hit this bug: `SupportsOffSession("BILLPLZ")` is false, so initiate calls `ProcessZeroAmount`, which `ActivateTrial(..., reminderOnly: true)` in-process.

A 100% coupon on a **non-trial** Stripe monthly product is also not this bug: `type` is `commerce_subscription`, webhook accepts it, vaults, `Activate`s.

**Blast radius.** Every Stripe or CHIP product with `TrialDays > 0`. The sold trial path (card on file, convert at day N) does not create the membership. Buyer thinks they started a trial. Merchant sees an OPEN then EXPIRED session and no subscriber. Integrators waiting on `subscription.activated` never fire fulfillment. The orphaned Stripe customer/PM is not attached to anything Commerce can charge.

**Why tests missed it.** `SubscriptionTrialTests` only hit `ActivateTrial` and `SetTrialDays` on the domain. `InitiateCheckout_HundredPercentCoupon_StripeMonthly_MintsHop2SetupSession` asserts `type == "commerce_subscription"`, not a trial product. `IsCommerceSubscriptionType_AcceptsSaasAlias` asserts commerce + saas true and custom false; it never mentions `"trial"`. `GatewayPaymentCompleted_NonCommerceType_LeavesSessionOpen` uses `utility_credit_topup` and thereby **pins the drop behaviour** for any non-allowlisted type, which includes `trial`.

**Fix direction (do not implement).** Stop sending `type=trial`. Keep `commerce_subscription` (or saas) and let `SubscriptionActivation.IsTrialOffer(product)` decide `ActivateTrial` — the open-checkout handler already does that. Alternatively add `"trial"` to the allow-list **and** to `IsCommerceSubscriptionType`, and add a test: initiate trial Stripe → webhook with `type=trial` (or whatever you stamp) → one `TRIALING` row with vault ids. Do not treat this as a Payments bug; Payments is doing what Commerce asked.

---

### B01-C02 — Coupon `FOR UPDATE` is not inside a transaction

**Severity:** P1  
**One-sentence fault:** `GetCouponByCodeWithLockAsync` issues `SELECT … FOR UPDATE` and then `Validate` / `Reserve` / `SaveChanges` run without an ambient transaction, so the row lock is gone before the reservation is committed.

**Evidence.**

```66:76:apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs
    public async Task<Coupon?> GetCouponByCodeWithLockAsync(...)
    {
        return await _context.Coupons
            .FromSqlRaw(@"
                SELECT * FROM commerce.""Coupons"" 
                WHERE ""OrganizationId"" = {0} AND ""Code"" = {1} AND ""IsActive"" = true 
                FOR UPDATE", organizationId, normalizedCode)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ct);
    }
```

There is no `IPipelineBehavior` transaction wrapper in this repo (grep of `IPipelineBehavior` / `BeginTransaction` in Commerce HTTP handlers is empty). BillingEngineJob and InboxConsumerJob start their own transactions; initiate does not. In PostgreSQL a `FOR UPDATE` taken outside a transaction is released at the end of the statement.

`Coupon.Validate` then reads the in-memory `UsedCount + ReservedCount` snapshot from that SELECT.

```88:91:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Coupon.cs
        if (MaxUses > 0 && (UsedCount + ReservedCount) >= MaxUses)
        {
            CheckRule(new GenericBusinessRule("This coupon has reached its maximum usage limit."));
        }
```

**Reproduction in words.** Coupon `MaxUses = 1`. Two buyers submit hop-1 with that code in the same second. Both SELECTs see reserved=0, both `Validate`, both `Reserve` in memory (`ReservedCount = 1` on two tracked instances), both `SaveChanges`. Last writer wins on the integer columns. Two OPEN sessions hold the same coupon. Two payments can confirm (webhook only checks `ReservedCount > 0` at confirm time; after two last-write-wins the DB reserved count may be 1, so the second confirm can throw — or, if both confirm before the other’s save, used can go to 2 while max is 1).

**Blast radius.** Limited-run launch codes, “first 50 customers”, influencer one-use codes. Merchant promised a cap. The cap is not a serializable constraint; it is an unlocked integer.

**Why tests missed it.** `CouponLifecycleTests` are single-threaded in-memory. Completeness tests reserve once. There is no concurrent initiate test and InMemoryDatabase would not honour `FOR UPDATE` anyway.

**Fix direction.** Open a transaction on the Commerce context that covers lock + validate + reserve + session insert + `SaveChanges`. Or add a check constraint / trigger. A unique “one confirmed redemption per (coupon, client)” is a second line of defence, not a substitute for the lock.

---

### B01-C03 — Zero-amount and offline re-discount `product.Price`, not the chosen price row

**Severity:** P1  
**One-sentence fault:** Initiate applies the coupon to `resolved.Amount`; `ProcessZeroAmount` and mark-paid apply it to `product.Price`, then compare against the chosen row, so a 100% yearly coupon on a monthly-default product throws (or under-discounts cash).

**Evidence.** Initiate (honest):

```223:224:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
            coupon.Validate(resolved.Amount, product.Id);
            unitDiscount = coupon.CalculateDiscount(resolved.Amount);
```

Zero-amount (not honest):

```50:64:apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs
                unitDiscount = coupon.CalculateDiscount(product.Price);
                // ...
        var chosen = product.Prices.FirstOrDefault(p => p.Id == session.PriceId);
        var unitAmount = chosen?.Amount ?? product.Price;
        var lineGross = unitAmount * quantity;
        var lineDiscount = unitDiscount * quantity;
        var isTrial = SubscriptionActivation.IsTrialOffer(product);
        var finalPrice = isTrial ? 0m : Math.Max(0, lineGross - lineDiscount);
        if (finalPrice > 0)
        {
            throw new InvalidOperationException("This checkout session requires payment and cannot bypass the gateway.");
        }
```

Mark-paid (same lie):

```85:92:apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs
                unitDiscount = coupon.CalculateDiscount(product.Price);
                coupon.ConfirmReservation();
            }
        }

        var lineGross = product.Price * quantity;
        var lineDiscount = unitDiscount * quantity;
        var totalAmount = Math.Max(0, lineGross - lineDiscount);
```

Mark-paid is worse: the **transaction log and Order amount** use `product.Price`, while the subscription snapshot uses `chosen?.Amount ?? product.Price`. A yearly cash settlement is booked at the monthly catalog price.

008 already named this as P1 item 10. It is still in the tree.

**Reproduction in words.** Product default `Interval=mo`, `Price=100`, yearly row `1000`. Coupon `PERCENTAGE 100`. Buyer selects yearly on hop-1 (Billplz). Initiate: unitNet = 0, `lineNet = 0`, not vaulting, calls ProcessZeroAmount. ProcessZeroAmount: discount = 100, unitAmount = 1000, finalPrice = 900, throws. Session stays OPEN, coupon stays reserved, buyer sees a 400. Clerk mark-paid of a yearly session with a 10% coupon books `100 * qty * 0.9` instead of `1000 * qty * 0.9`.

Stripe yearly 100% coupon does **not** hit ProcessZeroAmount (hop-2 $0, type commerce). The webhook confirms the coupon and snapshots 1000. The Billplz / one-time-adjacent / mark-paid paths are the broken ones.

**Blast radius.** Dual-price catalogs (the Wave 3 monthly+yearly product). Reminder-only rails. Clerk cash against a quote that used a coupon. Inverse: a FIXED coupon sized to the monthly price can zero a yearly line if someone called ProcessZeroAmount with a yearly `PriceId` and a monthly `product.Price` larger than the coupon — wait, FIXED 100 on monthly 100 is 100 off; yearly unit 1000 − 100 = 900, still throws. A FIXED coupon of 1000 validated against yearly at initiate would discount only 100 at zero-amount. Always under-discount relative to the chosen row when the chosen row is larger.

**Why tests missed it.** Completeness $0 tests use `CreateProduct` with a single price equal to `product.Price`. `MarkCheckoutAsPaidOffline_OneTime_Qty3` has no coupon and no yearly row.

**Fix direction.** Both handlers must resolve `chosen` first, then `CalculateDiscount(unitAmount)` and (for mark-paid) `lineGross = unitAmount * quantity`. Confirm the reservation after the payable check so a throw does not need a reserve.

---

### B01-C04 — Idempotency replay returns EXPIRED URLs and empty-URL rows fall through to a second insert

**Severity:** P1  
**One-sentence fault:** Replay is “same fingerprint + non-empty URL”, not “same fingerprint + still OPEN”; a first save without a URL is not treated as in-flight.

**Evidence.** See the block in §4.1 (`InitiateCheckoutCommandHandler.cs` 71–88). Unique index:

```218:220:apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs
            builder.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
```

Race catch (261–280) only returns if the other row **has** a URL; otherwise it rethrows the unique violation.

**Reproduction in words.**

1. **Expired replay.** Buyer initiates, gets a hop-2 URL, walks away 25 hours. Expiry job marks EXPIRED and releases the coupon. Same tab retries (portal reuses sessionStorage UUID). Handler finds the EXPIRED row, fingerprint matches, URL is set, returns that Stripe/Billplz link. Buyer pays. Webhook: session status is not OPEN → `HandleSubscriptionPaymentAsync` finds no subscription with that id → no-op. Processor has the money. Commerce has no Order / Subscription.
2. **Empty URL.** First request inserts the session + reserve, then `GenerateCheckoutSessionQuery` throws (CHIP missing brand id, Stripe key rejected). `GatewayCheckoutUrl` is still null. Retry: existing found, no URL, fall through, `AddCheckoutSession` another row with the same key, unique violation, catch sees no URL, rethrow → HTTP 400 with a database message. Coupon remains reserved on the orphan OPEN session until expiry.

**Blast radius.** Anyone using `Idempotency-Key` (the hosted portal always does). Abandoned hop-2 after 24h is the common case. Gateway-down first attempt is the support case.

**Why tests missed it.** `CommerceCheckoutIdempotencyTests` only test normalize and fingerprint change. No handler test for EXPIRED replay or missing URL.

**Fix direction.** Replay only `OPEN && ExpiresAt > now && URL present`. If OPEN and URL missing, resume mint on **that** row (do not insert). If EXPIRED / COMPLETED, mint a new session **or** reject with a typed error; do not hand back a dead processor URL. Catch unique violations and wait/re-read until the winner has a URL or has failed.

---

### B01-C05 — Custom quote remints hop-2 every time; portal key is per slug not per quote

**Severity:** P1  
**One-sentence fault:** The custom branch never writes idempotency onto the quote session and always calls `GenerateCheckoutSessionQuery` again, so retries mint a second live processor session; the portal key `lazuar-checkout-idem:{tenant}:custom` also collides product checkout in the same tab.

**Evidence.** Custom branch (§4.1) has no `SetIdempotency`, no “if `GatewayCheckoutUrl` already set, return it”. Portal:

```34:45:apps/lazuar-portal/src/modules/checkout/lib/api.ts
function checkoutIdempotencyKey(tenantSlug: string, productSlug: string) {
  const storageKey = `lazuar-checkout-idem:${tenantSlug}:${productSlug}`;
  // random UUID persisted in sessionStorage
}
```

`QuoteView.handleProceedToPayment` posts `product_slug: "custom"` and `session_id`. `CheckoutForm` posts the real slug. After a product initiate in that tab, the key exists on a product session. A later quote pay sends the same header with a different fingerprint (`session_id` / slug) → `IDEMPOTENCY_CONFLICT`. Changing quantity or coupon on the product form after the first 200 also 409s; the form has no recovery other than `onError(err.message)`.

**Reproduction in words.** Buyer double-clicks “Pay” on a quote. Two Stripe Checkout sessions are created for the same OPEN quote. Buyer pays the first link in one window and the second in another. First webhook completes the session. Second webhook sees COMPLETED and no-ops. Merchant has two processor captures, one Commerce completion. Ledger (out of slice) may journal both if Payments emits two events with different gateway transaction ids.

Same tab: buy a product, then open a quote, click pay → 409 `IDEMPOTENCY_CONFLICT`.

**Blast radius.** Every custom quote. Double-click is the default browser behaviour. Two live processor URLs for one Commerce session is a money duplicate, not a polish issue.

**Why tests missed it.** `InitiateCheckout_CustomSession_StillSendsLineSumAndQuantityOne` and the B2B custom tests stub a single `GenerateCheckoutSessionQuery`. No second call. No header.

**Fix direction.** If the quote is OPEN and already has a URL and is not expired, return it. Persist the idempotency key on the quote session on first mint. Portal key must include `session_id` (and email / qty / coupon / interval / price_id for product). Rotate the key on 409.

---

### B01-C06 — Hop-1 total omits SST; buyer is charged unit+tax

**Severity:** P1  
**One-sentence fault:** Public product DTO includes `sst_tax_type` / `sst_rate_percent`; the hosted summary never uses them; initiate adds exclusive SST after submit.

**Evidence.** `OrderSummaryCard` total is `finalPriceToDisplay` derived from `currentPrice` / coupon. Grep of that file for `sst` is empty. Initiate paid path:

```336:360:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
            if (unitTax > 0)
            {
                metadata["sst_tax_type"] = sstType;
                metadata["sst_tax_amount"] = (unitTax * quantity).ToString("0.00");
                metadata["sst_rate_percent"] = product.SstRatePercent.ToString("0.##");
            }
            // Amount is unit price (net + SST); adapters multiply by Quantity.
            var gatewayQuery = new GenerateCheckoutSessionQuery(
                tenantId.Value,
                unitGross,
                // ...
                quantity,
```

Product GET **does** return the rate (`CommerceQueryService.Products.cs` 140–141). The lie is hop-1 chrome, not missing catalog data.

008 P1 item 6. Still true. In this slice because it is the first-charge amount the buyer consents to.

**Reproduction in words.** SST-registered merchant, product `02` / 8%, price 100. Hop-1 shows RM 100. Billplz/Stripe hop-2 shows RM 108. Buyer who does not read the processor page is over-surprised, not undercharged. Charge is legally the SST-inclusive amount; consent UX is the pre-tax amount.

**Blast radius.** Every SST `02` product on hosted checkout. Conversion and complaint risk. Not a silent undercharge on hop-1 itself (that was the pre-eba0741 bug).

**Why tests missed it.** No portal test of the total. API tests that initiate without `IBillingQueryService` never add tax, so they cannot catch a UI miss.

**Fix direction.** Compute exclusive tax on hop-1 from product SST fields **and** a public “merchant has SST id” flag (or always show “SST applies at payment if registered”). Show tax line + gross. Keep GrossBreakdown as SSoT.

---

### B01-C07 — Validate-coupon and hop-1 discount math ignore the selected price row

**Severity:** P1  
**One-sentence fault:** `ValidateCouponQuery` discounts `product.Price`; hop-1 then scales that number as a ratio of `product.price` against the selected yearly×qty line, which is wrong for FIXED coupons and can fail `MinimumOriginalPrice` on the wrong amount.

**Evidence.**

```32:41:apps/lazuar-api/Modules/Commerce/Application/Queries/ValidateCouponQueryHandler.cs
        coupon.Validate(product.Price, product.Id);

        var discount = coupon.CalculateDiscount(product.Price);
        var finalPrice = Math.Max(0, product.Price - discount);

        return new ValidateCouponResponseDto
        {
            Is_valid = true,
            Discount_amount = (double)discount,
            Final_price = (double)finalPrice
        };
```

```55:60:apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx
      const data = await validateCouponCode(tenantSlug, product.slug, code);
      const discountRatio = data.discount_amount / product.price;
      const totalDiscount = basePriceForQuantity * discountRatio;
      setDiscountAmount(totalDiscount);
      setFinalPrice(Math.max(0, basePriceForQuantity - totalDiscount));
```

Initiate (the real charge) uses `resolved.Amount`.

**Reproduction in words.** Monthly 100, yearly 1000, FIXED 30 coupon. Validate: discount 30, final 70. UI on yearly qty 3: ratio 0.3, totalDiscount 900, shows RM 2100 off a RM 3000 line. Charge: (1000−30)×3 = 2910 (+ SST). Percentage coupons accidentally look right because the ratio is the percent. A coupon with `MinimumOriginalPrice = 500` cannot be applied on the yearly toggle if monthly is 100 — validate throws “minimum original price” even though 1000 qualifies.

**Blast radius.** Dual-price products + FIXED coupons. UI lie on every yearly toggle. Real initiate may still charge correctly (P1 trust, not always P1 money). Combined with B01-C06 the hop-1 number is wrong twice.

**Why tests missed it.** No validate-coupon test with `Prices` populated. No frontend money test.

**Fix direction.** Validate-coupon takes `interval` / `price_id` / `quantity`. Return unit discount and line discount against the resolved amount. Delete the client-side ratio.

---

### B01-C08 — Custom quotes and offline mark-paid never apply SST on first charge

**Severity:** P1  
**One-sentence fault:** Hop-1 SST exists only on the product initiate path; a custom quote hop-2 and a clerk mark-paid book the pre-tax line even when the merchant has an SST id.

**Evidence.** Custom initiate amount is the raw sum, currency `MYR`, no GrossBreakdown, no `sst_tax_*` metadata (§4.1). Mark-paid `totalAmount` is catalog/coupon math with no tax (§4.8). Quote GET `Total_amount` is the same raw sum (`CommerceQueryService.CustomCheckouts.cs` 67, 112). Billing journals tax from metadata:

```159:174:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
        if (@event.Metadata != null
            && @event.Metadata.TryGetValue("sst_tax_amount", out var raw)
            && decimal.TryParse(...)
            && parsed > 0)
        {
            return parsed;
        }
```

No stamp → tax 0 on the ledger for that first charge (ledger internals are out of slice; the missing stamp is in slice).

**Reproduction in words.** SST-registered studio sends a QT- for RM 5000 design. Buyer pays RM 5000. No 8% collected. Clerk marks a product session paid: tx log 100, not 108.

**Blast radius.** Every custom quote and every offline settlement for an SST-registered merchant. This is first-charge tax, not renewal tax (renewals are report 02).

**Why tests missed it.** Custom tests assert 500 and quantity 1. Mark-paid tests assert 300 for qty 3 at 100. No billing fake with an SST number is injected into those handlers.

**Fix direction.** Run GrossBreakdown on custom line totals (or per line) when the merchant has an SST id; stamp metadata; show tax on `QuoteView`. Mark-paid should book the same gross the hop-1 product path would have charged, or require the clerk to type the tax-inclusive amount explicitly.

---

### B01-C09 — OPEN session has no concurrency token; two completers can both fulfill

**Severity:** P1  
**One-sentence fault:** `Complete()` is an unguarded string write; two concurrent `HandleOpenCheckoutSessionAsync` or mark-paid vs webhook both see OPEN and both insert a Subscription or Order.

**Evidence.** No `IsConcurrencyToken` / `RowVersion` / `xmin` in Commerce mappings (grep empty). Session load in the webhook is a plain `FirstOrDefaultAsync`. Inbox SKIP LOCKED serialises **messages**, not **sessions**. Two API instances can process two inbox rows for the same session (different EventIds) at once. Mark-paid is a synchronous admin POST against the same OPEN row.

Sequential replay is safe and tested (`GatewayPaymentCompleted_SameEventTwice_DoesNotCreateSecondSubscription`): after the first save, status is COMPLETED, second call goes to `HandleSubscriptionPaymentAsync` and no-ops.

**Reproduction in words.** Stripe delivers `checkout.session.completed` twice with two EventIds before Payments’ business key can collapse them (or mark-paid races the first webhook). Two Commerce inbox messages, two scopes, two OPEN reads, two `AddSubscription`, two `subscription.activated`, two fulfillment lists. Buyer is provisioned twice.

**Speculation (labeled):** Payments’ `BuildBusinessKey(eventType, gatewayTransactionId)` is supposed to collapse `checkout.session.completed` + `payment_intent.succeeded`. Setup-mode trials only have a SetupIntent id; a single EventId is the common case. The race is real for mark-paid vs webhook and for any dual EventId leak; it is not proven daily.

**Blast radius.** Double entitlement, double outbound webhook, double ledger if Billing also consumes both. Money/access, not chrome.

**Why tests missed it.** InMemory + sequential `HandleAsync` twice. No parallel test, no row version.

**Fix direction.** `UPDATE … SET status='COMPLETED' WHERE id=@id AND status='OPEN' RETURNING *` (or EF concurrency token). Only the winner creates the Order/Subscription. Confirm coupon in the same transaction.

---

### B01-C10 — Expiry job vs paid webhook: money captured, session EXPIRED, no entitlement

**Severity:** P1  
**One-sentence fault:** The expiry job expires any OPEN row past `ExpiresAt` without locking against an in-flight payment; a late webhook then no-ops because status ≠ OPEN.

**Evidence.** Expiry (`CheckoutSessionExpiryJob.ExpireSessionsAsync` 56–91) loads OPEN + past ExpiresAt, `Expire()`, `ReleaseReservation()`, save. Webhook only fulfills `session.Status == "OPEN"`. Product sessions expire 24h after create (`AddHours(24)`). Custom sessions can be 30d + due+14.

**Reproduction in words.** Buyer opens hop-2 at hour 23:59. Pays at hour 24:02. Expiry tick at 24:00 already released the coupon and set EXPIRED. Webhook finds EXPIRED, looks up a subscription by session id, returns. Processor settled. Commerce has no sub. Status poller shows EXPIRED, not COMPLETED.

**Blast radius.** Slow banks / FPX / abandoned-then-returned Billplz pages near the 24h edge. Combined with B01-C04, an idempotent retry after expiry hands the same dead-or-still-payable processor URL back.

**Why tests missed it.** Expiry test uses `AddHours(-2)` and never pays. Webhook tests use `AddHours(1)`.

**Fix direction.** Expiry should skip rows that have a `GatewayCheckoutUrl` and a recently updated timestamp, or use a compare-and-swap that loses to Complete. Prefer: if a payment arrives for EXPIRED, **revive and fulfill** (and re-confirm or increment used without requiring reserved). Do not silently drop a paid event.

---

### B01-C11 — Optional `IBillingQueryService` silently zeroes hop-1 SST

**Severity:** P2 (latent in production; pinned in tests)  
**One-sentence fault:** `InitiateCheckoutCommandHandler` takes `IBillingQueryService? = null`; `MerchantHasSstAsync` returns false when billing is null, so SST is skipped without a log.

**Evidence.**

```28:36:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
    private readonly IBillingQueryService? _billingQueryService;
    public InitiateCheckoutCommandHandler(..., IBillingQueryService? billingQueryService = null)
```

```65:73:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static async Task<bool> MerchantHasSstAsync(IBillingQueryService? billing, Guid organizationId)
    {
        if (billing == null)
        {
            return false;
        }
        var profile = await billing.GetBillingProfileAsync(organizationId);
        return !string.IsNullOrWhiteSpace(profile?.Sst_registration_number);
    }
```

Monolith registers `IBillingQueryService` in `Billing/Infrastructure/DependencyInjection.cs`. Production MediatR will inject it. Tests (`CreateInitiateHandler`, B2B tests, completeness) construct the handler **without** billing. `SubscriptionBillingAmountTests.Gross_NoSst_Is100` **asserts** `billing: null` → 100.

**Reproduction in words.** Any host that composes Commerce without Billing (a future extract, a test server, a worker) undercharges SST on hop-1. Today’s API process is not that host.

**Blast radius.** Test suite cannot see hop-1 SST at all (see §7). A module-split would ship undercharge.

**Why tests missed it / pin it.** They treat null billing as the happy path.

**Fix direction.** Required constructor parameter. A missing billing dependency should fail DI, not fail closed to “no tax”. Add an initiate test with a stub profile `Sst_registration_number = "W10-…"` and assert `GenerateCheckoutSessionQuery.Amount == 108` for a 100 / 8% unit.

---

### B01-C12 — SST is rounded per unit then multiplied

**Severity:** P2  
**One-sentence fault:** Exclusive 8% on the line can differ by sen from 8% on the unit × seats because `Math.Round` runs on the unit.

**Evidence.** `GrossBreakdown` in §4.4. Example: unitNet 10.03, 8%, qty 3 → unit tax 0.80, line tax 2.40, gross 32.49. Tax on 30.09 = 2.41, gross 32.50.

**Reproduction in words.** Sell 3 seats of a price that is not a multiple of 0.125. Hop-2 charge is `unitGross * qty` in the adapter. MyInvois (out of slice) typically wants tax on the line.

**Blast radius.** Sen-level. SST merchants with odd unit prices and qty > 1.

**Why tests missed it.** Tests use 100 × 1 and 100 × 3 (8% lands on whole sen).

**Fix direction.** Compute tax on `unitNet * seats`, then allocate, or document that Lazuar’s SSoT is per-unit. Do not mix the two across hop-1 vs LHDN.

---

### B01-C13 — `CheckoutSession` status machine is two unguarded setters

**Severity:** P2  
**One-sentence fault:** `Complete()` and `Expire()` do not refuse COMPLETED→EXPIRED, EXPIRED→COMPLETED, or double complete.

**Evidence.** Domain block in §4.6. Expiry query is `Status == "OPEN"`, so the job will not expire a COMPLETED row if the filter is honoured. A future caller that loads by id and calls `Expire()` will.

**Reproduction in words.** Today only via a new caller or a missed filter. Not a current HTTP path by itself.

**Blast radius.** Status lies in the poller if it ever happens (`EXPIRED` after a real COMPLETED, or COMPLETED of an EXPIRED without fulfillment).

**Why tests missed it.** No domain test of illegal transitions.

**Fix direction.** Guard: Complete only from OPEN; Expire only from OPEN; throw otherwise.

---

### B01-C14 — Public checkout does not call `HasActiveSubscriptionAsync`

**Severity:** P2  
**One-sentence fault:** Manual enroll rejects a second ACTIVE/TRIALING row for the same client+product; hosted checkout will happily create a second subscription on a second paid session.

**Evidence.** `ICommerceRepository.HasActiveSubscriptionAsync` exists and is used by `CreateManualSubscriberCommandHandler` (82–85). Grep of `InitiateCheckoutCommandHandler` and the open-checkout webhook: no call.

**Reproduction in words.** Buyer pays monthly twice (two tabs, two emails that resolve to one CRM profile, or one email after the first COMPLETED). Two ACTIVE rows, two fulfillment events, two renewal clocks.

**Blast radius.** Double access / double charge next cycle. Some merchants want this (two seats as two subs). Quantity exists for seats, so a second sub is usually an accident.

**Why tests missed it.** No “already subscribed” initiate test.

**Fix direction.** Product decision: reject, or attach to the existing sub. If reject, do it after CRM resolve using the same helper as manual enroll.

---

### B01-C15 — Ad-hoc lines accept qty ≤ 0 and negative prices

**Severity:** P2  
**One-sentence fault:** `AdHocLineItem` stores whatever it is given; a zero or negative quote still mints hop-2.

**Evidence.**

```12:17:apps/lazuar-api/Modules/Commerce/Domain/ValueObjects/AdHocLineItem.cs
    public AdHocLineItem(string description, int quantity, decimal unitPrice)
    {
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
```

Session constructor only rejects **zero lines**, not zero money. Custom initiate with sum 0 still calls Payments with amount 0 and `SetupFutureUsage: false`. Stripe `Mode = "payment"` with a $0 line is invalid (`amount == 0 && setupFutureUsage` is the only $0 branch).

**Reproduction in words.** Ops posts a line `qty=1, unit=0` or `qty=-1, unit=100`. Quote shows RM 0 or negative. Pay either 400s at the adapter or, if a gateway accepts 0 payment, completes a custom session for free.

**Blast radius.** Clerk error. Not a buyer exploit unless ops API is open to them (it is `OrgMember`).

**Why tests missed it.** Create-custom tests use 500 / 1.

**Fix direction.** Domain: qty ≥ 1, unit ≥ 0, sum > 0 (or explicit $0 quote policy that does not mint a payment-mode session).

---

### B01-C16 — Custom hop-2 currency is hardcoded `MYR`

**Severity:** P2  
**One-sentence fault:** Product checkout uses `product.Currency`; custom initiate always sends `"MYR"` and mark-paid custom always books `"MYR"`.

**Evidence.** `InitiateCheckoutCommandHandler.cs` 149; `MarkCheckoutAsPaidOfflineCommandHandler.cs` 188. `CreateCustomCheckoutCommand` has no currency field.

**Reproduction in words.** A workspace whose products are SGD still issues MYR processor sessions for quotes.

**Blast radius.** Today the product is Malaysia-first. The moment a non-MYR tenant uses quotes, first charge is the wrong ISO code.

**Why tests missed it.** All custom tests assume MYR.

**Fix direction.** Persist currency on the quote (workspace default or request field) and thread it through initiate + mark-paid + DTO.

---

### B01-C17 — Failed or abandoned hop-2 holds coupon inventory until expiry

**Severity:** P2  
**One-sentence fault:** `GatewayPaymentFailed` does not look at OPEN checkout sessions; `ReleaseReservation` only runs in the expiry job.

**Evidence.** `GatewayPaymentFailedIntegrationEventHandler` resolves a **subscription** id and returns if none. Coupon `Reserve` happens at initiate. Expiry is 24h (product) or longer (quotes, unused). `Validate` counts reserved toward `MaxUses`.

**Reproduction in words.** MaxUses=1. Buyer initiates, Billplz fails, session stays OPEN, reserved=1. A second buyer cannot use the code for up to 24 hours.

**Blast radius.** Tight caps during a launch. Not money loss; inventory freeze.

**Why tests missed it.** Failed-payment tests are subscription-shaped.

**Fix direction.** On hop-2 failure (or explicit cancel webhook), release if the session is still OPEN, or shorten product session TTL. Do not confirm on failure.

---

### B01-C18 — Open-checkout one-time vs subscription keys off `product.Interval`, not the paid price

**Severity:** P2  
**One-sentence fault:** Webhook creates a Subscription whenever `product.Interval != "one_time"`, even if `session.PriceId` points at a one-time `ProductPrice` (the domain `UpsertPrice` allows mixing).

**Evidence.**

```79:89:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs
        if (product.Interval != "one_time")
        {
            var subscription = new Subscription(...);
            var chosen = product.Prices.FirstOrDefault(p => p.Id == session.PriceId);
            var unitAmount = chosen?.Amount ?? product.Price;
            var interval = chosen?.Interval ?? product.Interval;
```

`Product.UpsertPrice` accepts `one_time` alongside `mo` if you only have one other interval. Ops create/update normally will not, but the domain allows it. Initiate would mint `SetupFutureUsage: false` for a resolved one-time price on a monthly product, then the webhook would still `Start` a subscription.

**Speculation (labeled):** this is not reachable from the current product form if it only writes default + yearly. It is reachable from any caller of `UpsertPrice`.

**Blast radius.** A one-time add-on price on a recurring product would create a subscription after a one-shot payment.

**Why tests missed it.** Completeness products have a single interval.

**Fix direction.** Branch on `chosen?.Interval ?? product.Interval`, the same value already computed two lines later.

---

### B01-C19 — Session-by-id and coupon-by-id repository loads honour the fail-closed tenant filter

**Severity:** P2  
**One-sentence fault:** `GetCheckoutSessionByIdAsync` and `GetCouponByIdAsync` do not `IgnoreQueryFilters`; a worker or empty ambient tenant cannot see the row.

**Evidence.** `CommerceRepository.cs` 54–56 and 78–81. `PlatformDbContext` filter: `OrganizationId == ExecutionContext.TenantId`; empty tenant matches nothing. ProcessZeroAmount and mark-paid use those methods. HTTP sets `TenantId` first, so today’s portal/admin paths work. Webhook correctly uses `IgnoreQueryFilters` + org predicate.

**Reproduction in words.** If ProcessZeroAmount is ever dispatched from a background scope with empty tenant (outbox replay of a command, a job), it throws “invalid or already processed” on a perfectly OPEN session.

**Blast radius.** Latent. Not the current HTTP initiate (ambient tenant is set in `PublicCheckoutEndpoints`).

**Why tests missed it.** Substitutes do not apply query filters.

**Fix direction.** Mirror `GetCheckoutSessionByIdempotencyKeyAsync`: `IgnoreQueryFilters` + explicit org id on every load that already has an org in the command.

---

### B01-C20 — Address country default `MYS` vs hop-1 form `MY`

**Severity:** P2  
**One-sentence fault:** When the product requires an address, the portal posts `country_code: "MY"`; the handler default (only when omitted) is `"MYS"`.

**Evidence.** `CheckoutForm.tsx` 53 (`useState("MY")`). `InitiateCheckoutCommandHandler.cs` 194 (`request.CountryCode ?? "MYS"`). The posted value wins, so CRM stores `MY` not `MYS`.

**Reproduction in words.** Requires-address product. Buyer submits. CRM stores `MY`. Downstream that expects alpha-3 `MYS` (LHDN is out of slice) sees a 2-letter code.

**Blast radius / tests / fix.** Address-required products only. B2B tests do not post an address. Normalize at the handler.

---

### B01-C21 — Mark-paid / zero-amount `ConfirmReservation` throws if reserved was already released

**Severity:** P2  
**One-sentence fault:** Unlike the webhook (`ReservedCount > 0` guard), ProcessZeroAmount and mark-paid call `ConfirmReservation()` unconditionally once the coupon row exists.

**Evidence.** `Coupon.ConfirmReservation` throws when `ReservedCount <= 0`. Webhook:

```25:28:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs
            if (coupon != null && coupon.ReservedCount > 0)
            {
                coupon.ConfirmReservation();
            }
```

Zero/offline have no such guard (`ProcessZeroAmountCheckoutCommand.cs` 48–53; mark-paid 83–87).

**Reproduction in words.** Expiry also sets EXPIRED, so mark-paid will not run on an expired row. Remaining path: two zero-amount calls, or a reserve lost to B01-C02 last-write-wins so this instance’s coupon has `ReservedCount` 0. Confirm throws, session not completed, buyer 400.

**Blast radius / tests / fix.** Narrow. Happy-path reserve-then-confirm. Use the webhook’s `ReservedCount > 0` guard.

---

### B01-C22 — Quote pay posts a fake email when CRM email is missing

**Severity:** P2  
**One-sentence fault:** `QuoteView` sends `email: checkout.client_email || "customer@example.com"` and `name: checkout.client_name || "Customer"`.

**Evidence.** `QuoteView.tsx` 50–51. `GatewayCommon.PlaceholderEmail` is the same string on the adapter side. Custom initiate then mints hop-2 for `customer@example.com` if create-custom was given a blank that still passed CRM.

**Reproduction in words.** If the quote DTO email is empty, hop-2 is minted for the placeholder. Create-custom requires an email today, so this is a belt-and-suspenders hole. Custom initiate tests pass `buyer@example.com`. Refuse hop-2 without a real email.

---

## 6. Closed 008 items in this slice (re-verified)

008 `01-commerce-subscriptions-checkout.md` P0/P1s that touch first payment, as of HEAD `297ba98`:

| 008 item | 008 claim | Live on this tree |
|----------|-----------|-------------------|
| P0 #3 Zero-amount Stripe/CHIP forced reminder-only | ProcessZeroAmount always `reminderOnly: true`; 100% coupon never vaults | **Closed for initiate.** `8b3567d` mints hop-2 `$0` + `SetupFutureUsage` when `SupportsOffSession` and interval is mo/yr. Completeness test `InitiateCheckout_HundredPercentCoupon_StripeMonthly_MintsHop2SetupSession` pins URL + `type=commerce_subscription` + no ProcessZeroAmount. Billplz still bypasses (`InitiateCheckout_HundredPercentCoupon_BillplzMonthly_StillBypasses`). **ProcessZeroAmount itself is still hard-coded `reminderOnly: true`** if called directly (`ProcessZeroAmount_Recurring_ActivatesReminderOnly`). That is now the reminder-only / one-time door, not the Stripe $0 door. |
| P1 #5 SST on hop 1 only | SstTaxMath only from initiate | **Hop-1 product path closed** (`eba0741` GrossBreakdown). Renewals / arrears / dunning are report 02. **Custom + offline first charge still have no SST (B01-C08).** |
| P1 #6 Hop-1 total omits SST | OrderSummaryCard pre-tax | **Still open (B01-C06).** |
| P1 #10 Coupon vs chosen price in zero/offline | ProcessZeroAmount 51, offline 85 | **Still open (B01-C03).** |
| P1 #12 PWYW decorative | No amount on InitiateCheckoutCommand | **Not re-opened as a bug.** Missing field on the refuse list. Hop-1 input is still a lie; 008 already classified it as a hole. |
| Trial vaulting narrative | type=trial then webhook ActivateTrial | **Still broken (B01-C01).** 008 described the wire it wanted, not the filter that exists. |
| Trial cancel HTTP | SubscriptionCancelDecision | **Out of slice** (report 02 / 03). `616b37d` exists on this branch; not re-audited here. |
| Collection-pause reclaim / arrears GUID | Billing + PublicArrears | **Out of slice.** Commits `911d358` / `9b531d2` exist; not re-audited here. |

`eba0741`’s title (“SST on renewals”) does not mean custom quotes or mark-paid grew tax. They did not.

---

## 7. Tests that lie or pin a bug

**Pins the trial drop (B01-C01).**

`CommerceCheckoutMetadataTests.IsCommerceSubscriptionType_AcceptsSaasAlias` — commerce and saas are true; `custom_payment_link` is false. `"trial"` is unmentioned. Combined with `GatewayPaymentCompleted_NonCommerceType_LeavesSessionOpen`, any type outside the allow-list is required to leave the session OPEN and create nothing. A future author who adds a trial webhook test against today’s filter will be forced to expect the bug.

`InitiateCheckout_HundredPercentCoupon_StripeMonthly_MintsHop2SetupSession` is an honest pin of the **coupon** hop-2 type (`commerce_subscription`). It is not a trial test. It can make a reader think “vaulting $0 is fine” while trials still die.

**Pins optional SST skip (B01-C11).**

`SubscriptionBillingAmountTests.Gross_NoSst_Is100` — `billing: null` → 100. That is the helper contract. Completeness initiate tests construct `InitiateCheckoutCommandHandler` without billing and then assert `Amount == 100` / `90`. Those tests would go red if someone injected a registered SST merchant, but they also **cannot fail** if hop-1 SST is wired and then later broken, because they never register billing. There is no test that initiate sends 108.

**Pins ProcessZeroAmount reminder-only.**

`ProcessZeroAmount_Recurring_ActivatesReminderOnly` uses a STRIPE product with price 0 and calls the command directly. After `8b3567d` this is no longer the initiate path for Stripe $0 recurring. The test is honest about the command. It would become a lie if someone read it as “initiate Stripe $0 is reminder-only” — initiate no longer does that.

**Does not prove concurrency.** `GatewayPaymentCompleted_SameEventTwice_DoesNotCreateSecondSubscription` is sequential (COMPLETED short-circuit, not B01-C09). `CouponLifecycleTests` and completeness confirm/expiry tests are single-threaded (not B01-C02).

**Quantity tests are honest** about adapter-multiply (`InitiateCheckout_FixedOneTime_Qty3_SendsUnitNetAndQuantity` comments `100 × 3 × 3 = 900`; custom `500`/`1`). **Idempotency tests are thin** (normalize + fingerprint only; no EXPIRED replay, empty-URL, or portal-key 409).

**Mark-paid tests pin the wrong money for dual-price / SST** by using a single `product.Price` and no tax (B01-C03, B01-C08). They will go red if you fix those bugs without editing the tests — that is good — but today they certify the under-book.

**`GetCheckoutStatusTests`** honestly map OPEN→PENDING. Together with B01-C01 this is why a trial buyer polls PENDING forever.

---

## 8. What you did not read

Out of slice, or opened only when a first-charge line forced it: `BillingEngineJob` and its tests; dunning / arrears HTTP (`PublicArrearsEndpoints` was not used as a first-charge path); ledger internals beyond the SST metadata reader; LHDN XML / MyInvois / PDF renderers; One identity (`297ba98` is an One commit); TypeSpec except incidental DTO names; full adapters beyond $0 setup and quantity multiply; `RecordSubscriberPaymentCommandHandler`; plan change, portal cancel/keep, magic-link, MRR; ops product form beyond SST fields; portal success / i18n / branding; inbox retry policy; `InvoiceReminderJob`; `CreateManualSubscriber` beyond the `HasActiveSubscriptionAsync` contrast in B01-C14.

If a claim above needed one of those files, it is labeled speculation. Files opened for this audit are the table in §2.

---

## 9. Ranked open bugs in this slice

1. **B01-C01 P0** — `type=trial` hop-2 dropped; Stripe/CHIP trials never become `TRIALING`. Card is vaulted at the processor; Commerce stores nothing. This is the live bug the 009 brief asked to verify.
2. **B01-C05 P1** — Custom quote remints a new processor session on every Pay click; portal idempotency key is per slug. Double capture is the default double-click.
3. **B01-C04 P1** — Idempotency replay returns EXPIRED URLs; empty-URL first attempts 500/400 and leave a reserved coupon.
4. **B01-C03 P1** — ProcessZeroAmount / mark-paid discount the catalog default, not `PriceId`. Billplz 100% yearly coupon throws. Cash books the wrong interval.
5. **B01-C02 P1** — Coupon `FOR UPDATE` without a transaction. MaxUses is not serialisable.
6. **B01-C08 P1** — Custom quotes and offline mark-paid collect no SST on first charge.
7. **B01-C06 P1** — Hop-1 shows pre-tax, hop-2 charges tax-inclusive.
8. **B01-C07 P1** — Validate-coupon + hop-1 ratio lie for FIXED / yearly / min-price.
9. **B01-C10 P1** — Expiry vs late payment: captured money, EXPIRED session, no entitlement.
10. **B01-C09 P1** — No compare-and-swap on OPEN→COMPLETED; concurrent fulfill can double-provision.
11. **B01-C11 P2** — Null billing ⇒ no SST; tests pin it; production DI currently saves you.
12. **B01-C12 P2** — Per-unit SST rounding × seats can be a sen off the line.
13. **B01-C14 P2** — Hosted checkout allows a second ACTIVE sub for the same client+product.
14. **B01-C17 P2** — Failed hop-2 holds coupon reserved until expiry.
15. **B01-C15 P2** — Ad-hoc lines accept 0 / negative money.
16. **B01-C18 P2** — Webhook one-time vs sub uses catalog interval, not the paid price row.
17. **B01-C13 P2** — Unguarded Complete/Expire.
18. **B01-C16 P2** — Custom currency hardcoded MYR.
19. **B01-C19 P2** — Session/coupon-by-id honour fail-closed tenant filter.
20. **B01-C21 P2** — Zero/offline confirm throws if reserved is already 0.
21. **B01-C20 P2** — `MY` vs `MYS`.
22. **B01-C22 P2** — Quote pay can post `customer@example.com`.

Nothing in this list is a missing feature from the 007 refuse list (no PWYW amount field, no wallet QR on hop-1, no unused-time proration). Those stay holes.

The first-charge path that is actually sellable today, if you stay inside the tests: **FIXED catalog price, single interval, Stripe/CHIP or Billplz, no trial, optional percentage coupon on that same price, optional SST only if Billing is in the process and the buyer is looking at the processor page, one click, session still OPEN when they pay.** Trials, dual-price reminder-only rails, custom-quote double-click, and SST-honest hop-1 are not that path.
