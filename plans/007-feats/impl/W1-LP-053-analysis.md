# W1-LP-053 — Implementation analysis: Reminder-only / send-link-each-cycle (Billplz-honest)

**Date:** 16 August 2026  
**ID:** LP-053 (Wave 1 — sellable CaaS)  
**Status:** analysis only. Do not implement from this file.  
**Canonical name:** Reminder-only / send-link-each-cycle as a first-class product mode (Billplz-honest)

Tracker rows:

- [00-implement-ids.md](../00-implement-ids.md) — `LP-053 | Reminder-only / send-link-each-cycle (Billplz-honest)`
- [00-checklist-tracker.md](../00-checklist-tracker.md) — Wave 1 `LP-053` Lazuar = **P**; backlog “First-class reminder-only / offline renewals” pairs this with **LP-065**
- [00-evaluation.md](../00-evaluation.md) — Wave 1: “Honest Billplz path: reminder-only renewals (link each cycle) as a first-class mode, not a surprise.”

**ID collision (ignore for this ticket):** [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) remaps `LP-053` to “PDPA anonymize”. That is a different numbering scheme. Wave 1 PDPA is `LP-123`.

Related but **not** this ticket:

| ID | Why adjacent, not this work |
|----|-----------------------------|
| LP-047 | Wave 0 already sets/honors `IsReminderOnly`, capability matrix, AUTO_CHARGE gates, DTO flags, ops badges |
| LP-052 | Wave 0 already **mints** `CurrentRenewalCheckoutUrl` on the no-vault due path |
| LP-073 / LP-151 / LP-153 | Email hop commits; hydrator aliases `{{renewal_link}}` → update-payment **page**. Do not rebuild the pipe |
| LP-065 | Offline / record-payment enroll polish (cash in hand). This ticket is the **hosted pay-link** mode |
| LP-056 | Cancel at period end |
| LP-173 | ACTIVE change-card portal (vaulted). Reminder-only has no card |
| LP-074 / LP-155 | WhatsApp recovery |

**Wave 0 is a prerequisite, not a redo.** Do not add another billing job, another `IsReminderOnly` column, or a new invoice object. This ticket sells the mode that Wave 0 already runs.

---

## 1. Product contract (what “done” means)

A Malaysian creator can choose **Billplz / no-vault** on a monthly or yearly product and honestly sell:

> Members pay the first cycle on a hosted bill. Every later cycle we **email a new hosted payment link**. There is no card on file and no silent charge.

That is the HitPay / Billplz “send a bill each month” job — not Stripe `send_invoice`, not Chargebee net terms, not a broken auto-debit.

Sellable after this ticket:

1. **Ops treats it as a collection mode**, not a warning that the merchant picked the wrong gateway.
2. **Buyer hop 1 says so** before they pay the first cycle (`RM X / month · we email a new pay link each cycle`).
3. **The due-cycle email CTA is the pay-this-cycle path** — the minted hosted checkout URL when it exists, not “update your payment method” card copy pointing only at a Lazuar interstitial.
4. **The update-payment path stays the fallback** (already reuses the minted URL). Email should not *require* that extra hop when a live Billplz/CHIP/Stripe hosted URL is sitting on the row.
5. **Docs / legal / README do not claim auto-renew** on Billplz.

Recurring + Billplz stays legal. It is the product.

---

## 2. What Wave 0 already shipped (do not re-implement)

| Piece | Where | Status |
|-------|--------|--------|
| `PaymentGatewayCapabilities.SupportsOffSession` / `IsReminderOnlyGateway` | [PaymentGatewayCapabilities.cs](../../../apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs) | STRIPE/CHIP only |
| Paid Billplz / no-token / zero-amount / manual / offline → `IsReminderOnly=true` | OpenCheckout, zero-amount, manual enroll, offline mark-paid | Done ([W0-LP-047-done.md](./W0-LP-047-done.md)) |
| Billing: vaulted + not reminder-only → attempt 1; else mint + `PAST_DUE` | [BillingEngineJob.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs) | Done ([W0-LP-052-done.md](./W0-LP-052-done.md)) |
| `CurrentRenewalCheckoutUrl` / `CurrentRenewalCheckoutForDate` | [Subscription.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs) + migration `20260816120000_AddSubscriptionRenewalCheckout` | Done |
| Mint bound to **existing** `subscription_id` | [RenewalCheckoutIssuer.cs](../../../apps/lazuar-api/Modules/Commerce/Application/RenewalCheckoutIssuer.cs) | Done |
| Update-payment POST reuses stored URL when dates match | [PublicArrearsEndpoints.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs) 66–71 | Done |
| Same billing tick starts past-due dunning (day-0 EMAIL can fire immediately) | `StartPastDueDunningRunAsync` | Done |
| `subscription.past_due` webhook optional `checkout_url` | [CommerceWebhookPayload.cs](../../../apps/lazuar-api/Modules/Commerce/Application/CommerceWebhookPayload.cs) | Done — **integrators** see the URL; **email does not** |
| `ProductDto.supports_off_session` / `CommerceSubscriptionDto.is_reminder_only` | TypeSpec + query map | Done |
| Ops badges / AUTO_CHARGE hide / targeting labels | Product detail, subscribers, campaign builder | Done |
| Product-form amber “Reminder-only renewals” | [ProductForm.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx) 135–139 | Warning only |
| Update-payment page no longer says “failed” | [update-payment page](../../../apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx) | Neutral “Payment is due” |
| Hydrator `{{renewal_link}}` == `{{update_payment_link}}` == `/{slug}/update-payment/{subId}` | [MessageLinkBuilder](../../../apps/lazuar-api/Modules/Communications/Application/MessageTemplateHydrator.cs) 22–38 | Done — **wrong CTA for this mode** |

`reminder.due` is still unpublished. Live mail is **`reminder.dunning` only**. Do not revive `reminder.due`.

---

## 3. The sellable loop (as coded today)

```text
Due date (BillingEngineJob, no vault / IsReminderOnly / !SupportsOffSession)
  → RenewalCheckoutIssuer.MintAsync  (Billplz bill / Stripe Checkout / CHIP purchase)
  → sub.SetCurrentRenewalCheckout(url, NextBillingDate)
  → MarkAsPastDue
  → StartPastDueDunningRunAsync
       → assign default campaign (empty targets = everyone)
       → day 0 EMAIL  (same hour if DayOffset 0 is due)
            payload: plan, amount, update_payment_link tags in body
            NO checkout_url
       → AUTO_CHARGE +1/+5 consumed as skip (reminder-only)
  → OutboundWebhookRequested subscription.past_due  (HAS checkout_url)

Buyer inbox:
  "Action Required: {{plan_name}} renewal due today"
  "…please update your payment method here: {{update_payment_link}}"
       → portal /{slug}/update-payment/{subId}
            → click Complete Payment
            → POST reuses CurrentRenewalCheckoutUrl
            → Billplz hosted page
```

The **money path works**. The **product path is two hops and card language**. That is why the tracker cell is still **P**.

Competitor honesty ([02-local-sea-competitor-landscape.md](../02-local-sea-competitor-landscape.md)): a new FPX/Billplz link each month is a **reminder engine**, not merchant-initiated debit. [08-subscription-billing-engines.md](../08-subscription-billing-engines.md) BE-069: “Ops copy exists; product claim must match.” HitPay scores **Y** on this row because they sell “send a request each cycle” out loud. We must too.

---

## 4. Copy inventory (honest vs still Stripe-shaped)

### Already honest enough (keep, tighten wording only)

| Surface | Today |
|---------|--------|
| Product form / CreateProductForm | Amber: “**Reminder-only renewals.** Customers pay via a hosted link each cycle. AUTO_CHARGE will not run.” Still framed as a *limitation* vs Stripe/CHIP. |
| Product detail badge | Reminder-only vs Auto-renew next to gateway. |
| Subscriber row / detail | Badge + “Reminder-only (pay link / record payment)”. No Zap. **No copy-pay-link** — URL is not on the DTO. |
| Create subscriber modal | “flagged as Reminder Only and will receive manual payment links upon renewal.” Honest; “manual” undersells the worker mint. |
| Campaign targeting | “Vaulted auto-debit (Stripe / CHIP)” vs “Reminder-only / offline (incl. Billplz)”. |
| Dunning AUTO_CHARGE gate | Hidden/rejected when every target is reminder-only. |
| Payment settings Billplz banner | “Offline / hosted checkout only… Customers must complete each payment on Billplz’s hosted page.” “Offline” is slightly wrong (it is hosted FPX/card, not cash). |
| Update-payment arrears card | “Payment is due” / Complete Payment. |

### Still dishonest or second-class

| Surface | Gap |
|---------|-----|
| **Default campaign seed** ([DunningCampaignCommandHandlers.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs) 145–157) | −3 / 0 / +3 all say **“update your payment method”**. Reminder-only members have no method. |
| **Dunning step editor placeholder** | Lists `{{update_payment_link}}` as the recovery URL. No hosted-bill tag. |
| **Wiki** ([CommunicationsQueryService.cs](../../../apps/lazuar-api/Modules/Communications/Infrastructure/Services/CommunicationsQueryService.cs) 98–99) | `{{renewal_link}}` = “Same as update-payment link (recovery checkout).” Not the minted Billplz URL. |
| **Payment Failed catalog** | Card-decline copy. Correct for vaulted `GatewayPaymentFailed` only. Reminder-only **never** hits that handler. Leave it. |
| **Products list** | Interval + Active. **No** gateway / collection-mode column. Merchant cannot scan which links auto-debit. |
| **Checkout hop 1** ([OrderSummaryCard.tsx](../../../apps/lazuar-portal/src/modules/checkout/components/OrderSummaryCard.tsx)) | “Total Due Today” only. `interval` and `supports_off_session` are on `ProductDto` and unused. Monthly Billplz looks like a one-time bill. |
| **Legal refund** ([refund/page.tsx](../../../apps/lazuar-portal/src/app/legal/refund/page.tsx) 38) | “Canceling … will immediately stop all future **automated charges**.” False for this mode (and cancel is immediate for everyone — LP-056). |
| **README** | Hero still promises “automated WhatsApp dunning”. Honest watermark exists but Phase 1 still lists WhatsApp + Xendit as current. |
| **Docs product-lines** | Commerce = “subscription lifecycle”. No sentence that Billplz Commerce renewals are pay-link, not off-session. |
| **Pre-dunning −3** | Default body uses `{{update_payment_link}}`. Page is **Account in Good Standing**; POST 400s while `ACTIVE`. For reminder-only there is **no bill yet** (mint happens on due). |

Matcher still infers `MANUAL` vs `ONLINE_GATEWAY` from **vault token**, not `IsReminderOnly`. Billplz paid members are `MANUAL`. Empty campaign targets (default) still match them — emails send. Targeting is good enough; copy is not.

---

## 5. Email / path gap (the actual product hole)

### 5.1 What the buyer should open

| Cycle | Desired CTA | Today |
|-------|-------------|--------|
| First purchase | Hop 2 hosted checkout (unchanged) | Works |
| Renewal due (reminder-only) | **Minted hosted URL** (`CurrentRenewalCheckoutUrl`) | Email → Lazuar interstitial → POST → same URL |
| Vaulted decline | Update-payment page (no minted URL on that path) | Already correct |
| No CRM email at mint | `PAST_DUE` without URL (billing warning) | Ops must record-payment or buyer cannot self-serve |

One-click to Billplz is the sellable artifact. The interstitial is a fine **fallback** (magic-link users, expired bill, copy-paste from ops). It must not be the only advertised CTA.

### 5.2 Why hydrator cannot see the bill today

[DunningStepDispatcher.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs) payload fields: `subscription_id`, `client_profile_id`, `product_id`, action/copy, `plan_name`, `amount`, `total_price`, `currency`, `days_overdue`, `current_period_end`.

**No `checkout_url`.** Hydrator builds links only from `App:ClientUrl` + slug + sub id.

[SubscriptionMailContext](../../../apps/lazuar-api/Modules/Commerce/Contracts/ISubscriberQueryService.cs) (lifecycle / Payment Failed) also omits the stored URL.

### 5.3 Recommended link rules (do not invent a third CMS)

Keep one hydrator. Change **what `{{renewal_link}}` means when a live cycle bill exists**.

```text
hosted = payload.checkout_url
         ?? mailContext.CurrentRenewalCheckoutUrl
         (only if CurrentRenewalCheckoutForDate == NextBillingDate.Date)

{{update_payment_link}}  = {ClientUrl}/{slug}/update-payment/{subId}     // always the page
{{renewal_link}}         = hosted if present else update_payment_link    // pay-this-cycle
{{checkout_url}}         = same as renewal_link (optional alias, wiki)
{{portal_magic_link}}    = unchanged (tokenized portal)
```

Day-0 default body should use `{{renewal_link}}` (or keep `{{update_payment_link}}` if you refuse a seed change — then the interstitial stays the CTA; **do not do that** for a sellable mode).

Existing tenant rows that still say “update your payment method” + `{{update_payment_link}}` keep working (page reuses the bill). **New orgs** get pay-this-cycle copy. Same “don’t migrate old campaigns” rule as LP-073.

### 5.4 Pre-dunning (−3) for this mode

Do **not** mint a bill three days early (new expiry / double-bill risk; out of scope).

For new default seed only:

- −3 body: “{{plan_name}} renews on {{current_period_end}}. If we don’t have a card on file, we will email a payment link on the due date.” **No** `{{update_payment_link}}`.
- Day 0 / +3: “{{plan_name}} is due. Pay this cycle: {{renewal_link}}.”

Do not add a second default campaign in this ticket. Empty targets + skip AUTO_CHARGE already cover mixed catalogs. A MANUAL-only campaign is LP-065-adjacent polish.

### 5.5 Do not publish `reminder.due`

Day 0 already fires from the billing tick. A second publisher would double-email. Orphan catalog names (“Subscription Renewal Due Today”) stay orphans.

---

## 6. Exact gaps (priority)

### P0 — sellable CTA

1. Dunning (and mail-context) never pass `CurrentRenewalCheckoutUrl`. Inbox cannot contain the hosted bill.
2. Default day-0 / +3 copy is card-update language. Unsellable as “we send a pay link.”

### P1 — first-class mode in the product UI

3. Checkout hop 1 hides interval collection mode. Buyer is surprised at month two.
4. Products list has no Reminder-only / Auto-renew column.
5. Product form is a warning, not a chosen mode (no confirmation line like “You are creating a pay-link membership”).
6. Subscriber detail cannot copy the live pay link (DTO omit).
7. Pre-dunning −3 CTA 400s for ACTIVE reminder-only.

### P2 — claims outside ops

8. Legal refund “automated charges.”
9. Payment-settings “Offline” wording.
10. Docs product-lines / README still imply auto-renew / WhatsApp as current for this rail.
11. Create-subscriber “manual payment links” undersells the hourly mint + email.

### Explicitly not a gap for LP-053

- Engine mint / `IsReminderOnly` / AUTO_CHARGE skip (Wave 0).
- Payment Failed catalog (vaulted only).
- `reminder.due` publisher.
- Invoice / `send_invoice` / net terms.
- WhatsApp.
- Record-payment UX (LP-065).
- ACTIVE update-PM (LP-173).
- `Product.IsReminderOnly` column — still derive from `GatewayName`.
- Blocking CANCEL/SUSPEND for reminder-only (stale Rule B; fights LP-078).
- Migrating existing campaign bodies.

---

## 7. Minimal changes

No new tables. No new hosted service. No new adapters. Touch copy + one payload field + hydrator preference + hop-1 / list badges.

### 7.1 Pass the minted URL into mail

| File | Change |
|------|--------|
| [DunningStepDispatcher.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs) | Add `checkout_url` when `CurrentRenewalCheckoutUrl` is set and `CurrentRenewalCheckoutForDate == NextBillingDate.Date`. |
| [FulfillmentRequestedIntegrationEventHandler.cs](../../../apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs) | Read `checkout_url`. `RenewalLink = checkout_url ?? links.UpdatePaymentLink`. Keep `UpdatePaymentLink` as the page. |
| [MessageTemplateHydrator.cs](../../../apps/lazuar-api/Modules/Communications/Application/MessageTemplateHydrator.cs) | Preview `renewal_link` can stay the update-payment mock **or** a sample `https://www.billplz-sandbox.com/bills/…` plus wiki text. Optional `{{checkout_url}}` replace = same string as `renewal_link`. |
| [CommunicationsQueryService.cs](../../../apps/lazuar-api/Modules/Communications/Infrastructure/Services/CommunicationsQueryService.cs) | Wiki: `renewal_link` = “Hosted pay-this-cycle checkout when minted; otherwise the update-payment page.” |
| Mail context (optional, lifecycle only) | Not required for day-0 dunning. Skip unless a test needs it. |

### 7.2 Default campaign copy (new orgs only)

In `GenerateDefaultDunningCampaignsCommand` (same handler that already seeds +1/+5 AUTO_CHARGE):

| Offset | Subject | Body |
|--------|---------|------|
| −3 | Upcoming renewal for {{plan_name}} | Renews on {{current_period_end}}. If there is no card on file, we email a payment link on the due date. |
| 0 | {{plan_name}} is due — pay this cycle | {{plan_name}} is due today ({{amount}} {{currency}}). [Pay now]({{renewal_link}}) |
| +3 | {{plan_name}} is still unpaid | Still unpaid. [Pay this cycle]({{renewal_link}}) |

Do **not** change AUTO_CHARGE steps. Do **not** UPDATE existing `DunningSteps` rows.

Dunning step editor placeholder: add `{{renewal_link}}` and say it is the hosted bill when one exists.

### 7.3 Ops — mode, not apology

| File | Change |
|------|--------|
| ProductForm + CreateProductForm | Recurring + `!supports_off_session`: title **Collection mode: pay link each cycle**. Body: we email a hosted Billplz/CHIP/Stripe page every period; no card stored; AUTO_CHARGE will not run. Stripe/CHIP recurring: one line **Auto-debit: card is saved for renewals.** |
| [ProductsPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/ProductsPage.tsx) | Column or badge: Reminder-only vs Auto-renew for `interval !== one_time` (same helper as detail). |
| [SubscribersPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx) | If PAST_DUE/SUSPENDED + reminder-only: **Copy pay link** when URL present. |
| [subscriber.tsp](../../../packages/api-spec/modules/commerce/models/subscriber.tsp) + Subscribers query | Optional `current_renewal_checkout_url` (null when cleared). `task gen`. |
| CreateSubscriberModal | “We will email a hosted payment link each cycle (no auto-debit).” |
| Payment settings + admin twin | Drop “Offline” as the headline. “**Pay-link renewals.** Billplz cannot vault. Each cycle we create a hosted bill and email it.” |

### 7.4 Buyer hop 1 + legal

| File | Change |
|------|--------|
| OrderSummaryCard / CheckoutView | If `interval` is `mo`/`yr`: show “then {{currency}} {{price}} / month\|year”. If `!product.supports_off_session`: amber one-liner **Not auto-debit. We email a new payment link each cycle.** ProductDto already has the flag. |
| update-payment page | Optional: if you want zero extra hop from old emails, keep the button (POST already reuses URL). Do **not** client-redirect before POST unless you add the URL to the arrears GET (extra TypeSpec). Prefer email CTA (7.1). |
| Legal refund §4 | “Cancel stops **future renewals** (auto-debit **or** further pay-link emails).” Do not say “automated charges” only. |

### 7.5 Docs (BE-069, small)

One paragraph, not a README rewrite:

- [apps/lazuar-docs/docs/guide/product-lines.md](../../../apps/lazuar-docs/docs/guide/product-lines.md) — Commerce subscriptions: Stripe/CHIP can auto-debit; Billplz (and any `supports_off_session=false` rail) is **pay-link each cycle**.
- Root [README.md](../../../README.md) honest-capability line: add “Billplz renewals = emailed hosted link, not silent charge.” Do not touch Phase 1 fantasy list in this ticket unless you are already editing that file for the one sentence.

### 7.6 What not to change

- BillingEngineJob mint / PAST_DUE / failedIds.
- Capability helper / campaign AUTO_CHARGE reject.
- Payment Failed / Portal Access / Official Receipt templates.
- WhatsApp flag.
- Interval math, invoice objects, `Product` schema.
- Stale gap memos under `docs/001-gaps/` (do not treat them as source).

---

## 8. Tests

### Must

| Test | File | Assert |
|------|------|--------|
| Dispatcher payload includes `checkout_url` when URL+date match | extend [DunningEngineJobTests](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs) or a dispatcher unit | property present |
| Dispatcher omits / empty when no URL | same | hydrator falls back |
| Hydrate `renewal_link` == payload checkout_url | [DunningTemplateVariableSubstitutionTests](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/DunningTemplateVariableSubstitutionTests.cs) | not `/update-payment/` when bill present |
| Hydrate `update_payment_link` still the page | same | `/update-payment/{id}` |
| Hydrate without checkout_url: both tags = page (LP-153 still green) | same | alias preserved |
| Default seed day 0 body contains `{{renewal_link}}` and does **not** say “update your payment method” | [DunningCampaignCommandHandlerTests](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/DunningCampaignCommandHandlerTests.cs) | new orgs |
| Default −3 body has no `{{update_payment_link}}` | same | |
| Billing no-vault still mints then day-0 `reminder.dunning` | existing [BillingEngineJobTests](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs) | do not regress; add payload `checkout_url` if the day-0 event is asserted |
| Subscriber query maps `current_renewal_checkout_url` | [CommerceHonestyDtoTests](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceHonestyDtoTests.cs) | |
| Wiki description no longer “same as update-payment only” | [TemplateVariablesWikiTests](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/TemplateVariablesWikiTests.cs) | |

### Should

- Preview mock: if `{{checkout_url}}` added, preview replaces it.
- Public product still returns `supports_off_session` (already mapped).

### Not required

- Ops component tests.
- Full Billplz sandbox soak (operator residual; same demo as LP-052 plus inbox).
- Migrating old campaign HTML.

---

## 9. Acceptance

A reviewer can flip tracker LP-053 **P → Y** when:

### Mode

- [ ] Recurring + Billplz (or any `!supports_off_session`) create/edit states **pay-link each cycle** as the collection mode, not only “cannot vault.”
- [ ] Products list shows Reminder-only vs Auto-renew.
- [ ] Checkout hop 1 for that product states it is **not** auto-debit and that the next cycle is an emailed link.
- [ ] Stripe/CHIP recurring hop 1 / form still says card will be saved (do not scare vaulted buyers).

### Email / path

- [ ] Due tick on a reminder-only sub with CRM email: hosted URL stored, `PAST_DUE`, day-0 email HTML contains **that same hosted URL** (not only `/update-payment/`).
- [ ] Paying that URL does not create a second Subscription (Wave 0; do not regress).
- [ ] Vaulted decline email still uses the update-payment page (no hosted URL on that path).
- [ ] New-org default day-0 copy is “pay this cycle,” not “update your payment method.”
- [ ] New-org −3 does not send a CTA that 400s on ACTIVE.
- [ ] Ops subscriber detail can copy the live pay link when one exists.

### Honesty regression

- [ ] AUTO_CHARGE still hidden/rejected for Billplz-only targets.
- [ ] Manual enroll still reminder-only.
- [ ] Legal refund does not claim only “automated charges.”
- [ ] Docs product-lines name the two Commerce renewal modes.
- [ ] `reminder.due` still unpublished (no double send).

### Honest demo (after implement)

1. Create monthly Billplz product. Read the form: pay-link mode. Open public checkout: hop 1 discloses it.
2. Pay first cycle. Subscriber shows Reminder-only, no Zap.
3. Set `NextBillingDate` a minute ago (or wait). Run billing once. Confirm stored Billplz URL.
4. Open the day-0 email (Resend BYOK): primary button is the Billplz host, amount matches catalog.
5. Pay the bill. Same row `ACTIVE`, dates advanced, URL cleared.
6. Repeat with Stripe + vault: no pay-link email on success; decline still gets update-payment, not a surprise Billplz bill.

---

## 10. Suggested implementation order

1. Dispatcher `checkout_url` + hydrator preference + tests (unblocks the inbox).
2. Default seed copy + wiki + step-editor placeholder (new orgs).
3. TypeSpec `current_renewal_checkout_url` + `task gen` + subscriber Copy link.
4. Product form / list / payment-settings / create-subscriber wording.
5. Checkout hop 1 one-liner + legal sentence + docs paragraph.

Estimate: one focused PR. Mostly copy + one JSON field. No migration if you skip persisting anything new (subscriber DTO is computed from existing columns).

---

## 11. File index

### Mail path

- [DunningStepDispatcher.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs)
- [FulfillmentRequestedIntegrationEventHandler.cs](../../../apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs)
- [MessageTemplateHydrator.cs](../../../apps/lazuar-api/Modules/Communications/Application/MessageTemplateHydrator.cs)
- [CommunicationsQueryService.cs](../../../apps/lazuar-api/Modules/Communications/Infrastructure/Services/CommunicationsQueryService.cs)
- [DunningCampaignCommandHandlers.cs](../../../apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs)

### Already-correct engine (read, don’t rewrite)

- [BillingEngineJob.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs)
- [RenewalCheckoutIssuer.cs](../../../apps/lazuar-api/Modules/Commerce/Application/RenewalCheckoutIssuer.cs)
- [Subscription.cs](../../../apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs)
- [PublicArrearsEndpoints.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs)
- [PaymentGatewayCapabilities.cs](../../../apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs)

### Ops / portal / contracts

- [ProductForm.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx)
- [CreateProductForm.tsx](../../../apps/lazuar-ops/src/components/forms/CreateProductForm.tsx)
- [ProductsPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/ProductsPage.tsx)
- [ProductDetailPanel.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/ProductDetailPanel.tsx)
- [SubscribersPage.tsx](../../../apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx)
- [CreateSubscriberModal.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/CreateSubscriberModal.tsx)
- [DunningStepEditor.tsx](../../../apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx)
- [PaymentSettingsPage.tsx](../../../apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx)
- [PlatformPaymentSettingsPage.tsx](../../../apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx)
- [OrderSummaryCard.tsx](../../../apps/lazuar-portal/src/modules/checkout/components/OrderSummaryCard.tsx)
- [CheckoutView.tsx](../../../apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx)
- [update-payment page](../../../apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx)
- [refund/page.tsx](../../../apps/lazuar-portal/src/app/legal/refund/page.tsx)
- [subscriber.tsp](../../../packages/api-spec/modules/commerce/models/subscriber.tsp)
- [CommerceQueryService.Subscribers.cs](../../../apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Subscribers.cs)

### Docs

- [apps/lazuar-docs/docs/guide/product-lines.md](../../../apps/lazuar-docs/docs/guide/product-lines.md)
- [README.md](../../../README.md) (one honest-capability sentence only)

### Tests to extend

- [DunningTemplateVariableSubstitutionTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/DunningTemplateVariableSubstitutionTests.cs)
- [DunningCampaignCommandHandlerTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/DunningCampaignCommandHandlerTests.cs)
- [DunningEngineJobTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs)
- [BillingEngineJobTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs)
- [CommerceHonestyDtoTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceHonestyDtoTests.cs)
- [TemplateVariablesWikiTests.cs](../../../apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/TemplateVariablesWikiTests.cs)

### Prior research (do not re-litigate)

- [W0-LP-047-analysis.md](./W0-LP-047-analysis.md) / [W0-LP-047-done.md](./W0-LP-047-done.md)
- [W0-LP-052-analysis.md](./W0-LP-052-analysis.md) / [W0-LP-052-done.md](./W0-LP-052-done.md)
- [W0-LP-073-analysis.md](./W0-LP-073-analysis.md) / [W0-LP-153-done.md](./W0-LP-153-done.md)
- [11-subscriptions-lifecycle.md](../11-subscriptions-lifecycle.md) SL-022 / SL-081
- [08-subscription-billing-engines.md](../08-subscription-billing-engines.md) BE-069
- [09-checkout-and-payment-links.md](../09-checkout-and-payment-links.md) §19 interval honesty

---

## 12. Verdict

| Layer | Today | After LP-053 |
|-------|--------|----------------|
| Engine | Mint + `IsReminderOnly` + skip AUTO_CHARGE | Unchanged |
| Inbox | Day-0 email sends; CTA is “update payment method” → interstitial | Day-0 email **is** the product: hosted pay-this-cycle URL |
| Ops | Warning + badges | Collection mode you can sell and scan |
| Buyer hop 1 | One-time-shaped | Discloses pay-link membership |
| Claims | BE-069 still partial | Product claim matches Billplz physics |

Tracker stays **P** until P0 (hosted URL in the email) and P1 hop-1 / list mode land. Copy-only without the hydrator change is still a two-hop surprise — not the HitPay-shaped mode Wave 1 asked for.
