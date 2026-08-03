<!-- Source subagent: 019fc650-3514-7d71-af79-0643981d0c47 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Event-Driven Architecture Gap Analysis

**Scope:** Lazuar Hub modular monolith (`/Users/akmalfirdaus/Code/lazuar/lazuar-hub`), focused on `apps/lazuar-api` integration events, outbox/inbox, and cross-module handlers.  
**Primary policy docs:**  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/003-event-driven-vs-building-blocks.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/docs/001-cross-module-communication.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/004-payment-integration-and-event-driven-guiideline.md`

---

## Communication Patterns Used

### 1. Documented ideal (async integration events)

Per `001-cross-module-communication.md` and ADR-003:

| Step | Intended behavior |
|------|-------------------|
| Publish | Module writes `IIntegrationEvent` to **local** `OutboxMessages` in the same DB transaction as state change |
| Dispatch | Module `OutboxPublisherJob` polls with `FOR UPDATE SKIP LOCKED`, deserializes, publishes via `InMemoryEventBus` |
| Receive | Subscribing handlers **persist to Inbox**, ack immediately |
| Consume | Module `InboxConsumerJob` processes inbox asynchronously (MediatR `INotification`) |

### 2. What is actually implemented

| Pattern | Where | Reality vs docs |
|---------|--------|-----------------|
| **Transactional Outbox (write path)** | Keyed `OutboxEventBus<TDbContext>` per module | **Present** for most modules |
| **Outbox poll + bus publish** | `OutboxPublisherJob<T>` | **Present** for One, Messaging, Payments, Billing, Commerce, Communications, Ops; **missing for Lhdn and CRM** |
| **Inbox store-and-ack (true inbox)** | Only **Messaging** tenant/workspace handlers | **Partial** — almost all other handlers run **inline** on outbox dispatch |
| **Inbox consumer (MediatR)** | Hosted per module with tables | **Mostly idle** except Messaging (and only for events written to Messaging inbox) |
| **In-process event bus** | Singleton `InMemoryEventBus` | Used as fan-out after outbox; multi-handler sequential invoke |
| **Domain events → integration events** | One `PlatformDbContext.SaveChangesAsync` → MediatR domain handlers → Outbox | Used for org create/update, user profile, auth notifications |
| **Sync cross-module queries** | Contracts (`ICrmQueryService`, `IBillingQueryService`, etc.) | Actively used (e.g. Commerce payment handler → CRM) |
| **Sync building-block I/O** | Messaging `DispatchMessage` → `IEmailService` / `IMessagingService` | Correct side-effect ownership (after event) |
| **Separate developer webhook outbox** | One `WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob` | Second outbox, HMAC POST to customer URLs |
| **LHDN customer webhooks** | `DispatchExternalWebhookCommand` + `WebhookSenderService` | Fire-and-forget HTTP; **not** outbox-backed |

### 3. Actual runtime topology

```
Command/Job (module TX)
  └─ OutboxEventBus → {schema}.OutboxMessages
        └─ *OutboxPublisherJob (5s / DatabaseJobTrigger)
              └─ InMemoryEventBus.PublishAsync (runtime type name)
                    ├─ Handler A (often mutates DB / external IO immediately)
                    ├─ Handler B …
                    └─ Messaging: write Inbox only → MessagingInboxConsumer → MediatR handlers
```

**Critical divergence:** docs describe **inbox-backed eventual consistency**; production code mostly uses **outbox + synchronous multi-handler fan-out**. Failure of handler N after handlers 1…N−1 still marks outbox row `ProcessedAt` (poisoned-message “always process” policy).

---

## Integration Event Catalog

### Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Published and has ≥1 live subscriber |
| ⚠️ | Defined + published **or** subscribed but chain broken |
| ❌ | Orphan: never published and/or never subscribed / never dispatched |

### Payments (`Modules/Payments/Contracts/Events`)

| Event | Payload (high level) | Publishers | Subscribers | Status |
|-------|----------------------|------------|-------------|--------|
| `GatewayPaymentCompletedIntegrationEvent` | OrgId, GatewayTransactionId, AmountPaid, Currency, fees/tax/net/FX, LineItems, Metadata, optional vault tokens | `ProcessGatewayWebhookCommandHandler` | Commerce `GatewayPaymentCompletedIntegrationEventHandler`; Billing `GatewayPaymentCompletedHandler` + `PlatformTopUpEventHandler` | ✅ Core payment spine |
| `GatewayPaymentFailedIntegrationEvent` | OrgId, GatewayTransactionId, Metadata | **None** (webhook ignores non-completed) | **None** | ❌ Documented (014/Payments README) but dead |
| `GatewayRefundCompletedIntegrationEvent` | OrgId, SubscriptionId, PaymentRecordId, amounts | `GatewayRefundRequestedIntegrationEventHandler` | Billing, Commerce, Lhdn handlers | ⚠️ Consumers exist; **request never published** → completion path starved |
| `GatewayRefundFailedIntegrationEvent` | OrgId, SubscriptionId, PaymentRecordId, ErrorMessage | Refund handler (on failure) | **None** | ⚠️ Published, zero consumers |
| `GatewayRefundRequestedIntegrationEvent` | OrgId, SubscriptionId, PaymentRecordId, GatewayTransactionId, GatewayName | **None found** | Payments refund handler | ❌ Request event orphaned |
| `GatewayDisputeCreatedIntegrationEvent` | OrgId, GatewayTransactionId, AmountDisputed, Currency, Metadata | `ProcessGatewayWebhookCommandHandler` (`DISPUTE_CREATED`) | Billing `ChargebackClawbackHandler` | ✅ |
| `ExecuteOffSessionChargeIntegrationEvent` (**Payments**) | TenantId, SubscriptionId, Amount, Currency, vault IDs, DunningCampaignId, GatewayName | Commerce `BillingEngineJob` / `DunningEngineJob` (explicit Payments namespace) | Payments `ExecuteOffSessionChargeIntegrationEventHandler` | ✅ |
| `ExecuteOffSessionChargeIntegrationEvent` (**Commerce duplicate**) | Same without GatewayName | **Unused** (Commerce contracts copy) | None for this type | ❌ Dead twin type — bus keys by short name, dual types risk confusion |
| `ApiCreditPurchasedIntegrationEvent` | OrgId, CreditAmount, AmountPaid, Currency, GatewayTransactionId | **None** | Handler exists, **not subscribed** in Billing DI | ❌ |

### Commerce (`Modules/Commerce/Contracts/Events`)

| Event | Payload | Publishers | Subscribers | Status |
|-------|---------|------------|-------------|--------|
| `SubscriptionActivatedIntegrationEvent` | Org, Sub, Client, Product, FulfillmentTargets, IsFirstPayment | Payment completed handler; zero-amount checkout; manual enroll path | Commerce lifecycle handlers (→ outbound webhooks for http targets); Communications lifecycle (suspended/canceled only — **not** activated) | ✅ |
| `SubscriptionSuspendedIntegrationEvent` | + FulfillmentTargets | Dunning / billing engine paths | Communications `LifecycleEventHandlers`; Commerce lifecycle → webhooks | ✅ |
| `SubscriptionCanceledIntegrationEvent` | + FulfillmentTargets | Dunning final action | Communications + Commerce lifecycle | ✅ |
| `SubscriptionResumedIntegrationEvent` | + FulfillmentTargets | Payment recovery path | Commerce lifecycle → webhooks | ✅ |
| `OrderCompletedIntegrationEvent` | Org, Order, Client, Product, FulfillmentTargets | Payment completed; zero-amount | Commerce `OrderCompletedIntegrationEventHandler` → webhooks | ✅ |
| `ZeroAmountCheckoutCompletedIntegrationEvent` | Session, Client, amounts, coupon, Metadata | `ProcessZeroAmountCheckoutCommand` | Billing `ZeroAmountCheckoutHandler` | ✅ |
| `ManualSubscriberEnrolledIntegrationEvent` | Sub, Client, Product, AmountPaid, method, ref | Manual subscriber / mark paid offline | Billing `ManualSubscriberEnrolledIntegrationEventHandler` | ✅ |
| `FulfillmentRequestedIntegrationEvent` | Org, InternalTargetApp, EventType, JsonElement Payload | BillingEngine, DunningEngine | Communications `FulfillmentRequestedIntegrationEventHandler` (only `COMMUNICATIONS` + reminder types) | ⚠️ Narrow filter; many event types may no-op |
| `OutboundWebhookRequestedIntegrationEvent` | Org, TargetUrl, EventType, JsonElement Payload | Lifecycle / order / dunning / billing engine | One `OutboundWebhookEventHandlers` → `WebhookDeliveryOutbox` | ✅ (with URL-match constraint) |

### Billing (`Modules/Billing/Contracts/Events`)

| Event | Publishers | Subscribers | Status |
|-------|------------|-------------|--------|
| `InvoiceIssuedIntegrationEvent` | **Never published** | Billing `InvoiceIssuedHandler`; Lhdn `InvoiceIssuedIntegrationEventHandler` | ❌ Full orphan (handlers + LHDN path dead) |
| `ConsolidatedInvoiceIssuedIntegrationEvent` | `B2cConsolidationJob` | Lhdn consolidated handler | ⚠️ Published via Billing outbox; Lhdn consumes **inline** (no Lhdn outbox needed for *consume*) |
| `DocumentPublishedIntegrationEvent` | `GenerateAndStoreDocumentCommandHandler` | Communications `DocumentPublishedIntegrationEventHandler` | ✅ |
| `ManualPaymentRecordedIntegrationEvent` | **None** | **None** | ❌ Contract-only |
| `CommissionAccruedIntegrationEvent` | **None** (no affiliates module) | Billing `CommissionAccruedHandler` subscribed | ❌ |

### One (`Modules/One/Contracts`)

| Event | Publishers | Subscribers | Status |
|-------|------------|-------------|--------|
| `TenantProvisionedIntegrationEvent` | `OrganizationCreatedDomainEventHandler` | Messaging inbox write → MediatR seeding | ✅ |
| `TenantUpdatedIntegrationEvent` | **None** | Messaging inbox + MediatR update handler | ❌ Subscribe-only orphan |
| `WorkspaceUpdatedIntegrationEvent` | `OrganizationUpdatedDomainEventHandler` | Messaging inbox; Api host cache invalidation | ✅ |
| `GlobalUserProfileUpdatedIntegrationEvent` | Domain handler | CRM `GlobalUserProfileUpdatedIntegrationEventHandler` | ⚠️ CRM has **no workers** but handler is **inline** via bus (works if One outbox runs) |
| `AppEntitlementGrantedIntegrationEvent` | Register user, create workspace, toggle entitlement | Communications template seeder; Billing starter credits | ✅ |

### Lhdn (`Modules/Lhdn/Contracts/Events`)

| Event | Publishers | Subscribers | Status |
|-------|------------|-------------|--------|
| `LhdnDocumentSubmittedIntegrationEvent` | `LhdnSubmissionJob` → LhdnEventBus outbox | Billing submitted handler | ⚠️ **Lhdn has no OutboxPublisherJob** → events **stuck in `lhdn.OutboxMessages`** |
| `LhdnDocumentValidatedIntegrationEvent` | `LhdnStatusPollingJob` | Billing validated handler | ⚠️ Same stuck-outbox; also **constructor arg / status mismatch** |
| `LhdnDocumentCancelledIntegrationEvent` | `CancelTaxDocumentCommand` | Billing cancelled handler | ⚠️ Same stuck-outbox |
| `ApiKeyRevokedIntegrationEvent` | `RevokeApiKeyCommand` | Api `ApiKeyRevokedIntegrationEventHandler` (cache) | ⚠️ Same stuck-outbox → **API key cache not invalidated** |

### CRM

| Event | Publishers | Subscribers | Status |
|-------|------------|-------------|--------|
| `ClientProfileAnonymizedIntegrationEvent` | `AnonymizeClientProfileCommandHandler` via CrmEventBus | **None** (docs mention Community purge) | ❌ **No CRM outbox job** + no subscribers → GDPR fan-out broken |

### Communications / Messaging

| Event | Publishers | Subscribers | Status |
|-------|------------|-------------|--------|
| `DefaultTemplatesSeededIntegrationEvent` | Communications entitlement seeder | Commerce `DefaultTemplatesSeededIntegrationEventHandler` | ✅ |
| `DispatchMessageIntegrationEvent` | Communications (fulfillment, lifecycle, document, broadcast); One auth notifications | Messaging **inline** `DispatchMessageIntegrationEventHandler` (not via inbox) | ✅ (by design of messaging I/O) |

### Ops

No integration event contracts or subscriptions (`UseOpsSubscriptions` empty). Inbox/outbox tables + workers present but idle.

---

## Outbox/Inbox Implementation Quality per Module

Shared building blocks:

| Component | Path | Notes |
|-----------|------|-------|
| `OutboxEventBus<T>` | `BuildingBlocks/Infrastructure/OutboxEventBus.cs` | Serializes with runtime type; Id = event.Id |
| `OutboxPublisherJob<T>` | `…/OutboxPublisherJob.cs` | SKIP LOCKED, batch 20, **always** sets ProcessedAt |
| `InboxConsumerJob<T>` | `…/InboxConsumerJob.cs` | MediatR `Publish` only; same always-processed |
| `InMemoryEventBus` | `…/InMemoryEventBus.cs` | Runtime `GetType().Name` (ADR-004 fix applied) |
| `TypeResolver` | AssemblyQualifiedName + scan fallback | Sensitive to assembly renames |
| `PlatformDbContext` | Domain events then save; `JobTrigger.Trigger()` | Wake workers promptly |

### Per-module matrix

| Module | Outbox tables | Outbox job | Inbox tables | Inbox job | Inbox write handlers | EventBus key | Quality |
|--------|---------------|------------|--------------|-----------|----------------------|--------------|---------|
| **One** | Yes | Yes | Yes | Yes | No (handlers process inline / webhook table) | `OneEventBus` | **Good** for outbox; inbox unused |
| **Messaging** | Yes | Yes | Yes | Yes | **Yes** (Tenant/Workspace) | `MessagingEventBus` | **Best inbox example**; DispatchMessage is **not** inbox’d |
| **Payments** | Yes | Yes | Yes | Yes | No | `PaymentsEventBus` | Outbox good; inbox empty |
| **Billing** | Yes | Yes | Yes | Yes | No | `BillingEventBus` | Outbox good; multi-handler side effects on bus thread |
| **Commerce** | Yes | Yes | Yes | Yes | No | `CommerceEventBus` | Outbox good; heavy work in payment handler on bus |
| **Communications** | Yes | Yes | Yes | Yes | No | `CommunicationsEventBus` | Outbox good; re-publishes DispatchMessage |
| **Ops** | Yes | Yes | Yes | Yes | No events | `OpsEventBus` | Scaffold only |
| **Lhdn** | Yes | **NO** | Yes | **NO** | No | `LhdnEventBus` | **Critical defect** — publishes never leave |
| **CRM** | Yes | **NO** | Yes | **NO** | No | `CrmEventBus` | **Critical defect** for anonymize; GlobalUserProfile works only because it is **consumer**, not publisher |

### Implementation quality issues (shared)

1. **Inbox pattern incomplete:** Docs require inbox for state mutation; only Messaging implements store-and-ack. Commerce/Billing/Lhdn/CRM handlers mutate state **during** outbox publish.
2. **No retries / backoff on outbox/inbox:** Error string stored; message marked processed forever. No dead-letter redrive, no exponential retry.
3. **No poison-message quarantine:** “Critical fix” prevents infinite loops but **drops** failed events.
4. **No consumer isolation:** One failed handler doesn’t prevent marking outbox processed after partial fan-out success (handlers run sequentially; exception only after partial work).
5. **InboxConsumer uses MediatR, Outbox uses IIntegrationEventHandler:** Dual mechanisms; most modules never register `INotificationHandler` for integration events → inbox rows would no-op if written.
6. **Idempotency uneven:** Payments webhook log + Billing ledger `HasEntryBeenProcessedAsync` good; many Commerce/Lhdn handlers weaker or status-gated only.
7. **Ordering:** Per-schema FIFO by `OccurredOn`/`ReceivedAt` batch of 20; **no global order** across modules; concurrent handlers on same payment possible between Billing and Commerce.

---

## Ordering, Idempotency, Failure Handling

### Ordering

- **Within module outbox:** Ordered by `OccurredOn`, SKIP LOCKED → multiple app instances OK for partition.
- **Across modules:** Eventual; Billing ledger vs Commerce subscription activation race possible.
- **Chained events:** Payment → Commerce activates → SubscriptionActivated → OutboundWebhook is multi-hop outbox (acceptable latency, hard to debug).
- **Lhdn status polling + external webhooks:** DispatchExternalWebhook runs **inside** polling job **before/with** SaveChanges, while integration event goes to stuck outbox — external webhook may fire even if internal Billing never sees validation.

### Idempotency

| Layer | Mechanism | Assessment |
|-------|-----------|------------|
| Gateway webhooks | `PaymentWebhookLog` unique EventId | Strong for ingress |
| Billing ledger | `HasEntryBeenProcessedAsync(referenceType, referenceId)` | Strong for gateway payment / invoice issued |
| Commerce checkout | Session status `COMPLETED` short-circuit | Partial; renewal path reuses subscription id heuristics |
| Off-session charge | `ChargeAttemptLog` per billing date | Good for renewals |
| Messaging inbox | Inbox Id = event.Id | Natural de-dupe if re-inserted (no unique index check in code) |
| Developer webhooks | Delivery outbox with retries on entity | Better than Lhdn fire-and-forget |
| Lhdn external webhooks | No delivery log / retry | Weak |
| Platform top-up | Wallet top-up by gateway id only if ledger uniqueness exists | Needs verification under double delivery |

### Failure handling

| Scenario | Behavior | Risk |
|----------|----------|------|
| Handler throws | Error on outbox row; **ProcessedAt set** | Silent loss after partial fan-out |
| No handlers | Log “no registered handlers”; success | Easy to miss orphan events |
| Lhdn publish | Rows pile in `lhdn.OutboxMessages` | **Systemic** loss of Lhdn→Billing + ApiKey revoke |
| CRM anonymize | Rows pile in `crm.OutboxMessages` | GDPR cascade never fires |
| Off-session charge fail | Log only; no `GatewayPaymentFailed` | Dunning may not advance correctly on hard failures |
| Refund request never published | Refund pipeline incomplete | Refunds/LHDN credit notes never start |

### Documented historical bugs (ADR-004) — status

| Pitfall | Status |
|---------|--------|
| Generic type binding → bus miss | **Fixed** (`GetType().Name`) |
| Billplz metadata loss | Documented mitigation via callback query string |
| EF child entity concurrency | Documented DbContext override pattern |

---

## Coupling Risks

### 1. Metadata dictionary as implicit contract

`GatewayPaymentCompletedIntegrationEvent.Metadata` carries:

- `type`: `commerce_subscription`, `custom_payment_link`, `utility_credit_topup`, …
- `subscription_id`, `tenant_id`, `dunning_campaign_id`, `is_b2b_required`

Handlers silently return if keys missing (ADR-009). This is **stringly-typed coupling** across Payments ↔ Commerce ↔ Billing with no schema versioning.

### 2. Product `FulfillmentTargets` dual semantics

Same string list mixes:

- `internal:COMMUNICATIONS` (and similar)
- Raw `https://…` customer webhook URLs

Outbound path requires URL to **exactly match** `TenantWebhookEndpoint.Url` in One; product-level URL without registered endpoint → silent drop in `OutboundWebhookEventHandlers`.

### 3. Compile-time module references for events

Consumers reference foreign `.Contracts` (good). But Communications handlers also pull CRM/One **query** contracts synchronously during fulfillment — acceptable read coupling, increases temporal coupling during event handling.

### 4. Duplicate event types

`ExecuteOffSessionChargeIntegrationEvent` in both Commerce and Payments contracts. Runtime name collision if both ever published: `InMemoryEventBus` keys by **short type name only** → handlers could be mis-dispatched.

### 5. Lhdn InvoiceIssued handler placeholder data

`InvoiceIssuedIntegrationEventHandler` hardcodes buyer TIN/address (“Resolved via CRM”). Even if publishing is restored, **tax documents would be wrong** without CRM resolution — semantic coupling unfinished.

### 6. Host layer as event subscriber

Api process subscribes to `ApiKeyRevoked` and `WorkspaceUpdated` for cache. Couples host process to module events (acceptable for monolith, bad for extraction).

### 7. Messaging credit deduction cross-module command

`DispatchMessageIntegrationEventHandler` calls Billing `DeductTenantCreditCommand` synchronously after send — financial side effect outside outbox of Messaging; failure logs “sent but not charged.”

### 8. Docs vs product reality (Community → Commerce)

ADR/docs still reference Community handlers; code lives in **Commerce**. Stale docs increase wrong-handler risk.

---

## Missing Events for Integration Webhooks to Customers

### Existing customer-facing outbound surfaces

| Surface | Module | Events / payloads today | Delivery quality |
|---------|--------|-------------------------|------------------|
| Workspace webhook (CaaS) | One `TenantWebhookEndpoint` + `OutboundWebhookDispatcherJob` | Only when Commerce publishes `OutboundWebhookRequested` with matching URL | HMAC, retries on delivery outbox |
| Product fulfillment URL | Commerce targets | `subscription.activated|suspended|canceled|resumed`, `order.completed`, dunning-related | Same path as above |
| LHDN developer webhooks | Lhdn `WebhookSubscription` | `invoice.{status}` from poll (`VALID`/`INVALID`) | **No outbox/retry**, best-effort HTTP |
| Gateway → Lazuar (inbound) | Payments | Not customer outbound | N/A |

### Customer webhook event coverage (desired vs present)

| Customer-visible domain | Emitted to customer webhooks? | Internal event exists? | Gap |
|-------------------------|-------------------------------|------------------------|-----|
| Payment succeeded (raw gateway) | **No** dedicated `payment.completed` | `GatewayPaymentCompleted` yes | ADR-019 promised listening to gateway payment for developer webhooks; **implementation only emits commerce lifecycle/order**, not payment itself |
| Payment failed | **No** | Event type exists, **never published** | Critical for SaaS unlock failures |
| Refund completed / failed | **No** | Internal refund events partial | Missing |
| Dispute / chargeback | **No** | `GatewayDisputeCreated` internal only | Missing |
| Subscription activated/resumed/canceled/suspended | **Yes** (if fulfillment URL registered) | Yes | OK if product configured |
| Order completed | **Yes** (same constraint) | Yes | OK |
| Checkout session completed (zero amount / offline) | Partial via order/sub events | Yes | No `checkout.completed` |
| Invoice issued (B2B) | **No** | `InvoiceIssued` never published | Missing |
| Consolidated invoice | **No** | Internal only | Missing |
| LHDN valid / invalid | **Yes** (Lhdn-specific webhooks) | Valid internal event stuck | Delivery split brain vs One webhooks |
| Document / receipt published | **No** | `DocumentPublished` → email path only | Missing webhook |
| Credits top-up | **No** | Platform top-up internal | Missing |
| Subscription past_due / dunning step | Partial (suspend webhooks; FulfillmentRequested for reminders) | Mixed | No first-class `subscription.past_due` / `invoice.payment_failed` |
| GDPR profile anonymized | **No** | Publish broken | Missing |
| Tenant/workspace lifecycle | **No** | Internal messaging only | Optional |

### Structural gaps for CaaS developer webhooks

1. **No event catalog / versioning** exposed to customers (OpenAPI/TypeSpec models for webhook payloads are thin).
2. **Single workspace endpoint** vs multi-endpoint + event filters (Stripe-style `enabled_events`).
3. **No fan-out from gateway payment** to workspace webhook (must configure per-product URLs).
4. **No `payment.failed` / refund / dispute** customer events.
5. **Two webhook systems** (One vs Lhdn) with different reliability and payload shapes.
6. **OutboundWebhookRequested requires TargetUrl** — cannot “notify all endpoints subscribed to event type X.”
7. **Signature scheme** present (`X-Lazuar-Signature`) but no documented event envelope version field beyond ad-hoc `event_type`.

---

## Gaps & Recommendations

### P0 — Correctness / data loss

1. **Register Lhdn OutboxPublisherJob (+ ideally InboxConsumerJob)**  
   Without this, `LhdnDocumentSubmitted|Validated|Cancelled` and `ApiKeyRevoked` never reach Billing/Api. Highest severity infrastructure gap.

2. **Register CRM OutboxPublisherJob**  
   Required for `ClientProfileAnonymizedIntegrationEvent` (GDPR).

3. **Fix `LhdnDocumentValidatedIntegrationEvent` publish call**  
   Current call passes `(…, LongId, "VALID")` into `(Status, QrLink)`. Billing handler expects `@event.Status == "VALIDATED"` while poll publishes `"VALID"` even under correct mapping → PDF generation path never runs.

4. **Publish `InvoiceIssuedIntegrationEvent`** (or remove dead handlers)  
   B2B LHDN submission path is currently unreachable.

5. **Wire refund request path**  
   Publish `GatewayRefundRequestedIntegrationEvent` from Commerce/Billing when refunds are initiated; add consumers for `GatewayRefundFailed`.

### P1 — Align architecture with docs

6. **Choose one inbox strategy and implement consistently**  
   - Option A (docs): every cross-module handler = “write inbox only”; business logic only in InboxConsumer (MediatR or dedicated handlers).  
   - Option B (current simplified): drop inbox requirement from docs; treat outbox+inline handlers as official; use inbox only for Messaging-style replication.  
   Current hybrid is the worst of both worlds.

7. **Retry / dead-letter**  
   Do not mark ProcessedAt on failure without retry count; add `Attempts`, `NextVisibleAt`, max attempts → DLQ table + ops alert.

8. **Handler isolation**  
   On multi-handler fan-out, catch per-handler and continue; or insert per-subscriber inbox rows so failures don’t drop sibling handlers.

### P2 — Orphans & dual definitions

9. **Delete or implement:**  
   - `GatewayPaymentFailedIntegrationEvent` (publish from webhook + off-session failure)  
   - `ApiCreditPurchasedIntegrationEvent` + subscribe Billing handler **or** fold into top-up metadata path  
   - `ManualPaymentRecordedIntegrationEvent`, `CommissionAccruedIntegrationEvent`, `TenantUpdatedIntegrationEvent`  
   - Commerce duplicate `ExecuteOffSessionChargeIntegrationEvent`

10. **Bus keying**  
    Prefer full type name / AssemblyQualifiedName for subscriptions to avoid short-name collisions.

### P3 — Customer integration webhooks (CaaS)

11. **Central outbound projector** listening to high-value internal events:  
    `payment.completed`, `payment.failed`, `subscription.*`, `order.completed`, `refund.*`, `dispute.created`, `invoice.*`, `lhdn.document.*`, `document.published`.

12. **Endpoint model:** multi-URL, `enabled_events[]`, optional product filter; remove exact URL match requirement from fulfillment targets (resolve endpoints by org + event type).

13. **Unify Lhdn + One delivery** onto one durable dispatcher (HMAC, retries, delivery logs, replay).

14. **Public webhook contract** in TypeSpec/docs with versioned envelopes and sample signatures.

### P4 — Domain completeness

15. **Resolve real buyer data** in Lhdn invoice handlers via CRM query contracts.  
16. **Publish GatewayPaymentFailed** and drive dunning from it (docs already assume this).  
17. **ClientProfileAnonymized** subscribers in Commerce (cancel subs / purge PII).  
18. **Idempotency** for PlatformTopUp and DispatchMessage credit paths under at-least-once bus delivery.

### P5 — Documentation hygiene

19. Update ADR-014 / module READMEs: Community → Commerce; document real inbox deviation; document Lhdn worker requirement in “new module” checklist.  
20. Architecture tests: assert every module with `OutboxEventBus` has matching OutboxPublisherJob registration; assert every published event type has ≥1 subscriber (or allowlist).

---

## File Evidence Notes

### Policy / ADR

| File | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/003-event-driven-vs-building-blocks.md` | Default to integration events over sync BB side effects |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/docs/001-cross-module-communication.md` | Outbox + inbox rules; sync query exception |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/004-payment-integration-and-event-driven-guiideline.md` | Bus generic bug, metadata, EF pitfall |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/009-stateless-webhook-metadata-transmission.md` | Metadata-required handoff |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/019-checkout-as-a-service-pivot.md` | Developer outbound webhooks vision |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/014-apps.md` | Intended event matrix (partially stale) |

### Building blocks

| File | Role |
|------|------|
| `…/BuildingBlocks/Application/IEventBus.cs` | `IIntegrationEvent`, handler, bus |
| `…/BuildingBlocks/Infrastructure/OutboxEventBus.cs` | TX outbox write |
| `…/BuildingBlocks/Infrastructure/OutboxPublisherJob.cs` | Poll + always-processed |
| `…/BuildingBlocks/Infrastructure/InboxConsumerJob.cs` | Poll + MediatR + always-processed |
| `…/BuildingBlocks/Infrastructure/InMemoryEventBus.cs` | Runtime type dispatch |
| `…/BuildingBlocks/Infrastructure/PlatformDbContext.cs` | Domain events + job trigger |
| `…/BuildingBlocks/Infrastructure/TypeResolver.cs` | Deserialize type map |

### Module DI / workers (evidence of missing Lhdn/CRM jobs)

| File | Finding |
|------|---------|
| `Modules/Lhdn/Infrastructure/DependencyInjection.cs` | OutboxEventBus; **no** hosted outbox/inbox |
| `Modules/CRM/Infrastructure/DependencyInjection.cs` | OutboxEventBus; **no** hosted workers |
| `Modules/*/Infrastructure/DependencyInjection.cs` (One, Payments, Billing, Commerce, Communications, Messaging, Ops) | Hosted outbox/inbox present |

### Representative handlers / publishers

| File | Finding |
|------|---------|
| `Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | Only COMPLETED + DISPUTE; no FAILED |
| `Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` | Metadata routing; publishes sub/order events |
| `Modules/Messaging/Infrastructure/TenantProvisionedIntegrationEventHandler.cs` | True inbox write pattern |
| `Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` | Inline I/O + credit deduct |
| `Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs` | URL-matched webhook outbox |
| `Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` | HMAC customer delivery |
| `Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` | Publishes validated event + external webhook; arg-order bug |
| `Modules/Lhdn/Infrastructure/EventHandlers/InvoiceIssuedIntegrationEventHandler.cs` | Hardcoded buyer; never fed |
| `Modules/Lhdn/Application/Commands/DispatchExternalWebhookCommand.cs` | LHDN customer webhook payload |
| `Modules/CRM/Infrastructure/AnonymizeClientProfileCommandHandler.cs` | Publishes anonymized (stuck without worker) |
| `src/Lazuar.Api/Program.cs` | Host subscriptions for cache events |

### Event contract directories (complete inventory roots)

- `Modules/Payments/Contracts/Events/` — 8 files  
- `Modules/Commerce/Contracts/Events/` — 10 files  
- `Modules/Billing/Contracts/Events/` — 5 files  
- `Modules/Lhdn/Contracts/Events/` — 4 files  
- `Modules/One/Contracts/*IntegrationEvent*.cs` — 5  
- `Modules/CRM/Contracts/ClientProfileAnonymizedIntegrationEvent.cs`  
- `Modules/Messaging/Contracts/DispatchMessageIntegrationEvent.cs`  
- `Modules/Communications/Contracts/Events/DefaultTemplatesSeededIntegrationEvent.cs`

### Tests (partial coverage)

- `tests/Lazuar.ModuleTests/Billing/EventHandlers/GatewayPaymentCompletedHandlerTests.cs`  
- `tests/Lazuar.ModuleTests/Billing/EventHandlers/ManualSubscriberEnrolledHandlerTests.cs`  
- No architecture test found that asserts outbox job registration or “every event has a consumer.”

---

### Executive summary

Lazuar Hub has a **solid outbox primitive** and a **working payment → commerce → fulfillment/webhook chain** for happy paths, plus a mature **Messaging inbox** for tenant replication. The architecture **does not match** the documented full outbox/inbox model: most handlers run **inline on outbox dispatch**, failures are **non-retried**, and two modules (**Lhdn**, **CRM**) publish into outbox tables with **no publisher workers**—a production data-loss class bug for tax lifecycle, API key revocation, and GDPR. Several event types are **orphans** (`InvoiceIssued`, `GatewayPaymentFailed`, refund request, commissions, etc.). Customer-facing webhooks for CaaS are **narrow** (commerce lifecycle URLs only), missing payment/refund/dispute/invoice events promised by product strategy, and split across two unreliable delivery styles.
