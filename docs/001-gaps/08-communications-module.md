<!-- Source subagent: 019fc650-3512-7283-86ea-56608b7c5d7a -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Communications Module Gap Analysis

**Scope:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Communications/`  
**Related:** `Modules/Messaging/`, BuildingBlocks email/messaging services, Commerce dunning/fulfillment, ops-page templates/email settings, TypeSpec contracts.

**Architecture intent (as implemented):**  
- **Communications** = templates, suppressions, tenant email BYOK, broadcasts, event→render orchestration.  
- **Messaging** = “dumb pipe” terminal sink: `DispatchMessageIntegrationEvent` → Resend / `IMessagingService`.

---

## Module Inventory

### Communications layers (46 files under module)

| Layer | Contents |
|--------|----------|
| **Domain** | `MessageTemplate`, `SuppressionEntry`, `Broadcast`, `TenantEmailConfiguration` |
| **Application** | Command handlers (templates, broadcast, email config), `ICommunicationsRepository`, `ICommunicationsQueryService` |
| **Contracts** | Commands, `ISuppressionService`, broadcast DTOs, `DefaultTemplatesSeededIntegrationEvent` |
| **Infrastructure** | EF `communications` schema, endpoints, 4 integration handlers, 3 workers, Dapper query service, suppression service |

### Workers

| Worker | Role |
|--------|------|
| `CommunicationsInboxConsumerJob` | Inbox consumer (module inbox) |
| `CommunicationsOutboxPublisherJob` | Outbox → in-memory event bus |
| `BroadcastFanoutJob` | Polls `QUEUED` broadcasts every 10s, pages subscribers, publishes `DispatchMessageIntegrationEvent` |

### Events consumed (Communications)

| Event | Handler | Outcome |
|-------|---------|---------|
| `AppEntitlementGrantedIntegrationEvent` | `AppEntitlementGranted…` | Seeds default templates for COMMUNITY/COMMERCE/VAULT |
| `FulfillmentRequestedIntegrationEvent` | `FulfillmentRequested…` | Only `reminder.due` / `reminder.dunning` + target `COMMUNICATIONS` |
| `SubscriptionSuspendedIntegrationEvent` | `LifecycleEventHandlers` | “Payment Failed” template → email dispatch |
| `SubscriptionCanceledIntegrationEvent` | `LifecycleEventHandlers` | “Subscription Cancelled” template → email |
| `DocumentPublishedIntegrationEvent` | `DocumentPublished…` | Quotation/receipt templates → dispatch |

### Sibling: Messaging module

- Contracts: `DispatchMessageIntegrationEvent`
- Handler: credit check (WhatsApp), email suppression, Resend BYOK lookup, `IMessagingService.SendMessageAsync`
- DB: `messaging.TenantReplicas` (+ inbox/outbox)
- **No real WhatsApp/SMS provider** — `ConsoleMessagingService` only

### Frontend (ops-page)

- Templates: `TemplatesPage`, `MessageTemplateEditor`
- Email BYOK: `EmailSettingsPage`
- Dunning content: `CampaignBuilderPage` / `DunningStepEditor` (inline EMAIL/WHATSAPP bodies, not Communications templates)
- Checkout gated on email config status

### Tests

- Domain only: `BroadcastTests`, `SuppressionEntryTests`
- No integration tests for dispatch, Resend webhook, dunning→WhatsApp path

---

## Domain Model

### `MessageTemplate`
- Fields: `Name`, `Channel` (`EMAIL` / `WHATSAPP` / `ALL`), `Subject`, `EmailBody`, `WhatsAppBody`, `IsDefault`, JSON lists `RequiredVariables` / `OptionalVariables`
- Mutations: create; `UpdateContent` (clears `IsDefault`); no true “restore system default”
- **Gaps:** no unique `(OrganizationId, Name)`; no versioning; no Meta template name / language / category; no SMS body; no locale; reset blanks content instead of re-seeding defaults

### `SuppressionEntry`
- Email-only, org-scoped, unique `(OrganizationId, Email)`
- Reasons: `UNSUBSCRIBE`, `BOUNCE`, `COMPLAINT`
- **Gaps:** no phone / WhatsApp opt-out; no category (marketing vs transactional); no admin CRUD UI/API; no soft-delete / unsuppress

### `Broadcast`
- Email subject/body, status `DRAFT→QUEUED→SENDING→COMPLETED|FAILED`, counts
- **Gaps:** no channel/WhatsApp body on aggregate; audience filters ignored at fan-out; credit columns removed but DTO still exposes credits as 0; `FailedCount` never incremented on send errors

### `TenantEmailConfiguration`
- Plaintext `ApiKey`, `SenderEmail`, `IsActive`, unique per org
- **Gaps:** no encryption/KMS; API key returned full to clients; no domain verification persistence beyond live Resend `GET domains`; no reply-to / display name; no multi-sender

### Outbox / Inbox
- Standard platform pattern; no delivery-result entity (no provider message IDs, open/click/bounce timeline in-module)

---

## Channel Support (Email, WhatsApp, SMS?)

| Channel | Product claim | Implementation reality |
|---------|---------------|------------------------|
| **Email** | Resend BYOK + platform Resend | **Working path** for tenant email when BYOK active; system tenant uses platform key |
| **WhatsApp** | Meta Cloud API dunning | **Stub only**: `ConsoleMessagingService` logs to console; no Graph API, no WABA, no templates, no interactive buttons |
| **SMS** | Messaging README mentions SMS | **Not a first-class channel**. `IMessagingService` is generic “messaging”; channel enum is EMAIL / WHATSAPP / ALL only |

### Channel routing (`DispatchMessageIntegrationEventHandler`)

- Email if channel is `EMAIL` or `ALL` and email + HTML body present  
- WhatsApp if channel is `WHATSAPP` or `ALL` and phone + plain body present  
- Email suppressed via `ISuppressionService`; WhatsApp **not** suppressed by any list  
- WhatsApp costs credits (`CreditAction.WhatsAppSend`, config default **2**); email **not** credited (`EmailSend` enum exists but unused)  
- Insufficient WhatsApp credits → silent skip (log warning), not durable failure  

### Tenant gating

- Checkout **requires** active Resend BYOK (`HasValidEmailConfigAsync`)  
- No equivalent WhatsApp credential / phone-number-id config  

---

## Template System

### Seeding (`AppEntitlementGranted`, AppId COMMUNITY | COMMERCE | VAULT)

Defaults include:

- Community Welcome, Community Payment Success  
- Digital Product Delivery, Event Ticket Confirmation  
- Payment Failed, Subscription Renewal (3 Days / Due Today / Overdue), Subscription Cancelled  
- Abandoned Cart 12h (WA) / 24h (Email)  
- Generic Receipt, Quotation Ready, Official Receipt  

### Variable catalog (query service)

Documented: customer_*, plan_name, total_price, renewal_link, current_period_end, portal_magic_link, fulfillment_url, meeting_link, group_link  

**Missing from catalog but used in seed/dunning:** `document_link`, `checkout_url`, `item_name`, `update_payment_link`, `customer_email`/`phone` (partially populated in dunning handler only)

### Validation

- Create: regex `{{tag}}` must be in required∪optional; required tags must appear  
- Update: **no** variable re-validation  
- Reset: sets subject/bodies to `""` — **does not** restore seed defaults (UI toast claims “system defaults”)

### Rendering

- Markdown → HTML via Markdig; WhatsApp/plain via `MarkdownParser.ToPlainText`  
- `EmailTemplateBuilder.WrapWithBrandHtml` wraps HTML and also `Replace("\n","<br/>")` on already-parsed HTML (risk of double breaks / noisy HTML)  
- Generic “Powered by Lazuar” footer only — **no** List-Unsubscribe, no injected unsubscribe link  

### Dual ownership of dunning copy

- **Commerce `DunningStep`**: inline `Subject` / `EmailBody` / `WhatsAppBody` (primary path)  
- **Communications templates**: legacy dunning names; cleanup endpoint deletes them; lifecycle still depends on “Payment Failed” / “Subscription Cancelled” by name  

### Dead / unused template surface

| Seeded / supported | Actually dispatched? |
|--------------------|----------------------|
| Community Welcome / Payment Success / Digital Product / Event Ticket | **No** consumer on order/subscription activation |
| Abandoned cart templates | **No** cart abandonment job |
| Generic Receipt | **No** |
| `reminder.due` + `template_id` path | Handler supports it; **nothing publishes** `reminder.due` |
| `GetDefaultTemplateIdsAsync` (renewal names) | **Dead** — never called outside repository |

---

## Delivery Pipeline & Providers

```
Domain event / API
  → Communications (render, variables)
  → Outbox (communications) → InMemoryEventBus
  → Messaging inbox/handler
  → IEmailService (Resend) / IMessagingService (console)
```

### Email (`ResendEmailService`)

- Endpoint: `POST https://api.resend.com/emails`  
- Payload: from, to[], subject, html, tags `[{name:"org", value: orgId}]`  
- Tenant path: BYOK key + sender required; **no platform fallback** for tenant orgs  
- System tenant: platform `Resend:ApiKey` / `SenderEmail`  
- **Missing:** reply-to, attachments, CC/BCC, scheduled send, idempotency key, List-Unsubscribe headers, plain-text part, Resend `reply_to`, batch API, rate-limit backoff  

### WhatsApp / SMS

- Interface: `Task SendMessageAsync(string recipient, string text)`  
- Implementation: console log only  
- **Missing vs Meta Cloud API:** OAuth/permanent token, phone_number_id, WABA, template messages (marketing window), free-form session messages, interactive CTA/URL buttons, media, delivery webhooks (`statuses`), quality rating, 24h session rules  

### BYOK save path

- Validates Resend key via `GET domains`  
- Does **not** verify sender domain ownership of `SenderEmail` specifically  
- Stores API key in plaintext  

---

## Suppression / Opt-out

### What works

- Public `GET /public/communications/unsubscribe?org=&email=&sig=` (HMAC over `Jwt:Secret`)  
- Resend webhook `POST /public/communications/webhooks/resend` for `email.bounced` / `email.complained`  
- Svix-style verification when `Resend:WebhookSecret` set; **open in dev if empty**  
- Fan-out + messaging dispatch skip suppressed emails  

### Critical gaps

1. **`BuildUnsubscribeUrl` is never used** in send path — marketing emails lack unsubscribe links / headers  
2. Suppression is **email-only** — WhatsApp dunning ignores opt-out  
3. No admin list/export/remove-suppression APIs  
4. Soft bounce vs hard bounce not distinguished  
5. Webhook without org tag logs warning and **does not suppress**  
6. `ResendOptions` typed class has no `WebhookSecret` (config key used ad hoc)  
7. Transactional vs marketing opt-out not separated (CAN-SPAM / PDPA risk for broadcasts)  

---

## Integration with Dunning & Fulfillment Events

### Dunning (Commerce → Communications → Messaging)

`DunningEngineJob` (hourly):

1. Pre-due ACTIVE subs (≤14 days): steps with `DayOffset < 0` matching days-until-due  
2. PAST_DUE: assign campaign, fire steps by `DayOffset == daysOverdue`, AUTOCHARGE via Payments, final CANCEL/SUSPEND  
3. Communication steps → `FulfillmentRequestedIntegrationEvent(…, "COMMUNICATIONS", "reminder.dunning", payload)`  

`FulfillmentRequestedIntegrationEventHandler`:

- Loads CRM profile + workspace  
- Populates a **small** variable set (`customer_*`, `business_name`, `renewal_link`, `portal_magic_link`, `update_payment_link`)  
- **Does not** populate plan_name, total_price, current_period_end from product/sub  
- Publishes `DispatchMessageIntegrationEvent` with channel from step  

**Gaps:**

- `portal_magic_link` is a plain portal URL, **not** a magic-link token  
- Hardcoded portal host `https://portal.lazuar.com`  
- Lifecycle suspend handler still uses old “Payment Failed” template + broken renewal URL `…/checkout/update`  
- Dunning templates cleanup vs lifecycle dependency conflict  
- No WhatsApp interactive pay button (roadmap ADR 020) — plain text only even if Meta were wired  
- Credit check can drop WhatsApp silently after Commerce already logged “dispatched”  

### Fulfillment / purchase messaging

| Commerce event | Communications reaction |
|----------------|-------------------------|
| `OrderCompleted` | HTTP webhooks only — **no** internal COMMUNICATIONS / templates |
| `SubscriptionActivated` | Same — webhooks only |
| Digital product / welcome / ticket templates | Seeded only — **orphan** |

This is a **P0 product gap** vs README (“automated WhatsApp dunning” + digital delivery messaging).

### Document messaging

- Billing `DocumentPublished` → Quotation Ready / Official Receipt with signed document link — **does work** (email/WhatsApp channel per template, phone often null → email only)

### System auth email (One)

- Password reset / verify / invite → `DispatchMessageIntegrationEvent` with system or org tenant — bypasses Communications templates  

---

## Endpoints

### Admin (`/admin/communications`, `OrgAdmin`)

| Method | Path | Notes |
|--------|------|--------|
| GET/POST | `/templates` | List / create |
| GET | `/templates/variables` | Static dictionary |
| POST | `/templates/preview` | Mock populate |
| PUT/DELETE | `/templates/{id}` | Update / “reset” (blank) |
| DELETE | `/templates/legacy-cleanup` | Hard-delete legacy dunning names |
| POST | `/reminders/test` | Always targets hardcoded `admin@lazuars.io` + `+60123456789` |
| POST | `/broadcasts` | EMAIL only v1 |
| GET | `/broadcasts/preview` | Cost always 0 |
| GET | `/broadcasts/{id}` | Status |
| GET/PUT | `/email-config` | BYOK; returns raw API key |

### Public

| Method | Path |
|--------|------|
| GET | `/public/communications/unsubscribe` |
| POST | `/public/communications/webhooks/resend` |

### TypeSpec gaps

- `admin-routes.tsp` omits broadcast GET/preview, legacy-cleanup, public compliance routes  
- No WhatsApp config, delivery logs, suppressions admin surface  

---

## Reliability, Retries, Rate Limits

| Concern | Current state |
|---------|----------------|
| Cross-module transport | Outbox + inbox + `FOR UPDATE SKIP LOCKED` |
| Poison messages | Marked processed with Error — **no retry / DLQ** |
| Resend failure | Exception logged; inbox message still “processed” — **lost send** |
| WhatsApp failure | N/A (console) |
| Broadcast fan-out | Per-recipient send counted as success **before** provider delivery; no per-recipient error accounting; no rate limit pacing beyond page size 100 |
| Broadcast concurrency | Only 1-minute “recent broadcast” guard |
| Dunning idempotency | `ReminderDispatchLog` unique on step+billing date — good |
| Provider rate limits | None |
| Idempotent provider keys | None |
| Delivery observability | Logs only; no `MessageDelivery` aggregate |
| Credit deduction race | Deduct after send; failure after successful WA → free message |

---

## Gaps vs Meta Cloud API / Multi-channel Needs

| Capability | Needed for product story | Status |
|------------|--------------------------|--------|
| Meta Cloud API client | WA dunning | **Missing** (console stub) |
| Template message registration / sync | Outside 24h window | **Missing** (free-form bodies only) |
| Interactive URL buttons (“Pay now”) | ADR 020 | **Missing** |
| Delivery / read receipts webhooks | Reliability & billing | **Missing** |
| Phone normalization (E.164 MY) | Asia WA | **Missing** (pass-through) |
| Tenant WABA / token storage | Multi-tenant WA | **Missing** |
| SMS provider (Twilio etc.) | Multi-channel | **Missing** |
| Channel preference on customer | Prefer WA vs email | **Missing** |
| Unified delivery log / status API | Ops UI | **Missing** |
| Marketing vs utility message classification | Meta policy + credits | **Missing** |
| Abandoned cart automation | Seeded templates | **Missing** job |
| Purchase / welcome fulfillment messaging | Seeded templates | **Missing** dispatch |
| Real magic links in templates | Variable docs claim 24h | **Not generated** in handlers |
| CAN-SPAM unsubscribe on every commercial email | Compliance | Helper exists, **not wired** |
| Phone-level suppression | WA opt-out | **Missing** |

---

## Recommendations

### P0 — Ship truth vs marketing claims

1. **Implement real WhatsApp provider** (`MetaCloudMessagingService` implementing `IMessagingService` or richer interface) with phone_number_id, access token, template vs session send modes.  
2. **Wire purchase/activation fulfillment** in Communications: on `OrderCompleted` / `SubscriptionActivated` (or dedicated fulfillment events) render Welcome / Digital Product / Ticket templates and dispatch — or stop seeding unused templates.  
3. **Wire List-Unsubscribe + `BuildUnsubscribeUrl`** into `ResendEmailService` / brand wrapper for marketing + broadcasts.  
4. **Stop silent WhatsApp credit failure** — surface failed delivery to dunning logs / tenant ops.  
5. **Encrypt Resend API keys**; never return full key on GET (mask).  

### P1 — Coherent domain

6. **Single source of truth for dunning copy:** either DunningStep-only (delete lifecycle template coupling) or templates-by-id referenced from steps.  
7. **Fix “reset template”** to re-copy from seed definitions.  
8. **Implement or delete** `reminder.due`, abandoned cart, `GetDefaultTemplateIdsAsync`.  
9. **MessageDeliveryLog** (org, channel, to, template/step id, provider id, status, error, cost).  
10. **Phone suppression** + Meta STOP / opt-out webhooks.  
11. **Config-driven portal base URL**; generate real portal magic links.  
12. Populate full variables in dunning (plan_name, price, period end).  

### P2 — Multi-channel maturity

13. Meta **approved templates** store + mapping to Lazuar template bodies.  
14. Interactive CTA buttons with update-payment / checkout deep links.  
15. Broadcast audience filters (`target_plan_id`, status) already in command contract — implement in fan-out.  
16. Optional SMS channel + provider abstraction.  
17. Inbox retry with exponential backoff; DLQ for failed sends.  
18. Resend webhook: delivered/opened metrics optional; hard vs soft bounce.  
19. Integration tests for dunning EMAIL/WA path and Resend webhook → suppress.  
20. Align TypeSpec with actual endpoints; document public compliance routes.  

### Architectural cleanup

21. Rename migration `RemoveBroadcasts` (only drops credit columns) or actually remove broadcast feature if abandoned.  
22. Stop reusing `CreditHoldId` to pass broadcast Id (semantic lie).  
23. Messaging README still mentions Community/Vault ownership of templates — update to Communications.  
24. Fix `EmailTemplateBuilder` so it does not `<br/>`-escape already-rendered HTML.  

---

## File-by-File Notes

### Domain

| File | Notes |
|------|--------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Communications/Domain/Aggregates/MessageTemplate.cs` | Dual-body model; weak lifecycle; no Meta fields |
| `…/SuppressionEntry.cs` | Email-only suppression; solid uniqueness model |
| `…/Broadcast.cs` | Email-only aggregate; FailedCount unused |
| `…/TenantEmailConfiguration.cs` | Plaintext secrets; minimal fields |

### Contracts

| File | Notes |
|------|--------|
| `…/Contracts/ISuppressionService.cs` | Cross-module suppression API |
| `…/Contracts/Commands/MessageTemplateCommands.cs` | Create/Update/Reset/Test |
| `…/Contracts/Commands/BroadcastCommands.cs` | Filters in DTO unused by handler |
| `…/Contracts/BroadcastDtos.cs` | Credits fields zombie after free broadcasts |
| `…/Contracts/Events/DefaultTemplatesSeededIntegrationEvent.cs` | Published; no known consumers found |

### Application

| File | Notes |
|------|--------|
| `…/Commands/MessageTemplateCommandHandlers.cs` | Strong create validation; weak update/reset; test sends hardcoded recipients |
| `…/Commands/BroadcastCommandHandlers.cs` | EMAIL-only; 1-min throttle; ignores audience filters |
| `…/Commands/SaveEmailConfigCommand.cs` | Resend domains check; no sender ownership check; blocks system tenant |
| `…/ICommunicationsRepository.cs` | Templates, broadcasts, email config |
| `…/Queries/ICommunicationsQueryService.cs` | Includes `HasValidEmailConfigAsync` for Commerce gate |

### Infrastructure — endpoints

| File | Notes |
|------|--------|
| `…/Endpoints.cs` | Maps templates, broadcasts, email-config, public compliance |
| `…/Endpoints/TemplateEndpoints.cs` | Preview mocks; legacy cleanup; test reminder |
| `…/Endpoints/BroadcastEndpoints.cs` | Status/preview; credits zeroed |
| `…/Endpoints/PublicComplianceEndpoints.cs` | Unsubscribe + Resend webhook; `BuildUnsubscribeUrl` unused elsewhere |

### Infrastructure — handlers

| File | Notes |
|------|--------|
| `…/EventHandlers/AppEntitlementGrantedIntegrationEventHandler.cs` | Large seed set; many orphan templates |
| `…/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` | Live dunning path; thin variables; fixed portal host |
| `…/EventHandlers/LifecycleEventHandlers.cs` | Legacy template names; incomplete variables; WA body null |
| `…/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` | Cross-schema Dapper join; solid document link signing |

### Infrastructure — workers / data

| File | Notes |
|------|--------|
| `…/Workers/BroadcastFanoutJob.cs` | Suppresses email; EMAIL-only; CreditHoldId = broadcast.Id misuse |
| `…/Workers/CommunicationsInboxConsumerJob.cs` | Generic inbox |
| `…/Workers/CommunicationsOutboxPublisherJob.cs` | Generic outbox |
| `…/Services/SuppressionService.cs` | Idempotent insert |
| `…/Services/CommunicationsQueryService.cs` | Dapper; returns full ApiKey |
| `…/Repositories/CommunicationsRepository.cs` | IgnoreQueryFilters for multi-tenant jobs |
| `…/CommunicationsDbContext.cs` | Schema `communications`; jsonb variables; broadcast still mapped |
| Migrations | Initial templates/outbox; suppressions; broadcasts; email config; “RemoveBroadcasts” only drops credit columns |

### Messaging (dispatch edge)

| File | Notes |
|------|--------|
| `…/Modules/Messaging/README.md` | Correct “render at source” rule; outdated Community ownership text |
| `…/Contracts/DispatchMessageIntegrationEvent.cs` | Channel + optional CreditHoldId |
| `…/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` | Suppression, BYOK, credit for WA only |

### BuildingBlocks providers

| File | Notes |
|------|--------|
| `BuildingBlocks/Infrastructure/ResendEmailService.cs` | Real Resend; org tags; no compliance headers |
| `BuildingBlocks/Infrastructure/ConsoleMessagingService.cs` | **Blocks all production WhatsApp** |
| `BuildingBlocks/Application/IMessagingService.cs` | Too narrow for Meta templates/buttons |
| `BuildingBlocks/Application/EmailTemplateBuilder.cs` | Brand wrap; newline→br on HTML |
| `BuildingBlocks/Application/MarkdownParser.cs` | Markdig pipeline |
| `BuildingBlocks/Infrastructure/Configuration/ResendOptions.cs` | ApiKey/SenderEmail only |

### Commerce coupling

| File | Notes |
|------|--------|
| `Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` | Primary dunning producer to COMMUNICATIONS |
| `Modules/Commerce/Domain/Entities/DunningStep.cs` | Owns live message bodies |
| `Modules/Commerce/Application/EventHandlers/OrderCompletedIntegrationEventHandler.cs` | No comms dispatch |
| `Modules/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs` | Webhooks only |
| `Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` | Dead `GetDefaultTemplateIdsAsync` |

### API / UI / config

| File | Notes |
|------|--------|
| `packages/api-spec/modules/communications/*.tsp` | Partial surface |
| `apps/ops-page/.../TemplatesPage.tsx` | Filters EMAIL/ALL; creates channel ALL |
| `apps/ops-page/.../EmailSettingsPage.tsx` | BYOK UX |
| `apps/ops-page/.../dunning/DunningStepEditor.tsx` | EMAIL / WHATSAPP / AUTOCHARGE |
| `apps/lazuar-api/src/Lazuar.Api/Program.cs` | Registers ConsoleMessaging + ResendEmail |
| `apps/lazuar-api/src/Lazuar.Api/appsettings.json` | Resend, WhatsAppSend:2, empty WebhookSecret |
| Tests under `Lazuar.ModuleTests/Communications/` | Domain unit only |

---

## Executive Summary

The **Communications module is a solid partial implementation of email template + BYOK Resend + suppression plumbing**, with a clean split from the Messaging dispatch module.  

It **does not** yet deliver the platform’s headline **Meta WhatsApp dunning** (provider is a console stub), **does not** send most fulfillment/welcome/digital-product templates it seeds, and has **compliance holes** (unsubscribe helper unused, email-only suppression). Dunning **email** content works via Commerce steps → Communications render → Resend **if** BYOK is configured; WhatsApp dunning is **log-only** while still consuming the product narrative and credit model.

**Highest-leverage fixes:** Meta Cloud API provider + fulfillment dispatch wiring + unsubscribe-on-send + delivery logging + secret hygiene.
