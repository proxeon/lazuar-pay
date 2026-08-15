# 12 — Dunning and revenue recovery

**Program:** `plans/007-feats` — competitor features vs **Lazuar Pay** (Checkout-as-a-Service recovery engine)  
**Date:** 2026-08-16  
**Status:** Analysis only — **no product code from this file**  
**Scope:** Commerce dunning campaigns, workers, Communications + Messaging delivery, Payments off-session charges, update-payment recovery, recovered-revenue metrics, and how that stack compares to Stripe Smart Retries, Chargebee Revive / Smart Dunning, Recurly Dunning Management, and ProfitWell Retain / Paddle Retain.  
**Author role:** staff payments / recovery analyst for Lazuar Pay CaaS (not Aura salon guest money)

**This file is not** an Aura guest-deposit analysis. Aura System B (guest → salon via Hub + Billplz) is a different money plane. This file is **Lazuar Pay’s own subscription recovery product** — the thing README and ADR 020/021 sell as “Native WhatsApp Dunning.”

**Standing constraints (do not contradict):**

- Guest money (Aura System B / Lazuar Pay / Billplz) is **not** SaaS money (Aura System A / Paddle). This analysis does not reopen that boundary.
- Lazuar Pay decision **00.4** (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/decisions.md`): **no production WhatsApp** for the maintenance horizon; Console WhatsApp is not a channel; docs/defaults must not claim automated WhatsApp dunning as live.
- ADR 021 keeps dunning (auto-retries) as core CaaS; ADR 023 ships checkout + dunning first. Neither ADR ships Meta Cloud.
- Do not treat `docs/001-gaps/01-dunning-engine.md` as current truth. That gap file is a **pre–Phase A** snapshot. Phase A of `plans/001-backend/001-backend-solidification-checklist.md` closed the payment-failed → PAST_DUE → retry/message → recover loop **in code**. This file re-reads the engine as of 16 August 2026.

---

## Method

### What this file answers

1. What do the four named recovery products actually do (retries, channels, hard/soft declines, hosted update-payment, metrics, versioning, terminal actions)?  
2. What does Lazuar Pay’s engine do today, file by file, after Phase A?  
3. What does the ops campaign builder sell versus what the worker will execute?  
4. Can we sell “WhatsApp dunning” without lying?  
5. What tracker IDs should a later implementation program open?

### Sources read (not summarized away)

| Source | Absolute path | Role |
|--------|---------------|------|
| Pre–Phase A gap (stale in places) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/01-dunning-engine.md` | Historical inventory; many P0 rows are **fixed** |
| Communications gap | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/08-communications-module.md` | Dual CMS, no Meta templates, SMS absent |
| Intent vs implementation | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/20-architecture-intent-vs-implementation.md` | README vs engine; WA stub |
| Background workers | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/17-background-workers.md` | Hourly loop, no Hangfire |
| Backend Phase A checklist | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/001-backend/001-backend-solidification-checklist.md` | Closed-loop work that landed |
| Decision 00.4 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/decisions.md` | WhatsApp freeze |
| Messaging README | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/README.md` | Thin transport; console WA |
| Root README | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md` | Conflicting claims (hero vs watermark vs Phase 1) |
| ADR 020 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/020-lazuar-platform-integration-roadmap.md` | Interactive “Tap here to pay RM50 via FPX” |
| ADR 021 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/021-compliance-caas-pivot.md` | Keep WhatsApp dunning |
| ADR 023 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` | Dunning as MVP |
| Campaign aggregate | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs` | Counters, targeting, no version |
| Subscription dunning state | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` | PAST_DUE, pause, `LastCompletedDayOffset` |
| Charge attempts | `.../Modules/Commerce/Domain/Entities/ChargeAttemptLog.cs` + `ChargeAttemptLimits.cs` | Multi-row, max 4 |
| Dispatch log | `.../Modules/Commerce/Domain/Entities/ReminderDispatchLog.cs` | DayOffset unique |
| Engine | `.../Workers/DunningEngineJob*.cs` (Claim, PreDunning, PastDue, Dispatch) | Hourly claim + catch-up |
| Billing entry | `.../Workers/BillingEngineJob.cs` | Attempt 1 vs no-token PAST_DUE |
| Failure bridge | `.../EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | PAST_DUE + campaign assign |
| Recovery | `.../EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` | `RecoverFromPayment` + `RecordRecovery` |
| Manual record-pay | `.../Commands/RecordSubscriberPaymentCommandHandler.cs` | Clears dunning, **no** `RecordRecovery` |
| Campaign CRUD | `.../Commands/DunningCampaignCommandHandlers.cs` | Defaults, delete-guard, ClearSteps |
| Off-session | `.../Payments/.../ExecuteOffSessionChargeIntegrationEventHandler.cs` | Publishes failed event |
| Stripe / CHIP / Razorpay / Billplz adapters | `.../Payments/Infrastructure/Gateways/` | Off-session coverage |
| Webhook processor | `.../ProcessGatewayWebhookCommandHandler.cs` | `PAYMENT_FAILED` now published |
| Communications hydrate | `.../FulfillmentRequestedIntegrationEventHandler.cs` | Variable fill + links |
| Lifecycle templates | `.../Communications/.../LifecycleEventHandlers.cs` | Suspend/cancel emails |
| Messaging dispatch | `.../DispatchMessageIntegrationEventHandler.cs` | Email real; WA flagged off |
| Console transport | `.../Messaging/ConsoleMessagingService.cs` | Log-only |
| Ops UI | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/` | List, builder, step editor, subscribers panel |
| Portal update-payment | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` | Public arrears page |
| Public arrears API | `.../Endpoints/PublicArrearsEndpoints.cs` | Checkout + metadata |
| Magic link | `.../Security/MagicLinkTokenService.cs` | HMAC 24h — **portal only** |
| TypeSpec | `packages/api-spec/modules/commerce/models/dunning.tsp` + `admin-routes.tsp` | Stringly action/final |
| Tests | `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/*` + `Communications/DunningTemplateVariableSubstitutionTests.cs` | Domain + failed-handler + vars; **no `DunningEngineJob` tests** |
| Stripe Smart Retries | https://docs.stripe.com/billing/revenue-recovery/smart-retries | Official 2026 docs |
| Chargebee Revive / Smart Dunning | https://www.chargebee.com/payments/retries-and-dunning/ | Official marketing + product rules |
| Recurly Dunning Management | https://docs.recurly.com/recurly-subscriptions/docs/dunning-management | Official 2026-07-07 docs |
| Paddle Retain / Payment Recovery | https://developer.paddle.com/concepts/retain/payment-recovery-dunning | Official (ProfitWell Retain successor) |

### How to read claims in this file

| Word | Meaning here |
|------|----------------|
| **Shipped** | Code path exists, is wired, and can run in a default deploy without a feature flag. |
| **Partial** | Code exists but is gated, incomplete, inverted, or only works for one rail. |
| **Stub** | Type / UI / credit cost exists; transport is console or skipped. |
| **Doc-off** | README/ADR sell it; runtime does not. |
| **Cannot sell** | A founder demo of that sentence will fail or silently degrade. |

No runtime soak was executed. Verdicts are from source + official competitor docs as of 16 August 2026.

### Stale-document warning (read before the rest)

`docs/001-gaps/01-dunning-engine.md` is still useful as a map of *filenames*, and it was correct when written. **It is no longer the engine.** Phase A landed:

- `GatewayPaymentFailedIntegrationEvent` is published from off-session failure **and** from `PAYMENT_FAILED` webhooks.
- Commerce marks PAST_DUE and assigns a campaign.
- `ChargeAttemptLogs` unique key is `(SubscriptionId, TargetBillingDate, AttemptNumber)` — multi-retry is schema-legal.
- Off-session metadata includes `type`, `subscription_id`, `tenant_id`, `dunning_campaign_id`.
- Past-due catch-up is `DayOffset <= daysOverdue` (not equality).
- `{{plan_name}}`, `{{amount}}`, `{{currency}}`, `{{days_overdue}}` are substituted on the dunning path.
- Delete-while-assigned is blocked; defaults are idempotent; `LastCompletedDayOffset` exists.
- WhatsApp is **honestly gated** in the builder (`Send WhatsApp (not connected)`) and by `Messaging:WhatsAppEnabled=false`.

Copying the old executive verdict (“failed vaulted charges never enter PAST_DUE”) into a 2026-08-16 plan would be a lie in the other direction. The honesty problem that remains is **channel and intelligence**, not **entry**.

---

## Competitor recovery products

This section is the pattern library. None of these companies are salon OS competitors. They are the products a Malaysian SaaS founder compares Lazuar Pay against when they hear “we have dunning.”

Industry background the four products all assume (and we mostly do not):

- Involuntary churn is 20–40% of subscription churn (Chargebee / Cardinal / Kaplan citations on the Revive page). Insufficient funds alone is ~44% of card declines and has a ~68% median recovery when retried in a 2–7 day payday window (Recurly / Ethoca / Stripe commentary, widely repeated 2025–2026).
- Card networks dislike more than ~3–4 attempts in 30 days; hard-decline retries burn merchant-account health.
- The job splits in two: **silent retry** (do not bother the customer if the card will clear) and **dunning conversation** (ask for a new method when the card will never clear). The good products keep those jobs separate.

### Stripe Billing — Smart Retries + Revenue Recovery

**What it is.** A Billing-native retry engine, not a campaign CMS. Configure at Dashboard → Billing → Revenue recovery → Retries. Customer emails are a **separate** Stripe product. Smart Retries is the default; a custom schedule is the escape hatch.

**Retry model.**

- **Smart Retries:** an ML policy. Official signals include time-dependent device-present counts on a payment method and “best time to pay” (example: debit cards in some countries at 12:01 AM local). The merchant sets **how many retries** and **the window** (1 week, 2 weeks, 3 weeks, 1 month, 2 months). Stripe’s recommended default is **8 tries within 2 weeks**.
- **Custom schedule:** up to **three** retries, each a fixed number of days after the previous attempt.
- **Payment-method order** on retry: subscription default PM → subscription default source → customer invoice default PM → legacy customer default source. Updating the wrong field silently retries the old card.
- **Local payment methods** (ACH, ACSS, AU BECS, Bacs, NZ BECS, SEPA) have their own retry tables (typically 1–2 retries over 30–40 days, **insufficient-funds only**, mandate required). Off by default.
- **India-issued cards** and disconnected Connect accounts are never retried.

**Hard vs soft declines (first-class).** Stripe will not execute a retry when the issuer returns any of:

`incorrect_number`, `lost_card`, `pickup_card`, `stolen_card`, `revocation_of_authorization`, `revocation_of_all_authorizations`, `authentication_required`, `highest_risk_level`, `transaction_not_allowed`.

Scheduled retries **continue to increment `attempt_count`**, but they **only fire after a new payment method appears**. Unexecuted retries do **not** create a Charge. That is the entire hard-decline product: do not burn the merchant account, wait for a card update.

**Entry.** Invoice / subscription collection failure. Webhook `invoice.payment_failed` carries `attempt_count`. `next_payment_attempt` is on the invoice (or on `invoice.updated` if Automations are on).

**Channels.** Not WhatsApp. Not SMS. Stripe Customer Emails + hosted invoice / customer portal / Payment Element update. Card Account Updater and Adaptive Acceptance sit next to Smart Retries in the same Revenue Recovery family (Deliveroo public number: >£100M recovered in a year with the trio).

**Terminal actions** after the last attempt:

| Setting | Effect |
|---------|--------|
| Cancel the subscription | `canceled` after max days in the schedule |
| Mark unpaid | `unpaid`; invoices keep generating as drafts |
| Leave past-due | stays `past_due`; invoices keep charging on retry settings |

**Metrics.** Recovery analytics: failure rate, recovery rate, recent failed payments for top customers. Network-wide Stripe claim in third-party 2026 writeups: ~55–57% of failed recurring payments recovered by Smart Retries alone (the remainder is the dunning-conversation problem).

**Versioning.** Retry policy is a Dashboard setting. Automations can segment. There is no “campaign snapshot on invoice” in the Smart Retries docs the way Recurly versions dunning.

**What we must not copy blindly.** Stripe owns the card network. Lazuar Pay is BYOK. We will never have Stripe’s device graph. We *can* copy: hard-decline gate, attempt counter that does not execute, separate conversation from retry, explicit terminal states.

### Chargebee — Smart Dunning + Revive

**What it is.** Two stacked products inside Chargebee Billing.

1. **Smart Dunning** — rules-based, merchant-configured. Reads gateway error codes. Soft declines retry (up to **12**). Hard declines **hold retries and send a card-update request**. Custom mode: up to **5** retries on days the merchant picks. Reminder emails are a **separate** stream so customers are nudged, not spammed. Covers cards, wallets, direct debit, **offline**, and one-time invoices. Final action is chosen for **both** the subscription and the invoice.
2. **Revive** — ML replacement for the retry *clock*. Marketing claim: **200+ signals** per failed payment (issuer, BIN, decline code, amount, currency, card country, timezone, funding cycle, invoice/retry history, plan value, tenure, LTV). One decision: retry now / retry Friday 9:00 AM / do not retry, request card update. Guardrails: merchant dunning period, **max 12 per invoice**, instant revert to Smart Dunning. Gateway coverage at time of write: **Stripe and Braintree**; everything else stays on Smart Dunning.

**Playbooks they publish as product, not blog advice:**

| Failure | Action |
|---------|--------|
| Insufficient funds | Do not retry immediately; schedule onto payday / salary cycle / start of month |
| Hard decline (expired / lost / stolen) | Pause retries; send card-update request; keep the conversation |
| Network / generic | Retry fast, often within hours, to catch the auth window |

**Entry.** Invoice payment failure inside Chargebee Billing. Offline invoices have their own dunning path (this matters for our MANUAL / FPX-reminder segment).

**Channels.** Email + hosted Pay Now. Not WhatsApp as a first-class Chargebee channel. SMS is not the headline.

**Campaign UX.** Settings → Dunning: period, retry mode (Smart vs Custom), reminder emails, final actions. Revive is a switch per gateway / payment method.

**Metrics.** Revive dashboard: revival rate vs previous logic, topline addition, volume revived, revived vs unaddressable vs addressable, split by gateway and payment method. Customer story numbers they are willing to print: Zenchef 60% of unpaid accounts recovered; Trade Ideas 4× revenue recovered per dollar spent on Chargebee.

**Versioning.** Dunning period and emails persist when Revive is toggled. The retry *decision-maker* swaps; in-flight invoices keep the dunning *window*.

**What we must not copy blindly.** We do not have 200 signals or multi-gateway learning. We *can* copy: hard vs soft as a fork, emails separate from AUTO_CHARGE, offline cycle distinct from card cycle, a recovery dashboard that is not three integers.

### Recurly — Dunning Management + Intelligent Retry

**What it is.** The closest UX cousin to our campaign builder, and the product that invented the **campaign snapshot** we lack.

**Campaigns.**

- Every site has a **default** campaign. Professional / Elite: up to **50** targeted campaigns.
- Priority: **Account → Plan → Default**. Bundled invoice uses the oldest subscription’s campaign. Account Hierarchy uses the *billed* account.
- Each campaign has **three cycle types**, each with its own emails and schedule:
  - **Payment Declined** — automatic invoices (card / ACH / DD)
  - **Invoice Past Due** — manual invoices (check / wire)
  - **Post-Trial Payment Declined** — trial → paid conversion failure (only real free-trial, not $0 first cycle)
- End of cycle: fail invoice (write off) or leave overdue forever. Optional expire subscription. Final “Subscription Expired for Non-Payment” email.
- **Account Updater** (if on) keeps running on overdue invoices even after dunning ends, and retries collection indefinitely.

**Versioning (the feature we do not have).** Official limitation, quoted: *“Dunning settings are versioned — changes to a campaign won't affect invoices already in dunning.”* Settings History on every campaign. Analytics per version (automatic invoices). Edit or reassign only affects **new** invoices.

**Ops control.**

- **Stop Dunning** on an invoice: stays Past Due; retries stop; emails stop; end-of-cycle action will not run; Account Updater continues.
- **Stop Collection** / **Mark Paid**: close invoice, stop everything, skip end-of-cycle. Does **not** auto-cancel the subscription (cancel separately). Subscriptions auto-cancel only if the campaign is configured to expire at end of cycle.

**Hard declines.** If the first dunning email is scheduled *after* the first payment attempt, Recurly **skips the retry schedule** for hard declines and incorrect billing details and waits for a billing-info update.

**Intelligent Retry** is a **separate** product from the email campaign. Recurly’s own writing: they split smart retry from dunning because subscribers do not all respond the same way to email, and the best retry time varies by company. Public numbers they own: involuntary churn 6% → 1% for some merchants; 12.7% monthly subscription revenue lift; custom retry models +7% (up to 16%) on top of intelligent retries. Best-practice window they publish: **3–4 emails over ~28 days** for monthly plans (avoid February overlap); 27 days max for monthly so Intelligent Retry can finish; 60 days for quarterly/annual.

**Channels.** Email templates (Payment Declined, Invoice Past Due, 3DS2 declined, Subscription Expired). Hosted account-management link via `hosted_login_token`. Webhook `new_dunning_event` so the merchant can add SMS / in-app themselves. Recurly does not sell WhatsApp dunning.

**Sandbox.** Documented accelerate path (success card → fail-but-save card `4000-0000-0000-0341` → move renewal 1 hour ahead). We have no equivalent dry-run.

**What we must not copy blindly.** Invoice-centric model (we are status+day). We *can* copy: three cycle types mapped to our ONLINE vs MANUAL, campaign versioning, Stop Dunning ≠ cancel, Account → Plan → Default priority (we only have product + method).

### ProfitWell Retain → Paddle Retain (Payment Recovery)

**What it is.** The “pay a cut of recovered revenue, write nothing” product. ProfitWell Retain was acquired by Paddle (2022) and is now **Paddle Retain / Payment Recovery**, bundled for Paddle Billing merchants. Pricing historically: **percentage of recovered revenue**. Third-party 2026 roundups still treat it as the performance-priced default.

**Paddle’s own claims (developer docs, fetched 2026-08-16):**

- **50%+** recovery rate for failed payments.
- **17%** cut in overall involuntary churn.
- **15+ factors** for Tactical Retries (payment method type, customer location, failure code, time of day, …).
- Default dunning window: **30 days**.
- When recovery succeeds, subscription returns to active **and the next billing date stays the same** (no “restart the year from today” — contrast our `RecoverFromPayment`, which sets `NextBillingDate` to now+1 month/year).

**Customer journey they sell:**

1. Short plaintext email + prominent update-payment link.  
2. If no action: retry the method on file + email again.  
3. **In-app / on-site** notification via Paddle.js when the customer is in the product or on the marketing site.  
4. Optional **SMS** (they cite 90% of texts read within three minutes).  
5. Click → **no-login** payment update form on the merchant’s site, with Apple Pay / Google Pay / PayPal / card.  
6. Exhaustion → **pause or cancel** (merchant choice).

**What it is not.** A campaign builder. There is no “add a WhatsApp step on day 3.” Templates are written by Paddle. No subscription-pause product, no cancellation interceptor, no win-back suite (Recurflux and others sell that gap). Best results assume you are **inside Paddle Billing** (they are the MoR). That is the opposite of Lazuar Pay’s BYOK promise.

**What we must not copy blindly.** Taking a % of recovered GMV would violate Lazuar’s “we do not take 8% / we are not MoR” README. We *can* copy: no-login update form (we have a cousin), SMS as a **real** channel if we ever open 00.4, “keep the original billing anniversary,” and a 30-day default window.

### Competitor comparison (recovery job, not brand)

| Job | Stripe Smart Retries | Chargebee Smart + Revive | Recurly Dunning + Intelligent Retry | Paddle Retain | Lazuar Pay (2026-08-16) |
|-----|----------------------|---------------------------|-------------------------------------|---------------|-------------------------|
| Entry | Invoice payment_failed | Invoice fail (online + offline) | Auto / manual / post-trial invoices | Paddle subscription fail | PAST_DUE from no-token billing **or** `GatewayPaymentFailed` |
| Silent retry | ML, 8 / 14d default | Soft: up to 12; Revive 200+ signals | Intelligent Retry separate from email | Tactical Retries, 30d | AUTO_CHARGE day-offset, max 4, no ML, no decline fork |
| Hard decline | 9 codes; schedule but do not execute | Pause retry + card-update request | Skip retry schedule; wait for new PM | Failure-code factor in tactical retry | **No fork.** `charge_declined` is a string. |
| Conversation | Separate Customer Emails | Reminder emails separate from retry | Versioned email campaign, 3 cycle types | Email + in-app + SMS, expert copy | Inline EMAIL / WHATSAPP / AUTO_CHARGE steps |
| WhatsApp | No | No | No | No | **Sold. Not connected.** |
| SMS | No | Not headline | Via merchant webhook | First-class optional | Console log label only |
| Update PM | Portal / Payment Element | Pay Now | Hosted login token | No-login form + wallets | Public `/update-payment/{subId}` (GUID, not signed) |
| Terminal | Cancel / unpaid / stay past_due | Sub + invoice actions | Fail invoice ± expire sub; or never fail | Pause or cancel | CANCEL / SUSPEND / NONE |
| Pause dunning | — | — | Stop Dunning on invoice | — | `DunningPausedUntil` datetime |
| Versioning | Policy settings | Window persists across Revive toggle | **Snapshot per invoice** | Paddle-owned | **Live mutate** (DayOffset idempotency only) |
| Metrics | Recovery analytics | Revive $ and rate vs baseline | Per-version effectiveness | 50%+ claim, % take | 3 counters, RM hardcoded, no time series |
| Card updater | Yes (separate) | Via gateway + Revive hold | Account Updater + infinite retry | Implicit in Paddle | **None** |
| Timezone | Local signals in ML | Timezone is a Revive signal | Site TZ | Location factor | **UTC date math** |

**Honest positioning sentence:** Lazuar Pay has a **Chargebee/Recurly-shaped campaign CMS** (priority, product targeting, day-offset timeline, grace, cancel/suspend) sitting on a **status+day worker**, not an invoice+attempt engine, with **Stripe/Chargebee-shaped retry ambition** that is currently a **fixed 4-attempt AUTO_CHARGE** and **no decline intelligence**, plus a **Paddle-Retain-shaped WhatsApp/SMS story** that is **not implemented**.

---

## Our engine vs our UI

This section is the ground truth. UI first (what a founder sees), then the closed loop, then each required topic.

### Campaign builder UX

**List** — `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/DunningCampaignsPage.tsx`

- Empty state: “Revenue Recovery Engine” + **Deploy Recommended Strategy** (POST `/admin/commerce/dunning-campaigns/defaults`).
- Table: name, priority, product count / step count, final action + grace, **RM {recovered_revenue}**, saved/churned, Active/Archived.
- Click row → builder. No analytics drill-down. No version history. Currency is **hardcoded RM** regardless of `product.Currency`.

**Builder** — `CampaignBuilderPage.tsx` + `CampaignSettingsPanel.tsx` + `CampaignTimeline.tsx` + `DunningStepEditor.tsx`

Settings column:

1. **Identity:** name, priority (higher evaluates first), active checkbox (edit only).
2. **Targeting:** product multi-select (empty = all); payment methods as two checkboxes — `ONLINE_GATEWAY` labeled **“Online Gateways (Cards/FPX)”** and `MANUAL` labeled **“Manual/Offline Transfers.”** That FPX label is a product lie (see Entry conditions).
3. **Terminal escalation:** CANCEL / SUSPEND / NONE + grace days. Rose warning: “protect your revenue.”

Timeline column:

- T0 marker, vertical line, add-step, terminal warning icon.
- Each step: day-offset **select only** (`-14,-7,-3,-1,0,1,3,5,7,14,30`). Domain accepts any `int`. API accepts any `int`. UI does not.
- Action select: `EMAIL` | `WHATSAPP` (**label: “Send WhatsApp (not connected)”**) | `AUTO_CHARGE`.
- EMAIL: subject + markdown/HTML body; live preview via POST `/admin/communications/templates/preview`.
- WHATSAPP: amber banner *“Email only until WhatsApp connected”* — WHATSAPP steps run as email **if an email body is present**, else skip. The builder **does not collect an email body** for WHATSAPP steps (save strips `email_body` unless `action_type === "EMAIL"`). So a WHATSAPP step created in the UI **cannot** fall back to email. That contradicts the banner.
- AUTO_CHARGE: blue card — “silently request Stripe/CHIP,” “max 4 attempts,” plus an honest **Billplz does not support off-session** note.
- Placeholder hint: `{{customer_name}}`, `{{plan_name}}`, `{{update_payment_link}}`. Catalog in Communications query service does **not** list `{{update_payment_link}}` or `{{amount}}` / `{{days_overdue}}` (those exist only in the dunning hydrator).

**Save behavior.**

- Create POST / update PUT full replace.
- Steps sorted by `day_offset` on save.
- Update handler: `ClearSteps()` + `AddStep()` → **new step GUIDs every save**.
- Delete: `window.confirm` then DELETE. Backend now **refuses** if any subscription has `CurrentDunningCampaignId == campaign`. UI does not explain that; the toast will surface `error.detail`.
- No GET-by-id: builder loads **all** campaigns and `find`s by id.
- No simulation, no dry-run, no per-subscriber “what already sent.”

**Default strategy** (`GenerateDefaultDunningCampaignsCommandHandler`):

| Day | Action | Copy |
|-----|--------|------|
| −3 | EMAIL | “Upcoming renewal for `{{plan_name}}`” + update-payment link |
| 0 | EMAIL | “renewal due today” + update-payment link |
| +3 | WHATSAPP | “Hey `{{customer_name}}`, your `{{plan_name}}` is past due…” + update-payment link. **No email body.** |
| Grace 7 | CANCEL | — |

No AUTO_CHARGE in the default. Idempotent: if any campaign exists, `/defaults` is a no-op (still returns `"generated"`). Seeded on tenant template bootstrap via `DefaultTemplatesSeededIntegrationEventHandler`.

**Subscriber recovery panel** — `SubscribersPage.tsx`, **only when `status === "PAST_DUE"`**.

- Campaign name or “None (Will not escalate)”.
- “Current Step” = `current_dunning_step` = `LastCompletedDayOffset ?? CurrentDunningStepIndex`. The label says **“Step N”**. The number is a **day offset** (0, 3, 7…), not a 1-based step index. After the default +3 WhatsApp (or skip), ops sees “Step 3”.
- Pause until datetime-local; resume. Copy says “emails and escalation” — AUTO_CHARGE is also paused because the PAST_DUE claim query respects `DunningPausedUntil`.
- SUSPENDED subscribers **do not** see this panel. After grace SUSPEND they disappear from recovery UX even though update-payment still accepts SUSPENDED.
- Human WhatsApp deep-link (`wa.me`) on the phone field is **staff-initiated chat**, not the engine.

**API surface (TypeSpec + Minimal).**

| Method | Route | Notes |
|--------|-------|-------|
| GET | `/admin/commerce/dunning-campaigns` | List + nested steps (no step ids in DTO) |
| POST | `/admin/commerce/dunning-campaigns` | Create |
| PUT | `/admin/commerce/dunning-campaigns/{id}` | Full replace |
| DELETE | `/admin/commerce/dunning-campaigns/{id}` | Blocked if assigned |
| POST | `/admin/commerce/dunning-campaigns/defaults` | Idempotent seed |
| POST | `/admin/commerce/subscribers/{id}/dunning/pause` | `pause_until` |
| POST | `/admin/commerce/subscribers/{id}/dunning/resume` | Clear pause |
| GET | `/public/commerce/checkout/{subId}/arrears` | Product, amount, status |
| POST | `/public/commerce/checkout/{subId}/update-payment` | Hosted checkout; injects `dunning_campaign_id` |

Missing vs Recurly/Chargebee: GET-by-id, assign-campaign, force-run, skip-step, preview schedule, activity timeline, analytics, `dunning.step_dispatched` webhook, enums for `action_type` / `final_action`.

### Closed loop as coded (2026-08-16)

```
ACTIVE + NextBillingDate <= now
  BillingEngineJob claims FOR UPDATE SKIP LOCKED (batch 50)
    if vaulted customer+token:
      if no ChargeAttemptLog for that date:
        insert attempt #1 source=BILLING
        publish ExecuteOffSessionCharge (DunningCampaignId=null)
      else:
        do nothing (retries belong to dunning)
    else:
      MarkAsPastDue
      publish subscription.past_due (fulfillment + outbound webhook)

ExecuteOffSessionCharge
  no/inactive gateway → GatewayPaymentFailed (failure_reason=gateway_not_configured)
  adapter returns false → GatewayPaymentFailed (failure_reason=charge_declined)
  adapter throws (Billplz NotSupportedException) → **unhandled; no failed event**
  adapter true (Stripe succeeded|processing, CHIP paid|pending_charge, Razorpay payment id)

GatewayPaymentFailed (Commerce)
  resolve subscription_id or receipt
  MarkFailed on PENDING ChargeAttemptLog (by charge_attempt_id or latest PENDING)
  if not CANCELED/SUSPENDED:
    MarkAsPastDue if needed
    assign highest-priority matching campaign if none
    if newly PAST_DUE: outbound subscription.past_due

DunningEngineJob every Workers:DunningEngineInterval (default 1h)
  load all active campaigns (all tenants) AsNoTracking, priority desc
  read Messaging:WhatsAppEnabled (default false)
  claim ACTIVE in next 14 days → pre-dunning
  claim PAST_DUE and (pause is null or expired) → past-due steps / grace

Payment success (webhook PAYMENT_COMPLETED or update-payment checkout)
  if PAST_DUE → RecoverFromPayment (advance dates, ClearDunning)
  if SUSPENDED → Resume (advance next billing, ClearDunning)
  if wasInArrears → RecordRecovery(amount) on campaign from metadata or CurrentDunningCampaignId
  MarkSucceeded on PENDING attempt
  vault tokens if present
```

**This loop is real.** Module tests cover PAST_DUE assign, attempt multi-row, `RecoverFromPayment`, variable substitution, webhook PAYMENT_FAILED publish. Full vaulted-gateway → email → portal pay remains an operator residual on the Phase A checklist, not a missing handler.

### Entry conditions (card vs FPX vs offline)

Inference is a **boolean on `VaultedTokenId`**, used in billing, failed-handler, pre-dunning, and past-due:

```text
inferredPaymentMethod = string.IsNullOrEmpty(sub.VaultedTokenId) ? "MANUAL" : "ONLINE_GATEWAY"
```

| Real-world rail | How they enter recovery | Targeting bucket | AUTO_CHARGE | Conversation that can work |
|-----------------|-------------------------|------------------|-------------|----------------------------|
| **Vaulted card (Stripe / CHIP / Razorpay)** | Billing attempt 1 → off-session → fail event → PAST_DUE + campaign | `ONLINE_GATEWAY` | Attempts 2–4 on AUTO_CHARGE steps | Email (WA skipped) + update-payment |
| **FPX via Billplz** | Almost never vaulted. Billing **no-token** path → PAST_DUE on due date **without a failed charge** | `MANUAL` | Skipped (no token). Billplz `ChargeOffSession` **throws** | Email reminder + update-payment (customer-present FPX again) |
| **FPX via CHIP** (if they stored a recurring token) | Treated as ONLINE_GATEWAY | `ONLINE_GATEWAY` | CHIP off-session may work | Same as card |
| **Offline / bank transfer / “record payment”** | No token → PAST_DUE on due date | `MANUAL` | Skipped | Email + ops “Log Payment” |
| **Reminder-only sub** (`IsReminderOnly`) | Same no-token path if no vault | `MANUAL` | Skipped | Email |
| **UI checkbox “Cards/FPX”** | Sets `ONLINE_GATEWAY` only | Misses typical Billplz FPX (no vault) | — | A “FPX recovery” campaign targeted ONLINE_GATEWAY **will not attach** to Billplz FPX subscribers |

Billplz adapter, verbatim:

```246:251:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
    public Task<bool> ChargeOffSessionAsync(...)
    {
        throw new NotSupportedException("Billplz does not support vaulted token off-session charges.");
    }
```

If a product is on Billplz **and** somehow has vault fields, AUTO_CHARGE publishes `ExecuteOffSessionCharge`, the handler calls the adapter, the exception is **not** caught, and **`GatewayPaymentFailed` is not published**. The attempt row stays PENDING. The step is still recorded as dispatched (`RecordReminderDispatched` happens after the publish, not after success). That subscription will not retry that DayOffset.

CHIP off-session: fetch old purchase → new purchase with metadata → `charge/` with `recurring_token`. Success if `paid` **or `pending_charge`**. Pending is treated as success at the adapter; Commerce only `MarkSucceeded` on `PAYMENT_COMPLETED`. A pending CHIP charge can leave the attempt PENDING until a paid webhook — acceptable if CHIP always follows up; opaque if not.

Razorpay off-session: still uses **hardcoded** `billing@lazuar.com` / `0000000000`. Metadata notes are correct. Recurring create can “succeed” with junk contact. Treat Razorpay AUTO_CHARGE as **not demoable**.

Fiuu / Xendit / SenangPay: **no adapters** under `Gateways/` despite README Phase 1 list. No off-session, no dunning rail.

Stripe webhook parser handles `checkout.session.completed`, `payment_intent.succeeded`, `charge.dispute.created`. It does **not** map `payment_intent.payment_failed` or `invoice.payment_failed`. Asynchronous Stripe failure after `processing` (adapter returns true) will **not** mark PAST_DUE via webhook. Synchronous `StripeException` on confirm **does** (adapter returns false → failed event). SCA / `authentication_required` is a Stripe hard-decline code; we treat it as generic `charge_declined` and will happily AUTO_CHARGE again.

**Pre-dunning entry** is independent of PAST_DUE: any ACTIVE sub with `NextBillingDate` in `(now, now+14d]` is claimed. Campaign is chosen **every hour** by targeting (not pinned). Pause is **ignored** for pre-dunning. A CS pause on a past-due sub does not stop “your renewal is in 3 days” if they somehow became ACTIVE again with a pause leftover — pause is cleared on `ClearDunning` / recover, so this is narrow.

**No invoice entity.** Entry is `Subscription.Status` + `NextBillingDate`. Partial payments, multiple open invoices, mid-cycle plan change, and “this retry is for invoice X” do not exist. Chargebee/Recurly are invoice-centric; we are not.

### Retry schedule and catch-up

**Ownership.** Billing owns attempt **1**. Dunning AUTO_CHARGE owns **2–4**. `ChargeAttemptLimits.MaxAttemptsPerBillingCycle = 4` is a **constant**, not a campaign field. UI copy matches the constant. Schema unique `(SubscriptionId, TargetBillingDate, AttemptNumber)` matches the constant.

**Past-due catch-up (correct direction).**

```120:125:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs
        var dueSteps = campaign.Steps
            .Where(s => s.DayOffset >= 0 && s.DayOffset <= daysOverdue)
            .Where(s => !sub.ReminderLogs.Any(l =>
                l.DayOffset == s.DayOffset && l.TargetBillingDate.Date == targetDate))
            .OrderBy(s => s.DayOffset)
```

If the job is down across day 3, day 5 catch-up fires the day-3 step, then day 5. Unique index `(SubscriptionId, TargetBillingDate, DayOffset)` is the idempotency key — **not** step GUID. Editing a campaign and regenerating step IDs will **not** re-spam the same offset. Adding a **new** offset that is already `<= daysOverdue` **will** fire immediately (catch-up). That is the opposite of Recurly’s snapshot.

**Same-day multi-step is still impossible.** Unique key is DayOffset, not StepId. Two steps on day 3 (EMAIL + AUTO_CHARGE) — first `RecordReminderDispatched` wins; second insert unique-violates or is filtered out by the `ReminderLogs.Any(DayOffset == …)` check after the first is in the collection. The old `FirstOrDefault` bug was replaced by `foreach`, but the unique index re-imposes “one action per calendar offset.”

**Pre-dunning catch-up is inverted (P0 scheduler bug).**

```37:40:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PreDunning.cs
        var dueSteps = campaign.Steps
            .Where(s => s.DayOffset < 0
                && Math.Abs(s.DayOffset) <= daysUntilDue
                && (s.ActionType == "EMAIL" || s.ActionType == "WHATSAPP" || s.ActionType == "ALL"))
```

Worked example. Today is **10 days** before due. Campaign has −14, −7, −3, −1.

| Step | `Abs(offset) <= 10`? | Should fire if catch-up means “due or overdue”? |
|------|----------------------|--------------------------------------------------|
| −14 | 14 ≤ 10? **No** | Not yet (correct to skip) |
| −7 | 7 ≤ 10? **Yes** | **No — this is 3 days early** |
| −3 | 3 ≤ 10? **Yes** | **No — 7 days early** |
| −1 | 1 ≤ 10? **Yes** | **No — 9 days early** |

The first hourly tick after a sub enters the **14-day pre-window** fires **every** negative step whose `|offset| <= daysUntilDue` — i.e. almost the entire pre-dunning sequence at once — then logs those offsets so they never send on the real day.

Correct catch-up is the **other** inequality: fire when `daysUntilDue <= Abs(DayOffset)` (the scheduled day has arrived or passed) and not yet logged. There is **no `DunningEngineJob` test**. `BillingEngineJobTests` exists; the inverted pre-dunning predicate was never asserted.

Default campaign −3 EMAIL will therefore send “renews in 3 days” as soon as the sub is **14 days** out (3 ≤ 14), with `days_overdue` forced to **0** in the pre-dunning dispatch payload.

**Other schedule rigidities.**

- Job interval: `Workers:DunningEngineInterval` default 1 hour (now **configurable**, unlike the old gap doc).
- Pre-window: **hardcoded 14 days** in SQL `INTERVAL '14 days'` and in-memory `AddDays(14)`. UI allows −14 only; a −21 step would never be claimed.
- Timezone: `now.Date` and `NextBillingDate.Value.Date` are UTC. MYT (UTC+8) merchants get a day-boundary skew every night 00:00–08:00 MYT.
- No time-of-day, no quiet hours, no payday alignment.
- AUTO_CHARGE does not wait for the previous attempt to leave PENDING. If attempt 2 is still PENDING (CHIP `pending_charge`, Stripe `processing`), a later AUTO_CHARGE step still inserts attempt 3.
- Max 4 is not decline-aware. Four `stolen_card` retries will execute if four AUTO_CHARGE offsets exist.

**Concurrency.** Claim uses `FOR UPDATE SKIP LOCKED` per subscription, per-sub `SaveChanges`, failed ids skipped in-batch. Multi-replica is **claimed-safe** for the row. Deploy docs still prefer replica=1 for the API process. Unique indexes remain the last line of defense for double-dispatch races.

### Email vs WhatsApp vs SMS

| Channel | Builder | Engine | Transport | Credits | Suppression | Sellable? |
|---------|---------|--------|-----------|---------|-------------|-----------|
| **Email** | First-class | `EMAIL` / demoted `ALL` / demoted `WHATSAPP` if email body exists | Resend platform key or tenant BYOK | Email send **not** deducted | Email suppressions honored | **Yes**, if tenant email config is active |
| **WhatsApp** | Labeled “not connected”; no email-body field | `Messaging:WhatsAppEnabled` default **false** → skip or demote | `ConsoleMessagingService` logs `[MESSAGING/SMS]` | Cost 2 if a send actually runs | **None** (phone opt-out does not exist) | **No** |
| **SMS** | Not in the select | Not an action type | Console class comment says SMS; no provider | — | — | **No** |
| **ALL** | Not in the select | Engine accepts it | Email + WA | WA portion only | Email only | Hidden / unused |
| **In-app** | No | No | No | — | — | No (Paddle Retain has this) |

Dispatch path that *does* run for EMAIL:

1. `DispatchCommunicationStepAsync` publishes `FulfillmentRequested(COMMUNICATIONS, reminder.dunning)` with plan/amount/currency/days_overdue and **effective** action (WA body stripped if demoted to EMAIL).
2. Communications loads CRM + workspace, builds links, substitutes variables, markdown→HTML, publishes `DispatchMessage`.
3. Messaging: skip WA if flag false (log `SKIPPED` / “WhatsApp channel disabled”); send email via Resend; persist `MessageDeliveryLog`.

**Default +3 WHATSAPP step has no email body.** With the flag false, `ResolveEffectiveCommunicationAction` returns **null**. Past-due logs “skipped” and **still** `RecordReminderDispatched` — the offset is consumed. Pre-dunning does the same. Deploy Recommended Strategy therefore includes a step that **never talks to the customer** on a default deploy.

**Lifecycle emails (separate CMS).** On grace CANCEL/SUSPEND the engine publishes typed `SubscriptionCanceled` / `SubscriptionSuspended`. `LifecycleEventHandlers` looks up Communications templates named **“Payment Failed”** (for suspend!) and **“Subscription Cancelled”**. Suspend uses `{{renewal_link}}` replaced with the **literal** `https://portal.lazuar.com/checkout/update` — not the tenant update-payment URL, not a magic link, not `App:ClientUrl`. That is a second, broken recovery conversation after the campaign ends.

**Dual CMS.** Dunning copy lives on `DunningStep`. Communications `MessageTemplates` are unused for `reminder.dunning` (inline bodies). Legacy names “Subscription Renewal (3 Days / Due Today / Overdue)” are in `OrphanNames` and slated for cleanup. Founders editing **Templates** will not change dunning. Founders editing **Dunning** will not change suspend/cancel mail.

### Update-payment “magic links”

There are **two** links. Only one is magic.

| Placeholder | URL built | Auth | Used by default campaign |
|-------------|-----------|------|---------------------------|
| `{{update_payment_link}}` | `{App:ClientUrl}/{slug}/update-payment/{subscriptionId}` | **None.** Knowledge of the GUID is the capability. | **Yes** |
| `{{portal_magic_link}}` | `{App:ClientUrl}/{slug}/portal?token={HMAC}` | HMAC-SHA256 over `subscriptionId:expiry`, 24h TTL, `Jwt:Secret` | **No** (only if merchant types it) |
| `{{renewal_link}}` | Same as portal **without** token (`.../{slug}/portal`) | None | Not in default dunning steps |

`MagicLinkTokenService` is real and tested. Communications **does** call `GenerateToken` on every dunning hydrate. The default copy never uses the token.

Portal page `update-payment/[subId]/page.tsx`:

- GET arrears (product, amount, status).
- If not PAST_DUE / SUSPENDED: “Account in Good Standing.”
- Else: amount due + **Update Payment Method** → POST update-payment → hosted checkout.
- Checkout metadata: `type=commerce_subscription`, `subscription_id`, `tenant_id`, `dunning_campaign_id` if assigned. Gateway = **product gateway**, not hardcoded Billplz (Phase A A.7). `setup_future_usage` requested so a successful recovery can vault a new card.

**Not magic, not no-login in the Paddle sense.** Paddle Retain’s form is a signed, customer-bound, wallet-ready overlay. Ours is a public GUID URL that starts a full checkout. Enumeration of UUIDv7 is hard; forwarding the email leaks a permanent-until-paid recovery URL (no expiry). There is no token rotation, no one-time consume, no Apple Pay / Google Pay surface on the arrears page itself (those only exist if the hosted checkout gateway offers them).

ACTIVE subscribers hitting update-payment get BadRequest “does not require a payment update.” There is no “change card before it fails” hosted path from dunning (pre-dunning email still points at update-payment, which will bounce if they are still ACTIVE). That is a **pre-dunning UX hole**: the −3 email says “ensure your payment method is up to date here: {{update_payment_link}}” and the endpoint **rejects ACTIVE**.

### Hard vs soft declines

**Schema pretends.** `ChargeAttemptLog` has `FailureReason`, `GatewayName`, `GatewayResponseCode`. Tests write `card_declined`. The failed-handler copies `gateway_response_code` **if present in metadata**.

**Adapters do not emit codes.**

```303:307:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe off-session charge failed for customer {CustomerId}", customerId);
            return false;
        }
```

`StripeException.StripeError.DeclineCode` is discarded. Off-session failed metadata is only:

`failure_source=off_session`, `failure_reason=charge_declined|gateway_not_configured`, plus ids.

CHIP / Razorpay return false with no code. Billplz throws.

**Engine does not branch.** AUTO_CHARGE is “if attemptCount < 4 and vault exists, fire.” Stolen card, expired card, insufficient funds, and `authentication_required` are the same path. We will retry a lost card three more times and pay gateway fees to do it.

**Webhook side.** CHIP `purchase.payment_failure` → `PAYMENT_FAILED` → Commerce. Stripe `payment_intent.payment_failed` is **not mapped** (falls through as raw type, ignored unless it equals the three handled enums). Billplz unpaid → `PAYMENT_FAILED`.

This is the largest **intelligence** gap versus every named competitor. It is not a missing UI checkbox; it is a missing classification on the only path that costs money.

### Recovered revenue metrics

**What exists.**

- `DunningCampaign.RecoveredRevenue` (decimal, += amount)
- `SavedSubscriptions` (int, ++ per `RecordRecovery`)
- `ChurnedSubscriptions` (int, ++ per `RecordChurn`)
- Ops list: `RM {n.toFixed(2)}` and `saved / churned`

**When `RecordRecovery` runs.** Only `GatewayPaymentCompletedIntegrationEventHandler` subscription path, and only if `wasInArrears` (PAST_DUE or SUSPENDED) and a campaign id is available (metadata or `CurrentDunningCampaignId` captured **before** clear). Amount = `@event.AmountPaid`.

**When it does not run.**

| Recovery path | Clears dunning? | Increments recovered $? |
|---------------|-----------------|-------------------------|
| Update-payment checkout success | Yes | **Yes** (metadata has campaign id) |
| Silent AUTO_CHARGE success (off-session metadata complete) | Yes | **Yes** (handler falls back to `CurrentDunningCampaignId`) |
| Ops **Log Payment** / `RecordSubscriberPayment` | Yes (`RecoverFromPayment` / `Resume`) | **No** |
| Comped / zero amount record | Yes | **No** (and AmountPaid would be 0 anyway) |
| Recover while campaign deleted | Yes | **No** (campaign lookup fails) |
| Success while still ACTIVE (retry before PAST_DUE lands) | N/A | **No** (`wasInArrears` false) |

Offline / FPX-manual recovery — the Malaysia-shaped path — **systematically undercounts** the only metric the list page shows.

**Quality of the numbers.**

- No per-sub audit. No “which step / which channel.”
- No time series. No recovery **rate**. No cohort.
- `SavedSubscriptions++` on every `RecordRecovery` even if the same sub recovers twice in two cycles (not wrong, but it is “saves,” not “unique saved”).
- `RecordChurn` only on **CANCEL**. SUSPEND is invisible to churned.
- Double `float64` in the DTO (`recovered_revenue: float64`). Not accounting-grade.
- Currency: RM in the UI. A USD product increments the same bucket and is painted as ringgit.

Health metric `dunning_cancels_since_start` is a process counter (`LazuarMetrics.DunningCancelsTotal`), not recovered revenue.

### Pause / cancel terminal actions

**Pause.** `PauseDunning(until)` / `ResumeDunning()`. PAST_DUE claim: `DunningPausedUntil IS NULL OR <= NOW()`. Pre-dunning **does not** consult pause. No validation that `until` is in the future. No audit log. Resume is “clear the timestamp,” not “resume the subscription.”

**Grace.** When `daysOverdue >= GracePeriodDays` **before** remaining steps: CANCEL or SUSPEND, then `return` (later steps never run). If grace is **3** and steps exist on day 5 and 7, those steps are dead. The builder does not warn that grace < last step offset.

**CANCEL.** `sub.Cancel()`, `RecordChurn()` on a **tracked** reload of the campaign (AsNoTracking snapshot cannot mutate — they got this right), `LazuarMetrics.RecordDunningCancel()`, `SubscriptionCanceledIntegrationEvent`, `subscription.canceled` internal fulfillment. Sub leaves the PAST_DUE query. Campaign id is **not** cleared (canceled rows are just orphaned). Fine.

**SUSPEND.** `sub.Suspend()`, **no** `RecordChurn()`, `SubscriptionSuspendedIntegrationEvent`, `subscription.suspended` fulfillment. Leaves PAST_DUE query. **No further campaign messages.** Recovery is update-payment (allowed) or ops Log Payment. Lifecycle email is the misnamed “Payment Failed” template with a hardcoded portal host.

**NONE.** Stay PAST_DUE forever. Steps fire once per offset via catch-up. No hourly re-cancel. Silent.

**Delete campaign.** Blocked if assigned. Archive (`IsActive=false`) leaves assigned PAST_DUE subs pinned to a campaign the engine **cannot load** (`campaigns` query is `Where(IsActive)`; `if (campaign == null) return`). Those subs **never reach grace**. Archive-while-assigned is a **stuck PAST_DUE** footgun. Recurly deactivation reassigns to default; we do not.

**No mid-funnel actions.** No coupon, no restrict-but-keep-active, no extend grace per sub, no force charge, no skip step, no notes. Pause-until is the only CS tool.

### Campaign snapshot versioning

**We do not have it.** Recurly’s sentence — changes do not affect invoices already in dunning — is the industry bar.

What we did instead (Phase A A.4):

- Idempotency key moved from `(SubscriptionId, ScheduleId=step.Id, TargetBillingDate)` to `(SubscriptionId, TargetBillingDate, DayOffset)`.
- Live edits no longer **re-fire** old offsets.
- Live edits **do** change: copy for not-yet-sent offsets, grace length, final action, targeting (does not unpin), priority (does not reassign pinned subs), and **new** offsets (catch-up fires them now).

`ClearSteps()` still drops and recreates rows. Step GUIDs in `ChargeAttemptLog.DunningStepId` go stale. There is no `DunningRun` / `CampaignVersion` / Settings History / per-version analytics.

Pinned assignment (`CurrentDunningCampaignId`) is sticky until clear/resume/recover. A sub will not jump campaigns when a higher-priority campaign is created. Recurly’s Account → Plan → Default is evaluated at invoice entry; we evaluate at PAST_DUE assign and then freeze the **id**, not the **definition**.

---

## Honesty audit (WhatsApp, email, retries)

### Can we sell “WhatsApp dunning”?

**No.** Not on a default deploy, not on a demo to a paying founder, not in a sales deck that a customer can click through.

Decision 00.4 is explicit: *“Console WhatsApp is not a production channel; docs/defaults must not claim automated WhatsApp dunning as live.”* Messaging README repeats it. Ops builder labels the action **“not connected.”** `appsettings.json` has `"Messaging:WhatsAppEnabled": false`. `IMessagingService` is registered as `ConsoleMessagingService` only.

What “WhatsApp dunning” would require to be sellable (ADR 020 bar):

1. Meta Cloud / Wati / Twilio adapter behind `IMessagingService`.  
2. WABA + phone-number-id tenant config (does not exist).  
3. Approved **utility** templates (business-initiated, outside 24h session). Dunning is the textbook utility case.  
4. Interactive button: “Tap here to pay RM50 via FPX” **in chat** (ADR 020). We send a plaintext URL at best.  
5. Flag on. Credits funded. Phone on the CRM profile in MSISDN form.  
6. Default campaign step that actually delivers (today: WA-only +3, skipped).  
7. Phone suppression / PDPA opt-out.  
8. Delivery receipts that ops can see as SENT, not SKIPPED.

None of 1–4 exist. 5 is off. 6 is skipped. 7 is email-only. 8 logs SKIPPED when the flag is false.

**If someone flips `Messaging:WhatsAppEnabled=true` without a provider**, the handler calls `ConsoleMessagingService.SendMessageAsync`, which logs `[Local Dispatch] [MESSAGING/SMS]` and returns success. Credits **are deducted**. Ops delivery log says **SENT**. That is worse than the flag-off path: it **looks** live and bills the wallet for a console write. Do not flip the flag to “demo WhatsApp.”

### README vs itself vs code

| Location | Claim | Truth |
|----------|-------|-------|
| README L6 hero | “automated WhatsApp dunning” in the opening paragraph | Doc-off |
| README L18 watermark | “email dunning templates + … WhatsApp dunning … are roadmap (Phase D)” | **Closest to truth** |
| README L36 diagram | Failure → “WhatsApp (Smart Dunning)” | Doc-off |
| README L65 | WA dunning deducts micro-credits | Wired **if** a send runs; default send does not run |
| README L77 Phase 1 | “Native WhatsApp Dunning: Meta Cloud API” | Not in Phase 1 code; 00.4 freeze |
| ADR 020 | Interactive FPX button in chat, 95% open rate | Aspirational |
| ADR 021 | Keep WhatsApp dunning | Keep the *job*; channel frozen |
| ADR 023 | Compete on Billplz FPX + automated WhatsApp dunning | FPX reminder path exists; WA does not |
| Ops empty state | “chase failing payments” | Email + AUTO_CHARGE, not WA |
| Ops WA option | “not connected” | Honest |
| Ops WA banner | “run as email when an email body is present” | True in engine; **UI does not collect that body** |
| Default strategy +3 | WhatsApp past-due ping | **Skipped** on default deploy |
| Phase A A.9 | Flag off **or** real provider | They chose flag off — correct |
| Checklist L712 | README claims must match demoable paths | **Still open** |

**Sellable sentence (watermark quality):** “Lazuar emails the customer when a renewal fails, can retry a vaulted Stripe/CHIP card up to four times, and gives them a link to pay or update the method. WhatsApp is on the roadmap.”

**Unsellable sentences:** “Native WhatsApp Dunning.” “Meta Cloud API.” “Tap here to pay RM50 via FPX.” “Smart Dunning” as if Chargebee Revive.

### Email honesty

Email **is** the production recovery channel.

What is true:

- Tenant Resend BYOK + platform Resend path is real.
- Dunning hydrator fills `{{plan_name}}`, `{{amount}}`, `{{currency}}`, `{{days_overdue}}`, `{{customer_*}}`, `{{business_name}}`, `{{update_payment_link}}`, `{{portal_magic_link}}`. Unit test `DunningTemplateVariableSubstitutionTests` locks this.
- Suppression list is honored for email.
- List-Unsubscribe is wired for marketing/broadcast; dunning is transactional and should stay that way.
- Checkout still **requires** valid email config — a tenant who can sell can usually mail.

What is still dishonest or sharp-edged:

- Pre-dunning inverted catch-up can dump “renews in 3 days” at day −14.
- Pre-dunning `update_payment_link` **rejects ACTIVE**.
- Lifecycle suspend mail points at `https://portal.lazuar.com/checkout/update`.
- No open/click tracking in-module (Resend webhooks exist for bounce/complaint; not a dunning funnel).
- `{{amount}}` is `product.Price`, not an invoice balance (no proration, no tax line, no failed-attempt fee).

### Retries honesty

| Promise | Reality |
|---------|---------|
| “Max 4 attempts” (UI) | True as a cap. Billing 1 + dunning 2–4. Schema allows it. |
| “Smart” retries | **False.** Calendar AUTO_CHARGE only. |
| Hard vs soft | **False.** Codes dropped on the floor. |
| Billplz auto-retry | **False.** UI says so; good. |
| Razorpay auto-retry | Adapter exists; dummy contact; **do not demo.** |
| Catch-up if the job was down | **True for PAST_DUE.** **False/inverted for pre-dunning.** |
| Failed vaulted charge enters dunning | **True** (Phase A). Old gap doc is stale. |
| Silent retry advances the sub | **True** if metadata + webhook completed path runs. |
| 4 retries of a stolen card | **Will happen** if the campaign says so. |

Stripe Smart Retries’ whole product is “do not execute the hard-decline retry.” We execute it. That is not a small gap; it is the difference between a recovery engine and a retry hammer.

### What we *can* demo tomorrow (operator script)

1. Deploy Recommended Strategy (or keep the seeded default).  
2. Confirm tenant Resend BYOK.  
3. Confirm `Messaging:WhatsAppEnabled=false` (do not “turn on WhatsApp”).  
4. Stripe or CHIP product with a vaulted test card that declines.  
5. Run billing job (or wait an hour) → subscriber **PAST_DUE**, campaign assigned.  
6. Wait for day-0 EMAIL (or temporarily set a 0-offset EMAIL if already overdue — catch-up will fire).  
7. Open update-payment from the email, pay, see ACTIVE + RM counter increment.  
8. Optional: add an AUTO_CHARGE step on a future offset; confirm attempt 2 row; do **not** use Billplz.

That is an **email + hosted checkout** recovery demo. Call it that.

---

## Gap table

Depth: `shipped` · `partial` · `doc_off` · `stub` · `none`.  
V is a product verdict for **Lazuar Pay**, not Aura.

| ID | Job | Ours | Stripe | Chargebee | Recurly | Retain | V | Why it matters |
|----|-----|------|--------|-----------|---------|--------|---|----------------|
| DN-001 | Campaign builder (timeline, priority, targeting) | shipped | — (not a CMS) | shipped | shipped | none (managed) | Both | UX is real; this is the Chargebee/Recurly cousin |
| DN-002 | Deploy default strategy | shipped | n/a | shipped | shipped | shipped (theirs) | Both | Idempotent seed exists |
| DN-003 | Entry: vaulted card fail → PAST_DUE | shipped | shipped | shipped | shipped | shipped | Both | Phase A closed this |
| DN-004 | Entry: no-token / offline due → PAST_DUE | shipped | n/a | shipped (offline cycle) | shipped (manual cycle) | n/a | Both | Malaysia-shaped path |
| DN-005 | Entry: Billplz FPX as its own cycle | none (lumped MANUAL; UI says FPX∈ONLINE) | local-PM retries | offline + smart | manual cycle | n/a | Theirs | SEA rail is our claimed moat |
| DN-006 | Past-due catch-up | shipped | n/a (their scheduler) | shipped | shipped | shipped | Both | `DayOffset <= daysOverdue` |
| DN-007 | Pre-dunning catch-up | **partial (inverted)** | n/a | shipped | shipped | shipped | Partial | Fires future −N steps at day −14 |
| DN-008 | Same-day multi-action (email+charge) | none (unique DayOffset) | separate products | emails ⊥ retries | emails ⊥ Intelligent Retry | emails ⊥ tactical retry | Theirs | We fused two jobs into one step list |
| DN-009 | Configurable retry count / window | none (const 4, day offsets) | 8/14d or custom 3 | 12 smart or 5 custom | per campaign | 30d managed | Theirs | |
| DN-010 | Hard vs soft decline fork | none | shipped (9 codes) | shipped | shipped | partial (factor) | Theirs | We retry lost cards |
| DN-011 | Decline code persistence | partial (columns; adapters drop codes) | shipped | shipped | shipped | shipped | Partial | |
| DN-012 | Timezone / payday / TOD | none (UTC date) | ML local | Revive TZ + funding | site TZ + best practice | location + TOD | Theirs | MYT skew |
| DN-013 | Email conversation | shipped | separate product | shipped | shipped | shipped | Both | **The sellable channel** |
| DN-014 | WhatsApp conversation | **stub + doc_off** | none | none | none | none | Never* | *Until 00.4 reopen + Meta utility templates |
| DN-015 | SMS conversation | none | none | not headline | via webhook | shipped | Later | Retain’s actual extra channel |
| DN-016 | In-app recovery prompt | none | customer portal | Pay Now | hosted account | Paddle.js | Later | |
| DN-017 | Interactive in-chat pay | none | n/a | n/a | n/a | n/a | Never* | ADR 020 fantasy; Meta + FPX-in-WA |
| DN-018 | Update-payment link | partial (unsigned GUID; ACTIVE rejected) | portal / PE | Pay Now | hosted token | no-login + wallets | Partial | Pre-dunning link is broken for ACTIVE |
| DN-019 | Signed magic recovery link | partial (portal token unused in default copy) | hosted | hosted | `hosted_login_token` | signed form | Partial | Token exists; default email ignores it |
| DN-020 | Card Account Updater | none | shipped | via gateway | shipped | implicit | Later | Hard-decline recovery without email |
| DN-021 | Recovered $ metrics | partial (3 counters; RM; skip manual pay) | shipped | shipped | per-version | 50%+ / % take | Partial | Offline recovery invisible |
| DN-022 | Recovery funnel / rate | none | shipped | Revive dashboard | effectiveness report | managed | Theirs | |
| DN-023 | Pause dunning | shipped | — | — | Stop Dunning | — | Both | Ours is datetime; theirs is invoice action |
| DN-024 | Terminal cancel / suspend / none | shipped | cancel / unpaid / past_due | sub+invoice | fail invoice ± expire | pause or cancel | Both | SUSPEND skips churn counter |
| DN-025 | Archive-safe / delete-safe | partial (delete blocked; archive sticks PAST_DUE) | n/a | revert to default | revert to default | n/a | Partial | Archive footgun |
| DN-026 | Campaign snapshot versioning | none | weak | window persists | **shipped** | n/a | Theirs | Live mutate still rewrites in-flight copy/grace |
| DN-027 | Assign campaign to account/plan | partial (product+method auto only) | automations | per gateway | Account → Plan → Default | n/a | Theirs | No manual assign API |
| DN-028 | Force retry / skip / preview | none | Dashboard | Dashboard | sandbox accelerate | n/a | Theirs | No dry-run |
| DN-029 | `dunning.step_dispatched` webhook | none | `invoice.payment_failed` | yes | `new_dunning_event` | Paddle events | Theirs | Integrators cannot add SMS themselves |
| DN-030 | Invoice / attempt-centric model | none (status+day) | invoice | invoice | invoice | subscription | Later | Caps partial pay / multi-invoice |
| DN-031 | Billplz off-session | none (throws) | n/a | n/a | n/a | n/a | N/A | Documented; keep honest |
| DN-032 | Razorpay off-session | stub (dummy contact) | n/a | n/a | n/a | n/a | Partial | Do not demo |
| DN-033 | CHIP off-session | partial (`pending_charge` = success) | n/a | n/a | n/a | n/a | Partial | |
| DN-034 | Stripe `payment_intent.payment_failed` webhook | none | native | via Stripe | via Stripe | n/a | Partial | Async processing holes |
| DN-035 | Keep original billing anniversary on recover | none (now+interval) | yes | yes | yes | **yes (docs)** | Theirs | We shift the anniversary |
| DN-036 | Default copy uses WA | shipped (and skipped) | n/a | n/a | n/a | n/a | Partial | Defaults should be email-only until 00.4 |
| DN-037 | README / ADR WhatsApp claims | doc_off | n/a | n/a | n/a | n/a | Never (as-is) | Checklist L712 still open |
| DN-038 | Engine job tests | none | n/a | n/a | n/a | n/a | Partial | Inverted pre-dunning uncaught |
| DN-039 | Quiet hours / time-of-day | none | ML | Revive | — | TOD factor | Later | |
| DN-040 | Multi-currency recovered bucket | none (RM paint) | shipped | shipped | shipped | shipped | Partial | |

\*Never **as a current sales claim**. Not Never-the-company-shape. Reopen only with a Meta utility-template epic after 00.4.

### Priority order if a recovery program opens

1. **Honesty (this week, no feature work):** README hero/diagram/Phase 1; default campaign +3 EMAIL not WHATSAPP; builder banner vs missing email-body field; do not flip `WhatsAppEnabled`.  
2. **P0 correctness:** invert pre-dunning predicate; reject ACTIVE-safe pre-dunning link (portal magic or a “manage payment” path); extract Stripe decline codes; do not AUTO_CHARGE hard declines.  
3. **P1 reliability:** campaign version snapshot **or** freeze copy+grace+steps on assign; archive reassign; `RecordRecovery` on manual pay; same-day multi-action (or split retry from message); Stripe `payment_intent.payment_failed`; `DunningEngineJob` tests.  
4. **P2 product:** FPX/MANUAL cycle distinct from card; recovery dashboard (rate, $ by day, exclude RM hardcode); signed expiring update-payment; keep anniversary; assign/force/skip APIs.  
5. **P3 / 00.4 reopen only:** Meta utility templates + interactive CTA. SMS before WhatsApp if the job is “reach them where they read” (Retain’s lesson). Do not build WA to match a README sentence.

### What we should refuse

- Selling WhatsApp dunning to close a CaaS deal.  
- Taking a % of recovered revenue (Retain pricing) — conflicts with BYOK / no-take-rate.  
- Building Revive-class ML on our volume. Copy the **hard/soft fork** and **payday-ish offsets**, not 200 signals.  
- Merging Messaging into Communications to “make WhatsApp real” (00.4 / Phase 16 lock).  
- Invoice microservice split “because Recurly has invoices.” Status+day can grow an `Invoice` later; not this program.

---

## Tracker IDs

Family **`DN`** — Lazuar Pay dunning / revenue recovery.  
Promote into the Pay-side checklist in this folder (do **not** drop these into Aura `PY-*`; those are guest-deposit soak rows).  
Schema aligned with `plans/007-feats` tracker conventions: depth · V · W · P · class.

**Marks:** V = Ours / Theirs / Both / Partial / Later / Never / N/A.  
**W** = suggested wave (0 = honesty/docs, 1 = correctness, 2 = reliability, 3 = product, 8 = later-nice).  
**P** = priority inside the wave (0 = first).  
**Class:** table-stakes · differentiator · later-nice · hygiene · trap.

| ID | Feature | Depth | V | W | P | Class | Src / notes |
|----|---------|-------|---|--:|--:|-------|-------------|
| DN-001 | Campaign list + builder + defaults | shipped | Both | — | — | table-stakes | Keep. Currency label is DN-021. |
| DN-002 | Payment-failed → PAST_DUE + assign | shipped | Both | — | — | table-stakes | Phase A. Do not reopen. |
| DN-003 | Off-session success clears dunning + RecordRecovery | shipped | Both | — | — | table-stakes | Metadata + handler fallback. |
| DN-004 | ChargeAttempt multi-row (max 4) | shipped | Both | — | — | table-stakes | Const is enough until DN-009. |
| DN-005 | Email dunning dispatch (Resend/BYOK) | shipped | Both | — | — | table-stakes | **Demo this.** |
| DN-006 | Variable fill plan/amount/days/links | shipped | Both | — | — | hygiene | Test locked. Catalog UI still incomplete. |
| DN-007 | Pause / resume per subscriber | shipped | Both | — | — | table-stakes | PAST_DUE only in UI. |
| DN-008 | Terminal CANCEL / SUSPEND / NONE | shipped | Both | 2 | 2 | table-stakes | RecordChurn on SUSPEND (hygiene). |
| DN-009 | Delete-while-assigned guard | shipped | Both | — | — | hygiene | |
| DN-010 | Idempotent `/defaults` | shipped | Both | — | — | hygiene | Response string still “generated”. |
| DN-011 | SKIP LOCKED claim workers | shipped | Both | — | — | hygiene | |
| DN-012 | README / ADR WhatsApp honesty | doc_off | Never | **0** | **0** | trap | Close checklist L712. Hero + diagram + Phase 1. |
| DN-013 | Default campaign email-only | partial | Partial | **0** | **0** | hygiene | Change +3 WHATSAPP → EMAIL until 00.4. |
| DN-014 | Builder: WA fallback body or hide WA | partial | Partial | **0** | 1 | hygiene | Banner lies if email_body cannot be saved. |
| DN-015 | Pre-dunning catch-up inequality | partial | Partial | **1** | **0** | table-stakes | `daysUntilDue <= Abs(offset)`. Add engine tests. |
| DN-016 | Pre-dunning ACTIVE update-payment | partial | Partial | **1** | **0** | table-stakes | Portal magic or allow manage-PM while ACTIVE. |
| DN-017 | Hard/soft decline fork | none | Theirs | **1** | 1 | differentiator | Persist Stripe `DeclineCode`; skip AUTO_CHARGE on hard list. |
| DN-018 | Stripe `payment_intent.payment_failed` | none | Partial | **1** | 2 | table-stakes | Async processing hole. |
| DN-019 | Billplz AUTO_CHARGE does not throw | partial | Partial | **1** | 2 | hygiene | Return false + failed event, never throw. |
| DN-020 | `DunningEngineJob` test matrix | none | Partial | **1** | 0 | hygiene | Pre/post, pause, grace, WA flag, multi-offset. |
| DN-021 | Recovered $ honesty (currency, manual pay, suspend churn) | partial | Partial | **2** | 0 | table-stakes | RecordRecovery on Log Payment; drop RM hardcode. |
| DN-022 | Campaign snapshot on assign | none | Theirs | **2** | 0 | differentiator | Recurly bar. Freeze steps+grace+final+copy. |
| DN-023 | Archive-while-assigned | partial | Partial | **2** | 1 | hygiene | Reassign or keep processing archived if pinned. |
| DN-024 | Same-day message + charge | none | Theirs | **2** | 1 | table-stakes | Unique on (offset, action) or split retry calendar. |
| DN-025 | Signed expiring update-payment | partial | Partial | **2** | 2 | differentiator | Reuse HMAC; default copy uses it. |
| DN-026 | Keep billing anniversary on recover | none | Theirs | **2** | 2 | later-nice | Paddle/Stripe default. |
| DN-027 | FPX / MANUAL vs card campaigns | partial | Theirs | **3** | 0 | differentiator | Relabel UI; optional third method `FPX_PRESENT`. |
| DN-028 | Recovery dashboard (rate, series, by step) | none | Theirs | **3** | 1 | later-nice | Do not build Revive. Three honest charts. |
| DN-029 | Assign / force / skip / timeline APIs | none | Theirs | **3** | 1 | later-nice | CS tooling. |
| DN-030 | `dunning.step_dispatched` outbound webhook | none | Theirs | **3** | 2 | later-nice | Lets merchants add SMS themselves. |
| DN-031 | Lifecycle suspend URL uses ClientUrl | partial | Partial | **2** | 2 | hygiene | Kill hardcoded portal.lazuar.com. |
| DN-032 | Razorpay off-session real contact | stub | Partial | **3** | 3 | later-nice | Or hide Razorpay AUTO_CHARGE. |
| DN-033 | GET campaign by id | none | Partial | **3** | 3 | hygiene | Stop list-and-find. |
| DN-034 | Timezone + optional TOD | none | Theirs | 8 | 3 | later-nice | After DN-015. |
| DN-035 | Invoice abstraction | none | Later | 8 | — | later-nice | Only if CaaS grows multi-open-balance. |
| DN-036 | Card updater | none | Later | 8 | — | later-nice | Stripe Network Token / CAU via BYOK. |
| DN-037 | SMS recovery | none | Later | 8 | — | later-nice | Cheaper than Meta; Retain’s real extra channel. |
| DN-038 | WhatsApp utility templates + CTA | stub | Never | — | — | trap | 00.4. Reopen as a named epic, not a dunning tweak. |
| DN-039 | In-chat FPX checkout | none | Never | — | — | trap | ADR 020. Not a Wave item. |
| DN-040 | % of recovered revenue fee | none | Never | — | — | trap | Retain pricing. Breaks BYOK story. |

### Wave 0 (honesty, no product epic)

- DN-012, DN-013, DN-014.  
- Do not flip `Messaging:WhatsAppEnabled`.  
- Sales talk track: email recovery + optional Stripe/CHIP retry. WhatsApp is roadmap.

### Wave 1 (the engine must not lie to the calendar or the card network)

- DN-015, DN-016, DN-017, DN-018, DN-019, DN-020.

### Wave 2 (Recurly-shaped reliability)

- DN-021–DN-026, DN-031.

### Wave 3 (product, only if a founder asks twice)

- DN-027–DN-030, DN-032, DN-033.

### Never (as claims or as this-year work)

- DN-038, DN-039, DN-040.

---

*Analysis based on Lazuar Pay source as of 16 August 2026 and official competitor documentation fetched the same day. No production soak of a vaulted decline → email → update-payment loop was executed for this file. Phase A marks that loop “operator residual,” not missing code.*
