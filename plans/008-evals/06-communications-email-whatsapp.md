# 06 — Communications + Messaging after Waves 0–4

**Program:** 008-evals  
**Slice:** Email, templates, suppressions, invoice reminders, WhatsApp stub  
**Date:** 2026-08-16  
**Branch / tree:** live workspace after Waves 0–4 (`feat/007-waves-1-4-implement` per [008-evals README](./README.md))  
**Does not implement.** Does not condense. Does not treat `plans/007-feats` tracker cells as truth unless this file re-checks the code.

Parent index: [README.md](./README.md). Historical (pre-wave) comms write-up: `plans/007-feats/16-communications-whatsapp-email.md`. Gap doc this file supersedes on facts: `docs/001-gaps/08-communications-module.md`.

---

## Standing locks this file must not contradict

| Lock | Source | Consequence |
|------|--------|-------------|
| Lazuar is Compliance CaaS, not a marketing / inbox product | ADR 021 | Broadcasts stay API-only vitamins. Do not become Customer.io / WATI. |
| Keep the *job* of WhatsApp utility dunning | ADR 021 “Keep”; ADR 020 §4; ADR 023 mitigation | The job is still the Asian differentiator. The *channel* is not live. |
| No WhatsApp / multi-channel for ~6 months from 2026-08-09 | `plans/004-maintenance/decisions.md` §00.4 | Freeze through ~2027-02-09. Console is not a production channel. Reopen 00.4 before Meta / Twilio / WATI. |
| Credits stay in Billing | decisions.md §00.5 | `CreditAction.WhatsAppSend` / `EmailSend` / `BroadcastEmailPerRecipient` are Billing concerns. |
| Wave 4 chose **delete claims**, not Meta | `plans/007-feats/impl/W4-LP-155-done.md`, `W4-LP-074-done.md` | No Meta Cloud. Tracker `LP-155` / `LP-074` stay **N**. Do not flip `Messaging:WhatsAppEnabled`. |
| Render at the source, dispatch at the edge | `Modules/Messaging/README.md` §8 | Messaging must not learn template IDs. That split is still correct. |

This document is **not** permission to implement Meta Cloud, to turn broadcasts into a marketing product, or to claim “native WhatsApp dunning” on a sales call.

---

## Method

Question this file answers: after Waves 0–4, what does the live tree actually send, on which channel, with which honesty gaps — especially Resend BYOK, checkout gating, template variable fill, bounce/complaint suppression, invoice reminders, leftover WhatsApp claims, and whether emailed amounts match seats / subscription snapshot.

How the work was done:

1. Re-read every live file under `apps/lazuar-api/Modules/Communications/` and `Modules/Messaging/`.
2. Followed producers: Commerce dunning + invoice reminder job, Billing `DocumentPublished`, One auth mail, CRM anonymize.
3. Followed consumers: ops Email Provider / Templates / Dunning editor / dashboard checklist; portal privacy + checkout phone label.
4. Cross-checked `appsettings.json`, TypeSpec `packages/api-spec/modules/communications/`, tests under `Lazuar.ModuleTests/{Communications,Messaging}/`, and Wave 0–4 done notes that actually touched this slice (`W0-LP-073`, `W0-LP-151`, `W0-LP-153`, `W1-LP-053`, `W3-LP-105`, `W4-LP-074`, `W4-LP-155`).
5. Honesty marks come from **code paths that execute**, not from seed copy, UI tabs, or ADR wish lists.

Honesty rubric:

| Mark | Meaning |
|------|---------|
| **LIVE** | A real provider call happens on a tenant path when config is present (Resend HTTP). |
| **PARTIAL** | Plumbing exists; trigger, variables, amount, or UI is incomplete. |
| **STUB** | Interface + console / flag skip. No Graph API, no WABA, no SMS. |
| **SEEDED-ORPHAN** | Template or DTO exists; nothing dispatches it (or dispatch is webhook-only). |
| **REFUSE** | Competitor surface that would violate ADR 021 / 00.4. |
| **FROZEN** | Explicit product freeze; do not “just add an adapter.” |

---

## Architecture the tree still implements

```
Domain event / admin API
  → Communications (policy, templates, suppressions, BYOK store, variable fill)
  → communications.OutboxMessages → in-process bus
  → Messaging inbox
  → DispatchMessageIntegrationEventHandler
       → IEmailService = ResendEmailService          (LIVE if tenant BYOK)
       → IMessagingService = ConsoleMessagingService (STUB; also gated off)
```

Communications owns content and policy:

- Aggregates: `MessageTemplate`, `SuppressionEntry`, `Broadcast`, `TenantEmailConfiguration` (`Modules/Communications/Domain/Aggregates/`).
- Shared hydrator: `Modules/Communications/Application/MessageTemplateHydrator.cs`.
- Admin HTTP under `/admin/communications` (`Endpoints.cs` 16–58).
- Public unsubscribe + Resend webhook (`PublicComplianceEndpoints.cs`).
- Workers: inbox, outbox, `BroadcastFanoutJob`.

Messaging owns the dumb pipe (R34 — ports live in the module, not BuildingBlocks):

- `DispatchMessageIntegrationEvent` (`Modules/Messaging/Contracts/DispatchMessageIntegrationEvent.cs` 10–20).
- `ResendEmailService`, `ConsoleEmailService` (implemented, **not registered**), `EmailTemplateBuilder`.
- `ConsoleMessagingService` (`IMessagingService`).
- `TenantReplica`, `MessageDeliveryLog`.
- `GET /messaging/delivery-logs`, `POST /messaging/notify` (`Modules/Messaging/Infrastructure/Endpoints.cs` 19–55).

Decision 00.4: do not merge these modules until a real multi-channel provider is funded (`plans/004-maintenance/decisions.md` 68–74).

There is **no** `Communications/README.md`. Messaging README is the only module-level product freeze text.

---

## What Waves 0–4 actually changed in this slice

The August 16 `007-feats/16` audit is **historical**. After the waves, these claims in that file are stale:

| 007-feats/16 said | Live tree after Waves 0–4 |
|-------------------|---------------------------|
| Immediate fail mail does not exist | **Exists** — `GatewayPaymentFailedIntegrationEventHandler` (W0-LP-151) |
| Payment Failed fires on suspend + dead URL | **Unhooked.** `LifecycleEventHandlers` is cancel-only (`LifecycleEventHandlersTests.cs` 27–31). Catalog CTA is `{{update_payment_link}}` (W0-LP-153) |
| No `Invoice Reminder` catalog | **Seeded** + hourly job (W3-LP-105) |
| No Portal Access / welcome | **Portal Access** on first activation + magic-link request (W0-LP-151) |
| RFC 8058 POST missing | **GET + POST** on the same unsubscribe URL |
| One suppression list blocks receipts | **Lanes:** `UNSUBSCRIBE` marketing-only; bounce/complaint/anonymize block both |
| `WhatsAppSend` config **2**; deduct on console success | Config **0**; `ConsoleMessagingService.IsBillable == false`; deduct forced to 0 |
| Default campaign +3 is WhatsApp-only | New-org default +3 is **EMAIL** (W0-LP-073). Existing tenants may still have a dead WA step |
| README hero “automated WhatsApp dunning” | Root README watermark + Phase 1 now say **not shipping** (W4-LP-155) |
| `{{plan_name}}` / `{{current_period_end}}` never filled on dunning | Hydrator fills them when the producer sends them |
| `BuildUnsubscribeUrl` unused | Used on broadcast fan-out |
| Digital delivery + receipt consumers missing | Both exist (weak) |

Wave tickets that touched this slice (done notes, not tracker cells):

- **W0-LP-073** — dunning hop 2 commits outbox; hydrate throws; new-org +3 EMAIL.
- **W0-LP-151** — receipt / immediate fail / Portal Access; suspend no longer mails Payment Failed.
- **W0-LP-153** — shared hydrator; dead `portal.lazuar.com/checkout/update` gone from production handlers.
- **W1-LP-053** — `{{renewal_link}}` / `{{checkout_url}}` prefer minted hosted bill; new-org copy is pay-this-cycle.
- **W3-LP-105** — quote `DueAt` + `InvoiceReminderJob` + catalog **Invoice Reminder**.
- **W4-LP-074 / LP-155** — delete public WhatsApp claims; do not build Meta.

---

## 1. Resend BYOK and checkout gate

### 1.1 What is stored

`TenantEmailConfiguration` (`Modules/Communications/Domain/Aggregates/TenantEmailConfiguration.cs` 6–49):

- One row per org (unique index `OrganizationId`, `CommunicationsDbContext.cs` 71–76).
- `ApiKey` is **AES-encrypted** via `ISecretVault` (`Kms:MasterKey`). Comment on the field: “Never return raw to clients” (line 11).
- `SenderEmail` lowercased. `IsActive`. No reply-to, no display name, no persisted domain-verification record, no multi-sender.

Save path (`SaveEmailConfigCommand.cs` 21–105):

1. System tenant (`Guid.Empty` / `…0001`) **cannot** save BYOK — “System tenant uses platform-level email configuration” (39–45).
2. Empty PUT key + no existing row → business rule (50–53). Empty PUT key + existing row → `UpdateWithoutKey` (keep ciphertext) (93–96).
3. Decrypt-or-passthrough for legacy plaintext rows (56–66, and again on read in `CommunicationsQueryService.cs` 189–198).
4. Live check: `GET https://api.resend.com/domains` with the tenant bearer (`SaveEmailConfigCommand.cs` 73–81). Failure → “Invalid Resend API Key or Domain not verified on Resend.”
5. That check does **not** assert `SenderEmail`’s host is one of the listed domains. A key that can list domains plus `receipts@gmail.com` still saves. Ops copy claims you cannot use Gmail (`EmailSettingsPage.tsx` 92–94); the server does not enforce it.

GET (`CommunicationsQueryService.GetEmailConfigAsync` 135–173; `Endpoints.cs` 24–36):

- Returns `has_api_key`, last-4 hint, `sender_email`, `is_active`.
- Unset config is **200 empty DTO**, not 404, so ops dashboard probes stay quiet (`Endpoints.cs` 23–35).
- Never returns the raw key. Ops password field stays blank (`EmailSettingsPage.tsx` 28–29, 126–137).

### 1.2 How dispatch uses the key

`DispatchMessageIntegrationEventHandler.cs` 112–146:

- Non-system tenant: load `GetEmailConfigCredentialsAsync`. Only if `IsActive` **and** key **and** sender are non-empty does it pass tenant credentials into Resend.
- Inactive or missing BYOK → `ResendEmailService` is called with null tenant key → **throws** “No platform fallback allowed for tenant emails…” (`ResendEmailService.cs` 66–69). Handler logs `FAILED` and rethrows (141–145). Tests lock this (`DispatchMessageIntegrationEventHandlerTests.cs` 143–214).
- System tenant (One password reset / verify): may use platform `Resend:ApiKey` / `SenderEmail`. Empty platform key → **console log, return null** (`ResendEmailService.cs` 56–64) — that is the only “fallback,” and it is not a send.
- Workspace invite is **org-scoped** (`NotificationDispatchDomainEventHandlers.cs` 65–80). New staff invite **fails without tenant BYOK**.

Resend HTTP (`ResendEmailService.cs` 71–125):

- `POST https://api.resend.com/emails` via named client `Resend` (base `https://api.resend.com/`, `DependencyInjection.cs` 40–49).
- Payload: `from`, `to[]`, `subject`, `html`, `tags: [{name:"org", value: orgId}]` (`OrgTagName = "org"`, line 19 — do not rename; bounce attribution depends on it).
- Optional `List-Unsubscribe` + `List-Unsubscribe-Post: List-Unsubscribe=One-Click` **only** when `UnsubscribeUrl` is set (76–85). Transactional/dunning omit it. Correct.
- No attachments, no `reply_to`, no text part, no Idempotency-Key, no batch, no scheduled send.
- Returns Resend `id` into `MessageDeliveryLog.ProviderMessageId`.

`EmailTemplateBuilder.WrapWithBrandHtml` (`EmailTemplateBuilder.cs` 9–54) still does `Replace("\n","<br/>")` on already-parsed HTML (16) then wraps “Powered by Lazuar”. Tests assert the `<br/>` (`DispatchMessageIntegrationEventHandlerTests.cs` 120–126). Noisy HTML is unchanged.

`ConsoleEmailService` exists (`ConsoleEmailService.cs` 1–31) and is **not registered**. Production `IEmailService` is `ResendEmailService` only (`AddMessagingModule` line 50).

### 1.3 Checkout is gated on email config

`HasValidEmailConfigAsync` (`CommunicationsQueryService.cs` 117–133):

```sql
SELECT 1 FROM communications."TenantEmailConfigurations"
WHERE "OrganizationId" = @TenantId
  AND "IsActive" = true
  AND "ApiKey" IS NOT NULL AND "ApiKey" != ''
  AND "SenderEmail" IS NOT NULL AND "SenderEmail" != ''
LIMIT 1
```

It does **not** decrypt. It does **not** call Resend. A row with garbage ciphertext still counts as “valid.”

Callers:

| Caller | Effect | File:line |
|--------|--------|-----------|
| `InitiateCheckoutCommandHandler` | Throws `InvalidOperationException`: “This workspace has not configured an active email provider. Checkout is temporarily disabled.” | `InitiateCheckoutCommandHandler.cs` 54–58 |
| `CreateProductCommandHandler` | Product is still created, then **`Archive()`** if no config | `CreateProductCommandHandler.cs` 43–48 |
| `UpdateProductCommandHandler` | Activating (`IsActive`) without config → business rule: “You must configure a valid Resend API key before activating checkout links.” | `UpdateProductCommandHandler.cs` 34–40 |

`CreateCustomCheckoutCommand` (quotes) does **not** call `HasValidEmailConfigAsync`. A merchant can mint an OPEN custom quote without Resend. `InvoiceReminderJob` will still publish `invoice.reminder`; Messaging will then throw no-fallback if BYOK is missing. The gate is hop-1 checkout + product activate, not quote create.

Ops surfaces that match the gate:

- Dashboard checklist: “Email (Resend) — required for paid checkout” (`DashboardPage.tsx` 86–99).
- Products page amber banner: “You must connect a Resend API key to activate checkout links and send automated receipts” (`ProductsPage.tsx` 125–126).
- Product form / detail toast the same sentence (`ProductForm.tsx` 205; `ProductDetailPanel.tsx` 275).
- Email Provider page blurb: “receipts, dunning emails, and broadcasts” (`EmailSettingsPage.tsx` 77–79). Receipts + dunning email **if BYOK**; broadcasts are API-only.

**Verdict: LIVE and stricter than Stripe.** Tenant mail has no platform-from. First sale is blocked until DNS + Resend key exist. Invite mail is also blocked. That is honest deliverability policy and a brutal onboarding order.

---

## 2. Catalog, hydrator, and who actually fills what

### 2.1 Seeded catalog (seven names)

`DefaultMessageTemplates.All` (`DefaultMessageTemplates.cs` 23–87) is the only seed. Entitlement grant for `COMMUNITY` | `COMMERCE` | `VAULT` inserts **missing** names only (`AppEntitlementGrantedIntegrationEventHandler.cs` 27–48). Existing orgs pick up `Portal Access` and `Invoice Reminder` on the next grant; there is no wipe.

| Name | Channel | Required | Consumer after Waves 0–4 |
|------|---------|----------|--------------------------|
| Payment Failed | ALL | `{{update_payment_link}}` | **Yes — immediate decline** (`GatewayPaymentFailedIntegrationEventHandler`) |
| Subscription Cancelled | ALL | (none) | **Yes** — cancel only (`LifecycleEventHandlers`) |
| Digital Product Delivery | ALL | `{{fulfillment_url}}` | **Yes** — `OrderCompletedDigitalDeliveryHandler` (portal URL, not R2) |
| Quotation Ready | ALL | `{{document_link}}` | **Yes** — `DocumentPublished` when `DocumentType == "Draft Quotation"` |
| Official Receipt | ALL | `{{document_link}}` | **Yes** — `DocumentPublished` when `DocumentType == "Official Receipt"` |
| Portal Access | EMAIL | `{{portal_magic_link}}` | **Yes** — first `SubscriptionActivated` + `PortalMagicLinkRequested` |
| Invoice Reminder | EMAIL | `{{checkout_url}}` | **Yes** — `InvoiceReminderJob` → `invoice.reminder` |

Orphan names (legacy-cleanup may delete; **not** re-seeded) (`DefaultMessageTemplates.cs` 94–105):

`Community Welcome`, `Community Payment Success`, `Event Ticket Confirmation`, `Abandoned Cart (12h)`, `Abandoned Cart (24h)`, `Generic Receipt`, `Subscription Renewal (3 Days)`, `Subscription Renewal Due Today`, `Subscription Renewal Overdue`.

`reminder.due` + `template_id` is still implemented in the hydrator (`FulfillmentRequestedIntegrationEventHandler.cs` 51, 161–172) and **still unpublished** by any job.

Reset-to-default is real (`ResetMessageTemplateCommandHandler.cs` 111–142; `MessageTemplate.RestoreFromDefault` 64–82). Custom names cannot reset.

Create validates `{{tags}}` against required∪optional (`MessageTemplateCommandHandlers.cs` 27, 47–87). **Update does not** (`UpdateMessageTemplateCommandHandler.cs` 99–108). Paste `{{garbage}}`; it ships. Hydrator leaves unknown tags as-is (`MessageTemplateHydratorTests.cs` 59–63).

Ops create modal hardcodes channel `ALL` and a tiny variable set (`TemplatesPage.tsx` 44–55): required `{{customer_name}}`, optional `plan_name` / `renewal_link` / `update_payment_link`. A merchant cannot create an Invoice Reminder–shaped template from the modal without fighting validation (`{{checkout_url}}` is not in that list).

There is still no unique `(OrganizationId, Name)`, no locale, no versioning, no Meta `template_name` / language / category / buttons, no SMS body.

### 2.2 Shared hydrator

`MessageTemplateHydrator` (`MessageTemplateHydrator.cs` 43–120) is the post-wave single replace table:

`customer_name`, `customer_email`, `customer_phone`, `business_name`, `plan_name`, `amount`, `total_price`, `currency`, `days_overdue`, `current_period_end`, `renewal_link`, `checkout_url` (**alias of renewal_link**, line 77), `portal_magic_link`, `update_payment_link`.

Money: invariant `0.00` (`FormatMoney` 89–98). Dates: `en-GB` `d MMM yyyy` (`FormatPeriodEnd` 100–119).

`MessageLinkBuilder.Build` (20–39) returns:

- `updatePaymentLink` = `{ClientUrl}/{slug}/update-payment/{subId}` (or without id).
- `portalMagicLink` = `{ClientUrl}/{slug}/portal?token=` when a token exists, else naked portal.
- **`RenewalLink` is the same string as `UpdatePaymentLink`** (line 38). Hosted-bill override happens in the dunning / invoice handler *before* context is built, not inside the builder.

Preview mocks (`Preview` 46–59) still use `https://portal.lazuar.com/acme/...` and Ahmad Firdaus / Founders Mastermind. Preview also fakes `{{fulfillment_url}}` as `https://cloudflare.r2/download.pdf` (44, 86) — a lie the production digital-delivery handler does not keep.

Wiki (`GetTemplateVariablesAsync` 72–115) now lists the dunning/billing tags and dropped Community leftovers. Tests lock that (`TemplateVariablesWikiTests.cs` 15–40). Wiki still describes `{{fulfillment_url}}` as “Cloudflare R2 Download Link” (109) and `{{total_price}}` as “Same as amount until invoice totals exist” (94). Both are honest about the second and dishonest about the first.

### 2.3 Dual CMS (still)

Live dunning copy is **on the Commerce step** (`DunningStepDispatcher.cs` 74–76: `subject` / `email_body` / `whatsapp_body` from the step or snapshot). Templates page is for lifecycle / document / digital-delivery / invoice-reminder **names**. Editing “Payment Failed” does **not** change day-0 dunning. Editing a dunning step does **not** change the decline template.

That is Chargebee-shaped debt. W1-LP-053 left it on purpose.

---

## 3. Transactional jobs (what actually sends)

### 3.1 Immediate failed payment — **LIVE (email), amount empty**

W0-LP-151 added `GatewayPaymentFailedIntegrationEventHandler` (`GatewayPaymentFailedIntegrationEventHandler.cs` 16–132).

Trigger: `GatewayPaymentFailedIntegrationEvent` with `metadata.subscription_id` or `metadata.receipt` parseable as a Guid (110–131). One-off payments without that metadata: no mail (tests 109–132).

Skip if Commerce context missing or status `CANCELED` (55–60). Skip if CRM email empty (62–67). Skip if catalog “Payment Failed” missing (69–75).

Hydration (`82–95`):

- `PlanName` = `context.ProductName` only (`CommerceDocumentLookup.GetSubscriptionCommsContextAsync` returns `(ClientProfileId, Status, product?.Name)` — `CommerceDocumentLookup.cs` 122–140; `ICommerceDocumentLookup.cs` 63–66).
- **`Amount`, `TotalPrice`, `Currency`, `DaysOverdue`, `CurrentPeriodEnd` are all `""`.**
- Links: real `MessageLinkBuilder` + 24h magic token.
- `ToPhone: null` (101). Channel is whatever the template says (`ALL`). WhatsApp body is populated in the event and then **skipped** by Messaging because the flag is off and/or phone is null.

Catalog copy (`DefaultMessageTemplates.cs` 25–32) does not mention `{{amount}}`. Subject becomes “Action Needed: Payment issue for {plan}”. Buyer is not told RM X.

Tests (`GatewayPaymentFailedEmailHandlerTests.cs` 27–77) lock update-payment URL, plan name, no `{{` leftovers, `ToPhone == null`. They do **not** assert an amount.

**Not** the dunning sequence. First decline mail and day-0 dunning can both fire. That is Stripe-like (immediate + sequenced), not a bug, but the two bodies are different CMS.

### 3.2 Sequenced email dunning — **LIVE if BYOK; amounts are catalog list price**

Hourly `DunningEngineJob` → `DunningStepDispatcher.DispatchCommunicationStepAsync` (`DunningStepDispatcher.cs` 56–92) publishes `FulfillmentRequested(…, "COMMUNICATIONS", "reminder.dunning", payload)`.

Payload amounts (`77–80`):

```
plan_name = product?.Name ?? ""
amount    = product?.Price ?? 0m
total_price = product?.Price ?? 0m
currency  = product?.Currency ?? ""
```

That is the **catalog `Product.Price`**, not `Subscription.UnitAmount`, not `Subscription.Quantity`, not `SubscriptionBillingAmount.Line`.

`SubscriptionBillingAmount` (`SubscriptionBillingAmount.cs` 7–22) is the money truth Billing Engine and AUTO_CHARGE already use:

```
Unit = sub.UnitAmount > 0 ? sub.UnitAmount : product.Price
Seats = Max(1, sub.Quantity)
Line  = Unit * Seats
```

`PastDueDunningProcessor` uses `SubscriptionBillingAmount.Line` for **charge** (`PastDueDunningProcessor.cs` 177). The **email** path next to it does not.

Wave 3 seats (`Subscription.Quantity` / `UnitAmount` / `SetSnapshot`, `Subscription.cs` 23–27, 136–152) therefore **do not appear in dunning mail**. A 5-seat × RM 99 snapshot still emails `99.00` if the product list price is 99.

`current_period_end` is `NextBillingDate` as `yyyy-MM-dd` (82–84). Hydrator formats it to `d MMM yyyy`.

`checkout_url` is the minted hosted bill **only** when `CurrentRenewalCheckoutUrl` is dated for `NextBillingDate` (`ResolveLiveRenewalCheckoutUrl` 41–54). Hydrator then sets `RenewalLink = hosted ?? update-payment page` (`FulfillmentRequestedIntegrationEventHandler.cs` 109–110). `{{checkout_url}}` aliases that. `{{update_payment_link}}` stays the Lazuar interstitial. W1-LP-053 tests lock both (`DunningTemplateVariableSubstitutionTests.cs` 345–418).

Default new-org campaign (`GenerateDefaultDunningCampaignsCommandHandler.cs` 138–165):

| Offset | Action | Copy |
|--------|--------|------|
| −3 | EMAIL | Upcoming renewal; `{{current_period_end}}`; no pay CTA |
| 0 | EMAIL | Due today (`{{amount}} {{currency}}`); `{{renewal_link}}` |
| +3 | EMAIL | Still unpaid; `{{renewal_link}}` |
| +1, +5 | AUTO_CHARGE | No body. Billplz products skip via capabilities. |
| grace 7 | CANCEL | Then “Subscription Cancelled” template |

No WhatsApp step in the **new** seed. Existing tenants who still have a pre-wave +3 `WHATSAPP` with empty `EmailBody` are skipped by `ResolveEffectiveCommunicationAction` (`DunningStepDispatcher.cs` 18–36) and still recorded as dispatched (engine log). They will not catch up when WA one day exists.

Hydrate throws (outbox not written) on missing `client_profile_id`, missing CRM, empty email, empty EMAIL body (`FulfillmentRequestedIntegrationEventHandler.cs` 67–96, 193–202). That is the W0-LP-073 hop-2 contract.

WhatsApp demotion: flag off → `WHATSAPP` with email body becomes EMAIL; `ALL` becomes EMAIL; pure WA with no email body → `null` (skip).

### 3.3 Official Receipt / quotation / tax-invoice mail — **PARTIAL (link wrapper, no amount)**

`DocumentPublishedIntegrationEvent` (`DocumentPublishedIntegrationEvent.cs` 10–18) carries org, ledger id, type, storage path, slug, business name, customer name, customer email. **No amount, no currency, no plan, no tax, no LHDN UUID.**

Handler (`DocumentPublishedIntegrationEventHandler.cs` 33–97):

- Requires customer email + tenant slug.
- Type map: Official Receipt / Draft Quotation / Tax Invoice / Credit Note. Tax Invoice and Credit Note fall back to Official Receipt template if their own names are missing (38–59). Catalog does **not** seed “Tax Invoice” or “Credit Note” (`DefaultMessageTemplates.cs` 23–87). So B2B legal PDFs email the **receipt** copy (“official receipt and tax invoice (if applicable)”) unless the merchant created those names by hand.
- Signs a 30-day document URL (`Jwt:Secret`, `DocumentLinkSigner`).
- Replaces only `{{customer_name}}`, `{{business_name}}`, `{{document_link}}`. Subject only gets `business_name`. Does **not** call `MessageTemplateHydrator`.
- Publishes `ToPhone: null` (90). Channel from template (`ALL`).

W4-LP-100 made the **PDF** say it is not a MyInvois tax invoice. The **email** subject is still “Your official receipt from {business}.”

Billplz may still send its own receipt. We do not coordinate.

**Verdict:** PARTIAL. Consumer exists. Amount never enters the mail. Seats/snapshot are irrelevant here because the event has no money fields.

### 3.4 Invoice reminder — **LIVE for OPEN custom quotes; amount is line-item sum, not SST, not seats**

W3-LP-105.

Producer: hourly `InvoiceReminderJob` (`InvoiceReminderJob.cs` 18–144).

- Selects `CheckoutSession` where `Status == "OPEN"` **and** `ProductId == null` **and** `DueAt != null` (65–70). Catalog product sessions are ignored (test 99–115).
- Offsets **−3 / 0 / +3** vs `DueAt.Date` in **UTC** (24, 64, 90–95). Exact-day only — no catch-up if the worker was down on day −3.
- Idempotent: `InvoiceReminderDispatchLog` unique on `(SessionId, DayOffset)` (78–99, entity `InvoiceReminderDispatchLog.cs` 6–23).
- Does **not** mark the session PAST_DUE (class comment 18–20).
- Pay URL: `{ClientUrl}/{slug}/pay/{session.Id}` (104–106).
- Amount (`108–120`):

```
total = session.AdHocLineItems.Sum(i => i.Quantity * i.UnitPrice)
currency = "MYR"          // hardcoded
plan_name = session.DocumentNumber ?? "Quote"
due_at = yyyy-MM-dd
```

`AdHocLineItem` (`AdHocLineItem.cs` 6–17) is description + quantity + unit price. **No SST field.** Quote SST, if charged at checkout, is not in this sum.

Payload uses `session_id`, **not** `subscription_id` (`InvoiceReminderJob.cs` 109–121). Hydrator reads `subscription_id` for the magic token (`FulfillmentRequestedIntegrationEventHandler.cs` 62–106). For invoice reminders that parse fails → **no token**. Invoice Reminder catalog does not use `{{portal_magic_link}}`, so this is currently harmless.

Consumer (`FulfillmentRequestedIntegrationEventHandler.cs` 145–160): load catalog “Invoice Reminder”; if missing, **warn and return** (not throw). Then same hydrator as dunning. `due_at` wins over `current_period_end` for the date slot (184). `checkout_url` from payload becomes `RenewalLink` (109–110), which fills both `{{checkout_url}}` and `{{renewal_link}}`.

Catalog body (`DefaultMessageTemplates.cs` 79–86):

> Payment reminder from {{business_name}}  
> … payment for {{plan_name}} ({{amount}} {{currency}}) is due on {{current_period_end}}.  
> [Pay now]({{checkout_url}})

So the buyer sees document number (or the word “Quote”), line-item subtotal as `0.00`, and `MYR` even if the quote was not MYR.

Tests: job sends once on day 0, skips COMPLETED, ignores product sessions (`InvoiceReminderJobTests.cs` 67–115). **No** Communications hydrator test for `EventType == "invoice.reminder"`. Catalog name is only asserted in `DefaultMessageTemplatesTests.cs` 26.

This is **not** subscription dunning. Chargebee would have both. We now have both, on two jobs, two copy sources.

### 3.5 Portal Access / “welcome” — **LIVE (email), first payment only**

`PortalAccessEmailHandlers` (`PortalAccessEmailHandlers.cs` 15–99):

- `SubscriptionActivated` only if `IsFirstPayment` (47–49). Renewals do not re-mail.
- `PortalMagicLinkRequested` always (52–53).
- Catalog “Portal Access”, EMAIL, `ToPhone: null`.
- Real 24h token in `{{portal_magic_link}}` (76–78).
- Does **not** use `MessageTemplateHydrator`; local replace of three tags (80–87). Amount/plan unused (catalog has neither).

Tests (`PortalAccessEmailHandlerTests.cs` 26–70).

This is “here is your dashboard,” not Chargebee “welcome to {plan} you now pay RM X.” Closest thing we have to welcome. **PARTIAL** vs Chargebee activation mail; **LIVE** as a magic-link product.

### 3.6 Cancel — **LIVE (email); amount is catalog price if anyone puts it in the body**

`LifecycleEventHandlers` (`LifecycleEventHandlers.cs` 16–92) handles **only** `SubscriptionCanceledIntegrationEvent`.

`GetSubscriptionMailContextAsync` (`SubscriberQueryService.cs` 82–104) returns `product?.Name`, **`product?.Price`**, `product?.Currency`, `NextBillingDate`. Comment on the port still says “list price” (`ISubscriberQueryService.cs` 21). Not `UnitAmount * Quantity`.

Handler formats that price into `Amount` / `TotalPrice` (63–72). Default cancel catalog (`DefaultMessageTemplates.cs` 34–41) does **not** mention amount. If a merchant adds `{{amount}}` to the cancel template, they get list price, not seats.

Missing mail context still sends, with empty plan → subject `"Your  membership has ended"` (test 88–106). Ugly, not a crash.

`ToPhone` is the CRM phone (85). Channel `ALL`. WhatsApp body filled, then skipped at the edge.

SUSPEND does not mail (test 27–31).

### 3.7 Digital delivery — **PARTIAL / dishonest fulfillment URL**

`OrderCompletedDigitalDeliveryHandler.cs` 15–96:

- Always sends if the template exists and CRM has email. No “is this a digital product?” check. Every completed order with the seeded template gets “Your download is ready.”
- `plan_name` hardcoded `"your purchase"` (82).
- `fulfillment_url` = `{ClientUrl}/{slug}/portal` (69–74). Comment 73 is honest: no R2 asset URL on products.
- `portal_magic_link` = **same portal URL, no token** (84). Wiki still says 24-hour auto-login.
- Does not use the shared hydrator.

**Verdict:** handler exists; two of five tags are lies.

### 3.8 One auth mail — **LIVE (platform or tenant BYOK)**

`NotificationDispatchDomainEventHandlers.cs` 29–80. Hardcoded Markdown, not Communications templates.

| Mail | Tenant | Token? |
|------|--------|--------|
| Password reset | system → platform Resend (or console if key empty) | Yes |
| Verify email (“Welcome to Lazuar!”) | system | Yes |
| Workspace invite | **org** → **requires tenant BYOK** | Yes `/accept-invite?token=` |

### 3.9 Test reminder — still hardcoded

`SendTestReminderCommandHandler.cs` 168–176 always targets `admin@lazuars.io` + `+60123456789` with preview mocks. Endpoint returns `sent_to: admin@lazuars.io` (`TemplateEndpoints.cs` 114–120). WhatsApp test would only “send” if the flag were on — still console.

---

## 4. Bounce / complaint / unsubscribe suppression

### 4.1 Model

`SuppressionEntry` (`SuppressionEntry.cs` 11–43): org-scoped unique `(OrganizationId, Email)`, reason + source + created_at. Comments still list `UNSUBSCRIBE`, `BOUNCE`, `COMPLAINT`. Runtime also writes `ANONYMIZED` (fits `Reason` unconstrained string).

No phone / WA opt-out. No admin list / lift / export. No soft-delete. Insert-if-missing (`SuppressionService.SuppressAsync` 50–62) — **first reason wins**. An unsubscribe that lands first will never be upgraded to BOUNCE if the webhook arrives later.

### 4.2 Lanes (post-wave; this is the important legal fix)

`SuppressionLane` (`ISuppressionService.cs` 11–15): `Transactional`, `Marketing`.

`IsSuppressedAsync` (`SuppressionService.cs` 19–48):

- `BOUNCE` / `COMPLAINT` / `ANONYMIZED` → **both** lanes.
- `UNSUBSCRIBE` → **marketing only**.
- Parameterless overload defaults to **Marketing** (19–20) — safe for callers that forget.

Dispatch uses **Transactional** (`DispatchMessageIntegrationEventHandler.cs` 78). Broadcast fan-out uses **Marketing** (`BroadcastFanoutJob.cs` 162).

Tests (`SuppressionLaneTests.cs` 19–42): unsub does not block transactional; bounce blocks both.

**This closes the 007-feats/16 P0 “unsub from a holiday blast kills receipts.”** A List-Unsubscribe from a broadcast no longer blocks Official Receipt or dunning. Bounce/complaint still do (correct for deliverability).

### 4.3 Public unsubscribe

`GET` and `POST` `/public/communications/unsubscribe?org=&email=&sig=` (`PublicComplianceEndpoints.cs` 35–88).

- HMAC-SHA256(`Jwt:Secret`, `orgId:email`), hex lower, fixed-time compare.
- GET returns HTML “You’re unsubscribed” / “You will no longer receive **marketing** emails” (21–27, 57–59). Copy now matches the lane.
- POST returns 200 (RFC 8058 one-click). Source `list_unsubscribe_one_click` (85).
- Fallback JWT secret in code if config empty (49, 77) — same foot-gun as other HMAC helpers.

`BuildUnsubscribeUrl` (179–183) is used by **broadcasts only** (`BroadcastFanoutJob.cs` 168–183). Transactional/dunning correctly omit it.

### 4.4 Resend inbound webhook

`POST /public/communications/webhooks/resend` (`PublicComplianceEndpoints.cs` 90–166):

- Svix headers `svix-id` / `svix-timestamp` / `svix-signature`. Timestamp skew > 300s rejected (123–124).
- Secret empty: **503 fail-closed** outside Development; skip verify in Development (105–113).
- Parser (`ResendWebhookParser.cs` 12–97) accepts send-shape tags (array `{name,value}`) and webhook-shape tags (object map), plus `data.to[0]` / `data.email.to[0]` / `data.recipient`.
- `email.bounced` → `BOUNCE`; `email.complained` → `COMPLAINT` (38–43). Soft vs hard bounce not distinguished.
- No org tag → warn, **do not suppress** (155–158). Attribution depends on outbound `org` tag (`ResendEmailService.cs` 93–95).
- Delivered / opened / clicked ignored.

`ResendOptions` still has no `WebhookSecret`; the endpoint reads `IConfiguration["Resend:WebhookSecret"]` ad hoc (`appsettings.json` 35–38). Default empty.

### 4.5 GDPR

`ClientProfileAnonymizedIntegrationEventHandler.cs` 12–51: suppress pre-wipe email as `ANONYMIZED` / `gdpr_client_profile_anonymized`. Skips `deleted_*@localhost`. Tests (`ClientProfileAnonymizedSuppressionTests.cs` 21–68).

**Verdict: email suppression is LIVE, laned, and almost RFC-compliant.** WhatsApp STOP: none (acceptable under 00.4). No admin CRUD. First-reason-wins can hide a later bounce under an earlier unsub (marketing already blocked; transactional would still send until a bounce row exists — if unsub was first, bounce never inserts). That last race is the remaining hygiene bug.

---

## 5. WhatsApp: ConsoleMessagingService, flag, credits

### 5.1 Is WhatsApp sending?

**No.**

Evidence stacked so a future PR cannot “fix” one layer and claim the product:

1. **Flag default false.** `appsettings.json` 103–105: `"Messaging": { "WhatsAppEnabled": false }`. Handler short-circuits before the port (`DispatchMessageIntegrationEventHandler.cs` 60–76). Log `SKIPPED` / `"WhatsApp channel disabled"`. Tests lock this (`DispatchMessageIntegrationEventHandlerTests.cs` 253–273, 338–361).
2. **Adapter is a logger.** `ConsoleMessagingService.cs` 6–24: `[Local Dispatch] [MESSAGING/SMS]`, `IsBillable => false`. Registered as the singleton `IMessagingService` (`Messaging/Infrastructure/DependencyInjection.cs` 51). There is no `MetaCloudMessagingService`, no Graph client, no `phone_number_id`, no access token store, no WABA table.
3. **Engine refuses to pretend.** `ResolveEffectiveCommunicationAction` demotes or skips (`DunningStepDispatcher.cs` 18–36). New-org seed has **no** WA step (`DunningCampaignCommandHandlers.cs` 145–165).
4. **Documented freeze.** Messaging README 6–9; decisions.md §00.4 lines 68–74; W4-LP-074/155 done notes.
5. **Ops builder admits it.** “Send WhatsApp (not connected)” + amber banner (`DunningStepEditor.tsx` 152, 165–167).
6. **Several handlers force `ToPhone: null`:** DocumentPublished (90), GatewayPaymentFailed (101), Portal Access (91). Even a future live adapter would not send those without another change.
7. **Port is too narrow.** `IMessagingService.SendMessageAsync(string recipient, string text)` (`IMessagingService.cs` 3–9) cannot express Meta template name / language / category / URL button. Redesign is part of 00.4 reopen, not a flip.

Flipping the flag without a Meta client would: skip fewer steps, write `SENT` to the delivery log (`DispatchMessageIntegrationEventHandlerTests.cs` 276–308), and **still not reach WhatsApp**. W4 done notes: do not set the flag on console.

### 5.2 Credit metering — no deduct

Three independent guards, all live:

| Guard | Where | Effect |
|-------|-------|--------|
| Config cost | `appsettings.json` 65–68 `"WhatsAppSend": 0` | `CreditCostService.GetCost` returns 0 (`CreditCostService.cs` 47). Omitted key also 0. Tests (`CreditCostServiceTests.cs` 17–35, 108–138) lock appsettings = 0. |
| Console / non-billable | `DispatchMessageIntegrationEventHandler.cs` 85–88 | If `_messagingService is ConsoleMessagingService \|\| !IsBillable` → `whatsappCost = 0`. |
| Deduct only if `actualCost > 0` | same file 163–177 | Zero never calls `DeductTenantCreditCommand`. |

`EmailSend` and `BroadcastEmailPerRecipient` exist on `CreditAction` (`ICreditCostService.cs` 7–13) and are **unused**. Email is unmetered. Broadcasts are free (`BroadcastEndpoints.cs` 52–62, 81–82; fan-out sets `CreditHoldId: broadcast.Id` which also skips deduct — `DispatchMessageIntegrationEventHandler.cs` 89, `BroadcastFanoutJob.cs` 182). That `CreditHoldId = broadcast.Id` is still a semantic lie; it is how v1 stays free.

Tests (`DispatchMessageIntegrationEventHandlerTests.cs` 276–361):

- Flag off + cost 2 → no deduct, SKIPPED.
- Flag on + **console** + cost 2 → no `HasSufficientCredits`, no `DeductTenantCreditCommand`, log SENT.
- Flag on + substitute transport + cost 0 → send, no deduct.

`ConsoleMessagingServiceTests.cs` 19–24: `IsBillable` is false.

README prepaid wallet (`README.md` 64–65): “Live LHDN MyInvois submissions deduct micro-credits… Console/stub WhatsApp is **not** billed.” That sentence is now true. Pricing page fallback: `whatsapp_credits_live: false`, `whatsapp_send_credits: 0` (`PricingPage.tsx` 21–23, 126–129). Billing settings copy: “WhatsApp is not connected and is not billed” (`BillingSettingsPage.tsx` 149).

**Do not sell WhatsApp credit packs.** `COM-033` / `LP-074` stay Never until a billable provider exists.

### 5.3 What the UI still lets a merchant type

Templates editor: dual tab “WhatsApp Version” (`MessageTemplateEditor.tsx` 135–193). Catalog ALL templates still have `WhatsAppBody` strings. Create modal requires a WhatsApp body field (`TemplatesPage.tsx` 249 in grep). Those strings are **not sent**.

Dunning step editor: option `WHATSAPP` still saveable. Banner is honest. AUTO_CHARGE card still says “use email/WhatsApp steps” (`DunningStepEditor.tsx` 195) — leftover “or WhatsApp” on a Billplz limitation sentence.

Campaign builder validates WA body if action is WHATSAPP (`CampaignBuilderPage.tsx` 102–114).

Checkout / product: phone field labeled **“WhatsApp Number”** (`lazuar-portal/.../messages.ts` 17, 119; ops `CreateProductForm` / `ProductForm` / `ProductDetailPanel`). We collect a phone we do not message.

---

## 6. README / docs leftover WhatsApp claims

W4-LP-155 cleaned the **root README**. It did not clean the archaeology.

### 6.1 Honest now (do not “fix” these again)

| Location | Text |
|----------|------|
| `README.md` 18 | “WhatsApp dunning, Xero/QuickBooks sync, and Xendit are **not** shipping until their adapters exist.” |
| `README.md` 36–37 | Failure path in the hero diagram is **Email (dunning)**, not WhatsApp. |
| `README.md` 65 | Console/stub WhatsApp is not billed. |
| `README.md` 77 | “Not shipping: … Meta Cloud WhatsApp dunning” |
| `Modules/Messaging/README.md` 6–9 | Freeze + “Console WhatsApp is not a production channel.” |
| Ops Pricing / Billing Settings | Explicitly not connected / not billed. |
| Dunning step editor option label | “Send WhatsApp (not connected)” |

### 6.2 Still lying or overselling (leftover)

**ADRs (strategy docs that sales will quote):**

| File | Line | Claim |
|------|------|-------|
| `docs/architecture-decision-log/019-checkout-as-a-service-pivot.md` | 16, 38–41 | WhatsApp welcome; “automated WhatsApp Dunning message” deducts micro-credits |
| `docs/architecture-decision-log/020-lazuar-platform-integration-roadmap.md` | 27–31 | “Native WhatsApp Commerce & Recovery”; “Tap here to pay RM50 via FPX”; 95% open rate |
| `docs/architecture-decision-log/021-compliance-caas-pivot.md` | 51 | “Keep: WhatsApp Dunning (Auto-retries).” (job keep is fine; readers hear “we have it”) |
| `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` | 12, 51 | Launch depends on “automated WhatsApp dunning”; compete on “Billplz + Automated WhatsApp Dunning” |

**Gap docs (stale vs this eval; still indexed from `docs/001-gaps/README.md`):**

| File | What it still says |
|------|--------------------|
| `docs/001-gaps/README.md` 73 | “Default messaging ships broken `{{plan_name}}`; WhatsApp is console-only.” First clause is **stale** for dunning hydrate. |
| `docs/001-gaps/00-what-we-need-to-do-next.md` 15, 42, 52, 203 | “native WhatsApp that doesn’t really send”; “commit to Meta Cloud as a near-term channel” |
| `docs/001-gaps/01-dunning-engine.md` 8, 378–689 | Product promise “Native WhatsApp Dunning”; orchestration-vs-channel essay that predates hop-2 / snapshot / +3 EMAIL seed |
| `docs/001-gaps/08-communications-module.md` entire | Predates encryption, hydrator, invoice reminder, lanes, RFC 8058 POST, Portal Access. Paths still say `lazuar-hub`. |
| `docs/001-gaps/11-ops-crm-messaging.md` 180, 486 | Console stub; BuildingBlocks path (R34 moved it) |
| `docs/001-gaps/12-buildingblocks-host.md` 198, 516 | Same |
| `docs/001-gaps/19-frontend-backend-integration.md` 259 | “Billing page mentions WhatsApp dunning credits” — **stale**; billing page now says not billed |
| `docs/001-gaps/20-architecture-intent-vs-implementation.md` 25, 76–107, 315 | Quotes README hero that was rewritten; still says “docs and UI sell WhatsApp dunning” |

**Legal / buyer-facing:**

| File | Line | Claim |
|------|------|-------|
| `apps/lazuar-portal/src/app/legal/privacy/page.tsx` | 30 | Phone “used for WhatsApp delivery” |
| same | 41 | Sub-processor **“Meta (WhatsApp): For delivering automated session reminders and invite links.”** |

That privacy sentence is a **PDPA/GDPR lie**. We do not send to Meta. We do not deliver session reminders on WhatsApp. Invite links are email. This is the highest-severity leftover because it is customer-facing, not an ADR.

**Ops / portal copy that implies a channel:**

| File | What |
|------|------|
| Portal checkout i18n `form.phone` | “WhatsApp Number” / “Nombor WhatsApp” |
| Ops product forms | “Require WhatsApp Number” |
| `MessageTemplateEditor` | “WhatsApp Version” tab |
| `lazuar-ops` / `lazuar-admin` prompt-library | “Send a direct WhatsApp or Email message”; “share on WhatsApp” |
| `DunningStepEditor` AUTO_CHARGE card | “use email/WhatsApp steps” |

**007-feats (historical; 008-evals README already says do not cite as truth):** files 00, 04, 08, 12, 16, 18, 19, 20 still narrate the pre-wave README hero. Leave them. Do not “fix” history.

`apps/lazuar-docs` has **no** WhatsApp hits.

---

## 7. Whether email amounts match seats / snapshot

This is the money-honesty question for this slice. Short answer: **no, except quote line-item subtotals.**

### 7.1 What “snapshot” means in Commerce after Wave 3

`Subscription` stores `Quantity` (default 1) and `UnitAmount` (`Subscription.cs` 23–27). `SetSnapshot` / `RefreshSnapshot` (136–152) persist the contracted unit price. `SubscriptionBillingAmount.Line` = `Unit * max(1, Quantity)` is what **charges** use (`BillingEngineJob`, `RenewalCheckoutIssuer`, `PastDueDunningProcessor`).

Dunning **campaign** snapshot (`DunningCampaignSnapshot`) freezes **step copy and offsets**, not money. Mid-flight campaign edits no longer change already-assigned step bodies (W0-LP-079). That snapshot is irrelevant to RM on the email.

### 7.2 Per-mail audit

| Mail | Amount source | Seats? | Snapshot unit? | SST / tax? | Matches charge? |
|------|---------------|--------|----------------|------------|-----------------|
| Dunning −3 / 0 / +3 | `product.Price` (`DunningStepDispatcher.cs` 77–79) | **No** — ignores `Quantity` | **No** — ignores `UnitAmount` | No | **No** if seats > 1, PWYW, or plan price changed after subscribe |
| Immediate Payment Failed | `""` (`GatewayPaymentFailedIntegrationEventHandler.cs` 88–91) | n/a | n/a | n/a | **Silent.** Plan name only |
| Subscription Cancelled | `product.Price` via `SubscriptionMailContext` (`SubscriberQueryService.cs` 96–103) | **No** | **No** | No | Default body omits amount; custom `{{amount}}` would lie the same way |
| Official Receipt / Quotation / Tax Invoice email | **none** — event has no money (`DocumentPublishedIntegrationEvent.cs` 10–18; handler 74–85) | n/a | n/a | In the PDF, not the mail | Mail is a link wrapper |
| Invoice Reminder | `Sum(qty * unitPrice)` of `AdHocLineItem` (`InvoiceReminderJob.cs` 108–118) | Line qty **yes** (quote lines, not subscription seats) | Quote unit price **yes** | **No SST** | **Matches quote subtotal only.** Currency forced `MYR`. `plan_name` is document number |
| Portal Access | none | n/a | n/a | n/a | n/a |
| Digital delivery | none (`plan_name` = `"your purchase"`) | n/a | n/a | n/a | n/a |
| One auth | none | n/a | n/a | n/a | n/a |
| Broadcast | merchant-authored HTML, no hydrate | n/a | n/a | n/a | n/a |
| Wiki `{{total_price}}` | “Same as amount until invoice totals exist” (`CommunicationsQueryService.cs` 94) | Honest about the alias | | | |

Day-0 default copy is the only seeded dunning body that prints money: `"{{plan_name}} is due today ({{amount}} {{currency}})"` (`DunningCampaignCommandHandlers.cs` 150–152). That prints **list price**.

Hydrator tests that show `99.00` (`DunningTemplateVariableSubstitutionTests.cs` 254–269, 493–495) inject the number in the payload. They do **not** prove Commerce computed seats. Engine test (`DunningEngineJobTests.cs` 103–104) asserts `total_price == 50m` from `CreateProduct(..., price: 50)` — again list price.

Invoice reminder tests do **not** assert the amount field at all (`InvoiceReminderJobTests.cs` 78–81 only checks `checkout_url`).

### 7.3 Verdict on amounts

**PARTIAL / dishonest for subscription mail. Acceptable for quote reminders if the quote has no SST and is MYR.**

If Wave 3 seats are sold, a 5× RM 99 renewal that AUTO_CHARGES RM 495 will email “due today (99.00 MYR)” on the default day-0 step. That is a support incident and a PDPA-adjacent “misleading commercial communication” risk.

Fix (not this file): `DunningStepDispatcher` and `GetSubscriptionMailContextAsync` must call `SubscriptionBillingAmount.Line` / `Unit` / `Seats` and put `quantity` in the payload if copy needs it. Immediate fail should use the same. Receipt mail should either stay link-only on purpose or take amount from the ledger event.

---

## 8. Broadcasts (API vitamin; still not a product)

`SendBroadcastCommandHandler.cs` 29–46: EMAIL only; 1-minute throttle; `TotalRecipients = GetActiveSubscriberCountAsync`.

`GetActiveSubscriberCountAsync` (`SubscriberQueryService.cs` 32–43) counts **all** `ACTIVE`/`PAST_DUE`. `GetActiveSubscriberRecipientsAsync` (47–79) then **drops** anyone without `Consented_to_marketing`. Preview (`BroadcastEndpoints.cs` 46–62) uses the inflated count. Completed status shows sent+suppressed << total with no “skipped no-consent” counter. TypeSpec still says “v1 fans out to all ACTIVE/PAST_DUE subscribers with marketing consent” (`models.tsp` 64–67) — half true.

Fan-out (`BroadcastFanoutJob.cs` 141–207): page 100, `FOR UPDATE SKIP LOCKED`, marketing-lane suppress, inject unsubscribe URL, `CreditHoldId = broadcast.Id`, `RecordSent()` **before** provider success. `RecordFailed()` exists on the aggregate (65) and is never called per recipient.

No ops Broadcasts page. Sidebar is Templates + Email Provider + Dunning. Prompt-library still offers mass announcement / personalized WhatsApp (`prompt-library.ts` 24, 38).

**ADR 021: do not productize.** Keep consent + List-Unsubscribe plumbing.

---

## 9. Delivery logs

**Exist:** `messaging.MessageDeliveryLogs` (`MessageDeliveryLog.cs` 6–45). Status `SENT` / `FAILED` / `SKIPPED`. Indexes used by `GET /messaging/delivery-logs?limit=` (1–200, OrgAdmin) (`Endpoints.cs` 30–53). Written on every dispatch attempt including WA-disabled skips.

**Do not exist:** TypeSpec (messaging models still empty / undocumented). Ops UI — `DeliveryLogsPage.tsx` 10, 52–56 hits **`/one/workspaces/{id}/webhooks/logs`**. Developer → Logs is webhook delivery, not mail. No open/click/delivered from Resend. Bounce does not update the log row. No template name / step id / broadcast id. No export.

Stripe’s 60-day typed log is the bar. We have an internal table. Support curls.

---

## 10. TypeSpec / contracts

`packages/api-spec/modules/communications/admin-routes.tsp` 11–81 documents templates, preview, reset, test reminder, broadcasts, email-config. Matches Minimal API for those paths.

Still omitted from TypeSpec: `DELETE /templates/legacy-cleanup`, public unsubscribe GET/POST, Resend webhook, suppressions admin, WhatsApp config, `GET /messaging/delivery-logs`, `POST /messaging/notify`.

Public compliance routes are intentionally undocumented (same pattern as Messaging). Fine if Scalar is not how Gmail discovers the POST URL; the POST now exists.

---

## 11. Tests (what is locked vs what is not)

Communications: `BroadcastTests`, `BroadcastClaimTests`, `DefaultMessageTemplatesTests`, `DocumentPublishedIntegrationEventHandlerTests`, `DunningTemplateVariableSubstitutionTests`, `GatewayPaymentFailedEmailHandlerTests`, `LifecycleEventHandlersTests`, `MessageTemplateHydratorTests`, `PortalAccessEmailHandlerTests`, `ResendWebhookParserTests`, `SuppressionEntryTests`, `SuppressionLaneTests`, `TemplateVariablesWikiTests`, `TenantEmailConfigurationTests`, `ClientProfileAnonymizedSuppressionTests`, `AppEntitlementGrantedIntegrationEventHandlerTests`.

Messaging: `DispatchMessageIntegrationEventHandlerTests` (BYOK fail, suppress transactional, WA skip, **console does not deduct**), `MessageDeliveryLogTests`, `ResendEmailServiceTests` (org tag, List-Unsubscribe, no fallback), `EmailTemplateBuilderTests`, `ConsoleMessagingServiceTests` (`IsBillable` false), authz.

Commerce: `InvoiceReminderJobTests` (day 0 once; not hydrator), `DunningEngineJobTests` (payload list price).

**Still missing:**

- Hydrator test for `EventType == "invoice.reminder"` (template load, `due_at`, `checkout_url`, hardcoded MYR).
- Dispatcher / mail-context test that `Quantity=5` does **not** currently change `amount` (lock the bug) or, after a fix, that it does.
- Immediate-fail amount (today: empty).
- Resend webhook → `SuppressionService` integration (parser is unit-tested; endpoint is not).
- Broadcast consent vs `TotalRecipients`.
- Digital-delivery missing token / `"your purchase"`.
- TypeSpec honesty for `/messaging/delivery-logs`.

---

## 12. File-by-file notes (delta vs gap-08 and vs 007-feats/16)

| File | Then | After Waves 0–4 |
|------|------|-----------------|
| `TenantEmailConfiguration` | plaintext + full key on GET | Encrypted + hint |
| `SaveEmailConfigCommand` | same domains check | Same; still no sender∈domains |
| `HasValidEmailConfigAsync` | checkout gate | Still; create product archives; update activate blocked; quotes not gated |
| `DefaultMessageTemplates` | 5 live + orphans | **7** live (`Portal Access`, `Invoice Reminder`) |
| `MessageTemplateHydrator` | missing / thin | Shared populate + money/date format + `checkout_url` alias |
| `FulfillmentRequested…` | no plan_name; dead host | Hydrator + throw on empty; `invoice.reminder` branch |
| `GatewayPaymentFailed…` (Communications) | missing | **Exists**; amount empty; `ToPhone` null |
| `LifecycleEventHandlers` | suspend + dead URL | Cancel only; list price in context |
| `PortalAccessEmailHandlers` | missing | First pay + request; real token |
| `OrderCompletedDigitalDeliveryHandler` | missing / weak | Still weak (portal, `"your purchase"`, no token) |
| `DocumentPublished…` | works, 3 tags | Same + Tax Invoice/Credit Note fallback to receipt template |
| `InvoiceReminderJob` | missing | **Exists**; line-item sum; MYR hardcoded |
| `SuppressionService` | one list | **Lanes** |
| `PublicComplianceEndpoints` | GET only | GET + **POST** one-click |
| `DispatchMessage…` | WA cost 2; deduct on console | Cost 0; console not billable; transactional suppress |
| `ConsoleMessagingService` | stub | Stub + `IsBillable=false` |
| `appsettings.json` | WhatsAppSend 2 | **0**; flag still false |
| `GenerateDefaultDunningCampaigns` | +3 WA | +3 EMAIL + AUTO_CHARGE 1/5 |
| `DunningStepDispatcher` | product.Price | **Still** product.Price (seats ignored) |
| `README.md` | hero WA | Honest watermark + email diagram |
| Privacy policy | Meta sub-processor | **Still** Meta sub-processor |
| `DeliveryLogsPage` | webhooks | Still webhooks |

---

## 13. Honesty verdict

### What we can sell without lying

- **Tenant Resend BYOK is required** for paid checkout, product activate, and every tenant email. Keys are encrypted. GET is masked. No platform-from for tenants. Ops checklist matches the gate.
- **Email dunning exists** for tenants who configured Resend: −3 / 0 / +3 EMAIL on the new-org seed, plus AUTO_CHARGE on vaulted rails. Variables on the dunning path fill. Hosted Billplz/CHIP/Stripe pay-this-cycle URL is preferred when minted (W1-LP-053).
- **Immediate decline email exists** (W0-LP-151). CTA is the real update-payment URL. Amount is not in the mail.
- **Portal Access magic-link email exists** on first activation and on request.
- **Official Receipt / quotation emails exist** as signed 30-day PDF links. They are not tax invoices (W4-LP-100 on the PDF; email copy is still “official receipt”).
- **Invoice reminders exist** for OPEN custom quotes at UTC offsets −3 / 0 / +3 with a `/pay/{id}` link (W3-LP-105).
- **Bounce / complaint suppression exists**, org-tagged, fail-closed webhook, RFC 8058 GET+POST. Unsub does **not** kill receipts.
- **WhatsApp is not a product.** Flag off, console stub, cost 0, not billed. Root README and ops billing/pricing say so.

### What we must not sell

- **Native WhatsApp dunning / Meta Cloud / tap-to-pay RM50.** Channel is `ConsoleMessagingService`. Decision 00.4 + W4-LP-155. Privacy policy and ADR 020 still say otherwise — that is doc debt, not a feature.
- **WhatsApp credit packs.** Cost is 0; console is not billable; `EmailSend` unused.
- **Seat-accurate subscription emails.** Dunning and cancel use `Product.Price`. Immediate fail uses empty amount. Receipt email has no amount. Invoice reminder is quote subtotal, MYR, no SST.
- **Digital download by email.** We email the portal home and call it `fulfillment_url`.
- **Broadcasts as a marketing product.** API + inflated counts + no UI. ADR 021 refuse.
- **Ops “Developer → Logs” as an email log.** It is outbound webhooks.

### Rubric roll-up

| Surface | Mark |
|---------|------|
| Resend BYOK + encrypt + mask | **LIVE** |
| Checkout / activate gated on email config | **LIVE** |
| Invite without BYOK | **LIVE (fails)** — onboarding foot-gun |
| Sequenced email dunning | **LIVE** if BYOK; **PARTIAL** amounts |
| Immediate Payment Failed email | **LIVE**; **PARTIAL** (no amount) |
| Portal Access | **LIVE** |
| Official Receipt / quotation email | **PARTIAL** (link only; tax-invoice type falls back to receipt template) |
| Invoice Reminder | **LIVE** for OPEN quotes; **PARTIAL** (MYR hardcode, no SST, no hydrator test) |
| Cancel email | **LIVE**; thin copy; list price if customized |
| Digital delivery | **PARTIAL** / dishonest URL |
| Welcome / Chargebee activation | **PARTIAL** (Portal Access ≠ plan welcome) |
| Suppression + RFC 8058 + lanes | **LIVE** (email) |
| WA phone suppression | **N** (frozen) |
| WhatsApp send | **STUB** + flag off |
| WA credits | **Implemented at 0; console not billable** |
| Broadcasts | **API vitamin / REFUSE to UI** |
| Delivery log product | **PARTIAL** (table + GET; wrong ops page) |
| BM/EN locale | **N** |
| SMTP / second ESP | **N** (Resend HTTP only) |
| Amounts = seats × snapshot | **N** on subscription mail; **line-item subtotal** on quote reminders |

### Highest-leverage leftovers (honesty, not Meta)

1. **Rewrite portal privacy §2–3.** Phone is not “for WhatsApp delivery.” Meta is not a sub-processor today. This is the only leftover that can get a lawyer involved.
2. **Put `SubscriptionBillingAmount.Line` on dunning + cancel + fail payloads** before anyone sells seats. Day-0 copy already prints `{{amount}}`.
3. **Stop labeling checkout phone “WhatsApp Number”** until 00.4 reopens. It is a phone number we store.
4. **ADR 019/020/023 and `docs/001-gaps/08`** still describe a product that Waves 0–4 deliberately unpublished. Stamp them historical or they will be pasted into decks.
5. **Invoice reminder:** assert amount in tests; do not hardcode `MYR`; include SST if the quote charged it; throw (like dunning) if the template is missing instead of silent skip.
6. **Receipt email** either stays a deliberate link wrapper (say so in Email Settings blurb) or takes amount/plan from a richer `DocumentPublished` payload.
7. **Do not flip `Messaging:WhatsAppEnabled`.**

### One sentence

After Waves 0–4 Lazuar Pay **does** send tenant Resend email for checkout-gated receipts, sequenced dunning, immediate declines, portal magic links, cancel, and quote reminders — and it **does not** send WhatsApp, **does not** bill WhatsApp, and **does not** put seat/snapshot totals on subscription emails.

---

*End of uncondensed analysis. Source of truth is the live tree under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` as of 2026-08-16, not `docs/001-gaps/08-communications-module.md` and not `plans/007-feats/16-communications-whatsapp-email.md`.*
