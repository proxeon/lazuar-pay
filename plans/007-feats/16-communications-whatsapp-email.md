# 16 — Communications: email and WhatsApp

**Program:** 007-feats  
**Scope:** Lazuar Pay (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`) Communications + Messaging — transactional email, dunning, WhatsApp, templates, suppressions, BYOK Resend, delivery logs  
**Status:** Full uncondensed analysis — no product code changed  
**Date:** 2026-08-16  
**Author role:** feature-area analyst (subagent 16 of 20)  
**Does not reopen:** Meta Cloud API product (`plans/004-maintenance/decisions.md` §00.4 — no WhatsApp / multi-channel for six months); Community/Vault resurrection (ADR 022); marketing-suite vitamins (ADR 021 kill list)

**Standing locks this file must not contradict**

| Lock | Source | Consequence for communications |
|------|--------|--------------------------------|
| Lazuar is Compliance CaaS, not a CMS / marketing app | ADR 021 | Broadcast / abandoned-cart / community welcome are **vitamins**. Keep transactional + dunning. Refuse Customer.io / WATI / respond.io inbox products. |
| Keep WhatsApp **dunning** as the recovery engine | ADR 021 “Kill / Delay” list; ADR 020 §4; ADR 023 mitigation | The *job* (recover failed renewals on the channel Malaysians actually read) is still the CaaS differentiator. The *implementation* is **not live**. |
| No WhatsApp / multi-channel in the next 6 months | `plans/004-maintenance/decisions.md` §00.4 (locked ~2026-08-09 → freeze through ~2027-02-09) | Do not treat leftover WA bodies, credits, or UI tabs as a shipping channel. Reopen 00.4 before any Meta / Twilio / WATI adapter. |
| Credits stay in Billing | 00.5 | `CreditAction.WhatsAppSend` / `EmailSend` / `BroadcastEmailPerRecipient` are Billing concerns. Do not invent a Communications wallet. |
| Buyer money on rails ≠ Lazuar SaaS fee | 007-feats README | This chapter is **Lazuar Pay tenant → buyer** mail, plus platform auth mail (One). Aura salon `MG-*` is a different product. Aura is a Hub customer, not a comms competitor. |
| Do not sell WhatsApp dunning as live | 007-feats README standing constraint | “WhatsApp dunning and full compliance UI are roadmap (Phase D), not guaranteed demoable surfaces.” The README watermark is **correct**. The hero diagram two screens later is **not**. |
| BYOK software, not a MoR / BSP / inbox | 007-feats README | Do not become WATI, Customer.io, or a website builder to match competitors. |

**Stale documents this file supersedes on communications facts (do not cite as current):**

- `docs/001-gaps/08-communications-module.md` — written against an earlier tree (plaintext Resend keys returned to clients; reset blanks content; no `MessageDeliveryLog`; no `OrderCompletedDigitalDeliveryHandler`; dunning hydrator missing `plan_name`; WhatsApp silent-skip). Those items have **moved**. Cite live files below.
- `docs/001-gaps/01-dunning-engine.md` §Communications — still says `{{plan_name}}` is not substituted and that `GatewayPaymentFailed → Commerce` is missing. **Both are stale** as of 2026-08-16.
- `docs/001-gaps/11-ops-crm-messaging.md` — CRM consent “always forced true” is stale (`ConsentDefaultFalse` migration 2026-08-04; entity default `false`). Anonymize fan-out now has a Communications consumer.

**This document is not permission** to implement Meta Cloud, to turn broadcasts into a marketing product, or to claim “native WhatsApp dunning” on a sales call.

---

## Method

### Question this file answers

Lazuar Pay sells itself as the Asian Checkout-as-a-Service that emails receipts, recovers failed subscriptions, and (in the roadmap) duns on WhatsApp. Stripe, Chargebee, Billplz, and HitPay already send a known notification set. Resend, Customer.io, Twilio, Meta Cloud API, WATI, and respond.io are the pipes and inboxes people will compare us to.

What does the **live tree** actually send, on which channel, with which honesty gaps — and which competitor surfaces should we implement later, refuse, or leave frozen?

### How the work was done

1. Read `docs/001-gaps/08-communications-module.md` end-to-end, then **re-audited every live file** under:
   - `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/`
   - `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/`
   - dunning / lifecycle / document publishers in Commerce, Billing, One, CRM
   - ops UI: templates, email settings, dunning step editor, “delivery logs”
   - TypeSpec `packages/api-spec/modules/communications/` and `messaging/`
   - tests under `apps/lazuar-api/tests/Lazuar.ModuleTests/{Communications,Messaging}/`
2. Cross-checked ADRs 020 / 021 / 022 / 023, maintenance 00.4 / 00.5, `Messaging/README.md` product freeze, `README.md` marketing vs watermark, `appsettings.json` (`Messaging:WhatsAppEnabled`, `Credits:Costs:WhatsAppSend`).
3. Researched competitor **notification sets** (not their entire companies): Stripe Billing/Invoicing emails (docs 2026), Chargebee email notification categories, Resend transactional + List-Unsubscribe, Customer.io dunning-as-lifecycle, Twilio WhatsApp + Meta fees, Meta Cloud API utility vs marketing (per-message pricing from 2025-07-01; service-window change 2026-10-01), WATI, respond.io, Billplz receipt/SMS, HitPay notifications + 7-day retry + 7-day pre-renewal.
4. Honesty questions were answered from **code paths that execute**, not from seed copy, UI tabs, or ADR wish lists.

### Honesty rubric used below

| Mark | Meaning |
|------|---------|
| **LIVE** | A real provider call happens on a tenant path when config is present (Resend HTTP for email). |
| **PARTIAL** | Plumbing exists; trigger, variables, or UI is incomplete; or a second path still lies. |
| **STUB** | Interface + console / flag skip. No Graph API, no WABA, no SMS. |
| **SEEDED-ORPHAN** | Template or DTO exists; nothing dispatches it (or dispatch is webhook-only). |
| **REFUSE** | Competitor surface that would violate ADR 021 / 00.4 / 00.6. |
| **FROZEN** | Explicit product freeze; do not “just add an adapter.” |

### Architecture the audit assumes (and the code still implements)

```
Domain event / admin API
  → Communications (policy, templates, suppressions, BYOK store, variable fill)
  → communications.OutboxMessages → in-process bus
  → Messaging inbox
  → DispatchMessageIntegrationEventHandler
       → IEmailService = ResendEmailService   (LIVE if tenant BYOK)
       → IMessagingService = ConsoleMessagingService  (STUB; also gated off)
```

Golden rule (Messaging README): **render at the source, dispatch at the edge.** Messaging must not learn template IDs. That split is still correct and should survive a future Meta adapter.

### Absolute paths used as ground truth

| Concern | Path |
|---------|------|
| Default catalog | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs` |
| Template / suppression / broadcast / BYOK aggregates | `.../Communications/Domain/Aggregates/` |
| Dunning hydrate | `.../Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` |
| Lifecycle mail | `.../Communications/Infrastructure/EventHandlers/LifecycleEventHandlers.cs` |
| Receipt / quotation mail | `.../Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` |
| Digital-delivery mail | `.../Communications/Infrastructure/EventHandlers/OrderCompletedDigitalDeliveryHandler.cs` |
| Resend webhook + unsubscribe | `.../Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs` |
| Dispatch + credits + delivery log | `.../Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` |
| Resend adapter | `.../Messaging/Infrastructure/Email/ResendEmailService.cs` |
| WhatsApp adapter | `.../Messaging/Infrastructure/Messaging/ConsoleMessagingService.cs` |
| Delivery log entity + GET | `.../Messaging/Domain/MessageDeliveryLog.cs`, `.../Messaging/Infrastructure/Endpoints.cs` |
| Default dunning copy | `.../Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` (`GenerateDefaultDunningCampaignsCommandHandler`) |
| WA demotion | `.../Commerce/Infrastructure/Workers/DunningEngineJob.Dispatch.cs` |
| Auth / invite mail | `.../One/Application/EventHandlers/NotificationDispatchDomainEventHandlers.cs` |
| Templates UI | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/TemplatesPage.tsx` |
| Email BYOK UI | `.../lazuar-ops/src/modules/workspace/pages/EmailSettingsPage.tsx` |
| Dunning step UI | `.../lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx` |
| TypeSpec | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/communications/` |
| Freeze | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/decisions.md` §00.4 |
| ADR 021 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/021-compliance-caas-pivot.md` |

---

## Competitor notification sets

These are **notification jobs**, not company strategies. Lazuar Pay competes with Stripe/Chargebee/Billplz/HitPay on *what the buyer is told after money moves or fails*. Resend/Twilio/Meta/WATI/respond.io/Customer.io are *how* those jobs get delivered. Copying a WATI shared inbox is how we become a vitamin.

### Stripe Billing + Invoicing (the global notification bar)

Stripe is not a Malaysian FPX competitor. It is the **default expectation** every founder who has used Stripe Billing brings to Lazuar: a dashboard toggle, branded mail, a hosted update-payment page, and a 60-day email log.

Documented customer emails (`https://docs.stripe.com/billing/revenue-recovery/customer-emails`, `https://docs.stripe.com/invoicing/send-email`, current 2026):

| Stripe job | Trigger | Buyer CTA | Logged? |
|------------|---------|-----------|---------|
| Payment confirmation / receipt | Successful invoice payment | Receipt; optional PDF of invoice + receipt | Yes (Customers page, 60 days) |
| Failed payment | Each failed card charge | Update payment method / hosted recovery | Yes |
| Unpaid recurring invoice reminders | `send_invoice` still open | Pay hosted invoice | Yes |
| Unpaid one-off invoice | Advanced invoicing | Pay | Yes |
| Finalized invoice | Invoice finalized | View / pay | Yes |
| Trial ending | 7 days before (settings) or `customer.subscription.trial_will_end` at 3 days | Add PM / activate | Yes |
| Renewal reminder | N days before period end; **customer TZ** | Manage / update | Yes |
| Expiring card | 1 month before default PM expiry | Update card | Yes |
| 3DS / confirmation required | Off-session needs SCA | Stripe-hosted confirm link; optional nag loop | Yes |
| Credit note created | Credit note | View | Yes |
| Refund issued | Refund | View | Yes |
| Subscription cancelled | Cancel | None / manage | Yes |
| Additional To / CC recipients | Per-customer dashboard | n/a | n/a |

Properties that matter for Lazuar:

- **One toggle surface.** Merchant does not write Markdown per step unless they want to. We require a campaign builder *and* a templates page that dunning no longer uses.
- **Hosted recovery URL in every recovery email.** We have `{{update_payment_link}}` on the dunning hydrate path only. Lifecycle “Payment Failed” still points at a hardcoded dead URL.
- **Email log is a product.** Stripe: 60-day typed log on the customer. We persist `MessageDeliveryLog` (`SENT` / `FAILED` / `SKIPPED`) and expose `GET /messaging/delivery-logs`, but **ops “Developer → Logs” is webhook delivery**, not mail.
- **No WhatsApp, no SMS** in first-party Stripe dunning. Third-party blog posts (Churnbuster, Triggla, asrrcrm) exist because email-only recovery is weak in some markets. That is exactly the Asian hole ADR 020/021 want to occupy — **later**, not by lying that we occupy it now.
- **Sandbox does not spontaneously email.** Stripe only sends test mail to verified domain / team members. Our test reminder always targets `admin@lazuars.io` + `+60123456789`.
- **Custom sending domain** after verification; otherwise Stripe-from. We **require** tenant Resend BYOK (no platform fallback) before checkout even opens. Stricter than Stripe; more operationally brittle for a first sale.
- **FTC / California near-one-click cancel** is in the Stripe email. Irrelevant to MY PDPA, but the pattern (manage-subscription link on every billing mail) is the right hygiene.

Stripe does **not** send: WhatsApp utility templates, BM/EN locale packs, LHDN QR receipts, FPX update-payment deep links.

### Chargebee (the billing-suite notification bar)

Chargebee’s Configure Emails screen is the honest “what a subscription OS mails” checklist. Categories (docs 2026):

- **Subscription Management** — welcome, activation, change, pause, resume, cancel, trial expiry follow-ups.
- **Invoices, Credit Notes, and Payments** — invoice, payment success, payment failed, refund, credits applied; plus gateway-specific extras.
- **Revenue Recovery** — dunning for online payments *and* dunning for offline payments; per-attempt enable/edit/segment.
- **Customer Retention** — card expiring / expired / invalid; cancel follow-up; win-back (this last one is a vitamin for us).

Chargebee also lets the merchant bring **optional SMTP**. We do not. Chargebee versions dunning emails per retry. Our dunning copy lives on the step row, so “version” is “whatever the merchant last saved,” with no run snapshot.

**Take for Lazuar:** match Chargebee’s *transactional + revenue-recovery* set (welcome, receipt, failed, cancel, card expiry, per-step dunning). Do not match Chargebee’s marketing/retention cloud.

### Resend (the pipe we actually use)

Resend is not a competitor for “dunning product.” It is the **only** email transport. Relevant product facts:

- HTTP `POST https://api.resend.com/emails` with `from`, `to[]`, `subject`, `html`, optional `tags`, optional `headers`.
- Domain verification via `GET /domains` — this is exactly what `SaveEmailConfigCommand` uses to validate a key. It does **not** check that `SenderEmail`’s domain is one of those verified domains.
- Webhooks (Svix): `email.bounced`, `email.complained`, plus delivered/opened/clicked we ignore.
- Transactional API does **not** manage a contact list. RFC 2369 / RFC 8058 `List-Unsubscribe` + `List-Unsubscribe-Post` is the merchant’s job. We set those headers **only** when `UnsubscribeUrl` is passed (broadcasts). We do **not** implement the POST one-click handler on the same URL (GET-only unsubscribe). Gmail/Yahoo bulk-sender rules will fail us if we ever send ≥5k/day marketing.
- Resend’s own suppression list is account-scoped. Ours is org-scoped in `communications.SuppressionEntries`. Both can be true; we must keep ours or we will keep calling Resend for dead addresses.
- Resend offers SMTP relay. **We have no SMTP adapter** (`IEmailService` is Resend HTTP or `ConsoleEmailService`, and Program registers Resend only).
- No attachments in our payload. Stripe/Chargebee attach invoice PDFs. We email a **signed 30-day document link** instead (`DocumentLinkSigner`). That is an acceptable CaaS choice (smaller payload, LHDN QR lives on the PDF at the URL) if the link works.

### Customer.io (lifecycle CDP — refuse as a product shape)

Customer.io’s public dunning guidance is the standard 3-beat email (failed → still overdue → final notice) plus in-app / SMS / WhatsApp / postal as extra channels. They are a **journey tool**. ADR 021 killed “we are marketing software.” Using Customer.io *as a pattern library* for three dunning emails is fine. Building Customer.io (segments, A/B, win-back, multi-channel journeys, BYO SMS providers per workspace) is **REFUSE**.

If a tenant wants Customer.io, they should consume our `subscription.past_due` / `order.completed` **outbound webhooks** (Commerce already emits those) and run journeys there. That is the headless CaaS move.

### Twilio (CPaaS — possible future adapter, not a product)

Twilio WhatsApp (2026 pricing page):

- Twilio message-handling fee starts ~$0.005 / message.
- Meta template fees on top: Marketing / Utility / Authentication / Support.
- Utility + authentication start ~$0.0034 Meta fee outside the customer-service window; free inside the window (until Meta’s Oct 2026 service-message change).
- “Utility direct send (Beta)” — send some utility messages without pre-approved templates.
- Malaysia voice rates exist; WhatsApp template rates follow recipient country.

Twilio SMS is the Western default for dunning. **Malaysian buyers do not live in SMS.** ADR 020 is explicit: WhatsApp open rates vs email. A Twilio SMS adapter is P3 at best. A Twilio WhatsApp adapter is one legal way to reopen 00.4 (the other is Meta Cloud directly). Either way we still need: WABA, phone_number_id, template names, language, category, webhooks, E.164, quality rating.

`IMessagingService.SendMessageAsync(string recipient, string text)` cannot express any of that. The port is the freeze.

### Meta Cloud API (the WhatsApp policy that would bind us)

This is the policy surface ADR 020/021 implied and 00.4 refused to implement yet.

**Categories (2026):**

| Category | What Meta thinks it is | Typical Lazuar payload | Cost (after 2025-07-01 per-delivered-template) |
|----------|------------------------|------------------------|-----------------------------------------------|
| **Utility** | Order update, account alert, payment confirmation, appointment reminder the user already expects | Receipt, payment failed, dunning “update payment,” subscription cancelled | Utility rate outside 24h service window; historically free inside. **From 2026-10-01 Meta starts charging more service/utility-in-window traffic.** |
| **Authentication** | OTP / verify | Portal magic-link-as-OTP, if we ever WA-verify | Auth rate; **authentication-international** surcharge includes **Malaysia** as a listed market (expanded 2025-02-01) |
| **Marketing** | Promo, offer, cart recovery, re-engagement, “we miss you” | Abandoned cart 12h/24h (orphan templates), broadcasts, win-back | Always billed; engagement-based volume limits since 2025; US numbers do not receive marketing templates |
| **Service** | Free-form replies inside a user-opened 24h window | Two-way support chat | Free until 2026-10-01, then per-message |

**Rules that kill our current WA bodies if we ever flipped `Messaging:WhatsAppEnabled=true` and swapped the console stub:**

1. Business-initiated messages **outside** the 24h window **must** be approved templates (`name` + `language` + named/positional variables). Our `WhatsAppBody` is free-form Markdown/plain with `{{mustache}}` tags. Meta will reject them.
2. Interactive URL buttons (“Tap here to pay RM50 via FPX” — ADR 020) are a **template component**, not a string in `IMessagingService`.
3. Marketing vs utility is a **template review** decision. A dunning message that includes a coupon or “upgrade now” will be recategorized as marketing (expensive + opt-in). A clean “your renewal failed; update payment: {link}” should stay utility.
4. Abandoned-cart templates in `OrphanNames` would be **marketing**. Seeding them again would be an ADR 021 violation *and* a Meta policy violation without opt-in.
5. Quality rating / throughput: a tenant blasting marketing from a new WABA will be throttled. We have no quality webhook, no pause, no per-tenant WABA.
6. Phone numbers must be E.164. We pass `profile.Phone` through. CRM has a Malaysia-centric leading-`0` → `60` habit in older notes; Communications does not normalize.

**Honesty:** even a perfect Meta client tomorrow cannot legally send the WhatsApp tab contents in `MessageTemplateEditor` as session messages to people who have not written in. The editor is a fiction until we store Meta template name / language / category / button URL.

### WATI (WhatsApp inbox + blasts — refuse as a product)

WATI (2026) is a BSP: shared team inbox, no-code chatbots, broadcasts, WhatsApp Flows, Click-to-WhatsApp ads, WA payments, calling, AI agent. Plans ~$69–$349/mo plus **marked-up** Meta fees (public comparisons: up to ~60% on marketing, ~80% on utility/auth vs Meta list).

That is a **conversation company**. Lazuar is a checkout/ledger/tax company. Integrating WATI as *an outbound utility sender* (like Twilio) is a possible 00.4 reopen option. Rebuilding WATI inside ops (inbox, chatbot, CTWA, Flows) is **REFUSE**.

### respond.io (multi-channel inbox — refuse as a product)

respond.io (2026) is the other BSP mid-market buyers will name: WhatsApp + Instagram + email + AI handoff, Meta rates passed through without WATI-sized markup, Starter ~$99/mo. Same verdict as WATI: **do not become an inbox**. Optional future: tenant pastes a respond.io webhook as a Commerce fulfillment target (already a generic HTTP fan-out). That is headless. That is allowed.

### Billplz (the local rail that already emails)

Billplz is our default online checkout rail. It is also a **silent second mailer**.

From Billplz support / partner FAQs (current):

- Receipts are sent **only for successful payments**.
- Dashboard: Settings → Email Notification to manage receipt notifications.
- Email receipt is **free**. SMS receipt is **charged**.
- Collection bills can be sent by email, SMS, or link.
- Payment-order API: “A receipt will be sent to this email once the Payment Order has been processed.”
- Merchant can share the same Billplz link on WhatsApp / IG / email — **human** channel, not Cloud API.

Implications for Lazuar:

1. A Billplz checkout that succeeds can produce **two buyer emails**: Billplz’s own receipt **and** our `Official Receipt` (Billing `GenerateAndStoreDocumentCommand` → `DocumentPublished` → Communications). We do not coordinate, suppress, or brand-unify them.
2. Billplz will **not** send failed-renewal dunning. Billplz cannot vault / off-session charge (`NotSupportedException` on AUTO_CHARGE). Our email dunning *is* the recovery path for Billplz products — and the DunningStepEditor already says so.
3. Billplz SMS receipts are a paid bolt-on. We should not promise SMS to “match Billplz.”
4. Billplz does not do subscription lifecycle mail (trial, cancel, card expiry). That is our job if we want to beat “just use Billplz bills.”

### HitPay (the SEA payments OS we actually lose deals to)

HitPay (docs 2026 + recurring-billing guide 2026-08-14):

**Merchant notifications** (Settings → Notifications): Daily Collection / payouts, New Order, Pending Order, Incoming Payment, Customer Receipt — email and/or mobile push.

**Buyer / subscription notifications (productized, not a campaign builder):**

- Instant customer payment receipt.
- **Automated 7-day pre-renewal email** before each recurring charge.
- **Failed-charge retry up to 7 consecutive days**, with a recommended merchant-side dunning email on day 1 / 4 / 7.
- Invoice: resend invoice email; unpaid-invoice follow-ups.
- Payment links designed to be **pasted** into WhatsApp / email / SMS — HitPay does not claim Meta Cloud dunning.
- App copy: “Pre populate phone / number fields when sending SMS / Whatsapp receipts” — **human share**, not Cloud API.
- PayNow cannot recur (same physics as DuitNow/Billplz for subscriptions).

HitPay’s bar is lower than Stripe’s and **higher than ours on two jobs we still miss**: (1) a guaranteed pre-renewal email that is not “whatever the tenant typed into a campaign,” (2) merchant push that money moved. Their bar is **honest** about WhatsApp: share a link, don’t pretend to be Meta.

### Cross-competitor notification matrix (buyer-facing)

| Job | Stripe | Chargebee | Billplz | HitPay | Customer.io | WATI / respond.io | Lazuar Pay today |
|-----|:------:|:---------:|:-------:|:------:|:-----------:|:-----------------:|------------------|
| Receipt / payment success | Y | Y | Y (own) | Y | if wired | if wired | **PARTIAL** — Official Receipt email when Billing PDF runs (B2C); Billplz may double-send; digital-delivery mail is a portal link, not a receipt |
| Failed payment (immediate) | Y | Y | N | Y (retry + email) | Y | utility WA possible | **PARTIAL** — no immediate mail on `GatewayPaymentFailed`; first mail is the next matching dunning step (day 0 / +3 / …) |
| Dunning sequence | Y (settings + Smart Retry) | Y (per attempt) | N | 7-day retry + 7-day pre-renewal | Y | marketing/utility mix | **PARTIAL** — email steps LIVE if BYOK; default +3 is WhatsApp-only and **skipped** |
| Trial ending | Y | Y | N | N | Y | — | **N** — no trial product mail |
| Renewal reminder | Y (TZ-aware) | Y | N | Y (fixed −7d) | Y | — | **PARTIAL** — default campaign −3 email; UTC day math; no TZ |
| Card expiring | Y (−1 month) | Y | N | advised, not first-class | Y | — | **N** |
| 3DS / confirm payment | Y | gateway-specific | n/a | n/a | — | — | **N** |
| Refund / credit note | Y | Y | gateway | dashboard | — | — | **N** as a Communications template |
| Cancel / end | Y | Y | N | N | Y | — | **PARTIAL** — `Subscription Cancelled` template on cancel event; variables thin |
| Welcome / activated | via invoice | Y | N | N | Y | — | **N** — `SubscriptionActivated` is **webhook only**; One “Welcome to Lazuar” is **verify-email** |
| Magic link / reset / invite | Stripe-hosted | hosted pages | n/a | n/a | — | auth templates | **LIVE** (One, platform or tenant BYOK) |
| Marketing broadcast | N (not their job) | optional | N | N | Y | Y | **API exists, no ops UI, ADR 021 refuse to productize** |
| WhatsApp utility | N | N | human paste | human paste | via Twilio | Y | **STUB + flag off** |
| WhatsApp marketing | N | N | N | N | Y | Y | **REFUSE** (ADR 021 + Meta) |
| SMS | N | add-on | paid receipts | share | Y | — | **N** |
| Locale packs (BM/EN) | account locale | template locale | EN/BM support-site mix | EN | per-user | per-template lang | **N** — EN copy only |
| Delivery log | 60-day typed | dashboard | dashboard | dashboard | full | full | **PARTIAL** — table + raw GET; no ops UI; no provider status timeline |
| BYO sending domain | custom domain | SMTP or Chargebee | Billplz-from | HitPay-from | BYO ESP | n/a | **LIVE** — Resend BYOK required |
| BYO SMTP | N | Y | N | N | Y | n/a | **N** — Resend HTTP only |
| Suppression / unsub | Stripe + ESP | Chargebee | n/a | n/a | Y | WA opt-out | **PARTIAL** — email only; RFC 8058 POST missing; no admin list |

---

## Our modules audit

### 1. Split of responsibilities (still the right architecture)

**Communications** owns content and policy:

- `MessageTemplate`, `SuppressionEntry`, `Broadcast`, `TenantEmailConfiguration`
- entitlement seeding, variable substitution, public unsubscribe, Resend inbound webhook
- admin HTTP under `/admin/communications`

**Messaging** owns the dumb pipe (R34 — ports live in the module, not BuildingBlocks):

- `DispatchMessageIntegrationEvent`
- `ResendEmailService`, `ConsoleEmailService`, `EmailTemplateBuilder`
- `ConsoleMessagingService` (`IMessagingService`)
- `TenantReplica`, `MessageDeliveryLog`
- `GET /messaging/delivery-logs`, `POST /messaging/notify` (system alert to replica slug — console only)

Maintenance 00.4: **do not merge** these modules until a real multi-channel provider is funded.

### 2. Domain as of 2026-08-16

#### `MessageTemplate`

Fields: `Name`, `Channel` (`EMAIL` / `WHATSAPP` / `ALL`), `Subject`, `EmailBody`, `WhatsAppBody`, `IsDefault`, jsonb `RequiredVariables` / `OptionalVariables`.

Mutations:

- `UpdateContent` — clears `IsDefault`.
- `RestoreFromDefault` — **now real** (catalog copy + `IsDefault = true`). The gap doc’s “reset blanks content” is fixed for catalog names.

Still missing vs Meta / Chargebee:

- no unique `(OrganizationId, Name)` (duplicate custom names possible)
- no versioning / locale
- no Meta `template_name`, `language`, `category`, button components
- no SMS body
- create validates `{{tags}}`; **update does not**

#### `DefaultMessageTemplates` (canonical catalog)

Only five definitions are seeded on `AppEntitlementGranted` for `COMMUNITY` | `COMMERCE` | `VAULT`:

| Name | Channel | Required vars | Has a consumer? |
|------|---------|---------------|-----------------|
| Payment Failed | ALL | `{{renewal_link}}` | **Yes, but wrong moment** — `LifecycleEventHandlers` on **suspend**, not on first decline |
| Subscription Cancelled | ALL | (none) | **Yes** — cancel from dunning final action / admin / portal / GDPR |
| Digital Product Delivery | ALL | `{{fulfillment_url}}` | **Yes** — `OrderCompletedDigitalDeliveryHandler` (fulfillment_url = **portal URL**, not R2) |
| Quotation Ready | ALL | `{{document_link}}` | **Yes** — `DocumentPublished` when `DocumentType == "Draft Quotation"` |
| Official Receipt | ALL | `{{document_link}}` | **Yes** — `DocumentPublished` when Billing generates B2C “Official Receipt” PDF |

`OrphanNames` (legacy-cleanup endpoint may delete; **not** re-seeded):

`Community Welcome`, `Community Payment Success`, `Event Ticket Confirmation`, `Abandoned Cart (12h)`, `Abandoned Cart (24h)`, `Generic Receipt`, `Subscription Renewal (3 Days)`, `Subscription Renewal Due Today`, `Subscription Renewal Overdue`.

Abandoned-cart and Community Welcome remaining in the orphan list is correct. Re-seeding them would be an ADR 021 / 022 regression.

`GetDefaultTemplateIdsAsync` is **gone** from the tree (the gap doc’s “dead method” was deleted).

#### `SuppressionEntry`

Org-scoped unique `(OrganizationId, Email)`. Reasons in comments: `UNSUBSCRIBE`, `BOUNCE`, `COMPLAINT`. Runtime also writes `ANONYMIZED` (GDPR fan-out; fits `Reason` max 20).

No phone / WA opt-out. No marketing-vs-transactional split. No admin list / unsuppress / export. No soft-delete. `ISuppressionService` is insert-if-missing + exists check.

#### `Broadcast`

Email subject/body only. `DRAFT → QUEUED → SENDING → COMPLETED | FAILED`. `RecordFailed()` exists; fan-out **still increments `SentCount` before provider success** and never calls `RecordFailed` per recipient. Credits columns were dropped; DTO still returns `credits_* = 0`.

`SendBroadcastCommand` rejects any channel except `EMAIL`. Audience filters are documented as not productized.

**Consent bug:** `GetActiveSubscriberCountAsync` counts all `ACTIVE`/`PAST_DUE` subscriptions. `GetActiveSubscriberRecipientsAsync` then **drops** anyone without `Consented_to_marketing`. `TotalRecipients` on the broadcast is therefore **inflated**; completed status will show sent+suppressed << total without a “skipped no-consent” counter.

**No ops page** for broadcasts. Sidebar has Templates + Email Provider + Dunning. Ops agent prompt-library still offers “Mass Announcement.” API-only marketing is how vitamins sneak back in.

#### `TenantEmailConfiguration`

`ApiKey` is **AES-encrypted** (`ISecretVault`). GET returns `has_api_key` + last-4 hint, never the raw key. Empty PUT key = keep existing. Legacy plaintext rows decrypt-or-passthrough.

Still missing: reply-to, display name, multiple senders, persisted domain-verification record, proof that `SenderEmail` belongs to a verified Resend domain.

System tenant (`Guid.Empty` / `…0001`) cannot save BYOK — uses `Resend:ApiKey` / `SenderEmail`.

### 3. Dispatch pipeline (Messaging)

`DispatchMessageIntegrationEvent`: `ToEmail`, `ToPhone`, `Subject`, `HtmlEmailBody`, `PlainTextPhoneBody`, `Channel`, optional `CreditHoldId`, optional `UnsubscribeUrl`.

Handler logic (live):

1. `Messaging:WhatsAppEnabled` default **false**. If WA wanted and flag off → log `SKIPPED` / `"WhatsApp channel disabled"` / do not call `IMessagingService`.
2. Email suppressed via `ISuppressionService` → `SKIPPED` / `"Address suppressed"`.
3. WhatsApp credits: `CreditAction.WhatsAppSend` (config **2**). If insufficient and not `CreditHoldId` → log `SKIPPED` / `"Insufficient credits"`. If that was a **pure-WA** dispatch, **throw** so inbox retries. If email already sent, WA failure is logged and swallowed.
4. Tenant email: decrypt BYOK; wrap HTML with `EmailTemplateBuilder.WrapWithBrandHtml` (still does `Replace("\n","<br/>")` on already-parsed HTML); `ResendEmailService.SendEmailAsync`. No BYOK → **throw** “No platform fallback allowed.”
5. Persist `MessageDeliveryLog` best-effort (`SENT` + Resend id, or `FAILED` + exception message).
6. Deduct credits **after** WA “send.” Console send always succeeds, so if someone flipped the flag, we would **bill 2 credits for a console log**. Deduct failure is logged, not retried (free message).

`CreditHoldId` is still set to `broadcast.Id` on fan-out. That is a semantic lie that also **skips** credit deduction (broadcasts are free by policy). Fine until someone puts a real hold id in that field.

`EmailSend` and `BroadcastEmailPerRecipient` exist on `CreditAction` and are **unused**. Email is not metered.

### 4. Transactional jobs (the four the brief named)

#### Receipt

**Path that exists**

1. B2C `GatewayPaymentCompleted` → Billing ledger → `GenerateAndStoreDocumentCommand(..., "Official Receipt")` → R2 PDF → `DocumentPublishedIntegrationEvent`.
2. Communications picks template **Official Receipt** (or **Quotation Ready** if `DocumentType == "Draft Quotation"`).
3. Signs a 30-day document URL with `Jwt:Secret`.
4. Substitutes `{{customer_name}}`, `{{business_name}}`, `{{document_link}}`. Subject only gets `business_name`.
5. Publishes dispatch with `ToPhone: null` — **email only**, even if the template channel is `ALL` and `WhatsAppBody` is filled.

**Gaps**

- B2B payments **do not** generate Official Receipt here (`isB2b` skips the command). Correct for LHDN consolidation; means high-ticket buyers get **no** Communications receipt unless something else sends.
- `plan_name` / amount / tax / LHDN UUID are **not** in the email. The PDF may have them; the mail is a link wrapper.
- Billplz may send a **second** receipt.
- Offline / manual / zero-amount paths only get a receipt if Billing also generated a document (manual enroll handler does).
- Digital Product Delivery on `OrderCompleted` is **not** a receipt. It says “download your file” and points at `{ClientUrl}/{slug}/portal` with `{{plan_name}}` hardcoded to `"your purchase"` and **no magic-link token**. There is still no R2 asset URL on products (comment in the handler is honest).

**Verdict:** PARTIAL. Better than the gap doc (the consumer exists). Not Stripe-grade (no PDF attach, no refund mail, no amount in the subject).

#### Failed payment

Two different products are entangled.

**A. Immediate “your card was declined” (Stripe-style)** — **does not exist.**  
`GatewayPaymentFailedIntegrationEventHandler` marks PAST_DUE, assigns a campaign, emits `subscription.past_due` **webhook**. It does **not** publish `DispatchMessage` or `FulfillmentRequested`.

**B. Sequenced dunning mail (Chargebee-style)** — **exists for EMAIL steps.**  
Hourly `DunningEngineJob` fires steps whose `DayOffset` is due and not in `ReminderDispatchLog`. Communications hydrates from the **step’s inline copy**, not from the Templates page.

Default seeded campaign (`GenerateDefaultDunningCampaignsCommandHandler`):

| Offset | Action | What happens with `WhatsAppEnabled=false` |
|--------|--------|-------------------------------------------|
| −3 | EMAIL pre-due | LIVE (if BYOK) |
| 0 | EMAIL due today | LIVE (if BYOK) |
| +3 | WHATSAPP only, **no EmailBody** | `ResolveEffectiveCommunicationAction` returns **null** → step **skipped** and still recorded as dispatched |
| grace 7 | CANCEL | Publishes `SubscriptionCanceled` → “Subscription Cancelled” template |

**C. “Payment Failed” template** — fires on **`SubscriptionSuspended`**, not on decline. Hydration is the **worst** remaining path:

- only replaces `{{customer_name}}` and `{{renewal_link}}`
- `{{renewal_link}}` is hardcoded `https://portal.lazuar.com/checkout/update` (ignores `App:ClientUrl`, slug, token)
- `WhatsAppBody` is passed as `null`
- `{{plan_name}}` / `{{business_name}}` left literal if present in the catalog body

**Verdict:** PARTIAL for sequenced email; **missing** immediate fail mail; **broken** suspend-template path; **default +3 WA is dead**.

#### Magic link

Three different “magic links” share a variable name.

| Mail | Token? | Tenant | Template? |
|------|--------|--------|-----------|
| Password reset (One) | **Yes** — `?email=&token=` | system → platform Resend | hardcoded Markdown, not Communications |
| Verify email (One) | **Yes** | system | hardcoded; subject “Verify your email”; body says “Welcome to Lazuar!” |
| Workspace invite (One) | **Yes** — `/accept-invite?token=` | **org** → **requires tenant BYOK** | hardcoded |
| Dunning `{{portal_magic_link}}` | **Yes** — `IMagicLinkTokenService` HMAC, 24h, `Jwt:Secret` | org | inline step copy / reminder.due template |
| Dunning `{{renewal_link}}` | **No** — naked `{ClientUrl}/{slug}/portal` | org | same |
| Dunning `{{update_payment_link}}` | **No token** — `{ClientUrl}/{slug}/update-payment/{subId}` | org | same |
| Digital delivery `{{portal_magic_link}}` | **No** — portal URL only | org | Digital Product Delivery |
| Lifecycle Payment Failed `{{renewal_link}}` | **No** — dead host path | org | Payment Failed |
| Variable wiki text | Claims “Secure, 24-hour auto-login” | — | **Only true on dunning hydrate** |

`MagicLinkTokenService` itself is solid (24h HMAC, tested). The lie is the **variable catalog** and the **digital-delivery / lifecycle** callers that do not generate tokens.

**Verdict:** LIVE for One auth; PARTIAL for portal; dishonest wiki copy.

#### Welcome

| Thing that looks like welcome | What it actually does |
|-------------------------------|------------------------|
| One verify-email copy “Welcome to Lazuar!” | Platform email verification. Not merchant welcome. |
| Manual subscriber `send_welcome_email` | Publishes `SubscriptionActivatedIntegrationEvent` |
| `SubscriptionLifecycleIntegrationEventHandlers` | Turns activation into **`order`/subscription outbound webhook only** |
| `Community Welcome` | Orphan name; not seeded; ADR 022 |
| Digital Product Delivery | Post-purchase download tease, not a welcome |

There is **no** merchant-branded “you are now a member of {business} / here is your portal” Communications send on first activation.

**Verdict:** N / SEEDED-ORPHAN. Chargebee-shaped hole. Highest-leverage transactional add after receipt + failed-payment-immediate.

### 5. Dunning sequences (email vs WhatsApp vs AUTO_CHARGE)

Orchestration is Commerce. Delivery is Communications → Messaging. That 3-hop is correct.

**What is good**

- `ReminderDispatchLog` unique on subscription + target billing date + day offset — idempotent.
- `GatewayPaymentFailed` now assigns campaigns and marks PAST_DUE (gap-01 stale).
- Cancel/Suspend final actions **do** publish typed events (gap-07 stale).
- Hydrator **does** replace `plan_name`, `amount`, `total_price`, `currency`, `days_overdue`, `customer_*`, `business_name`, `renewal_link`, `portal_magic_link` (real token), `update_payment_link` (gap-01 / gap-08 stale). Covered by `DunningTemplateVariableSubstitutionTests`.
- Engine reads `Messaging:WhatsAppEnabled` and demotes WHATSAPP→EMAIL only if `EmailBody` is present; ALL→EMAIL; pure WA without email → skip.
- Ops editor is **honest**: option label “Send WhatsApp (not connected)” + amber banner that WA steps run as email if a body exists, else skip.
- Billplz AUTO_CHARGE limitation is written on the AUTO_CHARGE card.

**What is still dual-CMS**

Live dunning copy is **on the step**. Templates page is for lifecycle/document/digital-delivery names. Merchants who edit “Payment Failed” in Templates do **not** change day-0 dunning. Merchants who edit a dunning step do **not** change the suspend template.

`reminder.due` + `template_id` is still implemented in the hydrator and **still unpublished** by any job.

**What default strategy actually does in production today**

With factory defaults (`WhatsAppEnabled=false`, Standard Recovery Strategy):

1. Day −3 email (if BYOK).
2. Day 0 email (if BYOK).
3. Day +3 WhatsApp **no-op** (logged skip, log row marked dispatched so it will never retry when WA is one day enabled).
4. Day +7 cancel + “Subscription Cancelled” email (if BYOK and template present).
5. No AUTO_CHARGE unless the merchant adds it (and the product is Stripe/CHIP with a vaulted token).

That is **not** “native WhatsApp dunning.” It is a two-email reminder and a cancel.

### 6. Marketing (refuse — ADR 021)

ADR 021: “If a feature does not directly facilitate a transaction or keep a business legally compliant, we will not build it.” Kill list includes viral giveaways, community DRM, website builders. Broadcasts are the same vitamin in thinner clothing.

What exists anyway:

- `POST /admin/communications/broadcasts` (EMAIL only, 1-minute throttle)
- `BroadcastFanoutJob` every 10s, page size 100, `FOR UPDATE SKIP LOCKED`
- Unsubscribe URL injected; List-Unsubscribe headers set
- Consent filter on **fan-out**, not on **count**
- Ops agent prompts for mass / targeted broadcast
- TypeSpec still describes “v1 fans out to all ACTIVE/PAST_DUE subscribers with marketing consent”

**Product recommendation:** keep the compliance plumbing (consent, suppression, List-Unsubscribe) because PDPA / Gmail will demand it if anyone uses the API. **Do not** build a Broadcasts UI, segments, A/B, or WhatsApp blasts. If the API is an accident, hide it from Scalar or 410 it the way Aura 410’d Layer T. Targeted “all members of plan X” is still marketing.

Abandoned cart templates: correctly orphaned. Do not rebuild; Meta would classify them as marketing.

### 7. Template variables (honesty)

**Wiki (`GetTemplateVariablesAsync`) documents:**

- Customer: `customer_name`, `customer_email`, `customer_phone`
- Billing: `plan_name`, `total_price`, `renewal_link`, `current_period_end`, `portal_magic_link`
- Fulfillment: `fulfillment_url`, `meeting_link`, `group_link`

**Used in catalog / dunning / documents but missing or mis-described in the wiki:**

| Tag | Wiki | Actual fill |
|-----|------|-------------|
| `{{document_link}}` | absent | Document handler only |
| `{{update_payment_link}}` | absent (dunning placeholder text in UI) | Dunning hydrator only |
| `{{amount}}`, `{{currency}}`, `{{days_overdue}}` | absent | Dunning hydrator |
| `{{current_period_end}}` | “date the cycle ends” | **Never filled in production handlers** — preview mocks “31 Dec 2026” |
| `{{meeting_link}}`, `{{group_link}}` | listed | **Never filled** (Community leftovers) |
| `{{fulfillment_url}}` | “Cloudflare R2 Download Link” | Portal URL, not R2 |
| `{{portal_magic_link}}` | “24-hour auto-login” | Token only on dunning path |
| `{{renewal_link}}` | “secure checkout billing link” | Portal home, or dead URL on lifecycle |

Create-time validation cannot see tags that are not in the template’s own required/optional lists. Templates UI create modal hardcodes required `{{customer_name}}` and optional `plan_name` / `renewal_link` — so a merchant cannot create a document-link template without fighting validation unless they add `{{document_link}}` to those lists (the modal does not).

Update path: paste any `{{garbage}}`; it will ship.

### 8. Suppression / opt-out

**Works**

- `GET /public/communications/unsubscribe?org=&email=&sig=` HMAC-SHA256(`Jwt:Secret`, `orgId:email`), HTML “You’re unsubscribed.”
- Resend webhook `POST /public/communications/webhooks/resend` for bounce/complaint; Svix verify when `Resend:WebhookSecret` set; **fail-closed in non-Development if secret empty**; org attribution via Resend tag `org`.
- Anonymize → `ANONYMIZED` suppression so mail cannot restart after GDPR wipe.
- Dispatch + broadcast skip suppressed emails.

**Broken / missing**

- RFC 8058 one-click is advertised (`List-Unsubscribe-Post: List-Unsubscribe=One-Click`) but **there is no POST handler**. Gmail will POST and get 404/405. Broadcasts that grow will fail bulk-sender rules.
- `BuildUnsubscribeUrl` is used by **broadcasts only**. Transactional/dunning correctly omit it (do not let people unsub from receipts). Good.
- No distinction: an unsub from a broadcast also blocks **Official Receipt** and **dunning**. That is a PDPA/CAN-SPAM foot-gun. Stripe does not let you unsub from “your card failed.”
- No admin UI/API to list or lift suppressions.
- Soft vs hard bounce not distinguished (`email.bounced` → always suppress).
- Events without org tag: warn, **do not suppress**.
- `ResendOptions` typed class still has only `ApiKey` / `SenderEmail`. Webhook secret is ad-hoc `IConfiguration["Resend:WebhookSecret"]`.
- WhatsApp STOP / phone suppression: **none**. Irrelevant while WA is off; blocking if 00.4 reopens.

### 9. BYO Resend / SMTP

| Question | Answer |
|----------|--------|
| BYO Resend? | **Yes. Required.** Checkout, create product, update product all call `HasValidEmailConfigAsync`. Ops dashboard banner if missing. |
| Key storage | AES via `Kms:MasterKey`; GET masked. |
| Key validation | `GET https://api.resend.com/domains` with tenant bearer. Failure → business rule. Does not assert sender domain ∈ listed domains. |
| Platform fallback for tenants | **Forbidden.** Correct for deliverability / abuse. Brutal for “first link before DNS.” |
| Platform Resend | System tenant only (password reset, verify). Invite is org-scoped — **new staff invite fails without BYOK.** |
| SMTP / SendGrid / SES / Mailgun | **No.** `plans/005-remaining/04-bb-email-messaging-move.md` says the port encodes Resend BYOK on purpose. |
| Reply-to / display name | **No.** `from` is the raw sender email. |
| Attachments / ICS / PDF | **No.** |
| Idempotency-Key / batch / scheduled | **No.** |
| Console email | Implemented, **not registered**. |

### 10. Localization BM / EN

There is **no** locale on templates, customers, workspaces, or dispatch.

- All catalog copy is informal English (“Hey {{customer_name}}”, “Quick heads up”).
- No `ms-MY` / `en-MY` column, no BM seed, no per-recipient language.
- `locale` hits in the repo are date-picker `date-fns` props, not product i18n.
- Meta templates are **language-coded** (`en`, `ms`, `en_US`). When WA exists we will need at least `en` + `ms` approved pairs.
- Stripe uses customer timezone for renewal copy. We use UTC date diffs.

Malaysia CaaS without BM is a real sales gap against Billplz/HitPay support surfaces, but it is not a Meta-policy bug until we send WA. Do not fake BM with a Google-translated second body in the same row without a locale field.

### 11. Delivery logs

**Exist**

- Table `messaging.MessageDeliveryLogs` (migration `20260804030120_AddMessageDeliveryLogs`)
- Columns: org, channel, recipient, status (`SENT`/`FAILED`/`SKIPPED`), `ProviderMessageId`, error (2k), `CorrelationEventId`, created_at
- Indexes: `(OrganizationId, CreatedAt)`, `CorrelationEventId`
- `GET /messaging/delivery-logs?limit=` (1–200, default 50), OrgAdmin
- Written on every dispatch attempt including WA-disabled skips

**Do not exist**

- TypeSpec (messaging `models.tsp` is intentionally empty: “HTTP notify/logs are not TypeSpec-documented”)
- Ops UI — `DeliveryLogsPage.tsx` is **One webhook** logs
- Open / click / delivered / deferred from Resend
- Bounce linkage back to this row (webhook writes SuppressionEntry, not an update to the log)
- Template name / dunning step id / broadcast id on the row
- Export, filter by channel/status, per-subscriber timeline

Stripe’s 60-day typed log is the bar. We have an internal table. Support cannot use it without curl.

### 12. Credit metering

| Action | Config | Used? |
|--------|--------|-------|
| `WhatsAppSend` | 2 | Yes, but only if flag on and `IMessagingService` called |
| `LhdnSubmit` | 3 | Yes (other module) |
| `EmailSend` | unset (defaults to 1 if ever called) | **No** |
| `BroadcastEmailPerRecipient` | unset | **No** (broadcasts free) |
| Starter grant | 50 | Yes on tenant create |

README still says prepaid credits monetize “WhatsApp dunning messages.” With the flag off, **zero WA credits will ever deduct** on a default deploy. Do not sell credit packs for WhatsApp until 00.4 reopens.

If the flag is flipped without a real adapter: we charge 2 credits for a console line. That is a billing incident waiting to happen. Guard deduct on “provider != console” before any flag experiment.

### 13. Frontend (ops)

| Page | Honesty |
|------|---------|
| **Notification Templates** | Lists EMAIL/ALL only. Dual-tab editor + live preview. Reset calls DELETE (maps to restore). Create forces channel ALL + a tiny variable set. WhatsApp tab is an editor for a channel that does not send. |
| **Email Provider** | Resend-only. Custom-domain warning is correct. Masked key UX is correct. No domain-status readout from Resend. |
| **Dunning step editor** | Best honesty in the product (“not connected”). Preview reuses Communications preview mocks (Ahmad Firdaus / Founders Mastermind) — not the real subscriber. |
| **Dashboard email banner** | Correctly blocks mental model: no BYOK, no checkout. |
| **Developer → Logs** | **Wrong noun.** Webhooks, not messages. |
| **Broadcasts** | No page. |

Portal buyer UI: no email preference center, no “resend receipt,” no locale.

### 14. Tests (better than the gap doc; still not an e2e send)

Communications: `BroadcastTests`, `BroadcastClaimTests`, `DefaultMessageTemplatesTests`, `DocumentPublishedIntegrationEventHandlerTests`, `DunningTemplateVariableSubstitutionTests`, `SuppressionEntryTests`, `TenantEmailConfigurationTests`.

Messaging: `DispatchMessageIntegrationEventHandlerTests` (email wrap + WA skip), `MessageDeliveryLogTests`, `ResendEmailServiceTests` (org tag, List-Unsubscribe, no fallback, system-tenant empty key), `EmailTemplateBuilderTests`, `ConsoleMessagingServiceTests`, authz.

**Still missing:** Resend webhook → suppress integration; RFC 8058 POST; broadcast consent vs count; lifecycle handler’s dead URL; digital-delivery missing token; a test that `WhatsAppEnabled=true` does **not** deduct credits against console; TypeSpec honesty for `/messaging/delivery-logs`.

### 15. TypeSpec / public contracts

`admin-routes.tsp` now includes broadcasts preview/status and email-config. Still omitted: `DELETE /templates/legacy-cleanup`, public unsubscribe, Resend webhook, delivery logs, suppressions admin, WhatsApp config.

Public compliance routes are **intentionally** undocumented in TypeSpec (same pattern as Messaging). Fine if Scalar is not how Gmail discovers the POST URL; still need the POST to exist.

### 16. File-by-file notes (delta vs `docs/001-gaps/08`)

| File | 08 said | 2026-08-16 |
|------|---------|------------|
| `MessageTemplate.RestoreFromDefault` | reset blanks | **Fixed** — catalog restore |
| `TenantEmailConfiguration` | plaintext + full key on GET | **Fixed** — encrypted + hint |
| `DefaultMessageTemplates` | large Community/cart seed | **Fixed** — 5 live + orphan list |
| `FulfillmentRequested…` | no plan_name; portal not magic | **Mostly fixed** — vars + token; `renewal_link` still not the update-payment URL |
| `OrderCompletedDigitalDeliveryHandler` | missing | **Exists** — weak fulfillment URL |
| `ClientProfileAnonymized…` | missing | **Exists** |
| `MessageDeliveryLog` + GET | missing | **Exists** — no UI |
| `Dispatch…` silent WA skip | silent | **Better** — log + throw on pure-WA credit fail; flag skip is explicit |
| `ConsoleMessagingService` | blocks prod WA | **Still true** + flag default false |
| `BuildUnsubscribeUrl` unused | unused | **Used on broadcasts**; POST one-click still missing |
| `LifecycleEventHandlers` | legacy names | **Still thin / dead URL** |
| `EmailTemplateBuilder` `\n`→`<br/>` on HTML | risk | **Unchanged** |
| Unique template name | missing | **Still missing** |
| WA phone suppression | missing | **Still missing** |

---

## Honesty on WhatsApp

This section is the whole point. Short answers first; then the mechanism.

### Is WhatsApp actually sending?

**No.**

Evidence, stacked so a future PR cannot “fix” one layer and claim the product:

1. **Feature flag off.** `appsettings.json` → `"Messaging": { "WhatsAppEnabled": false }`. Handler short-circuits before the port. Tests lock this default.
2. **Adapter is a logger.** `ConsoleMessagingService.SendMessageAsync` writes `[Local Dispatch] [MESSAGING/SMS]` and returns. Registered as the singleton `IMessagingService` in `AddMessagingModule`. There is no `MetaCloudMessagingService`, no Graph client, no `phone_number_id`, no access token store, no WABA table.
3. **Engine refuses to pretend.** `ResolveEffectiveCommunicationAction` demotes or skips WA steps. Default +3 step has **no email body**, so it is skipped.
4. **Documented freeze.** Messaging README §1, decisions.md §00.4, phase-17-done: “Console WhatsApp is not a production channel.”
5. **Ops UI admits it.** “Send WhatsApp (not connected).”
6. **DocumentPublished forces `ToPhone: null`.** Even a future live adapter would not send receipt WA without another change.
7. **Test reminder** would “send” WA to `+60123456789` only if the flag were on — still console.

README hero diagram (`Failure ───▶ WhatsApp Smart Dunning`) and Phase 1 bullet “Native WhatsApp Dunning: Meta Cloud API” are **marketing lies** sitting under a watermark that already tells the truth. Fix the diagram, not the watermark.

Flipping the flag without a Meta client would: skip fewer steps, write `SENT` to the delivery log, deduct 2 credits, and **still not reach WhatsApp**. That is worse than today.

### Credit metering?

**Implemented for a channel that does not send. Not implemented for the channel that does.**

- WA: cost 2, check-then-send-then-deduct, hold bypass, throw on pure-WA insufficient credits. Race: deduct after “success”; console success is free money in reverse (we charge for nothing) if flag on.
- Email: `EmailSend` enum unused. Receipts and dunning are **unmetered**. That is the right CaaS default (do not tax the merchant for “your customer paid you”).
- Broadcasts: free; preview zeros.
- README prepaid-wallet story that monetizes WA dunning is **aspirational**. Do not sell WA credit packs.

### Template variables?

**Partially real, inconsistently applied, over-advertised.**

- Dunning hydrate: the best path; tested.
- Document mail: three tags.
- Digital delivery: five tags, two of them lies (`fulfillment_url`, `portal_magic_link`).
- Lifecycle: two tags, one of them a dead URL.
- Wiki: Community leftovers (`meeting_link`, `group_link`) and a `current_period_end` that no handler fills.
- Create validates; update does not.
- Preview mocks always succeed, so merchants cannot see unfilled tags.

A merchant can save `{{plan_name}}` in a dunning step and it will fill. The same merchant can save `{{plan_name}}` on the Payment Failed template and the suspend email will say the literal characters `{{plan_name}}`.

### Suppression?

**Email: real, blunt, and almost RFC-compliant. WhatsApp: none.**

- Unsub / bounce / complaint / anonymize → skip email.
- Transactional and marketing share one list. Unsub from a holiday broadcast can block dunning and receipts. That is the opposite of Stripe.
- List-Unsubscribe **header** on broadcasts; **GET** landing page; **POST one-click missing**.
- No suppression admin.
- No WA opt-out, no STOP webhook, no phone uniqueness. Acceptable under 00.4. Must be P0 on any 00.4 reopen — Meta will ban a WABA that ignores STOP.

### BYO Resend / SMTP?

**BYO Resend: yes, mandatory, encrypted, no platform fallback.**  
**BYO SMTP: no, and should stay no unless a bank-tenant cannot use Resend.**

SMTP would duplicate BYOK policy (verify domain, store secret, tag org, map bounces) for a worse API. Chargebee’s SMTP option exists because their ICP includes enterprises with ironclad relays. Ours is a creator/B2B CaaS on Resend. If we add a second provider, make it a second `IEmailService` implementation, not a generic SMTP grab-bag, and still require a verified domain.

### Utility vs marketing (Meta policy) — what we would be if we shipped tomorrow

| Our artifact | Meta category if sent as a template | Legal to send as free-form outside 24h? |
|--------------|-------------------------------------|----------------------------------------|
| Official Receipt / Quotation link | Utility | No |
| Payment failed / update payment | Utility (if no promo language) | No |
| Subscription cancelled | Utility | No |
| Digital delivery / download | Utility | No |
| Default +3 “restore access” | Utility | No |
| Abandoned cart 12h/24h | **Marketing** | No |
| `POST /broadcasts` | **Marketing** | No |
| One password reset / OTP | Authentication (MY may be auth-international) | No |
| “Win back with 10% off” | **Marketing** | No |

ADR 021 already refuses the marketing rows. 00.4 refuses the utility rows **for six months**. When we reopen, we implement **utility templates only** (receipt, failed, cancel, maybe delivery) with a URL button to `update_payment_link`. We do **not** implement marketing templates, broadcasts-on-WA, or abandoned cart.

Interactive “Pay RM50 via FPX” (ADR 020) is a utility template **with a URL button**, not a session message, not a marketing catalog, and not a WhatsApp Pay raffle. WATI “WhatsApp Payments” is a different product.

### Localization BM / EN

**None.** All EN. Meta will require `language` per template; BM (`ms`) should be a paired approval when 00.4 reopens, not a second textarea on the same English row. Until then, do not advertise BM notifications.

### Delivery logs

**Backend partial, product absent.** We can answer “did Resend accept this email?” for the last N sends if an engineer curls `/messaging/delivery-logs`. We cannot answer “did they open it,” “did it bounce after accept,” or “show me Aisha’s thread.” Ops users think Developer → Logs is this feature. It is not.

### Marketing claims vs code (single table)

| Claim | Where | Truth |
|-------|-------|-------|
| “automated WhatsApp dunning” | README L6 | **False** (watermark on L18 is the truth) |
| Architecture diagram Failure → WhatsApp | README L36–37 | **False** |
| “Native WhatsApp Dunning: Meta Cloud API” | README Phase 1 | **False** — Phase D / 00.4 reopen |
| Prepaid credits for WA dunning | README L65 | **Code exists; channel off** |
| ADR 023 compete on “Billplz + Automated WhatsApp Dunning” | ADR 023 trade-off | **Strategy; not shipped** |
| Ops “WhatsApp Version” tab | Templates editor | Edits a string that is not sent |
| Default campaign +3 WhatsApp | Commerce seed | Skipped |
| Variable wiki 24h magic link | Query service | Only dunning hydrate |
| “Reset to system defaults” | Templates UI | **Now true** for catalog names |
| Email provider “receipts, dunning, broadcasts” | EmailSettingsPage blurb | Receipts + dunning email **if BYOK**; broadcasts API-only |

---

## Gap table

Priority: **P0** = honesty / money / legal; **P1** = CaaS table-stakes vs Stripe/HitPay/Chargebee; **P2** = after 00.4 or after P1; **Refuse** = do not implement.

| ID | Gap | Competitor that has it | Ours now | Pri | Verdict |
|----|-----|------------------------|----------|-----|---------|
| G01 | Immediate failed-payment email on `GatewayPaymentFailed` (not wait for day-offset) | Stripe, Chargebee, HitPay | PAST_DUE + webhook only | P0 | Implement (email) |
| G02 | Default +3 WhatsApp step is a no-op and is **recorded as dispatched** (cannot catch up when WA ships) | n/a | skip + log | P0 | Change default seed to EMAIL; do not mark skipped WA as dispatched |
| G03 | Marketing / README still draw Meta dunning as live | HitPay is honest (“paste into WA”) | watermark vs hero conflict | P0 | Edit README diagram + Phase 1 bullet |
| G04 | Lifecycle Payment Failed dead URL + unfilled `{{plan_name}}` | Stripe hosted recovery | hardcoded host | P0 | Delete coupling or reuse dunning hydrator |
| G05 | Unsub list blocks transactional / dunning | Stripe separates | one list | P0 | Category on `SuppressionEntry` (`TRANSACTIONAL` vs `MARKETING`) |
| G06 | RFC 8058 POST one-click missing | Resend docs / Gmail | GET only | P0 | Same URL accepts POST 200 |
| G07 | Invite / org mail requires BYOK; first-run chicken-and-egg | Stripe sends from themselves until domain verified | checkout + invite blocked | P1 | Keep gate for checkout; allow platform-from for **invite only** *or* document the order of operations in onboarding |
| G08 | No merchant welcome on `SubscriptionActivated` | Chargebee | webhook only | P1 | One catalog template + handler |
| G09 | Receipt email is link-only; no amount; B2B silent; Billplz double-send | Stripe PDF + HitPay receipt | Official Receipt PARTIAL | P1 | Put amount/plan in subject; optional “Billplz already mailed” toggle later |
| G10 | Digital delivery claims R2 / magic link, sends portal | Chargebee fulfillment | handler exists, weak | P1 | Generate token; don’t send if product has no asset |
| G11 | `current_period_end` advertised, never filled | Stripe TZ renewal | wiki lie | P1 | Fill from `Subscription.NextBillingDate` or remove from wiki |
| G12 | No card-expiry email | Stripe −1 month; Chargebee retention | none | P1 | Later; needs Payments PM expiry on file |
| G13 | No renewal TZ; UTC day math | Stripe customer TZ | UTC | P2 | After we store a TZ |
| G14 | No refund / credit-note email | Stripe, Chargebee | none | P1 | Template + `GatewayRefundCompleted` |
| G15 | No trial-ending email | Stripe, Chargebee | no trial comms | P2 | Only if we sell trials |
| G16 | Delivery log not in ops / TypeSpec | Stripe 60-day log | table + raw GET | P1 | Surface SENT/FAILED/SKIPPED in ops; do not promise opens |
| G17 | Dual CMS (steps vs templates) | Chargebee one library | both | P1 | Dunning steps remain source of truth; hide unused lifecycle names or deep-link |
| G18 | Template update skips variable validation; no unique name | Chargebee | weak | P2 | Hygiene |
| G19 | `EmailTemplateBuilder` `<br/>`-escapes HTML | — | noisy mail | P2 | Wrap Markdown output only |
| G20 | Broadcast API + agent prompts (marketing vitamin) | Customer.io / WATI | API, no UI | Refuse | No Broadcasts UI; consider 410 |
| G21 | Abandoned cart / Community welcome | WATI marketing | orphan list | Refuse | Keep deleted |
| G22 | Meta Cloud / interactive FPX button | ADR 020 wishlist; WATI payments ≠ this | stub + freeze | Frozen | Reopen 00.4; utility templates only |
| G23 | WA credits charged on console success | — | deduct-after-send | P0 *if* flag flipped | Guard provider |
| G24 | Phone suppression / STOP | Meta policy | none | P0 *on* 00.4 reopen | With the adapter, not before |
| G25 | Meta template name/lang/category store | Meta / Twilio | free-form body | Frozen | Part of 00.4 reopen |
| G26 | E.164 normalize | Twilio lookup | pass-through | Frozen | With adapter |
| G27 | Tenant WABA / token BYOK | WATI / respond.io | none | Frozen | Do not build a BSP |
| G28 | SMS channel | Billplz paid SMS; Twilio | none | Refuse / Later | Not MY-primary |
| G29 | Shared team inbox / chatbot / CTWA | WATI, respond.io | none | Refuse | Headless webhooks only |
| G30 | Customer.io journeys in-product | Customer.io | outbound webhooks | Refuse | Tell tenants to subscribe webhooks |
| G31 | SMTP BYOK | Chargebee | Resend only | Later / probably never | Second `IEmailService` only if a tenant is blocked on Resend |
| G32 | Sender domain membership check | Resend dashboard | key valid ≠ from valid | P1 | After `GET domains`, require sender host match |
| G33 | Reply-to / display name | Stripe branding | from=email | P2 | |
| G34 | Locale BM + EN | Meta lang; MY support expectation | EN only | P2 | After P1 transactional set is honest |
| G35 | Admin suppression CRUD | Resend / SendGrid UI | none | P1 | List + lift |
| G36 | Soft vs hard bounce | Resend event types | all suppress | P2 | |
| G37 | Resend delivered/opened → log | Resend webhooks | ignored | P2 | Optional; don’t build a ESP |
| G38 | Attach PDF receipt | Stripe | signed link | Later | Link is OK if it works |
| G39 | 3DS / confirm-payment email | Stripe | none | P2 | Only if we take SCA off-session on cards |
| G40 | Merchant push “you got paid” | HitPay app | none | Later | Not Communications; maybe Ops |
| G41 | Coordinate Billplz’s own receipt | Billplz settings | double mail | P2 | Docs + optional suppress-ours |
| G42 | Broadcast count vs consent mismatch | — | inflated TotalRecipients | P1 *if* API stays | Count the same filter as fan-out |
| G43 | CreditHoldId = broadcast.Id | — | semantic lie | P2 | Null it; broadcasts are free |
| G44 | Test reminder hardcoded recipients | Stripe sandbox rules | `admin@lazuars.io` | P2 | Send to the signed-in admin |
| G45 | `IMessagingService` too narrow | Twilio Content API | `(to, text)` | Frozen | Redesign **with** 00.4, not before |
| G46 | TypeSpec missing public compliance + delivery logs | our own honesty-allowlist | drift | P2 | Document or allowlist |
| G47 | No inbox retry narrative for Resend 429 | Resend | throw → processed? | P1 | Confirm inbox retry/DLQ actually re-sends (outbox/inbox retry migration exists; verify poison path) |
| G48 | Portal magic link not used on digital delivery / welcome | Chargebee login link | missing | P1 | Call `IMagicLinkTokenService` |
| G49 | `renewal_link` ≠ `update_payment_link` | Stripe one recovery URL | two URLs, worse one in `renewal_link` | P1 | Point both at update-payment |
| G50 | Selling WA credit packs while flag off | our README | unused | P0 | Stop until 00.4 |

---

## Tracker IDs

Promotion rule (`20-sequencing-and-tracker-schema.md`): these rows are **proposed** for a Lazuar Pay communications family. They are **not** a commit to ship. Do not collide with Aura `CM-*` (passes) or `MG-*` (salon messaging). Aura `MG-010` (Meta Cloud + credits = **Never / killed**) is an **Aura** lock from 018/019. Lazuar Pay **explicitly keeps** the *job* of WhatsApp **utility dunning** (ADR 021) and **freezes the implementation** (00.4). That is why Pay IDs are `COM-*`, not a reuse of `MG-010`.

**V** = Ours / Theirs / Both / Partial / Later / Never / N/A  
**W** = suggested wave on the Pay track (0 = honesty/soak, 1 = transactional completeness, 2 = dunning polish, 7+ = frozen WA, — = no wave)  
**Class** = table-stakes / differentiator / hygiene / trap / frozen

| ID | Feature | Depth now | V | W | P | Class | Why / evidence |
|----|---------|-----------|---|--:|--:|-------|----------------|
| COM-001 | Official receipt email (paid) | partial | Partial | 1 | 0 | table-stakes | Billing B2C PDF → template. Missing amount/B2B/double-Billplz. Maps conceptually to Aura `MG-001` but is a **different product**. |
| COM-002 | Immediate failed-payment email | none | Later | 1 | 0 | table-stakes | Stripe/HitPay send on decline. We wait for dunning offsets. |
| COM-003 | Sequenced **email** dunning | partial | Both | 1 | 1 | differentiator | Campaign builder + hydrate + Resend is real. Default +3 hole is COM-011. |
| COM-004 | Update-payment link in recovery mail | partial | Partial | 1 | 0 | table-stakes | Hydrator good; lifecycle URL dead; `renewal_link` is portal home. |
| COM-005 | Portal magic link (24h) | partial | Partial | 1 | 1 | table-stakes | Service exists; only dunning hydrate uses it. Wiki overclaims. |
| COM-006 | One auth mail (reset / verify / invite) | shipped | Both | 0 | 2 | table-stakes | LIVE via platform or tenant Resend. Invite needs BYOK. |
| COM-007 | Merchant welcome / activation email | none | Later | 1 | 1 | table-stakes | Chargebee-shaped. `SubscriptionActivated` is webhook-only. |
| COM-008 | Digital / fulfillment email | partial | Partial | 1 | 2 | table-stakes | Handler exists; URL is portal; `plan_name` = “your purchase.” |
| COM-009 | Quotation-ready email | shipped | Both | 1 | 3 | table-stakes | DocumentPublished path. Hidden if quote UI stays lobotomized (ADR 023). |
| COM-010 | Cancel / end email | partial | Partial | 1 | 2 | table-stakes | Template + event exist; variables thin. |
| COM-011 | Default campaign honesty (no dead WA step) | stub | Partial | 0 | 0 | hygiene | Seed +3 WA without email is a recorded no-op. |
| COM-012 | BYOK Resend (required) | shipped | Ours | 0 | 1 | differentiator | Stricter than Stripe. Encrypt + mask done. Domain membership check still open (G32). |
| COM-013 | SMTP / second ESP | none | Later | — | 3 | later-nice | Chargebee has it. Do not build unless a tenant is blocked. |
| COM-014 | Email suppression (bounce/complaint/unsub) | partial | Partial | 1 | 0 | hygiene | Real inserts; category split + POST one-click + admin list missing. |
| COM-015 | Marketing vs transactional suppression split | none | Later | 1 | 0 | hygiene | P0 legal. Unsub must not kill receipts. |
| COM-016 | RFC 8058 one-click POST | none | Later | 1 | 0 | hygiene | Headers already claim it. |
| COM-017 | Delivery log in ops | partial | Later | 1 | 1 | hygiene | Table lives; UI is the wrong page. |
| COM-018 | Template variable honesty | partial | Partial | 1 | 1 | hygiene | Fill or delete wiki tags; validate updates. |
| COM-019 | Single dunning copy source | partial | Later | 2 | 2 | hygiene | Keep steps; stop lifecycle-template fork. |
| COM-020 | Card-expiry email | none | Later | 2 | 3 | later-nice | Stripe/Chargebee. Needs PM metadata. |
| COM-021 | Refund / credit-note email | none | Later | 2 | 2 | table-stakes | After refund domain honesty. |
| COM-022 | Trial-ending email | none | Later | — | 3 | later-nice | Only if we sell trials. |
| COM-023 | Locale BM/EN on templates | none | Later | 8 | 3 | later-nice | After EN transactional set is true. |
| COM-024 | Merchant “you got paid” push | none | Later | 10 | 3 | later-nice | HitPay app. Not a Communications module job. |
| COM-025 | Broadcasts UI / segments / blasts | stub API | Never | — | — | trap | ADR 021 vitamin. Aura `MG-009` analog. |
| COM-026 | Abandoned-cart automation | orphan | Never | — | — | trap | Meta marketing + ADR 021. |
| COM-027 | Customer.io-like journeys | none | Never | — | — | trap | Use outbound webhooks. |
| COM-028 | WATI/respond.io inbox / chatbot / CTWA | none | Never | — | — | trap | We are not a BSP. |
| COM-029 | SMS receipts / SMS dunning | none | Never | — | — | trap | Billplz already sells SMS. MY channel is WA or email. |
| COM-030 | WhatsApp **marketing** templates / blasts | none | Never | — | — | trap | Meta + ADR 021. Not Aura `MG-010` (that killed *all* Meta); this kills **marketing** only. |
| COM-031 | WhatsApp **utility** dunning (Meta Cloud) | stub | Later | 7 | 0 | frozen-differentiator | ADR 021 keep; 00.4 freeze until ~2027-02. Requires new port, templates, buttons, STOP, E.164, credits guard. |
| COM-032 | Interactive “Pay via FPX” WA button | none | Later | 7 | 1 | frozen-differentiator | ADR 020. Same freeze as COM-031. |
| COM-033 | WA credit packs sold to tenants | config only | Never* | — | — | trap *until* COM-031 | Do not monetize a console stub. Revisit only with a real provider. |
| COM-034 | Phone / WA suppression | none | Later | 7 | 0 | frozen | Ships with COM-031. |
| COM-035 | Tenant WABA BYOK (become a BSP) | none | Never | — | — | trap | If we ever send WA, **platform** WABA first. Multi-tenant BYOK WABA is WATI. |

\*COM-033 is Never **while COM-031 is stub**. It does not become a credit-SKU brainstorm.

### Suggested sequence (Pay communications only)

1. **Wave 0 (honesty, this week, no Meta):** COM-011 default campaign; COM-003/G03 README diagram; stop implying WA credits (COM-033). Confirm inbox retry on Resend throw (G47).
2. **Wave 1 (transactional bar vs Stripe/HitPay):** COM-002 immediate fail mail; COM-001 receipt subject/amount; COM-007 welcome; COM-004/005/048/049 link honesty; COM-014–017 suppression + one-click + delivery log UI; COM-006 invite onboarding order.
3. **Wave 2:** COM-019 single copy; COM-020/021 card expiry + refund mail.
4. **Wave 7 (only if 00.4 reopened with budget):** COM-031 utility templates + COM-032 button + COM-034 STOP. Still Never: COM-025–030, COM-035.

### Mapping to Aura tracker (do not merge the products)

| Aura row | Pay row | Relationship |
|----------|---------|--------------|
| `MG-001` online receipt | `COM-001` | Same *noun*, different money plane (salon booking vs CaaS ledger). |
| `MG-009` campaigns/automations | `COM-025` | Both **Never**. |
| `MG-010` Meta Cloud + credits | `COM-030` + `COM-031` + `COM-033` | Aura killed the whole Meta product. Pay **splits** marketing (Never) from utility dunning (Later/frozen). |
| `MG-004` `wa.me` | — | Pay is not a staff inbox. Buyer WhatsApp is COM-031 or human paste (Billplz/HitPay style). |

### Anti-goals (repeat until it sticks)

- Do not reopen 00.4 as a “cleanup PR.”
- Do not build WATI.
- Do not put unsubscribe on receipts.
- Do not charge credits for `ConsoleMessagingService`.
- Do not seed abandoned cart again.
- Do not tell a merchant WhatsApp is connected because the editor has a WhatsApp tab.

---

*End of uncondensed analysis. Source of truth is the live tree under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` as of 2026-08-16, not `docs/001-gaps/08-communications-module.md`.*
