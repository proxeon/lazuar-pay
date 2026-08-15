# W0 — LP-153 analysis: template variables actually resolve

**Program:** 007-feats / Wave 0  
**ID:** LP-153 — “Variable resolution actually works”  
**Tracker:** [00-checklist-tracker.md](../00-checklist-tracker.md) (Communications, Wave 0, Lazuar = **P**)  
**Paired with:** LP-073 (email steps send). This ticket is **substitution + real links**, not “did Resend fire.”  
**Date:** 2026-08-16  
**Status:** Analysis only — **do not implement from this file until the implement pass.**  
**Does not reopen:** WhatsApp / Meta (00.4), broadcasts / abandoned cart (ADR 021), dual-CMS merge (COM-019), ACTIVE “change card” hosted path (LP-173), immediate decline mail (LP-151).

---

## 1. What “done” means

A buyer-facing **dunning or lifecycle email** must leave Communications with:

| Class | Tags | Must |
|-------|------|------|
| Names | `{{customer_name}}`, `{{plan_name}}`, `{{business_name}}` | Replaced. No leftover `{{…}}` on catalog / default-campaign copy. |
| Amounts | `{{amount}}`, `{{currency}}` (`{{total_price}}` = same number today) | Replaced when the tag is present. Formatted, not raw JSON. |
| Dates | `{{current_period_end}}`, `{{days_overdue}}` | Replaced when the tag is present. Period end is a **human date**, not empty / mock-only. |
| Recovery URL | `{{update_payment_link}}` and the catalog alias `{{renewal_link}}` | A **live** `{App:ClientUrl}/{slug}/update-payment/{subscriptionId}` URL. Not `https://portal.lazuar.com/checkout/update`. |

If a merchant types a tag that a path cannot know, leave it empty or omit it from the wiki — do not ship the literal `{{tag}}`.

**Not done:** a new email type, a unified template CMS, invoice-accurate arrears, BM/EN, or making pre-dunning update-payment accept ACTIVE (that is LP-173).

---

## 2. Verdict

| Path | Hydrate today | Honesty |
|------|----------------|---------|
| Sequenced dunning (`reminder.dunning`) | **Mostly LIVE** | Engine sends `plan_name` / `amount` / `currency` / `days_overdue`. Hydrator fills names, amounts, links, magic token. Default campaign copy only uses `plan_name` + `update_payment_link` (+ `customer_name` on the dead WA step). |
| Lifecycle “Payment Failed” (on **suspend**, not first decline) | **BROKEN** | Subject shipped raw (`{{plan_name}}` in the inbox). Body only fills `customer_name` + a **dead URL**. `business_name` / `plan_name` left literal. |
| Lifecycle “Subscription Cancelled” | **BROKEN** | Same: subject raw; only `customer_name` in the body. |
| Preview / test-reminder | **MOCK** | Fills a *different* tag set than production. Includes Community leftovers. Omits `update_payment_link` / `amount` / `days_overdue`. |
| Wiki `GET /templates/variables` | **STALE** | Documents tags production never fills (`current_period_end`, `meeting_link`, `group_link`). Omits tags production *does* fill. |

`docs/001-gaps/01-dunning-engine.md` still claims `{{plan_name}}` is not substituted and the engine omits product fields. **That is stale.** Cite the live files below.

Tracker stays **P** until lifecycle subjects/bodies resolve and recovery links are real. Dunning hydrate is not the remaining P0.

---

## 3. Ground-truth files

| Role | Absolute path |
|------|----------------|
| Catalog | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs` |
| Dunning hydrate | `.../Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` |
| Lifecycle hydrate | `.../Communications/Infrastructure/EventHandlers/LifecycleEventHandlers.cs` |
| Document hydrate | `.../Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` |
| Digital-delivery hydrate | `.../Communications/Infrastructure/EventHandlers/OrderCompletedDigitalDeliveryHandler.cs` |
| Wiki | `.../Communications/Infrastructure/Services/CommunicationsQueryService.cs` (`GetTemplateVariablesAsync`) |
| Preview mocks | `.../Communications/Infrastructure/Endpoints/TemplateEndpoints.cs` (`PopulateMocks`) |
| Test-reminder mocks | `.../Communications/Application/Commands/MessageTemplateCommandHandlers.cs` (`SendTestReminderCommandHandler`) |
| Engine payload | `.../Commerce/Infrastructure/Workers/DunningEngineJob.Dispatch.cs` |
| Pre-dunning `days_overdue` | `.../Commerce/Infrastructure/Workers/DunningEngineJob.PreDunning.cs` (hardcoded `0`) |
| Default campaign copy | `.../Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` |
| Typed lifecycle events | `.../Commerce/Contracts/Events/SubscriptionSuspendedIntegrationEvent.cs`, `SubscriptionCanceledIntegrationEvent.cs` |
| Update-payment route | `.../Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs` |
| Portal page | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` |
| Magic token | `.../Commerce/Infrastructure/Security/MagicLinkTokenService.cs` |
| Existing test | `.../tests/Lazuar.ModuleTests/Communications/DunningTemplateVariableSubstitutionTests.cs` |
| **Missing test** | No `LifecycleEventHandlers` tests exist |

Communications already references `Modules.Commerce.Contracts` (Application + Infrastructure). A thin mail-context port belongs there — do **not** JOIN `commerce.*` from Communications SQL.

---

## 4. Two CMS, two hydrators (do not merge in this ticket)

```
Dunning step (inline subject/body)
  → DunningEngineJob.DispatchCommunicationStepAsync
  → FulfillmentRequested(COMMUNICATIONS, "reminder.dunning", JSON)
  → FulfillmentRequestedIntegrationEventHandler.PopulateVariables
  → DispatchMessage

Grace SUSPEND / CANCEL (and admin / portal / GDPR cancel)
  → SubscriptionSuspended | SubscriptionCanceled  (ids only)
  → LifecycleEventHandlers  (name lookup: "Payment Failed" | "Subscription Cancelled")
  → DispatchMessage
```

Live dunning copy is **on the step**. Editing Templates does not change day −3 / 0 mail. Editing a step does not change suspend/cancel mail. That dual CMS is COM-019 / later. LP-153 only makes **each** path substitute the tags it already claims.

`reminder.due` + `template_id` is still implemented in the hydrator and **still unpublished** by any job. Leave it. Do not delete it in this ticket (drive-by). Do not write new publishers for it.

---

## 5. What each production `Populate` actually does

### 5.1 Dunning — `FulfillmentRequestedIntegrationEventHandler`

Filter: `InternalTargetApp == "COMMUNICATIONS"` and `EventType` in `reminder.due` | `reminder.dunning`. Anything else no-ops.

**Copy source**

| EventType | Copy |
|-----------|------|
| `reminder.dunning` | Inline `subject` / `email_body` / `whatsapp_body` from the payload (the step). |
| `reminder.due` | `template_id` → `ICommunicationsRepository.GetTemplateByIdAsync`. Dead publisher. |

**Links**

```
portalBase      = App:ClientUrl ?? "https://portal.lazuar.com"
portalLink      = {portalBase}/{slug}/portal                          → {{renewal_link}}
portalMagicLink = {portalBase}/{slug}/portal?token={HMAC, 24h}        → {{portal_magic_link}}
updatePayment   = {portalBase}/{slug}/update-payment/{subscriptionId} → {{update_payment_link}}
```

Token is real (`IMagicLinkTokenService`). Default campaign copy never uses the token. Default campaign **does** use `{{update_payment_link}}`.

**Replacements (case-insensitive)**

`customer_name`, `customer_email`, `customer_phone`, `business_name`, `plan_name`, `amount`, `total_price`, `currency`, `days_overdue`, `renewal_link`, `portal_magic_link`, `update_payment_link`.

**Not replaced here:** `current_period_end`, `document_link`, `fulfillment_url`, `meeting_link`, `group_link`.

**Payload the engine actually sends** (`DunningEngineJob.Dispatch.cs`):

| JSON field | Source | Notes |
|------------|--------|--------|
| `subscription_id`, `client_profile_id`, `product_id` | Subscription | Required for profile + links |
| `action_type`, `subject`, `email_body`, `whatsapp_body` | Step (WA body stripped if demoted to EMAIL) | |
| `plan_name` | `product?.Name ?? ""` | Empty if product row missing |
| `amount` | `product?.Price ?? 0m` | **JSON number**, not string. `ReadNumericString` handles this. **List price**, not invoice / tax / coupon. |
| `currency` | `product?.Currency ?? ""` | |
| `days_overdue` | Past-due: calendar days vs `NextBillingDate`. Pre-dunning: **literal `0`**. | |
| `total_price` | **Absent** | Hydrator falls back to `amount` |
| `current_period_end` | **Absent** | Date tag can never fill on this path |

`amount` via `System.Text.Json` is typically `99.00` or `99` (`GetRawText()`), not a guaranteed `N2` string. Fine for “it resolved”; ugly next to `MYR`. Format in the hydrator (`0.00`, invariant) — do not invent a new invoice amount.

Subject **is** passed through `PopulateVariables`. Markdown → HTML after replace. Good.

### 5.2 Lifecycle — `LifecycleEventHandlers` (the LP-153 hole)

Events carry **only** `OrganizationId`, `SubscriptionId`, `ClientProfileId`, `ProductId`, `FulfillmentTargets`. No plan, amount, date, slug.

Handler injects **only** `CommunicationsDbContext`, `ICrmQueryService`, `IEventBus`. No `IOneQueryService`, no `IConfiguration`, no `IMagicLinkTokenService`, no Commerce read port.

**Payment Failed** (on `SubscriptionSuspended`, not on first `GatewayPaymentFailed`):

```
body  = EmailBody
        .Replace({{customer_name}}, profile.Full_name)
        .Replace({{renewal_link}}, "https://portal.lazuar.com/checkout/update")
subject = template.Subject          // NOT populated
whatsapp = null                     // catalog WA body discarded
```

Catalog subject: `Action Needed: Payment issue for {{plan_name}}` → buyer sees the braces.  
Catalog body also has `{{plan_name}}` and `{{business_name}}` → leftover literals.  
CTA is `{{renewal_link}}`, not `{{update_payment_link}}`.

**Subscription Cancelled:**

```
body  = EmailBody.Replace({{customer_name}}, …)
subject = template.Subject          // {{plan_name}} leftover
```

`{{plan_name}}` and `{{business_name}}` remain.

Publishers of these events (all id-only today): dunning grace CANCEL/SUSPEND, admin cancel, portal cancel, GDPR anonymize. All four must keep compiling if the event record grows; prefer a **query port** over widening four publishers unless denormalizing is cheaper (see §8).

### 5.3 Catalog vs consumers (`DefaultMessageTemplates`)

| Name | Tags in copy | Required | Consumer | Resolves today? |
|------|----------------|----------|----------|-----------------|
| Payment Failed | `customer_name`, `plan_name`, `renewal_link`, `business_name` | `{{renewal_link}}` | Lifecycle **suspend** | **No** (dead URL + unfilled names/subject) |
| Subscription Cancelled | `customer_name`, `plan_name`, `business_name` | (none) | Lifecycle cancel | **No** (subject + plan + business) |
| Digital Product Delivery | `customer_name`, `plan_name`, `fulfillment_url`, `portal_magic_link`, `business_name` | `{{fulfillment_url}}` | `OrderCompleted` | Partial — `plan_name` hardcoded `"your purchase"`; portal URL, no token. **Out of LP-153** (not dunning/lifecycle). |
| Quotation Ready / Official Receipt | `customer_name`, `business_name`, `document_link` | `{{document_link}}` | `DocumentPublished` | **Yes** for those three. No amounts/dates in copy. **Out of LP-153.** |

Orphan names (Community / cart / old “Subscription Renewal *”) stay orphaned. Do not re-seed. Do not add hydrate for them.

### 5.4 Preview + test reminder (lie to the merchant)

Both `PopulateMocks` lists:

`customer_name`, `business_name`, `plan_name`, `group_link`, `meeting_link`, `total_price`, `renewal_link`, `portal_magic_link`, `fulfillment_url`, `current_period_end`.

Missing vs production dunning: `update_payment_link`, `amount`, `currency`, `days_overdue`, `customer_email`, `customer_phone`.  
Present vs production: `current_period_end` (mock `"31 Dec 2026"` only), `meeting_link` / `group_link` (Community leftovers, never filled in a handler).

Preview `renewal_link` = `https://portal.lazuar.com/checkout`. Test reminder = `https://example.com/renew`. Neither is a portal route.

Ops dunning editor placeholder only lists `{{customer_name}}`, `{{plan_name}}`, `{{update_payment_link}}`. Preview for steps hits the same Communications preview endpoint — so a merchant who types `{{update_payment_link}}` **sees the tag unreplaced** in the preview pane.

### 5.5 Wiki vs reality

| Tag | Wiki | Dunning | Lifecycle | Notes |
|-----|------|---------|-----------|--------|
| `customer_name` | Yes | Yes | Body only | Subject unused in catalog |
| `customer_email` / `customer_phone` | Yes | Yes | No | Not in catalog copy |
| `business_name` | **No** | Yes | **No** | In every catalog footer |
| `plan_name` | Yes | Yes | **No** | In both lifecycle subjects |
| `amount` / `currency` / `days_overdue` | **No** | Yes | No | Dunning-only |
| `total_price` | Yes | Fallback to amount | No | Engine does not send the key |
| `current_period_end` | Yes | **No** | **No** | Preview-only lie |
| `renewal_link` | “secure checkout billing link” | Bare portal, **no** token | **Dead host path** | Catalog CTA |
| `update_payment_link` | **No** (hint only in dunning UI) | Yes | No | Default campaign CTA |
| `portal_magic_link` | “24h auto-login” | Yes (token) | No | Honest only on dunning |
| `document_link` | **No** | No | No | Document handler only |
| `fulfillment_url` | “R2 download” | No | No | Portal URL, not R2 |
| `meeting_link` / `group_link` | Yes | **Never** | **Never** | Delete from wiki or mark unused |

---

## 6. Dead and misleading URLs

Portal app routes under `[tenantSlug]`: `checkout/[productSlug]`, `pay/[sessionId]`, `portal`, `update-payment/[subId]`. **There is no `/checkout/update`.**

| URL | Built by | What happens |
|-----|----------|----------------|
| `https://portal.lazuar.com/checkout/update` | Lifecycle Payment Failed, hardcoded | **404.** Ignores `App:ClientUrl`, tenant slug, subscription id. |
| `{ClientUrl}/{slug}/update-payment/{subId}` | Dunning hydrator | **Live page.** GET arrears always. POST checkout only if status is `PAST_DUE` or `SUSPENDED`. |
| `{ClientUrl}/{slug}/portal` | Dunning `{{renewal_link}}` | Live portal **without** token (buyer must magic-link / log in). Not a pay CTA. |
| `{ClientUrl}/{slug}/portal?token=` | Dunning `{{portal_magic_link}}` | Live 24h HMAC. Unused by default copy. |
| `https://portal.lazuar.com/checkout` | Preview mock | Not a real product checkout URL. |
| `https://example.com/renew` | Test reminder | Dummy. |

**Pre-dunning (−3) vs ACTIVE:** the default −3 body says “ensure your payment method is up to date here: `{{update_payment_link}}`”. The URL **resolves**. The page shows **“Account in Good Standing”** and the POST returns *“does not require a payment update.”* That is LP-173 (portal update-PM for ACTIVE), not a broken substitute. Do not change arrears status gates in LP-153. Optional copy tweak (out of scope unless one line): −3 could point at `{{portal_magic_link}}` instead. Not required for this ticket.

Fallback `App:ClientUrl` is inconsistent (`https://portal.lazuar.com` in Communications handlers vs `http://localhost:3004` in One / arrears). Out of scope unless a handler is already open; if you touch link building, use the same fallback as `OneLinkService` / arrears (`http://localhost:3004`) **or** keep current and do not churn local URLs. Prefer: read `App:ClientUrl` only, no new host constants.

---

## 7. Names, amounts, dates — remaining gaps

### Names

| Path | `customer_name` | `plan_name` | `business_name` |
|------|-----------------|-------------|-----------------|
| Dunning | CRM `Full_name` (can be empty → empty string) | Product name in payload | Workspace name, fallback `"Lazuar Merchant"` |
| Lifecycle | CRM only | **Leftover** | **Leftover** |
| Digital delivery | Fallback `"Customer"` | **Hardcoded `"your purchase"`** | Workspace / `"Business"` |

Align empty-name fallback to `"Customer"` when touching the shared hydrator. Do not invent a display-name service.

Lifecycle cannot see `plan_name` / amounts / dates from the event. Communications must **not** query `commerce.Products` directly. Need a Contracts port (see §8).

### Amounts

Engine amount = **product list price**. No proration, tax, coupon, or failed-invoice balance. Honest enough for Wave 0 (no invoice engine). Do not add a second “invoice due” field in this ticket.

Format once in the hydrator: invariant `0.00`. Same string for `{{amount}}` and `{{total_price}}` until a real invoice total exists.

### Dates

`{{current_period_end}}` is documented and preview-mocked and **never** written by a production handler. Source of truth elsewhere is `Subscription.NextBillingDate` (webhook payload even comments this).

Pre-dunning `days_overdue: 0` means a merchant who types `{{days_overdue}}` on the −3 mail gets `"0"`. Acceptable if we also fill `{{current_period_end}}`. Do not invent `{{days_until_due}}` in this ticket.

Format: `en-GB` / `d MMM yyyy` in UTC (matches preview `"31 Dec 2026"`). Do not introduce customer timezones (Stripe does; we do not).

---

## 8. Minimal change set

Goal: one replace table, two honest callers (dunning + lifecycle), wiki/preview/test aligned, tests that fail if a catalog tag ships raw.

### 8.1 Shared hydrator (do this first)

Add something like `Modules.Communications.Application.MessageTemplateHydrator` (static, no DI):

```csharp
public sealed record MessageTemplateContext(
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string BusinessName,
    string PlanName,
    string Amount,
    string TotalPrice,
    string Currency,
    string DaysOverdue,
    string CurrentPeriodEnd,
    string RenewalLink,
    string PortalMagicLink,
    string UpdatePaymentLink);

public static string Populate(string? text, in MessageTemplateContext ctx);
```

Single `Replace(..., OrdinalIgnoreCase)` chain. Empty input → empty. Missing values → `""` (never leave `{{tag}}`).

Also a small `MessageLinkBuilder` (or private static on the hydrator) that takes `clientUrl`, `slug`, `subscriptionId`, optional magic token and returns the three URLs. Kill the hardcoded `https://portal.lazuar.com/checkout/update`.

**Do not** put `meeting_link` / `group_link` on the context. **Do** keep `renewal_link` as an **alias of `update_payment_link`** (G49). Both strings the same recovery URL. Bare portal is available as `portal_magic_link` without a token if we ever need it — default should not use it as the pay CTA.

Callers after the extract:

| Caller | Use hydrator? |
|--------|----------------|
| `FulfillmentRequestedIntegrationEventHandler` | **Yes** — build context from payload + CRM + workspace + token |
| `LifecycleEventHandlers` | **Yes** — same context |
| Preview + `SendTestReminderCommandHandler` | **Yes** — one `MessageTemplateContext` of mock constants (including `update_payment_link`, `amount`, `currency`, `days_overdue`) |
| Document + digital-delivery | **No** in this ticket (different tags; LP-151 / COM-008) |

### 8.2 Commerce mail-context port (lifecycle names / amounts / dates)

Do **not** widen `SubscriptionSuspendedIntegrationEvent` / `SubscriptionCanceledIntegrationEvent` unless a port is refused. Widening touches dunning grace, admin cancel, portal cancel, GDPR, plus `SubscriptionLifecycleIntegrationEventHandlers` (outbound webhooks already load product themselves).

Extend the existing cross-module port:

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Contracts/ISubscriberQueryService.cs`

```csharp
Task<SubscriptionMailContext?> GetSubscriptionMailContextAsync(
    Guid organizationId, Guid subscriptionId);

public sealed record SubscriptionMailContext(
    Guid SubscriptionId,
    Guid ProductId,
    string PlanName,
    decimal Price,
    string Currency,
    DateTime? NextBillingDate,
    string Status);
```

Implement with **commerce-schema-only** SQL in `SubscriberQueryService` (same factory as today). No CRM/One JOIN.

`LifecycleEventHandlers` then: CRM profile + One workspace + this port + `IMagicLinkTokenService` + `App:ClientUrl`. Populate **subject, email body, and WhatsApp body** (WA still will not send while the flag is off; do not pass `null` and leave catalog WA tags raw if channel is `ALL`).

If the mail context is null (subscription already gone): still send if we have an email, with empty plan/amount/date and a best-effort update-payment URL from `event.SubscriptionId` + slug. Do not drop the cancel mail.

### 8.3 Dunning payload: add the date (one field)

In `DispatchCommunicationStepAsync` add:

- `current_period_end` = `sub.NextBillingDate` (ISO `o` or `yyyy-MM-dd`). Hydrator formats for display.
- `total_price` = same decimal as `amount` (stop the silent fallback).

Pre-dunning keeps `days_overdue: 0`. That is honest.

No other engine behavior change. No campaign snapshot. No default-copy rewrite required for the ticket (default already uses tags the hydrator fills). Optional later: put `{{currency}} {{amount}}` and `{{current_period_end}}` in default day-0 copy so merchants *see* amounts/dates without editing. **Do not** mutate existing campaign rows (LP-079 spirit).

### 8.4 Catalog copy (Payment Failed only)

In `DefaultMessageTemplates` “Payment Failed”:

- CTA markdown: `[Securely Update Payment]({{update_payment_link}})` (and the same tag in the WA line).
- Required: `{{update_payment_link}}`. Keep `{{renewal_link}}` in **optional** so old tenant edits still validate if they still contain it.
- Optional list stays `customer_name`, `business_name`, `plan_name` (add `renewal_link` as optional alias).

Reset-to-default will pick this up. Existing customized “Payment Failed” rows keep `{{renewal_link}}` — the hydrator alias still fills them with the **real** update-payment URL.

Do not change “Subscription Cancelled” copy except that hydrate will now fill its existing tags. No new required vars (no recovery CTA on a cancel mail). Still fill `update_payment_link` if a merchant adds it.

Do not touch orphan names.

### 8.5 Wiki + preview + ops hint

`GetTemplateVariablesAsync`:

- Add under Billing: `{{business_name}}`, `{{amount}}`, `{{currency}}`, `{{days_overdue}}`, `{{update_payment_link}}`.
- Rewrite `{{renewal_link}}` description to “Same as update-payment link (recovery checkout).”
- Rewrite `{{portal_magic_link}}` to stay accurate (24h token — **dunning + lifecycle after this ticket**).
- Rewrite `{{current_period_end}}` to “Next billing / paid-through date (`NextBillingDate`).”
- Remove `{{meeting_link}}` and `{{group_link}}` from the wiki (Community leftovers). Do not keep advertising dead tags.
- Leave `{{fulfillment_url}}` (digital delivery; out of ticket) but do not claim R2 if you touch the string — “Buyer portal / download URL” is enough. Prefer **not** editing that sentence if it grows scope.

Preview + test-reminder mocks: same tag set as `MessageTemplateContext`. `update_payment_link` mock = `https://portal.lazuar.com/{slug}/update-payment/{guid}` style, not `/checkout`.

`DunningStepEditor` placeholder: add `{{amount}}`, `{{currency}}`, `{{current_period_end}}`, `{{days_overdue}}` to the hint. One string. No new UI.

Create-template modal still hardcodes `required_variables: [{{customer_name}}]` and optional `plan_name` / `renewal_link`. Out of scope unless a one-line optional list add is free (`update_payment_link`). Do not redesign Templates UI.

### 8.6 Explicitly do **not** change

| Temptation | Why not |
|------------|---------|
| Immediate “card declined” mail | LP-151 / COM-002 |
| Welcome / activation mail | COM-007 |
| Digital-delivery `plan_name` + magic token | COM-008 / G48; not dunning/lifecycle |
| Receipt amount in Official Receipt subject | LP-151; document path already substitutes its three tags |
| Dual CMS (steps vs templates) | COM-019 |
| `reminder.due` publisher or deletion | Dead; not blocking LP-153 |
| ACTIVE update-payment / change-card | LP-173 |
| Invoice-true amount | No invoice balance object |
| WhatsApp send | 00.4 |
| Broadcast variable fill | ADR 021 vitamin |
| Unique template names / update-time tag validation | G18 hygiene, not Wave 0 |
| `App:ClientUrl` global cleanup | Only if a file is already open |

---

## 9. Tests (this is the lock)

Put tests next to the existing Communications suite. NSubstitute + in-memory Communications DB, same style as `DunningTemplateVariableSubstitutionTests` / `DocumentPublishedIntegrationEventHandlerTests`.

### 9.1 Expand `DunningTemplateVariableSubstitutionTests`

Current test fills `update_payment_link` in the body but **never asserts the URL**. Fix that.

Add / tighten:

1. Existing happy path also asserts:
   - `HtmlEmailBody` contains `https://portal.test/{slug}/update-payment/{subscriptionId}`
   - does **not** contain `{{update_payment_link}}`
   - `renewal_link` (if present in fixture body) equals that same update-payment URL, **not** `/portal` without token
   - `business_name` = workspace name
   - no leftover `{{`
2. `amount` as JSON **number** `99.00` (not only string) still formats into the body.
3. `current_period_end` in payload (ISO) appears as a human date; tag gone.
4. Missing optional keys → empty, not `{{amount}}`.
5. Keep the existing token / `plan_name` assertions.

The second test (`DefaultDunningCopy_WithPlanNamePayload_LeavesNoRawPlaceholder`) is a string-replace toy. Either delete it or replace with “default campaign strings + hydrator + sample context leave no `{{`”.

### 9.2 New `LifecycleEventHandlersTests`

Fixture: seed catalog “Payment Failed” / “Subscription Cancelled” via `DefaultMessageTemplates.CreateEntity`. Stub CRM, One, mail-context, tokens, `App:ClientUrl`.

| Test | Expect |
|------|--------|
| `Suspend_PopulatesSubjectPlanNameAndRealUpdatePaymentUrl` | Subject has plan name, no `{{`. Body has customer, business, plan. HTML contains `{ClientUrl}/{slug}/update-payment/{subId}`. Does **not** contain `portal.lazuar.com/checkout/update`. Token generated. Channel preserved. |
| `Cancel_PopulatesSubjectAndNames` | Subject + body: plan + business + customer; no `{{`. |
| `Suspend_MissingProfile_DoesNotDispatch` | No `PublishAsync`. |
| `Suspend_MissingMailContext_StillDispatchesWithLinks` | Update-payment URL still uses event `SubscriptionId` + slug; plan/amount empty, no leftover tags if catalog uses them — if catalog requires plan in subject, subject may be `"Action Needed: Payment issue for "` (empty plan). Assert no `{{`. |
| `Suspend_WhatsAppBodyPopulatedWhenChannelAll` | `PlainTextPhoneBody` is not null and has names/link (Messaging will still skip WA). |

No Resend / no host. Handler unit tests only.

### 9.3 `DefaultMessageTemplatesTests`

- Payment Failed required vars contain `{{update_payment_link}}`.
- Payment Failed email/WA bodies contain `{{update_payment_link}}` and do **not** use `https://portal.lazuar.com`.
- Existing catalog-name / orphan assertions stay.

### 9.4 `MessageTemplateHydratorTests` (new, cheap)

- Case-insensitive tags.
- Null/empty input.
- Every context field round-trips; unused tags in the string stay only if they are **unknown** (`{{garbage}}`). Document that unknown tags are **not** stripped (current `Replace` behavior). Do not add a “must not contain `{{`” sweeper in production — it would hide merchant typos. Tests for **catalog + default campaign** strings should assert no leftover **known** tags after populate.

### 9.5 Commerce port test

One Dapper/in-memory or existing Commerce test style: `GetSubscriptionMailContextAsync` returns name/price/currency/`NextBillingDate` for a seeded sub; null when org mismatch.

If adding `current_period_end` to the dunning payload, extend an existing `DunningEngineJob` dispatch test **if one already asserts the JSON**. Do not start a new engine suite for one field.

### 9.6 Do not add

- E2E Resend.
- Portal click-through (LP-173).
- Preview HTTP test unless hydrator tests already cover the mock context.

---

## 10. Suggested implement order

1. `MessageTemplateHydrator` + unit tests.  
2. Point dunning handler at it; set `renewal_link == update_payment_link`; format amount; format `current_period_end` if present. Expand dunning tests (red on missing URL assert → green).  
3. `ISubscriberQueryService.GetSubscriptionMailContextAsync` + impl + one test.  
4. Rewrite `LifecycleEventHandlers` to the hydrator + links + subject. New tests.  
5. Catalog Payment Failed CTA tag. `DefaultMessageTemplatesTests`.  
6. Engine payload `current_period_end` + `total_price`.  
7. Wiki + preview + test-reminder + dunning placeholder string.

Keep the PR inside Communications + the one Commerce port + Dispatch payload. No ops redesign. No TypeSpec change required (wiki DTO already has free-form tag/description).

---

## 11. Definition of done (LP-153 only)

- [ ] Default dunning EMAIL copy, after hydrate, contains no `{{` and contains a `{ClientUrl}/{slug}/update-payment/{guid}` link.
- [ ] Catalog “Payment Failed” (suspend) subject and body contain no `{{`; recovery URL is that same pattern; `https://portal.lazuar.com/checkout/update` is gone from the tree (except maybe a test that asserts absence).
- [ ] Catalog “Subscription Cancelled” subject and body contain no `{{`; `plan_name` and `business_name` are real when mail-context exists.
- [ ] `{{amount}}` / `{{currency}}` / `{{current_period_end}}` resolve on dunning when the merchant (or a test body) uses them; number payload works; date is human-readable.
- [ ] Wiki lists the dunning tags; does not list `meeting_link` / `group_link` as live.
- [ ] Preview/test-reminder mocks include `{{update_payment_link}}`.
- [ ] Tests in §9 exist and pass. No new product features.

After that, flip tracker LP-153 from **P** → **Y** only if a local send (tenant BYOK) of default day-0 + a suspend mail was glanced in `MessageDeliveryLog` / inbox. The unit tests are the merge gate; the glance is the honesty gate.

---

## 12. One-line implement brief

Extract one `MessageTemplateHydrator`, alias `renewal_link` to the real update-payment URL, add a Commerce mail-context read for lifecycle, stop shipping `{{plan_name}}` in suspend/cancel subjects, fill `current_period_end` from `NextBillingDate`, and lock it with lifecycle + dunning tests. Do not merge the two CMS, do not touch WhatsApp, do not open ACTIVE update-payment.
