# W0 — LP-151 analysis: Receipt / failed-pay / magic-link email actually sends via tenant Resend

**Program:** `plans/007-feats`  
**ID:** LP-151 — *Receipt / failed-pay / magic-link email*  
**Wave:** 0 (`00-implement-ids.md`; tracker row = **P**)  
**Date:** 2026-08-16  
**Status:** Analysis only — **do not implement from this file**  
**Evidence (do not reopen as product strategy):** [16-communications-whatsapp-email.md](../16-communications-whatsapp-email.md) COM-001 / COM-002 / COM-005; [12-dunning-and-recovery.md](../12-dunning-and-recovery.md) (lifecycle URL called out as LP-151); [11-subscriptions-lifecycle.md](../11-subscriptions-lifecycle.md) SL-071

**This ticket is not** LP-150 (BYOK Resend already **Y**), LP-152 (template editor already **Y**), LP-153 (shared variable resolver / wiki honesty), LP-073 (campaign **sequence** emails), LP-154 (suppression split), or LP-078 (terminal suspend/cancel). Adjacent holes are listed only so implementers do not “fix” them here.

**Feature in one sentence:** After a merchant saves an active Resend key (already required to open checkout), a paid B2C buyer gets a receipt email, a declined renewal gets a failed-payment email, and a subscriber gets a 24h portal magic-link email — each a real `POST https://api.resend.com/emails` on the **tenant** key.

---

## 0. Verdict

The **pipe** is live. The **three jobs** are not.

| Layer | Status |
|-------|--------|
| Tenant Resend BYOK + `ResendEmailService` | **Y** — no platform fallback; checkout/product create already refuse without it |
| `DispatchMessageIntegrationEvent` → wrap HTML → Resend → `MessageDeliveryLog` | **Y** — unit-tested |
| Catalog templates + Templates UI + “Send test” | **Y** — seeded names exist; test mail goes to `admin@lazuars.io` |
| Official Receipt on paid B2C | **Configured, almost never sent** — `DocumentPublished` is published with an empty `CustomerEmail` because Billing looks up `commerce.TransactionLogs` **before** Commerce writes the row |
| Immediate failed-payment email | **Never sent** — `GatewayPaymentFailed` only marks PAST_DUE + webhook. Catalog **Payment Failed** fires on **SUSPEND**, and the default campaign **CANCEL**s, so the template is dead on factory defaults |
| Portal magic-link email | **Never sent as its own mail** — `IMagicLinkTokenService` is real; only the dunning hydrator mints a token; default dunning copy does not even use `{{portal_magic_link}}`; portal / legal / privacy copy still say “we emailed you a link” |

That is the tracker **P**: Email Settings + Templates look finished; buyer inbox is empty.

**LP-151 is three send triggers + one customer-email lookup + unhook the miswired suspend template.** Do not build a variable engine (LP-153). Do not retune dunning offsets (LP-073). Do not attach PDFs.

---

## 1. Product contract for this ID

Sellable sentence after this ticket:

> When Resend is saved and active, a B2C payment emails **Official Receipt** (signed 30-day PDF link) from the tenant sender; a declined subscription charge emails **Payment Failed** with a working update-payment URL; a new subscriber (and a portal “email me a link”) gets a 24h `?token=` portal URL. Ops can see `MessageDeliveryLog.Status=SENT` with a Resend id.

| Job | Trigger | Template | Must reach Resend when |
|-----|---------|----------|------------------------|
| Receipt | Billing stored an **Official Receipt** PDF | `Official Receipt` | `CustomerEmail` non-empty, template present, BYOK active |
| Failed pay | `GatewayPaymentFailed` for a live subscription | `Payment Failed` | Profile has email, template present, BYOK active |
| Magic link | `SubscriptionActivated` (`IsFirstPayment`) **and** public request | New catalog **Portal Access** (or equivalent) | Same; body contains HMAC token, not a naked `/portal` |

| Input | Result |
|-------|--------|
| B2C `GatewayPaymentCompleted` + CRM email | `DocumentPublished` has that email → `DispatchMessage` → tenant Resend |
| B2C pay, no email on profile | No dispatch (log). Do **not** invent an address |
| B2B (`is_b2b_required=true`) | **No** Official Receipt email (unchanged; LHDN path) |
| `DocumentType` Tax Invoice / Credit Note | **No** Official Receipt template (today it would) |
| `GatewayPaymentFailed` + `subscription_id`/`receipt` Guid + not CANCELED | Payment Failed dispatch; CTA = `{ClientUrl}/{slug}/update-payment/{subId}` |
| `GatewayPaymentFailed` one-off / no sub | No mail (unchanged) |
| Default campaign still CANCEL at grace | **No** Payment Failed on terminal (unhook suspend) |
| First `SubscriptionActivated` | Portal Access dispatch with `GenerateToken(subscriptionId)` |
| `one_time` `OrderCompleted` | **No** magic-link token (service is subscription-scoped). Receipt only if Billing published one |
| Portal POST request + matching email | Always HTTP 200; send only if a sub exists for that email |
| Missing catalog row | No dispatch (do not throw). Seed-if-missing on entitlement so new names land |
| No / inactive BYOK | Checkout already blocked. If config later dies, dispatch **throws** (existing; outbox retries). Do not add platform fallback |

Industry cousins (do not copy extras): Stripe receipt + failed-invoice + customer-portal login; Chargebee payment-success / payment-failed / portal; HitPay receipt. We already chose **link-to-PDF**, not MIME attach.

---

## 2. What exists (read, not redesigned)

### 2.1 Catalog — five names, one of them unused on the money path

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs`

Seeded on `AppEntitlementGranted` for `COMMERCE` \| `COMMUNITY` \| `VAULT` **only if the org has zero template rows**:

| Name | Channel | Required | Who consumes it today |
|------|---------|----------|------------------------|
| Payment Failed | ALL | `{{renewal_link}}` | `LifecycleEventHandlers` on **`SubscriptionSuspended`**, not decline |
| Subscription Cancelled | ALL | — | Cancel (LP-078 / admin / portal) — **leave** |
| Digital Product Delivery | ALL | `{{fulfillment_url}}` | `OrderCompletedDigitalDeliveryHandler` — portal URL, **no token** |
| Quotation Ready | ALL | `{{document_link}}` | `DocumentPublished` when type is `Draft Quotation` |
| Official Receipt | ALL | `{{document_link}}` | `DocumentPublished` for **every other** document type |

Orphans (`Community Welcome`, `Abandoned Cart *`, `Generic Receipt`, old renewal names, …) are not re-seeded. Correct (ADR 021/022).

There is **no** “Portal Access” / “Magic Link” catalog row. Token minting is a **variable**, not a job.

Seed hole (`AppEntitlementGrantedIntegrationEventHandler`):

```29:40:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/AppEntitlementGrantedIntegrationEventHandler.cs
        var hasTemplates = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .AnyAsync(t => t.OrganizationId == @event.TenantId);

        if (!hasTemplates)
        {
            var templates = DefaultMessageTemplates.CreateAllForTenant(@event.TenantId).ToList();
```

If an old tenant still has only orphans, **Official Receipt / Payment Failed are never inserted**. Handlers then `return` with no dispatch. No handler test.

### 2.2 The pipe that already works (do not rebuild)

```
Domain / job
  → Communications (pick template, cheap Replace, MarkdownParser.ToHtml)
  → CommunicationsEventBus outbox
  → InMemoryEventBus
  → DispatchMessageIntegrationEventHandler
       BYOK from TenantEmailConfigurations (decrypt)
       EmailTemplateBuilder.WrapWithBrandHtml
       IEmailService = ResendEmailService   // tenant key + sender
       MessageDeliveryLog SENT | FAILED | SKIPPED
```

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs`

Email is attempted only when `Channel` is `EMAIL` or `ALL`, `ToEmail` non-blank, `HtmlEmailBody` non-blank.

Tenant without BYOK: `ResendEmailService` throws *“No platform fallback allowed for tenant emails.”* That is LP-150 policy. Keep it.

`HasValidEmailConfigAsync` (non-empty encrypted key + sender + `IsActive`) gates **create product** and **initiate checkout**. Ops Email Settings: “automated receipts, dunning emails, and broadcasts.” Test reminder (`SendTestReminderCommandHandler`) always targets `admin@lazuars.io`. Merchants can “prove” Resend while buyers still get nothing.

Covered today: `ResendEmailServiceTests`, `DispatchMessageIntegrationEventHandlerTests` (happy path + WA skip). **No** test that missing BYOK throws and writes `FAILED`.

### 2.3 Receipt path — DocumentPublished

**Publisher**

`GatewayPaymentCompletedHandler` (Billing), subscription order vs Commerce:

`UseAllModuleSubscriptions`: Payments → **Billing** → … → **Commerce**.

Billing B2C (`is_b2b_required` ≠ `true`):

1. Ledger + `RCPT-yyyy` number.  
2. **Synchronous** `GenerateAndStoreDocumentCommand(..., "Official Receipt")`.

`GenerateAndStoreDocumentCommandHandler`:

1. Customer via `ICommerceDocumentLookup.GetCustomerByGatewayTransactionAsync(org, ledger.ReferenceId)`.  
2. `ReferenceId` is the **gateway transaction id**.  
3. Lookup is `commerce.TransactionLogs` where `ExternalReference` or `Id::text` matches.  
4. Commerce writes that row in `GatewayPaymentCompletedIntegrationEventHandler.LogTransactionAsync` — **the next handler**.  
5. Result: `customerEmail = ""`, `customerName = "Customer"`.  
6. PDF still uploads to R2.  
7. `DocumentPublishedIntegrationEvent` is published with **empty** `CustomerEmail`.

`DocumentPublishedIntegrationEventHandler`:

```35:38:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs
        if (string.IsNullOrEmpty(@event.CustomerEmail) || string.IsNullOrEmpty(@event.TenantSlug))
            return;

        var templateName = @event.DocumentType == "Draft Quotation" ? "Quotation Ready" : "Official Receipt";
```

So:

- Empty email → **silent no-send** (tested).  
- Empty slug → same (tested).  
- Happy path with a **pre-seeded** email **is** tested and would call Resend. Production first-and-every gateway pay does not supply that email.  
- Any non-quotation type (including LHDN **Tax Invoice** / **Credit Note** from `LhdnDocumentValidatedIntegrationEventHandler`) uses **Official Receipt** copy. After Commerce has written the log, a later LHDN VALID can be the **first** mail the buyer gets — and it is the wrong document type.

Manual enroll: ledger `ReferenceId` = `subscriptionId`. `RecordSubscriberPayment` writes `ExternalReference` = `MANUAL-{subId}` or the clerk’s ref — **not** the raw Guid. Lookup misses. Same empty email.

Zero-amount checkout: **no** `GenerateAndStore`. No receipt mail (leave).

`ICommerceDocumentLookup.GetDraftCheckoutSessionAsync` already has CRM email for quotations. Gateway receipts do not use it. First-pay metadata `subscription_id` is the **checkout session id** (`CommerceCheckoutMetadata.MergeClientIntoGateway`). That session has `ClientProfileId`. Billing never asks.

### 2.4 Failed-pay path — two products tangled

**A. Immediate decline (this ticket)**

`GatewayPaymentFailedIntegrationEventHandler` (Commerce):

- Resolve `subscription_id` or legacy `receipt` Guid.  
- `MarkChargeAttemptFailed`.  
- Skip PAST_DUE if already `CANCELED` / `SUSPENDED`.  
- Else `MarkAsPastDue` + assign campaign + `subscription.past_due` **outbound webhook**.  
- **No** `DispatchMessage`. **No** `FulfillmentRequested`.

Communications does not subscribe to `GatewayPaymentFailed`.

**B. Sequenced dunning (LP-073 — read only)**

`DunningEngineJob` → `FulfillmentRequested(COMMUNICATIONS, reminder.dunning)` with **step inline copy**, not the Templates page.

`FulfillmentRequestedIntegrationEventHandler` hydrates and **does** mint `{{portal_magic_link}}`. Default seed copy uses `{{update_payment_link}}` only:

| Offset | Action | Factory deploy (`WhatsAppEnabled=false`) |
|--------|--------|------------------------------------------|
| −3 | EMAIL pre-due | Pre-dunning claim (ACTIVE, due within 14 days) |
| 0 | EMAIL | PAST_DUE comms |
| +3 | WHATSAPP, **no EmailBody** | Skipped, still logged dispatched |
| grace 7 | CANCEL | `SubscriptionCanceled` → **Subscription Cancelled** |

`reminder.due` + `template_id` is implemented in the hydrator and **never published** by `BillingEngineJob` or the dunning job.

**C. Catalog Payment Failed (wrong moment)**

`LifecycleEventHandlers.HandleAsync(SubscriptionSuspended)`:

- Loads **Payment Failed**.  
- Replaces only `{{customer_name}}` and `{{renewal_link}}`.  
- `{{renewal_link}}` = hardcoded `https://portal.lazuar.com/checkout/update` (not `App:ClientUrl`, not slug, not update-payment, not token).  
- Subject left with literal `{{plan_name}}`.  
- `WhatsAppBody` forced `null`.  

Default `FinalAction` is **CANCEL**, so this handler **does not run** on the seeded campaign. LP-078 already parked the dead URL as LP-151.

No `LifecycleEventHandlers` tests.

### 2.5 Magic-link path — token exists, mail does not

`MagicLinkTokenService`: Base64(`{subscriptionId}:{expiryUnix}:{hmacHex}`), 24h, `Jwt:Secret`. Tested.

| Surface | Token? |
|---------|--------|
| Dunning hydrate `{{portal_magic_link}}` | **Yes** — only if the merchant’s **step** text contains the tag. Seed text does not |
| Dunning `{{renewal_link}}` | Naked `{ClientUrl}/{slug}/portal` |
| Dunning `{{update_payment_link}}` | `{ClientUrl}/{slug}/update-payment/{subId}` — **no** token; page is public arrears |
| Digital delivery `{{portal_magic_link}}` | **No** — same portal origin |
| Lifecycle Payment Failed | Dead host |
| `SubscriptionActivated` | Webhook only (`SubscriptionLifecycleIntegrationEventHandlers`) |
| Public `requestMagicLink` | **Removed** from `public-routes.tsp`. No POST |
| Portal empty state / `/` / legal / privacy | Copy assumes the email exists |

Portal GET `/public/commerce/{tenantSlug}/portal?token=` **requires** a valid token (`PublicPortalEndpoints`). Untokened “Access Portal” links 401.

`OrderCompletedIntegrationEvent` has `OrderId`, not `SubscriptionId`. Cannot mint. One-time buyers have no portal row. Do not invent order-scoped tokens here.

### 2.6 Tests that exist vs missing

| Coverage | File | LP-151? |
|----------|------|---------|
| Catalog names / reset / orphans | `DefaultMessageTemplatesTests.cs` | Extend if adding Portal Access |
| DocumentPublished happy + empty email/slug | `DocumentPublishedIntegrationEventHandlerTests.cs` | Extend: wrong document type must not use Official Receipt |
| Dunning hydrate + token | `DunningTemplateVariableSubstitutionTests.cs` | LP-073 / LP-153; do not expand |
| Dispatch + Resend adapter | Messaging tests | Keep; add BYOK-missing `FAILED` if cheap |
| Token mint/validate | `MagicLinkTokenServiceTests.cs` | Enough |
| Fail → PAST_DUE + campaign | `GatewayPaymentFailedIntegrationEventHandlerTests.cs` | Add: still no comms from **Commerce** handler; new Communications tests own the mail |
| GenerateAndStore customer email | **none** | **Must add** — this is the receipt skip |
| Lifecycle Payment Failed | **none** | **Must add** (unhook + new fail handler) |
| Digital delivery | **none** | Do not start (not this ID) |
| Entitlement seed-if-missing | **none** | Add with catalog insert |

---

## 3. Gaps vs “configured but never sent”

Checkout cannot open without Resend. Templates page shows Official Receipt / Payment Failed. Portal says the link is in the inbox. Delivery:

| # | Gap | Why the buyer gets nothing |
|---|-----|----------------------------|
| G1 | Billing document lookup races Commerce `TransactionLogs` | `CustomerEmail=""` → DocumentPublished handler returns. **Every** gateway Official Receipt. |
| G2 | Manual enroll reference ≠ `TransactionLogs.ExternalReference` | Same skip for Log Payment / enroll receipts. |
| G3 | Seed is `Any()` not “missing catalog names” | Orphan-only orgs never get Official Receipt / Payment Failed rows; handlers no-op. |
| G4 | No Communications consumer on `GatewayPaymentFailed` | Decline is webhook-only. |
| G5 | Payment Failed bound to `SubscriptionSuspended` | Default CANCEL never hits it; even SUSPEND sends a dead URL. |
| G6 | No first-activation mail | `SubscriptionActivated` does not dispatch. Token service unused. |
| G7 | No public request-magic-link | Portal empty state is a dead end after 24h or if G6 never ran. |
| G8 | `DocumentType` default → Official Receipt | LHDN Tax Invoice / Credit Note would impersonate a receipt if G1 is fixed without a type guard. |

### Not LP-151 (do not touch)

| Item | Owner |
|------|--------|
| Shared hydrator, wiki tags, `{{plan_name}}` on every path, `current_period_end` | **LP-153** |
| Campaign day offsets, default +3 WA, `reminder.due`, pre-dunning early fire | **LP-073** |
| Terminal after last step | **LP-078** |
| Campaign snapshot | **LP-079** |
| Bounce / unsub vs transactional split | **LP-154** |
| Welcome / “you are a member” copy | COM-007 — not this ID |
| Digital file URL / R2 asset | COM-008 |
| PDF attachment, amount-in-subject polish, Billplz double receipt | G09 / G38 / G41 — later |
| B2B Official Receipt | Wrong legally until LHDN individual send |
| WhatsApp / `ToPhone` on receipts | 00.4 freeze |
| Platform fallback for tenant mail | LP-150 lock |
| One reset / verify (system Resend) | Already live; not tenant buyer mail |

CTA URLs that **must** work for the new sends (not a resolver rewrite):

- Receipt: existing `DocumentLinkSigner` 30-day link (already in the handler).  
- Failed pay: `{App:ClientUrl}/{slug}/update-payment/{subscriptionId}`. Map catalog `{{renewal_link}}` to **that** URL on this handler only. Full tag taxonomy is LP-153.  
- Magic link: `GenerateToken` + `{App:ClientUrl}/{slug}/portal?token=`.

Do **not** fix lifecycle cancel variable fill here. Do **not** change dunning step Replace lists.

---

## 4. Recommended design (lock this, then code)

Reuse the existing split: **render at the source, dispatch at the edge.** No new bus. No new ESP.

### 4.1 Receipt — put a real email on `DocumentPublished`

Do **not** reorder Billing vs Commerce subscriptions (fragile; other handlers depend on it).

Extend `ICommerceDocumentLookup` with one method, e.g. `GetCustomerForDocumentAsync(org, gatewayOrLedgerRef, correlationId)`:

1. Existing `TransactionLogs` match (renewals after the log exists; LHDN reprint).  
2. If `correlationId` (or ref) is a Guid: `CheckoutSessions` by id → `ClientProfileId` → `ICrmQueryService` (first pay: metadata `subscription_id` **is** the session).  
3. Else if Guid: `Subscriptions` by id → CRM (off-session renew / `receipt` legacy).  

`GenerateAndStoreDocumentCommand`: add optional `CorrelationId`.  

`GatewayPaymentCompletedHandler`: pass metadata `subscription_id` or `receipt`.  

`ManualSubscriberEnrolledIntegrationEventHandler`: pass `SubscriptionId` as correlation (CRM via sub, not `MANUAL-` external ref).

Still publish the PDF if email is empty (ops download). Communications skip stays.

**Type guard** in `DocumentPublishedIntegrationEventHandler`:

| `DocumentType` | Template |
|----------------|----------|
| `Official Receipt` | Official Receipt |
| `Draft Quotation` | Quotation Ready |
| anything else | **return** (Tax Invoice / Credit Note are not this ticket) |

### 4.2 Failed pay — new Communications handler, unhook suspend

Add `IIntegrationEventHandler<GatewayPaymentFailedIntegrationEvent>` in Communications (mirror Commerce’s Guid resolve).

Skip if no Guid, no profile email, or sub is `CANCELED`. Send on **each** failed event (Stripe-shaped). Webhook business-key idempotency is **LP-090**; do not add a second mail-dedupe table.

Load **Payment Failed**. Replace only what this send needs:

- `{{customer_name}}`, `{{business_name}}` (workspace name)  
- `{{renewal_link}}` **and** `{{update_payment_link}}` → update-payment URL  
- `{{portal_magic_link}}` → tokenized portal (optional; catalog body today uses `renewal_link`)  
- `{{plan_name}}` if cheap (product name via Commerce query). If that needs a new port, leave the tag for LP-153 rather than blocking the send — body still has static text.

`ToPhone: null`. Channel from template is fine (`ALL` still emails).

**Unhook** `LifecycleEventHandlers` suspend → Payment Failed. SUSPEND after grace is not a card decline. Do not add a Suspended template. Cancel handler stays.

Day-0 dunning EMAIL + this mail on the same calendar day is **allowed**. That is two products (immediate fail vs campaign). Do not skip dunning day 0 here (LP-073).

### 4.3 Magic link — first activation + request

Add catalog definition **Portal Access** (name lock):

- Channel `EMAIL` (not ALL — no WA).  
- Required: `{{portal_magic_link}}`.  
- Optional: `{{customer_name}}`, `{{business_name}}`.  
- Copy: open dashboard; 24h. Not “download your file.”

**Send 1:** Communications handler on `SubscriptionActivatedIntegrationEvent` when `IsFirstPayment`. Mint token. Dispatch. Recoveries (`IsFirstPayment=false`) do not re-spam.

**Send 2:** Public `POST /public/commerce/{tenantSlug}/portal/magic-link` `{ email }`. Always 200. If CRM email matches a subscription in that tenant, mint for the newest sub and dispatch the same template. Do not create a session. Rate-limit is nice-to-have (same IP/email); not a blocker if a one-line comment + existing public throttle is enough.

Portal empty state: one email field + submit. Do not redesign the portal.

Do **not** change `OrderCompletedDigitalDeliveryHandler` (wrong product, no sub id).

### 4.4 Seed missing catalog names

`AppEntitlementGranted`: for each `DefaultMessageTemplates.All` name, insert if that org lacks it. Still emit `DefaultTemplatesSeeded` only when **any** row was added (Commerce uses it to seed default campaigns — must stay idempotent via `HasAnyDunningCampaign`).

No unique `(OrganizationId, Name)` index today. Insert-if-missing in the handler is enough; do not add a migration unless a duplicate already bites tests.

---

## 5. Minimal code changes

### Must

1. **`ICommerceDocumentLookup` + `CommerceDocumentLookup`** — CRM fallback via checkout session / subscription.  
2. **`GenerateAndStoreDocumentCommand` (+ Billing callers)** — pass correlation; keep R2 + event.  
3. **`DocumentPublishedIntegrationEventHandler`** — exact type match; no default-to-receipt.  
4. **`GatewayPaymentFailed` Communications handler** + subscribe in `UseCommunicationsSubscriptions`.  
5. **`LifecycleEventHandlers`** — delete Payment Failed on suspend (keep cancel).  
6. **`DefaultMessageTemplates`** — add Portal Access.  
7. **`SubscriptionActivated` Communications handler** — first payment only.  
8. **Public request-magic-link** + portal form.  
9. **`AppEntitlementGrantedIntegrationEventHandler`** — seed missing names.

### Should (still this ticket, tiny)

10. `DispatchMessageIntegrationEventHandlerTests`: missing BYOK → throw + `FAILED` log (locks “no silent skip”).  
11. Failed-pay / activation: `ToPhone: null` so WA flag cannot charge credits.

### Must not

- Shared `PopulateVariables` service (LP-153).  
- DunningEngineJob / default campaign / `reminder.due`.  
- Reorder module subscriptions.  
- Platform Resend fallback.  
- PDF attach, amount-in-subject, B2B receipt, welcome template, digital-delivery rewrite.  
- TypeSpec event catalog beyond the new public POST.  
- Ops delivery-log UI (COM-017).

---

## 6. Tests

### Receipt

| Test | Assert |
|------|--------|
| `GetCustomerForDocument_FallsBackToCheckoutSessionCrm_WhenTransactionLogMissing` | Email from CRM, not `""` |
| `GetCustomerForDocument_FallsBackToSubscriptionCrm` | Off-session correlation |
| `GenerateAndStore_PublishesDocumentPublished_WithCustomerEmail` | Event email == CRM (in-memory lookup fake) |
| `GenerateAndStore_StillPublishes_WhenEmailEmpty` | Event email empty; PDF path still set |
| `DocumentPublished_OfficialReceipt_Dispatches` | existing happy path |
| `DocumentPublished_TaxInvoice_DoesNotDispatchOfficialReceipt` | **new** |
| `DocumentPublished_MissingEmail_NoDispatch` | existing |

### Failed pay

| Test | Assert |
|------|--------|
| `GatewayPaymentFailed_DispatchesPaymentFailed_WithUpdatePaymentUrl` | `DispatchMessage.ToEmail`, body contains `/{slug}/update-payment/{subId}`, **not** `portal.lazuar.com/checkout/update` |
| `GatewayPaymentFailed_CanceledSub_NoDispatch` | |
| `GatewayPaymentFailed_NoSubscriptionMetadata_NoDispatch` | |
| `SubscriptionSuspended_DoesNotDispatchPaymentFailed` | unhook |

Keep Commerce fail-handler tests: PAST_DUE + campaign assign **unchanged**.

### Magic link

| Test | Assert |
|------|--------|
| `SubscriptionActivated_FirstPayment_DispatchesPortalAccessWithToken` | `GenerateToken` called; HTML contains `token=` |
| `SubscriptionActivated_NotFirstPayment_NoDispatch` | |
| `RequestMagicLink_MatchingEmail_Dispatches` | |
| `RequestMagicLink_UnknownEmail_NoDispatch_Returns200` | |

Token crypto: existing `MagicLinkTokenServiceTests` only.

### Seed

| Test | Assert |
|------|--------|
| `Entitlement_EmptyOrg_SeedsFullCatalogIncludingPortalAccess` | |
| `Entitlement_OrphanOnlyOrg_InsertsMissingCatalogNames` | Official Receipt + Portal Access appear; orphans remain |

No live Resend in CI. Stop at `IEmailService` / `DispatchMessage` publish, same as today.

---

## 7. Files to touch (when implementing)

| File | Change |
|------|--------|
| `apps/lazuar-api/Modules/Commerce/Contracts/ICommerceDocumentLookup.cs` | New lookup |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` | Session / sub → CRM |
| `apps/lazuar-api/Modules/Billing/Contracts/Commands/GenerateAndStoreDocumentCommand.cs` | Optional correlation |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs` | Use it |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` | Pass metadata Guid |
| `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs` | Pass `SubscriptionId` |
| `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` | Type guard |
| `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/LifecycleEventHandlers.cs` | Unhook suspend |
| `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/` (new) | Failed-pay + activation handlers |
| `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/AppEntitlementGrantedIntegrationEventHandler.cs` | Seed-if-missing |
| `apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs` | Portal Access |
| `apps/lazuar-api/Modules/Communications/Infrastructure/DependencyInjection.cs` | Subscribe new handlers + `GatewayPaymentFailed` + `SubscriptionActivated` |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicPortalEndpoints.cs` | POST magic-link |
| `packages/api-spec/modules/commerce/` (public routes) | Same POST |
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` | Request form |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/` | GenerateAndStore email |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/` | Document type, fail, activate, seed |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/` | Lookup fallback + public request |

No new table. No Messaging/Resend adapter change unless a test requires it.

---

## 8. Definition of done

1. With tenant BYOK + catalog rows, a B2C test payment produces `MessageDeliveryLog` EMAIL/`SENT` (or a test double of `IEmailService.SendEmailAsync` with the tenant key) for Official Receipt, and `To` is the buyer.  
2. A failed off-session / webhook decline for an ACTIVE/PAST_DUE sub produces Payment Failed to the buyer with a tenant update-payment URL.  
3. First activation and portal request produce a token that `ValidateToken` accepts and portal GET returns 200.  
4. SUSPEND does not send Payment Failed. Tax Invoice does not send Official Receipt.  
5. Tests in §6 green. No LP-073 engine edits. No LP-153 resolver.

---

## 9. Honesty check after ship

Still true, and must not be sold as done by LP-151:

- Dunning **sequence** quality (LP-073 / default +3 WA).  
- Unresolved tags on cancel / digital delivery (LP-153).  
- Portal invoice history (LP-175).  
- Processor (Billplz/Stripe) may still send their own receipt.
