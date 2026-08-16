# W1-LP-092 — Partial refund

**Date:** 16 August 2026  
**Status:** Analysis only — **do not implement from this file**  
**Tracker:** [00-checklist-tracker.md](../00-checklist-tracker.md) `LP-092` (Wave 1, Lazuar **P**)  
**Implement list:** [00-implement-ids.md](../00-implement-ids.md) “Partial refund”  
**Siblings (same program):** [W1-LP-091-analysis.md](./W1-LP-091-analysis.md) persist + full + mark-refunded; [W1-LP-093-analysis.md](./W1-LP-093-analysis.md) amount field in ops  
**Evidence:** [13-payments-refunds-rails.md](../13-payments-refunds-rails.md) (`IssueRefundAsync` amount in major units; Commerce status binary); Billing `GatewayRefundCompletedHandler` already scales tax; `01` §19

**This ticket is not** LP-091 (first-hop persist, gateway default, Billplz SOP, failed consumer), LP-093 (ops amount input / badges), `LP-PAY-022` (dashboard-initiated refund webhooks), LP-104 (real credit notes), or proration / plan-change credit (LP-059).

§4 is the **same shared plan** as 091/093. This file owns **remaining money**: more than one refund per capture, a status that is not a lie, and a ledger that does not swallow the second contra.

**Land 091 foundation first** (or the same PR series). Partial on top of an unpublished outbox is theater.

---

## 0. Verdict

The **wire** already accepts a partial. TypeSpec `RecordRefundRequestDto.amount`, `RecordRefundCommand.Amount`, and all three refunding adapters take a major-unit amount smaller than the capture:

| Layer | Partial today |
|-------|----------------|
| HTTP / command | `amount` optional; `amount > log.Amount` rejected; `amount < log.Amount` **accepted** |
| Stripe | `RefundCreateOptions.Amount = amount * 100` |
| CHIP | `{ amount: sen }` on `purchases/{id}/refund/` |
| Razorpay | `Refund({ amount: sen })` |
| Billplz | `false` (same as full) |
| Billing ledger | Tax scaled by `RefundedAmount / originalPaid` (`GatewayRefundCompletedHandlerTests.PartialRefund_50Percent_ReversesHalfTax`) |
| Commerce log | **`TransitionToRefunded()`** — status becomes `REFUNDED` even if RM 1 of RM 100 came back |
| Second refund | Command rejects `status == REFUNDED`. Staff cannot return the other RM 99 |
| Billing second event | `HasEntryBeenProcessedAsync(GATEWAY_REFUND, PaymentRecordId)` → **silent no-op**. Even if Commerce allowed a second request, the ledger would not move |
| LHDN | Any completed refund &lt;72h **cancels the whole invoice** |
| Ops | No amount field (093). Always implies full |

Tracker **P** is correct: adapters + billing math are partial-capable; the **domain is binary**. HitPay / Stripe / Chargebee keep `amount_refunded` + remaining. We must too.

**LP-092 is: remaining = Amount − RefundedAmount; `PARTIALLY_REFUNDED` while remaining &gt; 0; a second (and Nth) refund until remaining hits 0; one billing row per attempt; LHDN must not cancel on a partial. Do not build the amount textbox here (093). Do not parse inbound refund webhooks.**

---

## 1. Product contract for this ID

Sellable sentence after this ticket:

> Staff can return **part** of a captured Stripe / CHIP / Razorpay payment. The transaction shows how much is already back and how much is still kept. They can refund the rest later. The ledger posts each slice. A RM 10 goodwill refund does not cancel the MyInvois invoice and does not tell the merchant the sale is gone.

| Input | HTTP | Log after success | Ledger | LHDN |
|-------|------|-------------------|--------|------|
| `amount` omitted or `== remaining` | Same as 091 full | `REFUNDED`, `RefundedAmount == Amount` | One row for this attempt | Existing cancel / CN (full) |
| `0 < amount < remaining` | 200 `refund_requested` | After complete: `PARTIALLY_REFUNDED`, `RefundedAmount += amount` | **New** `GATEWAY_REFUND` row | **No-op** |
| `amount > remaining` | 400 `AMOUNT_EXCEEDS_REMAINING` | Unchanged | Nothing | — |
| Second partial while first `REFUND_PENDING` | 400 (091 pending guard) | Unchanged | — | — |
| Second partial after first completed | Allowed | Remaining shrinks | Second ledger row | Still no-op unless this slice finishes the capture |
| Billplz / offline partial mark | Allow `mark_refunded` + amount ≤ remaining | Partial or full per amount | Completed event with that amount | Same LHDN rule |
| Inbound processor refund (dashboard) | Out of scope | — | — | — |

Industry cousins: Stripe PaymentIntent `amount_refunded` / Charge `refunded` vs `amount`; HitPay transaction refunded amount column. We do **not** copy Stripe’s refund list object into Commerce. Cumulative on the log is enough.

Currency: same as the log. Do not allow a different currency.

---

## 2. What exists (delta from 091)

Read [W1-LP-091-analysis.md](./W1-LP-091-analysis.md) §2 for the pipeline. Only partial-specific facts here.

### 2.1 Command already allows a slice

```csharp
var amount = request.Amount ?? log.Amount;
if (amount > log.Amount)
    throw new InvalidOperationException("Refund amount cannot exceed the original transaction amount.");
```

Compares to **original**, not remaining. After a successful RM 40 of RM 100, a 091-only tree has status `REFUNDED` so a second call dies first. If we only added remaining **without** changing the reject, a `PARTIALLY_REFUNDED` log could still accept `amount = log.Amount` (100) and over-refund the processor.

### 2.2 Adapters

All three money adapters already take a smaller amount. No adapter change required for partial **except** Billplz (still false; mark-refunded with amount is Commerce-only).

Stripe: always sends `Amount` (never “omit for full”). Fine for partial.  
CHIP: empty body = full at CHIP; we always send sen if amount &gt; 0. Fine.  
Razorpay: same.

Zero-decimal / JPY is not a 092 job.

### 2.3 Commerce completed = binary

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs`

```csharp
existingLog.TransitionToRefunded(); // Status = "REFUNDED"
```

No `RefundedAmount`. `Order.Refund()` unused.

### 2.4 Billing already scales tax — then deadlocks the next slice

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs`

- `referenceId = PaymentRecordId.ToString()`
- `HasEntryBeenProcessedAsync(GATEWAY_REFUND, referenceId)` → return
- Tax: `RefundedAmount / originalPaid * originalTax` when `TaxAmount == 0`

First partial of RM 54 on RM 108 posts tax 4 (tested). Second partial of RM 54 **never posts**.

This is the P0 inside 092. Changing Commerce status without changing this key leaves books short.

### 2.5 LHDN

`GetTaxDocumentByInternalId(PaymentRecordId)` then cancel if &lt;72h. Partial goodwill refund would **void the tax invoice**. Illegal for a Wave 1 money honesty ticket to leave that on.

### 2.6 Ops

No amount input. Copy says “full refund of RM {amount}”. Subscribers modal same. 093.

List filter statuses: `CONFIRMED | REFUNDED` only. 093 adds `PARTIALLY_REFUNDED`.

### 2.7 Tests

| Exists | Gap |
|--------|-----|
| Billing 50% tax | Second partial not tested |
| Command `amount > log.Amount` | No `amount > remaining` |
| No Commerce status test for partial | Must add |

---

## 3. Gaps (in scope for LP-092)

| # | Gap | Why LP-092 fails |
|---|-----|------------------|
| G1 | Status binary `REFUNDED` on first slice | Merchant thinks the sale is gone; cannot refund the rest |
| G2 | Cap is `log.Amount` not remaining | After G1 is fixed, a sloppy client can over-refund at Stripe |
| G3 | Billing idempotency key = `PaymentRecordId` only | Second slice is a silent accounting miss |
| G4 | LHDN cancel on any completed refund | Partial destroys the invoice |
| G5 | No `RefundedAmount` / remaining | API and UI cannot show the truth (column is 091 foundation; **use** it here) |
| G6 | `REFUND_PENDING` + partial: completed must **add**, not replace | Naive `RefundedAmount = event.Amount` on a retry is OK; on a second attempt must accumulate |

### Not LP-092

| Item | Owner |
|------|--------|
| Persist outbox / no STRIPE default / failed consumer / Billplz `false` | LP-091 |
| Amount textbox, remaining label, filter chips | LP-093 |
| Inbound dashboard refunds creating slices we did not request | `LP-PAY-022` (would use the same remaining math) |
| Multiple Stripe refund ids stored | Not needed; cumulative is enough |
| Proration / unused-time credit | LP-059 |
| Line-item / quantity refund | Not a CaaS v1 job |
| Refund of M2M integration checkouts | After Commerce path |

---

## 4. Shared refund program (identical in LP-091 / 092 / 093)

Implement as **one series** (091 foundation → 092 remaining → 093 UI). Do not ship the ops amount field before remaining exists. Do not ship remaining before the request actually persists.

### 4.1 Status machine

```
CONFIRMED
  → REFUND_PENDING          (API request committed)
       → REFUNDED           (full remaining returned or marked)
       → PARTIALLY_REFUNDED (092 only; remaining > 0)
       → REFUND_FAILED      (adapter false / config missing; still refundable)
PARTIALLY_REFUNDED
  → REFUND_PENDING → …      (092)
REFUNDED                    (terminal for money; no more refund)
```

`CONFIRMED` with `RefundedAmount == 0` is the only “Issue refund” source on 091. 092 adds `PARTIALLY_REFUNDED`.

Do **not** invent a second table for refund attempts unless 092 tests force it. Prefer columns on `CommerceTransactionLog`:

| Column | Type | Role |
|--------|------|------|
| `GatewayName` | `varchar(32) NULL` | `STRIPE` / `CHIP` / `RAZORPAY` / `BILLPLZ` / `OFFLINE` |
| `RefundedAmount` | `numeric(18,4)` default 0 | Cumulative returned |
| `RefundReason` | `varchar(255) NULL` | Last reason (093); optional on 091 |
| `Status` | existing | New values above (max length 50 already) |

Computed: `Remaining = Amount - RefundedAmount`. Do not store remaining.

No FK to Payments outbox.

### 4.2 Capability

`PaymentGatewayCapabilities`:

```
SupportsApiRefund(name) → STRIPE | CHIP | RAZORPAY
RequiresMarkRefunded(name) → BILLPLZ | OFFLINE | BANK_TRANSFER | CASH | MANUAL_OFFLINE | COMPED | blank
```

Do not reuse `SupportsOffSession` (Razorpay refunds; Billplz does neither).

### 4.3 Resolve gateway (never default STRIPE)

```
1. request.GatewayName if non-blank (ops override / backfill)
2. else log.GatewayName
3. else 400 GATEWAY_REQUIRED
```

Stamp `GatewayName` on **new** logs:

- Gateway completed: `product.GatewayName` if present, else event metadata `gateway` if you add it, else leave null and force ops to send it for old rows. Prefer passing gateway on `GatewayPaymentCompleted` **only if** you already have it in that handler’s stack (`product.GatewayName`). Do **not** add a field to the payments event unless the completed handler cannot see the product (custom `payment_link` has no product — stamp from checkout session `GatewayName` if set).
- Record-payment / mark-offline: `OFFLINE` (or the method string). `RequiresMarkRefunded` either way.

Backfill SQL: leave `GatewayName` null. Do not guess from `pi_` / UUID.

### 4.4 Persist rule

Every `PublishAsync` on `CommerceEventBus` in the refund command **must** be followed by `_repository.SaveChangesAsync(ct)` on the **same** `CommerceDbContext`. Test with real `OutboxEventBus<CommerceDbContext>`, not a mock bus (copy `OutboundWebhookRequestedPersistTests`).

Mark-refunded may publish `GatewayRefundCompleted` from Commerce (ledger + LHDN) **or** set the log and publish completed via Payments. Prefer Commerce publishing completed directly so Billplz never enters the Payments adapter. Payments still owns API execute.

### 4.5 Idempotency

- `REFUND_PENDING` → reject new request (091).
- Billing: keep one ledger row per **refund attempt**. 091 can keep `PaymentRecordId` for the single full refund. **092 must change** to `PaymentRecordId + ":" + event.Id` (or a new attempt id). If 091 and 092 land together, use the composite key from day one so 091 does not paint Billing into a corner.
- Stripe: pass `IdempotencyKey = "lazuar-refund:" + log.Id + ":" + amountMinor` on `RefundCreateOptions` request options if the SDK allows (same pattern as off-session). Should, not must.

### 4.6 LHDN safety

Existing handler cancels the **whole** MyInvois document on any completed refund &lt;72h. For 091 full that is acceptable. **092 must no-op LHDN unless `RefundedAmount` (this event) ≥ original paid** (or log remaining hits 0). Do not build CN product here.

### 4.7 HTTP honesty

Return RFC 7807 `ProblemDetails` on 400 (`detail` = message, `title` = code like `ALREADY_REFUNDED` / `MARK_REFUNDED_REQUIRED` / `GATEWAY_REQUIRED`) so the generated client and ops `error.detail` work. TypeSpec already claims this.

Success stays `{ status: "refund_requested" }` for API path and `{ status: "refunded" }` for mark-refunded (sync complete). Do not return `{ fulfilled: true }` from Payments.

### 4.8 What we refuse inside this program

- Billplz Payment Order as `IssueRefundAsync`
- Guest refund
- Connect / application fees
- Inbound refund webhooks as the **primary** merchant path (ops button is the path; webhooks are `LP-PAY-022`)
- Auto-cancel subscription

---

## 5. Recommended semantics (this ID)

### 5.1 Remaining

```
remaining = log.Amount - log.RefundedAmount
if remaining <= 0 → already REFUNDED (reject)
effective = request.Amount ?? remaining
if effective <= 0 → reject
if effective > remaining → 400 AMOUNT_EXCEEDS_REMAINING
```

Compare to **remaining**, not original. A full refund after a partial is `effective == remaining`, not `== log.Amount`.

Round with the same `decimal` rules as the rest of Commerce (4 dp on the column). Do not introduce sen rounding in Commerce; adapters already convert.

### 5.2 Apply on completed

```
log.RefundedAmount += event.RefundedAmount
if log.RefundedAmount >= log.Amount:
    Status = REFUNDED
else:
    Status = PARTIALLY_REFUNDED
```

If a duplicate **same** completed event is redelivered: Billing is idempotent on attempt id; Commerce must not double-add. Options:

- **A (preferred).** Commerce completed is idempotent on `(log.Id, event.Id)` — store `LastRefundEventId` **or** treat `REFUND_PENDING` as the only apply gate (pending → apply once → not pending). Redelivery when already `PARTIALLY_REFUNDED` / `REFUNDED` with same amount is a no-op.
- **B.** Refund-attempt table. Only if A races in tests.

Use A. `REFUND_PENDING` is the lock. Completed when not pending → ignore (already applied or mark-refunded path).

### 5.3 Billing reference id

Change:

```
referenceId = PaymentRecordId.ToString()
```

to:

```
referenceId = PaymentRecordId.ToString("N") + ":" + @event.Id.ToString("N")
```

(`GatewayRefundCompleted.Id` is the integration event / outbox id — unique per attempt.)

Update `GatewayRefundCompletedHandlerTests.SecondEvent_IsIdempotent` to reuse the **same** event `Id` (still one row) and add `SecondDistinctEvent_PostsSecondEntry`.

Tax on each slice: keep proportional on **this event’s** `RefundedAmount` vs original paid (already implemented). Sum of slices ≈ original tax (rounding 4 dp). Do not try to true-up pennies on this ticket unless a test shows a 0.01 drift you care about.

### 5.4 LHDN gate

In `Lhdn/.../GatewayRefundCompletedIntegrationEventHandler`:

```
if (@event.RefundedAmount < originalDocument gross/paid)
    log + return; // partial — Wave 2 CN (LP-104)
```

Need a number to compare. Prefer `RefundedAmount` vs the document’s total including tax if stored; else vs `@event` only when Commerce sends a `IsFullRefund` flag. **Simplest honest rule:** treat as full iff Commerce status after apply is `REFUNDED` **or** event amount ≥ original `GATEWAY_PAYMENT` paid (same lookup Billing already does). Do not cancel when the event is a slice.

If LHDN module cannot see Commerce status, pass `bool IsFullRefund` on `GatewayRefundCompletedIntegrationEvent`. That is a **contracts** change — allowed on 092; bump the record with a default `false` and set true from Payments handler when `@event.Amount` equals the requested remaining that 091/092 put on the request event (add `IsFullRefund` to **requested** too, or compare completed amount to a new `OriginalAmount` — messy).

**Lock:** add `bool IsFullRefund` to `GatewayRefundCompletedIntegrationEvent` (default false). Commerce request handler sets a flag on **requested**; Payments copies it onto completed; mark-refunded Commerce publisher sets it when `amount == remaining` before apply. LHDN only cancel/CN when `IsFullRefund`.

### 5.5 Mark-refunded partial (Billplz / cash)

091 invented mark-refunded for **full**. 092: same flag + `amount` &lt; remaining → `PARTIALLY_REFUNDED` without calling an adapter. Desk “we refunded RM 20 in the Billplz dashboard” must not flip the whole row.

### 5.6 Pending amount

Optional: store `PendingRefundAmount` so a crash mid-flight still knows what we asked Stripe for. Not required if we reject new requests while `REFUND_PENDING` and the requested amount lives on the outbox payload. Prefer **no extra column**.

---

## 6. Minimal code changes (092)

Assumes 091 columns + persist + failed consumer are in.

### Must

1. **`RecordRefundCommandHandler`** — cap against `Amount - RefundedAmount`; allow `PARTIALLY_REFUNDED` as a source status; treat omitted amount as **remaining**.
2. **`CommerceTransactionLog.ApplyRefund`** — accumulate; set `PARTIALLY_REFUNDED` vs `REFUNDED`.
3. **Completed handler** — apply only from `REFUND_PENDING` (or mark path); no-op redelivery.
4. **`GatewayRefundCompletedIntegrationEvent`** — `IsFullRefund`.
5. **Payments requested handler** — copy flag onto completed.
6. **Billing** — reference id includes `@event.Id`.
7. **LHDN handler** — skip unless `IsFullRefund`.
8. **TypeSpec** `TransactionLogDto.refunded_amount` (if 091 did not); list filter docs for new status. Remaining can be computed in ops (093) or added as `remaining_amount` for honesty.

### Should

9. Query `GET /transactions` status filter includes `PARTIALLY_REFUNDED` (SQL already `Status = @Status`).
10. Comment on Billing handler: key is per attempt, not per capture.

### Must not

- Amount UI (093).
- Inbound refund webhook → `ApplyRefund` (022).
- Changing adapter math.
- CN XML product (104).

---

## 7. Tests (092)

### 7.1 Command

| Test | Assert |
|------|--------|
| `Handle_Partial_SetsPending_AmountOnEvent` | event.Amount = 40, log was 100, `IsFullRefund` false on requested if you add it |
| `Handle_OmittedAmount_AfterPartial_UsesRemaining` | first 40 applied; second omit → 60 |
| `Handle_AmountGreaterThanRemaining_Throws` | |
| `Handle_FromPartiallyRefunded_Allowed` | |
| `Handle_FromRefunded_StillRejected` | |

### 7.2 Commerce completed

| Test | Assert |
|------|--------|
| Partial complete → `PARTIALLY_REFUNDED`, `RefundedAmount == slice` | |
| Second complete → remaining 0 → `REFUNDED` | |
| Redeliver same completed while not pending → no double add | |

### 7.3 Billing

| Test | Assert |
|------|--------|
| Keep `SecondEvent_IsIdempotent` with **same** `Id` | still 1 row |
| `TwoAttempts_TwoLedgerRows` | different `Id`, same `PaymentRecordId` → 2 `GATEWAY_REFUND` |
| Partial tax still scales per slice | extend existing 50% test |

### 7.4 LHDN (unit, mock repo)

| Test | Assert |
|------|--------|
| `IsFullRefund=false` → no `CancelDocumentAsync` | |
| `IsFullRefund=true` + &lt;72h → cancel still runs | |

No ops tests. No live Stripe.

---

## 8. Files to touch (when implementing 092)

| File | Change |
|------|--------|
| `RecordRefundCommandHandler.cs` | remaining cap; source statuses |
| `CommerceTransactionLog.cs` | accumulate + `PARTIALLY_REFUNDED` |
| `GatewayRefundCompletedIntegrationEvent.cs` | `IsFullRefund` |
| `GatewayRefundRequestedIntegrationEvent.cs` | optional same flag |
| `GatewayRefundRequestedIntegrationEventHandler.cs` | copy flag |
| `Commerce/.../GatewayRefundCompletedIntegrationEventHandler.cs` | apply + pending gate |
| `Billing/.../GatewayRefundCompletedHandler.cs` | attempt-scoped reference id |
| `Lhdn/.../GatewayRefundCompletedIntegrationEventHandler.cs` | skip partial |
| `packages/api-spec/modules/commerce/models/subscriber.tsp` | `refunded_amount` if missing |
| `tests` as §7 | |

Mark-refunded path in 091 handler: pass `IsFullRefund: amount == remaining`.

---

## 9. Acceptance (flip LP-092 to **Y** when)

1. A Stripe/CHIP/Razorpay log of RM 100 can take RM 40 then RM 60. After the first complete: status `PARTIALLY_REFUNDED`, `refunded_amount = 40`. After the second: `REFUNDED`, `refunded_amount = 100`.
2. `amount > remaining` is 400. Omitted amount after a partial refunds **only** the remainder.
3. Billing has **two** `GATEWAY_REFUND` entries for the two attempts; redelivery of the same event id does not create a third.
4. LHDN cancel / CN does **not** run on the RM 40 event. It may run on the RM 60 event if that event is marked full (remaining → 0).
5. Billplz mark-refunded of RM 20 leaves `PARTIALLY_REFUNDED` (if 091 mark path exists).
6. Tests in §7 green. Tracker LP-092 **P → Y**. LP-093 stays **N** until the amount field ships. `LP-PAY-009` stays partial until 093 + Billplz SOP UI (and still no inbound webhooks).

---

*Read-only analysis of partial refund domain as of 16 August 2026. No product code changed. Shared plan §4 matches W1-LP-091 and W1-LP-093.*
