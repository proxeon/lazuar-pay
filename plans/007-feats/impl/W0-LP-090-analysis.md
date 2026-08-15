# W0-LP-090 — Inbound webhook verify + business-key idempotency

**Date:** 16 August 2026  
**Status:** Analysis only — **do not implement from this file**  
**Tracker:** [00-checklist-tracker.md](../00-checklist-tracker.md) `LP-090` (Wave 0, Lazuar **P**)  
**Implement list:** [00-implement-ids.md](../00-implement-ids.md) “Inbound webhook verify + business-key idempotency”  
**Evidence:** [13-payments-refunds-rails.md](../13-payments-refunds-rails.md) Hop A / `LP-PAY-005`–`007`; [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) §13; [docs/001-gaps/02-payment-webhooks.md](../../../docs/001-gaps/02-payment-webhooks.md) (**Hub-era — treat as historical; §2.6 lists what is already closed**)

**This ticket is not** LP-024 (success page after payment truth), LP-091/092 (refunds), LP-094 (disputes as product), LP-132/133 (outbound delivery), `LP-PAY-016` (two-phase raw intake), or `LP-PAY-017` (replay UI). Adjacent holes are listed only so implementers do not “fix” them here.

---

## 0. Verdict

Verify + EventId idempotency + **business-key** idempotency + transactional outbox write + unique-race → 200 are **already in tree**. Razorpay no longer invents a Guid. CHIP no longer treats `purchase.preauthorized` as paid. `PAYMENT_FAILED` is published. Tracker **P** is still correct.

It is still **P**, not **Y**, because:

1. **HTTP `{ received: true }` is treated like “this payment is done.”** `PaymentWebhookLog.ProcessedAt` is stamped when the handler **queues** the integration event, not when Commerce / Billing / M2M session fulfills. There is no intake status. Support cannot tell received from fulfilled from this table (Phase C notes already send people to SQL + three schemas).
2. **Redelivery after a Dead outbox is a silent 200.** `HasBeenProcessedAsync` / `HasBusinessKeyBeenProcessedAsync` return early without looking at `payments.OutboxMessages`. After five worker failures the outbox is `Status=Dead` + `ProcessedAt` set. Provider retry hits the log, ACK 200, **never re-queues**. That is “ACK success then permanently drop domain work.”
3. **CHIP still falls back to `Guid.NewGuid()`** when `id` is missing (`ChipCollectGatewayAdapter` L169). EventId **and** `GatewayTransactionId` become that Guid (`purchaseId = eventId`). Every retry is a new payment. Proposed `LP-PAY-020` belongs **inside this ticket**, not later.
4. **Billplz empty `id` → `EventId=""`.** First garbage callback wins `(BILLPLZ, "")`; later real bills with a missing id are dropped as duplicates.

**LP-090 is: fail-closed stable identities + re-queue Dead/missing outbox on redelivery + honesty that received ≠ fulfilled. Do not build the raw-intake table (`LP-PAY-016`). Do not wait for Commerce inside the HTTP request.**

---

## 1. Product contract for this ID

Sellable sentence after this ticket:

> A signed Billplz / Stripe / CHIP (and Razorpay) callback is verified, recorded once per **provider event** and once per **business payment identity**, and answered `{ received: true }` only after domain work is **durably queued**. `{ received: true }` is not “paid.” A later provider retry will re-queue work if the outbox died; it will not double-fulfill.

| Input | HTTP | Intake | Domain |
|-------|------|--------|--------|
| Bad / missing signature, missing secret, empty body, unknown gateway | **400** (not 200) | No new log (or no write) | Nothing queued |
| CHIP / Billplz / Razorpay money event with **no stable id** | **400** (`Verified=false`) | None | Nothing — fail closed |
| Unknown / unmapped event type (verified) | **200** | No row | Nothing (stops retry storms) |
| First `PAYMENT_COMPLETED` / `PAYMENT_FAILED` / `DISPUTE_CREATED` | **200** `{ received: true }` | Row + outbox **same SaveChanges** | Worker → Commerce / Billing / M2M |
| Same `EventId` again, outbox Pending or succeeded | **200** | Unchanged | No second publish |
| Same `EventId` or same `BusinessKey`, outbox **Dead** or missing | **200** | Same log; outbox **re-queued** | Worker runs again; downstream stays idempotent |
| Stripe `checkout.session.completed` + `payment_intent.succeeded` (same PI) | **200** both | One business key `PAYMENT_COMPLETED:pi_…` | **One** `GatewayPaymentCompleted` |
| Concurrent duplicate | Winner commits; loser 23505 → **200** | One row | One outbox |
| Outbox handler throws (attempt &lt; 5) | Already 200 | Unchanged | Retry with `2^n` minutes |
| Outbox hits 5 failures | Already 200 | Unchanged until provider retry | **Must** re-queue on next signed POST (this ticket) |

Industry cousins (do not copy extra product): Stripe/Svix “return 2xx quickly, process async, idempotent on event id **and** payment id, DLQ not silent.” We already 2xx after outbox insert. We do **not** persist raw bodies or offer replay UI on this ticket.

---

## 2. What exists (read, not redesigned)

### 2.1 One public route — all four rails

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs`

```
POST /api/v1/webhooks/payments/{gatewayType}/{tenantId}
```

| Fact | Detail |
|------|--------|
| Allow-list | `STRIPE`, `BILLPLZ`, `RAZORPAY`, `CHIP` (case-insensitive). Else **400** JSON `{ error }` — not 500 |
| Body | Raw string; empty → `InvalidOperationException` → `GlobalExceptionHandler` **400** |
| Headers | All request headers + `Query-*` from `Request.Query` (ADR-009) |
| Success | `Results.Ok(new { received = true })` **after** `mediator.Send` returns |
| 400 vs 500 | `InvalidOperationException` / `BusinessRuleValidationException` bubble → 400. Other exceptions rethrow → 500 (provider retries). Unknown gateway also caught as 400 (defense in depth) |
| Auth | None (correct). Tenant is the **path**, not the signed payload. Isolation = per-tenant `WebhookSecret` |

There are **no** per-gateway endpoint classes. Billplz / Stripe / CHIP share this method.

`GlobalExceptionHandler` puts `exception.Message` in `Detail` (including signature-failure text). Residual probe surface; do not change on this ticket.

### 2.2 Handler pipeline

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs`  
partials: `.Idempotency.cs`, `.Metadata.cs`, `.Logging.cs`

```
load TenantPaymentConfiguration (IgnoreQueryFilters) by (TenantId, GatewayType)
  missing or empty WebhookSecret → InvalidOperationException (400)
decrypt ApiKey / WebhookSecret (soft-disable still processes — keep)
adapter.ParseWebhookAsync(..., 0, 0, 0)
!Verified → InvalidOperationException (400)
EventType ∉ {PAYMENT_COMPLETED, DISPUTE_CREATED, PAYMENT_FAILED} → return (200, no log)
HasBeenProcessedAsync(EventId, Provider) → return (200, no publish)
BuildBusinessKey(EventType, GatewayTransactionId) = EventType + ":" + id, or null if id empty
HasBusinessKeyBeenProcessedAsync → return (200, no publish, **second EventId not stored**)
MergeSessionMetadataAsync (IntegrationCheckoutSession by ProviderSessionId)
new PaymentWebhookLog(EventId, Provider, BusinessKey)
PublishAsync matching integration event (outbox Add on same PaymentsDbContext)
TrySaveChangesAsync — swallow SQLSTATE 23505 as success
LogInformation EventId, Provider, GatewayTransactionId, TenantId, EventType, CheckoutId
```

`PublishAsync` is `OutboxEventBus<PaymentsDbContext>`: **insert only**, same unit of work as the log. `SaveChanges` failure rolls back both. Unique-race 200 is correct **if the winner committed log + outbox**.

`PlatformDbContext.SaveChangesAsync` triggers `DatabaseJobTrigger` after a successful save so the outbox worker does not wait 5s.

Unexpected throw → `LazuarMetrics.RecordWebhookFailed("payment")`.

### 2.3 `PaymentWebhookLog`

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Domain/Entities/PaymentWebhookLog.cs`

| Column | Role today |
|--------|------------|
| `Id` | UUIDv7 |
| `EventId` | Provider delivery / event id |
| `Provider` | `STRIPE` / `BILLPLZ` / … |
| `BusinessKey` | nullable `PAYMENT_COMPLETED:pi_…` |
| `ProcessedAt` | **UtcNow at insert** — means “queued,” not “Commerce fulfilled” |

No `OrganizationId`, raw body, headers, status enum, or outbox correlation id.

Indexes (`PaymentWebhookLogConfig` + migration `20260803151832_AddPaymentWebhookBusinessKey`):

- unique `(Provider, EventId)`
- unique `(Provider, BusinessKey)` **filtered** `"BusinessKey" IS NOT NULL`

Repository (`IPaymentWebhookLogRepository`): `HasBeenProcessedAsync`, `HasBusinessKeyBeenProcessedAsync`, `Add`, `SaveChangesAsync`. **No Get.** Cannot inspect the existing row or its outbox on the duplicate path.

### 2.4 Per-adapter verify + identity (live, 16 Aug 2026)

| | Stripe | Billplz | CHIP | Razorpay |
|--|--------|---------|------|----------|
| Verify | `EventUtility.ConstructEvent` + `Stripe-Signature` | HMAC-SHA256 of form fields except `x_signature`; try with extra fields then without | RSA-SHA256 PKCS1 `X-Signature` over raw body, PEM public key | `Utils.verifyWebhookSignature` + `X-Razorpay-Signature` |
| Compare | SDK | `string.Equals` ordinal ignore-case (**not** fixed-time) | RSA verify | SDK |
| Replay window | SDK ~5 min | **None** | None | Provider |
| `PAYMENT_COMPLETED` | `checkout.session.completed`, `payment_intent.succeeded` | `paid=true` or `state=paid` | **`purchase.paid` only** (preauthorized dropped — gap doc **wrong**) | `payment.captured` only |
| `PAYMENT_FAILED` | **Not mapped** (off-session fail event is a different path) | unpaid callback | `purchase.payment_failure` | — |
| `DISPUTE_CREATED` | `charge.dispute.created` | — | — | — |
| `EventId` | `stripeEvent.Id` (`evt_…`) | bill `id` | `root.id` **else `Guid.NewGuid()`** | header `X-Razorpay-Event-Id` else payment id else **fail closed** |
| `GatewayTransactionId` | `PaymentIntentId` ?? `session.Id` / `pi.Id` / dispute PI | bill `id` | **same as EventId** (not nested `purchase.id`) | payment entity `id` (may be null if only header id) |
| Business key | `PAYMENT_COMPLETED:pi_…` — dual events collapse | `PAYMENT_COMPLETED:bill` vs `PAYMENT_FAILED:bill` (failed then paid **both** run — correct) | `PAYMENT_COMPLETED:{root.id or Guid}` | `PAYMENT_COMPLETED:{pay_…}` or null |

Fee args `0,0,0` and Billplz fee=0 are **LP-PAY-011**, not this ticket.

### 2.5 Outbox after ACK

`OutboxPublisherJob` + `MessageProcessingResultApplier`:

- Success → `ProcessedAt` set.
- Failure → `AttemptCount++`, `NextAttemptAt = now + 2^attempt` minutes.
- `AttemptCount >= 5` → `Status=Dead`, `ProcessedAt` set, `LazuarMetrics.RecordDeadLetter()`.
- Historical “always mark processed on first error” is **fixed**. Runbook: `apps/lazuar-api/docs/007-outbox-inbox-dead-letter-runbook.md` (SQL redrive only).

Integration event `Id` **is** `OutboxMessage.Id` (`GatewayPaymentCompletedIntegrationEvent.Id = Guid.CreateVersion7()`). That Guid is **not** stored on `PaymentWebhookLog`. After Dead, the handler has nothing to re-queue.

Downstream (already idempotent enough if we re-deliver the **same** payload):

| Consumer | Dedupe |
|----------|--------|
| Billing `GatewayPaymentCompletedHandler` | `HasEntryBeenProcessedAsync("GATEWAY_PAYMENT", GatewayTransactionId)` |
| `PlatformTopUpEventHandler` | ledger `SYSTEM_CREDIT_TOPUP` + `GatewayTransactionId` (blank tx id → **no credit**) |
| Commerce completed handler | session `Status != OPEN`; type / correlation guards |
| `IntegrationCheckoutGatewayEventsHandler` | only `open` → completed / failed |

Do **not** change those handlers here.

### 2.6 Stale claims in `docs/001-gaps/02-payment-webhooks.md`

That file is a Hub-era dump. **Do not re-implement these as P0s:**

| Gap-doc claim | Live fact |
|---------------|-----------|
| No tests on handler / ParseWebhook | `ProcessGatewayWebhookCommandHandlerTests` + Billplz HMAC fixtures exist; Stripe/CHIP/Razorpay parse tests still missing |
| Only COMPLETED + DISPUTE | `PAYMENT_FAILED` published |
| Event-id only | Business key + unique filter index |
| Unique race → 500 | 23505 swallowed |
| Razorpay `Guid.NewGuid()` | Fail closed |
| CHIP preauthorized = paid | Only `purchase.paid` |
| Outbox always ProcessedAt on error | 5 attempts then Dead |
| Unknown gateway → 500 | Allow-list 400 |
| Secrets plaintext | `AesSecretVault` |
| No pending session | `IntegrationCheckoutSession` + merge |

Still true (and in scope or explicitly deferred below): no raw body, no status enum, CHIP Guid, 400 on bad secret stops retries, `ProcessedAt` ≠ fulfilled, Dead + log short-circuit.

### 2.7 Tests that exist vs missing

| Coverage | File | LP-090? |
|----------|------|---------|
| Failed publish, EventId skip, business-key skip, 23505 swallow, 23505 detector, Stripe dual events, session merge | `ProcessGatewayWebhookCommandHandlerTests.cs` | Extend |
| Billplz HMAC + `Query-checkout_id` | `BillplzGatewayAdapterTests.cs` | Extend (empty id, bad sig, unpaid) |
| Stripe / CHIP / Razorpay `ParseWebhookAsync` | **none** | **Must add** (verify + identity) |
| HTTP allow-list / empty body / `{ received: true }` | **none** | Should |
| Dead outbox + redelivery | **none** | **Must add** |
| Soft-disable / encrypt | `PaymentSecretsAndSoftDisableTests.cs` | No change |
| M2M outbound after completed | `IntegrationCheckoutOutboundWebhookTests.cs` | Out of scope |

No test for missing secret, unverified parse, or unknown event type on the handler.

---

## 3. Gaps (in scope for LP-090)

| # | Gap | Why LP-090 fails |
|---|-----|------------------|
| G1 | `ProcessedAt` + `{ received: true }` are the only “success” signals | Received **is** fulfilled as far as this module can tell. Ticket: *received ≠ fulfilled*. |
| G2 | Duplicate EventId / BusinessKey **returns before** outbox inspection | Dead (or never-inserted) outbox + 200 = permanent non-fulfillment. Ticket: *never ACK success then drop domain work*. |
| G3 | No `OutboxMessageId` (or Get) on the log | Cannot implement G2 without a correlation. |
| G4 | CHIP `Guid.NewGuid()` + `purchaseId = eventId` | Retries double-fulfill; business key is random. Fold `LP-PAY-020` here. |
| G5 | CHIP ignores nested `purchase.id` when root `id` missing | Invents a Guid even when the purchase id is in the payload (CHIP sends purchase + `event_type`). |
| G6 | Billplz `id` default `""` | Unique `(BILLPLZ, "")` poison + false skip. |
| G7 | Handler tests never hit verify-fail / missing secret / unknown type / empty id / Dead re-queue | G1–G6 unasserted. |
| G8 | Billplz HMAC `string.Equals` | Timing leak; Resend + outbound webhooks already use `CryptographicOperations.FixedTimeEquals`. Tiny, same ticket. |

### Not LP-090 (do not touch)

| Item | Owner |
|------|--------|
| Persist raw body / headers / two-phase HTTP persist-then-worker | reserved `LP-PAY-016` (Wave 1) |
| Admin replay / redrive UI | reserved `LP-PAY-017`, `LP-OPS-005` |
| Success page / GET checkout as money truth | LP-024 |
| Outbound customer webhooks silent-drop / redrive | LP-132, LP-133 |
| Refund / `payment.refunded` inbound | LP-091 / proposed `LP-PAY-022` |
| Stripe `payment_intent.payment_failed` mapping | proposed `LP-PAY-021` |
| CHIP `purchase.preauthorized` (already dropped) | done |
| Fee 0 for Billplz | `LP-PAY-011` |
| 400 after secret rotation (no store-first) | `LP-PAY-017` |
| IP allow-list / rate limit | DEV residual |
| `OrganizationId` on the log | `14-tenant-isolation` residual |
| Commerce no-op when `metadata.type` missing | fulfillment routing, not intake |
| Rename `ProcessedAt` → `ReceivedAt` | churn; document instead |
| Sync-fulfill inside the webhook request | timeouts + couples modules |

---

## 4. Options (pick the small one)

| Option | What | Verdict |
|--------|------|---------|
| **A.** Two-phase raw intake table + worker | Persist body, 200, async parse | **No.** That is `LP-PAY-016`. Unlocks replay; too big for Wave 0. |
| **B.** HTTP waits until Commerce/Billing SaveChanges | 200 = fulfilled | **No.** Provider timeouts; crosses module DBs; breaks “2xx quickly.” |
| **C.** On duplicate, always re-publish a **new** integration event | Simple | **No.** Relies entirely on downstream dedupe; burns new outbox ids; can double outbound `payment.completed` if M2M race hits `open` twice. |
| **D. (this ticket)** Fail-closed identities + store `OutboxMessageId` + on redelivery **re-queue the same Dead/missing outbox** | One nullable column, handler branch, adapter fail-closed | **Yes.** Received stays HTTP 200 + intake row. Fulfilled stays Commerce/Billing/session. Dropped work becomes re-queued work. |

---

## 5. Recommended semantics (lock this, then code)

### 5.1 Identities

`EventId` = provider **delivery** id when the provider has one; else the stable money object id; **never** a process-local Guid.

`GatewayTransactionId` = money object (Stripe PI, Billplz bill, CHIP purchase, Razorpay `pay_`).

`BusinessKey` (unchanged formula):

```
if GatewayTransactionId blank → null
else EventType + ":" + GatewayTransactionId
```

Failed then paid (Billplz / CHIP) are **different** keys. Dual Stripe success events are the **same** key.

CHIP parse order:

1. `purchase.id` if the `purchase` object is present and has `id`.
2. Else root `id`.
3. Else `Verified=false`, error `Missing stable CHIP purchase id` — **no Guid**.

Billplz: if `id` is missing/whitespace after form parse → `Verified=false`, error `Missing stable Billplz bill id`.

Razorpay: already fail-closed; do not change except tests.

Stripe: SDK always has `evt_`; if `checkout.session.completed` has no `PaymentIntentId`, keep today’s `session.Id` fallback (off-session is PI-only). Do **not** invent a second business-key scheme.

### 5.2 Received vs fulfilled

| Word | Where it lives | LP-090 rule |
|------|----------------|-------------|
| **Received** | HTTP 200 `{ received: true }` + `PaymentWebhookLog` row | Durable intake. `ProcessedAt` = **received/queued at**. Never rename; comment the property. |
| **Queued** | `payments.OutboxMessages` row whose `Id` is stored on the log | Domain work exists. Pending / retrying / Dead / succeeded. |
| **Fulfilled** | Commerce sub/session, Billing ledger, `IntegrationCheckoutSession` | **Not this table.** Do not add `Fulfilled` / `Status=Fulfilled` on `PaymentWebhookLog`. |

Never return `{ fulfilled: true }`. Never flip checkout/subscription status in this handler.

### 5.3 ACK vs drop

`mediator.Send` may return (→ 200) only if one of:

1. Event type ignored (not money) — no work to drop.
2. First-time path: **this** SaveChanges committed log **and** outbox (or 23505 meaning the winner did).
3. Duplicate path: existing outbox is Pending or already succeeded, **or** we just re-queued Dead/missing.

If first-time SaveChanges fails for any reason other than 23505 → throw → **500**.

If duplicate path cannot read/update outbox → throw → **500** (provider retries).

### 5.4 Duplicate / Dead algorithm

Need `GetByEventId` and `GetByBusinessKey` (not only `Has*`).

```
verify + whitelist (unchanged)
existing = by EventId else (if businessKey) by BusinessKey

if existing is not null:
    if existing.OutboxMessageId is null:
        // pre-ticket backfill / seed from docs/006 — do **not** invent work
        return
    status = outbox.Status / ProcessedAt
    if Dead or row missing:
        if Dead: Status=Pending, ProcessedAt=null, NextAttemptAt=null, AttemptCount=0
        if missing: PublishAsync **the same event shape again**, set OutboxMessageId = new event.Id, SaveChanges
        return
    // Pending or succeeded
    return

// first time
merge metadata
construct integration event (Id = new v7)
log = new(..., businessKey) { OutboxMessageId = event.Id }
Add + PublishAsync + SaveChanges (23505 → 200)
```

Re-queue the **same** `OutboxMessage.Id` when Dead so Inbox/consumers see the same integration event id. Downstream tables already key on `GatewayTransactionId` / session status.

Backfill rows from `docs/006-payment-webhook-idempotency-backfilling.md` have no `OutboxMessageId` — leave them as “already received historically, do not re-queue.” Same as today’s skip.

### 5.5 Billplz HMAC

Compare hex with `CryptographicOperations.FixedTimeEquals` after normalizing both sides to the same casing/length (copy the pattern in `OutboundWebhookSignature.FixedTimeEqualsHex`). Still try extra-fields then without.

---

## 6. Minimal code changes

### Must

1. **`PaymentWebhookLog`**
   - Add nullable `Guid? OutboxMessageId`.
   - XML comment on `ProcessedAt`: received/queued time, not domain fulfillment.
   - Ctor: optional `outboxMessageId` (default null for tests / backfill).

2. **EF + migration**
   - `PaymentWebhookLogConfig`: property only (no extra unique index required).
   - New Payments migration: `OutboxMessageId uuid NULL` on `payments.PaymentWebhookLogs`.
   - Do **not** add FK to `OutboxMessages` (outbox rows are recycled/query-only; keep them decoupled).

3. **`IPaymentWebhookLogRepository` + `PaymentWebhookLogRepository`**
   - `GetByEventIdAsync`, `GetByBusinessKeyAsync`.
   - `TryRequeueDeadOutboxAsync(Guid outboxId)` on the **same** repository (it already owns `PaymentsDbContext`, which has `OutboxMessages`). Returns an enum/result: `Requeued` / `AlreadyActive` / `Missing`. Application layer stays free of EF.

4. **`ProcessGatewayWebhookCommandHandler`**
   - Replace the two `Has*` early-returns with the algorithm in §5.4.
   - Set `OutboxMessageId` from the integration event `Id` before `Add`.
   - Keep 23505 swallow, session merge, event whitelist, soft-disable comment, metrics on unexpected throw.

5. **`ChipCollectGatewayAdapter.ParseWebhookAsync`**
   - Purchase id from `purchase.id` then root `id`; else `Verified=false`. Delete both `Guid.NewGuid()` fallbacks.

6. **`BillplzGatewayAdapter.ParseWebhookAsync`**
   - Fail closed on blank bill id.
   - Fixed-time signature compare.

### Should (still this ticket, tiny)

7. **Comment** on the endpoint: `{ received: true }` is not payment fulfillment.
8. **`BuildBusinessKey`**: treat whitespace-only `GatewayTransactionId` as null (same as empty).
9. Metric or `LogInformation` when re-queueing Dead (`EventId`, `Provider`, `OutboxMessageId`). No new meter required if a log line is enough.

### Must not

- Raw body / headers columns or object-storage pointer.
- New worker or moving parse off the HTTP thread (`LP-PAY-016`).
- Ops replay page (`LP-PAY-017`).
- Changing `{ received: true }` to anything else.
- Publishing `GatewayPaymentFailed` from Stripe events this ticket does not map.
- Touching Commerce / Billing handlers, outbound dispatcher, or success-page poll.
- Adding `OrganizationId` to the log.
- Making unique-race rethrow.

---

## 7. Tests

Keep NSubstitute handler tests. Add adapter crypto tests next to Billplz. No host e2e required.

### 7.1 Handler — extend `ProcessGatewayWebhookCommandHandlerTests.cs`

| Test | Assert |
|------|--------|
| `Handle_MissingConfig_ThrowsInvalidOperation` | no parse / no publish |
| `Handle_Unverified_Throws_DoesNotWriteLog` | `Verified=false` → throw; no `Add` |
| `Handle_UnknownEventType_Returns_NoLog_NoPublish` | e.g. `charge.succeeded` passthrough |
| `Handle_Skips_When_EventId_Already_Processed_And_OutboxActive` | existing log + outbox Pending/success → no second `PublishAsync` |
| `Handle_Redelivery_Requeues_DeadOutbox` | EventId hit, `TryRequeueDeadOutboxAsync` → `Requeued`; **no** second `Add` |
| `Handle_BusinessKeyHit_Requeues_DeadOutbox` | different EventId, same `PAYMENT_COMPLETED:pi_x`, Dead → re-queue; no second completed publish **unless** missing-outbox path |
| `Handle_Duplicate_BackfillRow_WithoutOutboxId_DoesNotInventWork` | `OutboxMessageId=null` → return, no publish |
| `Handle_StripeDualEvents_SameBusinessKey_Publishes_OnlyOnce` | keep; still one completed event |
| `Handle_UniqueConstraintRace_Returns_WithoutRethrow` | keep |
| `Handle_PaymentFailed_Publishes_…` / session merge | keep |

Repository on these tests must fake `Get*` + `TryRequeue*` (today only `Has*`).

### 7.2 CHIP adapter — **new** `ChipCollectGatewayAdapterTests.cs`

Use a throwaway RSA key: sign `rawBody`, put base64 in `X-Signature`, PEM as `webhookSecret`.

| Test | Assert |
|------|--------|
| Missing `X-Signature` | `Verified=false` |
| Bad signature | `Verified=false` |
| `purchase.paid` + root `id` | `PAYMENT_COMPLETED`, EventId = purchase id, `GatewayTransactionId` = same, **not** a Guid |
| `purchase.paid` + only `purchase.id` | EventId / tx id = nested id |
| `purchase.paid` + no ids | `Verified=false`, error mentions purchase id |
| `purchase.preauthorized` | verified, **not** `PAYMENT_COMPLETED` (whitelist drop at handler) |
| `purchase.payment_failure` | `PAYMENT_FAILED`, stable purchase id |

### 7.3 Stripe adapter — **new** `StripeGatewayAdapterTests.cs`

Stripe SDK `EventUtility.GenerateTestHeaderString` (or construct a signed payload with a test `whsec`).

| Test | Assert |
|------|--------|
| Missing `Stripe-Signature` | `Verified=false` |
| Bad secret | `Verified=false` |
| `checkout.session.completed` with `payment_intent` | EventId = `evt_…`, `GatewayTransactionId` = `pi_…` |
| `payment_intent.succeeded` | same PI business identity |
| Unmapped type | `Verified=true`, EventType = Stripe type (handler ignores) |

Fee expand talks to Stripe HTTP — leave fee 0 in fixtures; do not require network.

### 7.4 Billplz adapter — extend `BillplzGatewayAdapterTests.cs`

| Test | Assert |
|------|--------|
| Bad `x_signature` | `Verified=false` |
| Missing `id` / empty `id` | `Verified=false` |
| Unpaid (`paid=false`) | `PAYMENT_FAILED`, EventId = bill id |
| Existing query-checkout + paid | keep |

### 7.5 Razorpay — **new** thin `RazorpayGatewayAdapterTests.cs` (verify already shipped; lock it)

| Test | Assert |
|------|--------|
| Missing signature | `Verified=false` |
| `payment.captured` without header and without payment id | `Verified=false` (no Guid) |
| Header event id + payment id | EventId = header, `GatewayTransactionId` = `pay_…` |

### 7.6 Repository (optional but cheap)

In-memory or EF InMemory: `TryRequeueDeadOutboxAsync` sets Dead → Pending, zeros attempts, clears `ProcessedAt`; Pending → `AlreadyActive`; unknown id → `Missing`.

Do **not** require Postgres unique-index integration for this ticket; 23505 is already unit-tested via message/`SqlState` walk.

---

## 8. Files to touch (when implementing)

| File | Change |
|------|--------|
| `Modules/Payments/Domain/Entities/PaymentWebhookLog.cs` | `OutboxMessageId` + comment |
| `Modules/Payments/Infrastructure/Configurations/PaymentConfigurations.cs` | map column |
| `Modules/Payments/Infrastructure/Migrations/*_AddPaymentWebhookOutboxMessageId.cs` | **new** |
| `Modules/Payments/Application/Ports/IPaymentRepositories.cs` | Get + re-queue |
| `Modules/Payments/Infrastructure/Repositories/PaymentRepositories.cs` | implement |
| `Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler*.cs` | §5.4 |
| `Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` | fail-closed id |
| `Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` | fail-closed id + fixed-time HMAC |
| `Modules/Payments/Infrastructure/Endpoints.cs` | optional received-vs-fulfilled comment |
| `tests/.../ProcessGatewayWebhookCommandHandlerTests.cs` | extend |
| `tests/.../BillplzGatewayAdapterTests.cs` | extend |
| `tests/.../ChipCollectGatewayAdapterTests.cs` | **new** |
| `tests/.../StripeGatewayAdapterTests.cs` | **new** |
| `tests/.../RazorpayGatewayAdapterTests.cs` | **new** |

No TypeSpec. No ops UI. No Commerce/Billing files.

---

## 9. Acceptance (flip LP-090 to **Y** when)

1. Stripe dual success events with the same PI still publish **one** `GatewayPaymentCompleted`.
2. CHIP / Billplz / Razorpay money webhooks **without** a stable id are **400**, never stored, never Guid-keyed.
3. CHIP `purchase.paid` uses `purchase.id` / root `id` as both EventId and business tx id.
4. A signed redelivery after the matching payments outbox is **Dead** re-queues that row (or republishes only if the row is missing) and does **not** insert a second `PaymentWebhookLog` for the same EventId.
5. HTTP success body remains `{ received: true }`. No code path in Payments treats the log as Commerce/Billing fulfillment.
6. Tests in §7 are green.
7. Tracker cell LP-090 Lazuar **P → Y**. Do **not** flip `LP-PAY-016` / `LP-PAY-017`. Close proposed `LP-PAY-020` as done-by-LP-090 in notes only (parent mints; do not invent a second family).

---

*Read-only analysis of Payments webhook intake as of 16 August 2026. No product code changed.*
