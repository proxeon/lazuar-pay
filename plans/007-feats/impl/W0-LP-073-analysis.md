# W0 — LP-073 analysis: email recovery sequence actually sends (Resend BYOK)

**Program:** `plans/007-feats`  
**ID:** LP-073 — *Email recovery sequence sends*  
**Wave:** 0 (`00-implement-ids.md`; tracker row LP-073 = **P**)  
**Date:** 2026-08-16  
**Status:** Analysis only — **do not implement from this file**  
**WhatsApp:** **out of scope.** Do not flip `Messaging:WhatsAppEnabled`, do not add Meta/Twilio, do not build LP-074.  
**Related (do not expand into):** LP-153 (variables — already unit-tested), LP-151 (receipt / immediate fail / magic-link), LP-079 (campaign snapshot), LP-071/072/078 (entry / AUTO_CHARGE / terminal).

**Feature in one sentence:** A merchant who configures Resend BYOK and EMAIL dunning steps must see those emails leave via Resend — not only persist in the campaign builder.

Tracker pairing in `00-checklist-tracker.md`: *“Email steps send with resolved variables and real links”* is LP-073 + LP-153. This file owns **send**. LP-153 owns leftover catalog/copy holes.

---

## 1. Verdict

| Question | Answer |
|----------|--------|
| Can ops configure EMAIL steps + Resend BYOK today? | **Yes.** Campaign builder + `/workspace/email`. Checkout is even gated on active BYOK. |
| Does the engine *intend* to send those emails? | **Yes.** `DunningEngineJob` publishes `FulfillmentRequested(COMMUNICATIONS, reminder.dunning)` and logs `ReminderDispatchLog`. |
| Does Resend BYOK *work* when `IEmailService` is called? | **Yes.** `ResendEmailService` + decrypt path are real and unit-tested. |
| Do recovery emails actually leave on a default deploy? | **No.** Hop 2 (`DispatchMessage`) is written to `communications.OutboxMessages` and **never `SaveChanges`**. Commerce then acks hop 1. The step is marked dispatched. Inbox/Resend never run. |
| Smallest fix? | Inject `CommunicationsDbContext` into the hydrate handler; `PublishAsync` then `SaveChangesAsync`. Same one-liner on the default-campaign seed publisher. |
| Migration / TypeSpec / ops UI? | **No.** |
| WhatsApp / SMS / AUTO_CHARGE? | **Out of scope.** |

`plans/007-feats/12-dunning-and-recovery.md` marks DN-005 “Email dunning dispatch (Resend/BYOK)” as **shipped** and says “demo this.” That is true of the **adapters** and the **intent**. It is **false of the live hop-2 commit**. Treat this file as the correction for LP-073.

---

## 2. What exists (read, not assumed)

### 2.1 End-to-end hop diagram (as coded)

```
Ops: campaign EMAIL steps          Ops: PUT /admin/communications/email-config
     (inline subject/body)              (validate GET /domains, AES-encrypt key)
                │                                         │
                ▼                                         ▼
     commerce.DunningSteps                 communications.TenantEmailConfigurations
                │
                ▼
     DunningEngineJob (1h, RunOnceAsync)
        DispatchCommunicationStepAsync
        → CommerceEventBus.PublishAsync(FulfillmentRequested reminder.dunning)
        → Subscription.RecordReminderDispatched(...)
        → CommerceDbContext.SaveChanges          ← hop 1 IS committed
                │
                ▼
     CommerceOutboxPublisherJob
        → InMemoryEventBus → FulfillmentRequestedIntegrationEventHandler
              CRM profile + workspace + variable fill + MarkdownParser.ToHtml
              → CommunicationsEventBus.PublishAsync(DispatchMessage)
              →  ✗ no CommunicationsDbContext.SaveChanges
                │
                ▼
     communications.OutboxMessages   (change-tracker only; disposed with nested scope)
                │
                ✗ never drained
                │
     MessagingOutbox / DispatchMessageIntegrationEventHandler / ResendEmailService
        (would decrypt BYOK, wrap HTML, POST https://api.resend.com/emails)
```

Rule this violates: `apps/lazuar-api/docs/001-cross-module-communication.md` — *PublishAsync then a single SaveChanges that covers domain + outbox.*

`InMemoryEventBus.PublishAsync` opens a **new** DI scope for handlers. `OutboxEventBus<CommunicationsDbContext>` stages a row on **that** scoped context. Disposing the scope without `SaveChanges` drops the row. Commerce outbox then marks `FulfillmentRequested` processed. There is no later retry of hop 1.

### 2.2 `DunningEngineJob` email dispatch

Hosted: `AddHostedService<DunningEngineJob>()`. Interval: `Workers:DunningEngineInterval` default `01:00:00`. `RunOnceAsync` is already `internal` for tests (same pattern as `BillingEngineJobTests`).

| File | Role for EMAIL |
|------|----------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` | Load **active** campaigns + steps `AsNoTracking`; `Messaging:WhatsAppEnabled` (default **false**); PreDunning batch then PastDue |
| `DunningEngineJob.Claim.cs` | `FOR UPDATE SKIP LOCKED`; in-memory claim for tests; PAST_DUE respects `DunningPausedUntil`; PreDunning is ACTIVE + `NextBillingDate` in `(now, now+14d]` |
| `DunningEngineJob.PreDunning.cs` | Negative offsets, actions `EMAIL` / `WHATSAPP` / `ALL` only |
| `DunningEngineJob.PastDue.cs` | Assign if null; grace/terminal; then `DayOffset >= 0 && <= daysOverdue`; AUTO_CHARGE vs comms |
| `DunningEngineJob.Dispatch.cs` | WA demotion + payload + `FulfillmentRequested` |

Payload (`DispatchCommunicationStepAsync`):

```csharp
subscription_id, client_profile_id, product_id, action_type,
subject, email_body,
whatsapp_body,          // emptied when effectiveAction == "EMAIL"
plan_name, amount,      // product.Name / product.Price
currency, days_overdue
```

Serialized with `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`. Event type is always `"reminder.dunning"` and target `"COMMUNICATIONS"`.

`ResolveEffectiveCommunicationAction` (WA flag false):

| Step `ActionType` | EmailBody? | Result |
|-------------------|------------|--------|
| `EMAIL` / `ALL` | any | `"EMAIL"` |
| `WHATSAPP` | non-empty | `"EMAIL"` (demote) |
| `WHATSAPP` | empty | `null` → skip, **still** `RecordReminderDispatched` |
| `AUTO_CHARGE` | — | not a comms step |

Default seeded campaign (`GenerateDefaultDunningCampaignsCommandHandler`):

| Offset | Action | Body | With flag false |
|--------|--------|------|-----------------|
| −3 | EMAIL | yes + `{{update_payment_link}}` | intended send |
| 0 | EMAIL | yes | intended send (first PAST_DUE tick, `daysOverdue` can be 0) |
| +3 | WHATSAPP | **no** `EmailBody` | skip + consume offset |
| grace 7 | CANCEL | — | typed cancel event, not this ticket |

So a default tenant’s **past-due recovery mail** is the **day-0 EMAIL** only. +3 never talks. That is an honesty hole, not the reason Resend is never called for day 0 / −3.

Idempotency: unique `(SubscriptionId, TargetBillingDate, DayOffset)` on `ReminderDispatchLogs`. Engine records the log in the **same** Commerce `SaveChanges` as hop 1. After that, the engine will not re-fire the offset even if hop 2 was lost. **That is why the P0 is a silent one-shot miss, not a retry loop.**

Pre-dunning catch-up is **inverted** (already in `12-dunning-and-recovery.md`):

```csharp
s.DayOffset < 0 && Math.Abs(s.DayOffset) <= daysUntilDue
```

Entering the 14-day window with `daysUntilDue = 14` fires −3 (and −7, −1, …) immediately. Correct catch-up is `daysUntilDue <= Math.Abs(s.DayOffset)`. Past-due catch-up (`DayOffset <= daysOverdue`) is already the right direction.

There is **no** `DunningEngineJob` test.

### 2.3 Communications `FulfillmentRequested` handler

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs`

Subscribed: `UseCommunicationsSubscriptions` → `FulfillmentRequestedIntegrationEvent`.

Gate: `InternalTargetApp == "COMMUNICATIONS"` and `EventType` in `{ reminder.due, reminder.dunning }`. Everything else is a silent return — including Billing’s `subscription.past_due` fulfillment (webhook-only; **no** immediate decline mail — that is LP-151).

`reminder.due` (template-by-id) has **no production publisher** in this tree. Live LP-073 traffic is **only** `reminder.dunning` (inline step copy). Do not revive `reminder.due`.

Hydrate (dunning path):

1. Parse `client_profile_id`. Missing / unparsable → **return**.  
2. `ICrmQueryService.GetClientProfileAsync` — null → **return**.  
3. `IOneQueryService.GetWorkspaceByIdAsync` for slug + `{{business_name}}`.  
4. Links from `App:ClientUrl` (default `https://portal.lazuar.com`):
   - `{{update_payment_link}}` = `{base}/{slug}/update-payment/{subId}` (unsigned GUID)
   - `{{portal_magic_link}}` = portal + HMAC token (`IMagicLinkTokenService`, 24h)
   - `{{renewal_link}}` = naked portal URL  
5. `PopulateVariables` then `MarkdownParser.ToHtml` on the email body.  
6. `CommunicationsEventBus.PublishAsync(DispatchMessageIntegrationEvent)` — **no SaveChanges**. Handler does not even inject `CommunicationsDbContext`.

Variables filled (locked by `DunningTemplateVariableSubstitutionTests`):  
`{{customer_name}}`, `{{customer_email}}`, `{{customer_phone}}`, `{{business_name}}`, `{{plan_name}}`, `{{amount}}`, `{{total_price}}`, `{{currency}}`, `{{days_overdue}}`, `{{renewal_link}}`, `{{portal_magic_link}}`, `{{update_payment_link}}`.

Silent returns (no throw → hop 1 acked → no retry):

| Condition | Email? |
|-----------|--------|
| Wrong target / event type | no |
| Bad / missing `client_profile_id` | no |
| CRM profile missing | no |
| Profile.Email empty | Dispatch publishes; Messaging then skips (`wantsEmail` requires `ToEmail`) |

Same-class bug on other Communications publishers ( **do not fix here** — LP-151 ): `LifecycleEventHandlers`, `DocumentPublishedIntegrationEventHandler`, `OrderCompletedDigitalDeliveryHandler`. All `PublishAsync(DispatchMessage)` with no flush.

Related seed bug **is** LP-073-adjacent: `AppEntitlementGrantedIntegrationEventHandler` `SaveChanges`s templates, then `PublishAsync(DefaultTemplatesSeededIntegrationEvent)` on the **same** context with **no second save**. Commerce’s `DefaultTemplatesSeededIntegrationEventHandler` never runs. New tenants get templates, **not** the default campaign, unless they click **Deploy Recommended Strategy**.

### 2.4 Messaging `ResendEmailService` + dispatch edge

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs`

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/Email/ResendEmailService.cs`

Registered: `IEmailService` → `ResendEmailService` (singleton). `DispatchMessage` subscribed in `UseMessagingSubscriptions`. Named HttpClient `"Resend"` base `https://api.resend.com/`, 30s timeout.

Handler (email slice only):

1. `wantsEmail` = channel `EMAIL` or `ALL`, and non-empty `ToEmail` + `HtmlEmailBody`.  
2. Suppression (`ISuppressionService`) → `MessageDeliveryLog` `SKIPPED` / `"Address suppressed"`.  
3. Tenant (not system `Guid.Empty` / `…0001`): `ICommunicationsQueryService.GetEmailConfigCredentialsAsync` — decrypt, require `IsActive` + key + sender.  
4. `EmailTemplateBuilder.WrapWithBrandHtml` (also `Replace("\n","<br/>")` on already-HTML).  
5. `SendEmailAsync(to, subject, html, orgId, tenantApiKey, tenantSenderEmail, unsubscribeUrl)`. Dunning does **not** pass `UnsubscribeUrl` (correct — transactional).  
6. Log `SENT` + Resend id, or `FAILED` + throw (communications outbox retries, max 5, exponential minutes).

`ResendEmailService` rules (tested in `ResendEmailServiceTests`):

| Caller | Credentials | Behavior |
|--------|-------------|----------|
| Tenant + BYOK key **and** sender | use them; tag `org` = organizationId | `POST emails` |
| Tenant, missing BYOK | — | **throw** `"No platform fallback allowed for tenant emails…"` |
| System tenant, platform `Resend:ApiKey` set | platform key + `Resend:SenderEmail` | send |
| System tenant, no platform key | — | log, return `null` (no HTTP) |

Default `appsettings.json` `Resend:ApiKey` / `SenderEmail` are empty. Tenant recovery **must** be BYOK. That matches checkout (`HasValidEmailConfigAsync`) and the dashboard amber banner.

HttpClient caveat (P2, not the miss): factory may stamp platform `Authorization` on the **named** client; `SendEmailAsync` then assigns `DefaultRequestHeaders.Authorization`. Shared-client mutation is not thread-safe. Tests construct a fresh `HttpClient`. Prefer per-request `request.Headers.Authorization`.

`GET /messaging/delivery-logs` exists. Ops **Developer → Logs** is **outbound webhooks**, not this table. Do not build a mail log UI in LP-073.

### 2.5 `TenantEmailConfiguration`

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Domain/Aggregates/TenantEmailConfiguration.cs`

| Field | Notes |
|-------|--------|
| `OrganizationId` | Unique index. `IMustHaveTenant`. |
| `ApiKey` | AES-256-CBC via `AesSecretVault` (`Kms:MasterKey` or `Jwt:Secret`). Format base64(IV[16]+ciphertext). |
| `SenderEmail` | trimmed, lowercased |
| `IsActive` | dispatch requires true |

`SaveEmailConfigCommandHandler`:

- System tenant cannot save BYOK.  
- Empty PUT key = keep existing (ops password field).  
- Decrypt-or-plaintext for legacy rows.  
- Validates `GET https://api.resend.com/domains` with the candidate key. **Does not** check that `SenderEmail`’s domain is in that list.  
- Encrypt + insert / `UpdateWithoutKey` / `UpdateConfiguration`.

GET `/admin/communications/email-config`: `has_api_key` + last-4 hint, never the raw key. Missing row → 200 empty DTO (dashboard probe).

`HasValidEmailConfigAsync`: active + non-empty key + sender. Used by create/update product and `InitiateCheckoutCommandHandler` (“checkout temporarily disabled”). A tenant who can sell almost always has a row. Manual enroll / already-ACTIVE subs can still be in dunning if they later **disable** the config — dispatch then throws no-fallback (after P0, outbox retries then dead-letters).

UI: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/EmailSettingsPage.tsx`. Dashboard + products warn if email is missing. Copy says receipts; it does **not** mention dunning. Fine.

Campaign builder save (`CampaignBuilderPage.tsx`) **strips** `email_body` unless `action_type === "EMAIL"`. Engine demotion of WHATSAPP→EMAIL therefore cannot fire for UI-created WA steps. Out of scope except: do not add a WA email-body field in this ticket.

---

## 3. Why the tracker is P (gap list)

Ordered by “does the buyer get an email.”

| # | Gap | Effect | In LP-073? |
|---|-----|--------|------------|
| G1 | Hop 2 never committed | Day −3 / 0 EMAIL: `ReminderDispatchLog` yes, Resend **never** | **P0 — this ticket** |
| G2 | `DefaultTemplatesSeeded` never committed | New tenant has no campaign until Deploy | **P0-adjacent — one line, do it** |
| G3 | Pre-dunning inequality inverted | “Renews in 3 days” at day −14; real day −3 already consumed | **P1 — one comparison** |
| G4 | Default +3 is WHATSAPP, no email body | Offset consumed, no second past-due mail | **P1 — seed only** |
| G5 | Hydrate silent-return (no CRM / no id) | Hop 1 acked, no DispatchMessage, no retry | **P1 — throw so hop 1 retries** |
| G6 | Empty `ToEmail` / empty HTML | Dispatch “succeeds”, `wantsEmail` false | **P1 — throw or skip-log** |
| G7 | Pre-dunning `{{update_payment_link}}` rejects ACTIVE | Email can send; CTA 400s | **No — LP-075 / DN-016** |
| G8 | `{{amount}}` is `product.Price` | Honesty of copy | **No — LP-153** |
| G9 | Dual CMS (Templates page ≠ dunning steps) | Founder edits the wrong page | **No** (document only) |
| G10 | Named HttpClient header mutation | Rare wrong-key / race | **P2 optional** |
| G11 | Sender not proven on a verified Resend domain | Resend 4xx after P0 | **No** (save already hits `/domains`) |
| G12 | No engine tests | G1/G3 uncaught | **Tests required** |
| G13 | Ops cannot see `MessageDeliveryLog` | Support hole | **No UI this ticket** |

G1 is sufficient to keep LP-073 at **P**. Adapters being “shipped” does not close the loop.

---

## 4. Options

### A — Flush hop 2 in the hydrate handler (choose this)

Inject `CommunicationsDbContext` into `FulfillmentRequestedIntegrationEventHandler`. After `PublishAsync(DispatchMessage)`, `await _db.SaveChangesAsync()`. Same scoped instance as `OutboxEventBus<CommunicationsDbContext>`.

`AppEntitlementGrantedIntegrationEventHandler`: second `SaveChanges` **after** `PublishAsync(DefaultTemplatesSeeded…)` (context is already injected).

No new types, no migration, no UI.

### B — Publish `DispatchMessage` on `InMemoryEventBus` (reject)

Sends Resend inside the Commerce outbox drain. Resend failure retries **hop 1** (duplicate mail; `ReminderDispatchLog` already written). Breaks “render at source, dispatch at edge.” No durability if the process dies after ack.

### C — Call `IEmailService` from Communications (reject)

Crosses the Messaging port. Duplicates suppression / BYOK / delivery-log policy. Architecture tests will fight it.

### D — Inbox hop for `DispatchMessage` (reject for Wave 0)

Messaging already has an inbox pattern for tenant replica events. Adding a fan-out writer + inbox consumer is correctness theater for a missing `SaveChanges`.

### E — Platform-key fallback for tenants (reject)

Would make demos easier and violate the BYOK / checkout gate. `ResendEmailService` throw is the product.

---

## 5. Minimal change set (option A)

### 5.1 Must — G1 flush

**File:** `FulfillmentRequestedIntegrationEventHandler.cs`

- Constructor: add `CommunicationsDbContext db`.  
- After successful `PublishAsync(dispatchEvent)`: `await _db.SaveChangesAsync()`.  
- Do this on the `reminder.dunning` path (the only live one). Harmless if `reminder.due` is ever published.

`PlatformDbContext.SaveChangesAsync` already `JobTrigger.Trigger()`s, so `CommunicationsOutboxPublisherJob` wakes without waiting 5s.

### 5.2 Must — G2 default campaign seed

**File:** `AppEntitlementGrantedIntegrationEventHandler.cs`

After `PublishAsync(new DefaultTemplatesSeededIntegrationEvent(...))`, `await _dbContext.SaveChangesAsync()` again (or move the existing save to **after** publish so templates + outbox share one commit). Prefer **one** save after both mutations.

Do **not** change `GenerateDefaultDunningCampaignsCommandHandler` idempotency (`HasAnyDunningCampaignAsync` no-op).

### 5.3 Should — G3 pre-dunning predicate (tiny, same job)

**File:** `DunningEngineJob.PreDunning.cs`

Replace

```csharp
s.DayOffset < 0 && Math.Abs(s.DayOffset) <= daysUntilDue
```

with

```csharp
s.DayOffset < 0 && daysUntilDue <= Math.Abs(s.DayOffset)
```

Keep the action-type filter and `ReminderLogs` guard. Past-due predicate is already correct — do not touch it.

### 5.4 Should — G4 default +3 becomes EMAIL

**File:** `GenerateDefaultDunningCampaignsCommandHandler` only.

Change the +3 step to `EMAIL` with a subject + body that reuse the existing WA copy (customer / plan / `{{update_payment_link}}`). Do **not** migrate existing rows. Tenants who already deployed keep a dead +3; they still have day 0. Document in the PR. No WhatsApp field work.

### 5.5 Should — G5/G6 fail hop 1 when hydrate cannot send

In the dunning branch, **throw** (so Commerce outbox retries, max 5) when:

- CRM profile is null, or  
- `profile.Email` is null/whitespace, or  
- after populate, subject **and** html body would both be empty (defensive; builder already requires them for EMAIL).

Log organizationId, subscriptionId, clientProfileId. Do **not** throw on workspace-slug miss (links degrade to `/{empty}/…`; still a send).

Empty EMAIL body from a hand-edited API payload: skip publish and **do not** treat as success — throw or leave hop 1 unacked. Prefer throw.

### 5.6 Optional P2 — per-request Authorization

`ResendEmailService` + `SaveEmailConfigCommandHandler`: set `Authorization` on the `HttpRequestMessage`, do not mutate `DefaultRequestHeaders` on the named client. Only if you are already in that file for tests.

### 5.7 Explicitly do not touch

- WhatsApp flag, `ConsoleMessagingService`, builder WA banner, credit costs.  
- `LifecycleEventHandlers` hardcoded portal host (LP-151).  
- Variable catalog / `{{renewal_link}}` vs update-payment (LP-153).  
- Campaign snapshot (LP-079).  
- AUTO_CHARGE, grace, pause, archive-stuck.  
- TypeSpec, ops delivery-log page, test-send button.  
- Encrypting legacy plaintext keys in bulk.  
- `EmailTemplateBuilder` `\n` → `<br/>` double-wrap.

---

## 6. Tests (required to call LP-073 done)

There is no engine fixture. Add  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs`  
beside `BillingEngineJobTests` (in-memory `CommerceDbContext`, keyed `"CommerceEventBus"`, `IConfiguration` `Messaging:WhatsAppEnabled=false`, `RunOnceAsync`).

### 6.1 Engine — EMAIL dispatch contract

| # | Setup | Assert |
|---|--------|--------|
| E1 | PAST_DUE, matching active campaign, day-0 EMAIL with subject+body, `daysOverdue >= 0`, no reminder log | One `FulfillmentRequested` (`COMMUNICATIONS`, `reminder.dunning`); payload `action_type=EMAIL`, `email_body` / `subject` / `plan_name` / `client_profile_id` present; one `ReminderDispatchLog` at offset 0 |
| E2 | Same sub, `RunOnce` again | **No** second publish (DayOffset unique) |
| E3 | Day +3 WHATSAPP, empty `EmailBody`, flag false | **No** `FulfillmentRequested`; log still recorded (today). After 5.4 this case is only for **non-default** campaigns |
| E4 | ACTIVE, `NextBillingDate` = now+10d, step −3 EMAIL | **Before 5.3:** publish (inverted). **After 5.3:** **no** publish. `NextBillingDate` = now+3d (or 2d): publish once |
| E5 | PAST_DUE, EMAIL step, no campaign match | No publish |
| E6 | Paused PAST_DUE (`DunningPausedUntil` future) | Not claimed; no publish |

Capture the bus with `Substitute.For<IEventBus>()` keyed as in billing tests.

### 6.2 Hydrate — extend `DunningTemplateVariableSubstitutionTests`

Existing test stays (variables + magic link). Add:

| # | Assert |
|---|--------|
| H1 | After `PublishAsync(DispatchMessage)`, **`CommunicationsDbContext.OutboxMessages` has one row** whose type is `DispatchMessageIntegrationEvent` and data contains the filled subject / to-email. Requires real `OutboxEventBus<CommunicationsDbContext>` (or the handler’s db + save), **not** a substitute bus that skips the flush. |
| H2 | Missing CRM profile → throw (after 5.5); **no** outbox row |
| H3 | Missing `client_profile_id` → throw (after 5.5) |
| H4 | Profile email empty → throw (after 5.5) |

H1 is the acceptance test for G1. A substitute `IEventBus` **cannot** prove the flush.

Suggested harness: in-memory `CommunicationsDbContext` + real `OutboxEventBus<CommunicationsDbContext>` as the keyed Communications bus + NSubstitute CRM/One/tokens.

### 6.3 Seed flush — small handler test

`AppEntitlementGranted` COMMERCE, no templates: after handle, `communications.OutboxMessages` contains `DefaultTemplatesSeededIntegrationEvent`. If you only assert `PublishAsync` on a substitute, G2 regresses again.

### 6.4 Default campaign copy

Unit-test `GenerateDefaultDunningCampaignsCommandHandler` (in-memory repo or real `CommerceDbContext`):

- First call inserts one campaign with EMAIL at −3, 0, and **+3** (after 5.4).  
- Second call is a no-op.  
- +3 has non-empty `EmailBody` containing `{{update_payment_link}}`.

### 6.5 Dispatch + Resend (already exist — keep, add one)

Keep `DispatchMessageIntegrationEventHandlerTests.HandleAsync_EmailChannel_WrapsBrandAndSendsViaIEmailService` (BYOK forwarded).  
Keep `ResendEmailServiceTests` (org tag, no fallback, List-Unsubscribe, system empty key).

Add:

| # | Assert |
|---|--------|
| D1 | Tenant, `GetEmailConfigCredentialsAsync` → inactive or null → `SendEmailAsync` not called **or** called without keys and throw `*No platform fallback*`; `MessageDeliveryLog.Status == FAILED` |
| D2 | Suppressed address → `SKIPPED`, no `SendEmailAsync` |

Optional P2: Resend test that `Authorization` is on the **request**, not only that the value is `Bearer tenant_key`.

### 6.6 Domain — no change required

`TenantEmailConfigurationTests` (keep / rotate key) stay. No new aggregate behavior.

### 6.7 Do not require

- Live Resend HTTP in CI.  
- Multi-module soak (commerce outbox job → comms outbox job → wiremock Resend). H1 + E1 is enough.  
- Ops UI tests.  
- WhatsApp cases beyond “flag false does not call `IMessagingService`” (already in Messaging tests).

---

## 7. Definition of done (LP-073)

A founder can:

1. Save Resend BYOK (already).  
2. Have a campaign with at least one EMAIL step (auto-seed **or** Deploy).  
3. Put a subscription in PAST_DUE with that campaign (or ACTIVE inside the pre-window for −3).  
4. Run `DunningEngineJob` once (or wait an hour).  
5. Observe, without touching WhatsApp:
   - `commerce.ReminderDispatchLogs` row for that offset, **and**
   - `communications.OutboxMessages` then `messaging` `MessageDeliveryLog` `SENT` (or `FAILED` with a Resend error if the key/domain is wrong — **not** silence), **and**
   - `ResendEmailService` invoked with the **tenant** key + sender.

Mark tracker LP-073 **Y** only after E1 + H1 pass. G3/G4/G5 should land in the same PR because they are small and otherwise the “sequence” is still a single early or single day-0 mail.

---

## 8. Out of scope (do not open)

| Topic | Owner |
|-------|--------|
| WhatsApp / Meta / credits / builder WA body | LP-074, 00.4 freeze |
| Immediate “card declined” mail; suspend template URL | LP-151 |
| Variable wiki, amount ≠ invoice, catalog missing `{{update_payment_link}}` | LP-153 |
| ACTIVE update-payment 400 | DN-016 / LP-075 |
| Campaign snapshot / live mutate | LP-079 |
| AUTO_CHARGE, hard/soft decline, Billplz throw | LP-072 / LP-076 |
| Terminal cancel/suspend | LP-078 |
| Mail UI / open-click / List-Unsubscribe on dunning | later / never on transactional |
| Platform fallback for tenants | never |

---

## 9. File checklist (implementer)

| Change | Path |
|--------|------|
| Flush hop 2 | `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` |
| Flush seed | `…/EventHandlers/AppEntitlementGrantedIntegrationEventHandler.cs` |
| Pre-dunning predicate | `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PreDunning.cs` |
| Default +3 EMAIL | `apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` |
| Engine tests | `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` |
| Hydrate + outbox tests | `…/Communications/DunningTemplateVariableSubstitutionTests.cs` (or new sibling) |
| Optional BYOK-miss dispatch | `…/Messaging/DispatchMessageIntegrationEventHandlerTests.cs` |

No EF migration. No TypeSpec. No ops TSX.
