# W1-LP-091 — Full refund

**Date:** 16 August 2026  
**Status:** Analysis only — **do not implement from this file**  
**Tracker:** [00-checklist-tracker.md](../00-checklist-tracker.md) `LP-091` (Wave 1, Lazuar **P**)  
**Implement list:** [00-implement-ids.md](../00-implement-ids.md) “Full refund”  
**Siblings (same program, do not implement in isolation):** [W1-LP-092-analysis.md](./W1-LP-092-analysis.md) partial remaining; [W1-LP-093-analysis.md](./W1-LP-093-analysis.md) ops UI honesty  
**Evidence:** [13-payments-refunds-rails.md](../13-payments-refunds-rails.md) `LP-PAY-009`; [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) §19; [17-merchant-dashboard-analytics.md](../17-merchant-dashboard-analytics.md) Transactions; `docs/001-gaps/06-payments-module.md` is **Hub-era — treat as historical** (publisher + real amount already exist)

**This ticket is not** LP-092 (remaining / `PARTIALLY_REFUNDED` / second refund), LP-093 (ops chrome), `LP-PAY-022` (inbound `charge.refunded` / CHIP `payment.refunded`), `LP-PAY-023` notes beyond the mark-refunded SOP below, LP-094 (disputes), LP-104 (LHDN credit-note product), LP-166/167 (roles / audit), M2M cashier refund, or guest self-serve refund.

The three Wave 1 refund IDs share one implementation plan (§4). This file owns the **full-refund money loop**: persist the request, hit the right adapter, complete or fail the Commerce row.

---

## 0. Verdict

Inventory (`01` §19) says merchant-initiated Stripe refund is **SHIPPED**. Tracker `LP-091 = P` is the honest cell. The HTTP route, command, event, three adapters, and ledger consumer exist. The first hop **does not commit**.

`RecordRefundCommandHandler` publishes `GatewayRefundRequestedIntegrationEvent` onto `CommerceEventBus` (`OutboxEventBus<CommerceDbContext>`) and **never calls `SaveChangesAsync`**. `OutboxEventBus` only `AddAsync`s an `OutboxMessage`. Same unpublished-outbox class LP-132 just closed for lifecycle webhooks. Request scope disposes; `commerce.OutboxMessages` never gets the row; Payments never runs `IssueRefundAsync`; Commerce never flips the log; Billing never posts contra. HTTP still returns `{ status: "refund_requested" }`. Ops toasts success and paints `REFUNDED`.

Even if that flush is added, full refund is still **P**:

1. Missing `gateway_name` defaults to **`STRIPE`**. Gateway payments stamp `RecordedByName = "SYSTEM"` and store **no** `GatewayName` on `CommerceTransactionLog`. Billplz / CHIP orgs refund against the wrong config (or a missing Stripe row → `GatewayRefundFailed`).
2. `BillplzGatewayAdapter.IssueRefundAsync` is **`return false`**. Offline / cash / bank-transfer logs have `ExternalReference` like `MANUAL-…` / `OFFLINE-…`; the handler will still call an adapter. There is no mark-refunded SOP.
3. `GatewayRefundFailedIntegrationEvent` has **zero subscribers**. Failed gateway leaves the log `CONFIRMED` while the UI already shows `REFUNDED`.
4. Commerce `TransitionToRefunded()` is all-or-nothing. Completion does not record how much came back. `Order.Refund()` exists and is never called.
5. Double POST is not guarded (no `REFUND_PENDING`). After the flush is fixed, two clicks can refund Stripe twice.
6. There is **no** `RecordRefund` happy-path test, no `GatewayRefundRequested` handler test, no adapter `IssueRefundAsync` test. The only Commerce refund test is IDOR (`RecordRefund_ForeignOrg_ThrowsNotFound`).

**LP-091 is: commit the request, resolve the real rail, execute a full API refund on Stripe/CHIP/Razorpay, mark-refunded (no fake Payment Order) on Billplz/offline, and close the log only after `GatewayRefundCompleted`. Do not invent inbound refund webhooks. Do not ship a remaining-amount state machine here — that is LP-092.**

---

## 1. Product contract for this ID

Sellable sentence after this ticket:

> An OrgAdmin can take a **confirmed** Commerce transaction that was captured on Stripe, CHIP, or Razorpay and reverse the **entire** remaining amount at the processor. The ops row stays `CONFIRMED` until the gateway (or an explicit mark-refunded) succeeds. Billplz and cash/bank rows are not sent to `IssueRefundAsync`; staff mark them refunded after they did the work in the processor dashboard or at the desk. A failed API refund is visible as `REFUND_FAILED` and can be retried. `{ status: "refund_requested" }` means the Commerce outbox row exists, not that money moved.

| Input | HTTP | Commerce log | Money |
|-------|------|--------------|-------|
| Unknown id / other org | **400** (not 404 today — keep message, prefer ProblemDetails `detail`) | Unchanged | Nothing |
| Status already `REFUNDED` | 400 | Unchanged | Nothing |
| Status `REFUND_PENDING` (in-flight) | 400 | Unchanged | Nothing |
| No `ExternalReference` | 400 | Unchanged | Nothing |
| Amount omitted | Treat as **full remaining** (= `Amount` on this ticket; remaining is LP-092) | `REFUND_PENDING` + outbox committed | Worker → adapter |
| Amount &lt; original | **400 on this ticket** (“use LP-092”) *or* accept and leave remaining to 092 if both land together — see §4. Prefer one PR series; if 091 ships alone, reject partial | — | — |
| Amount &gt; original / ≤ 0 | 400 | Unchanged | Nothing |
| Rail `STRIPE` / `CHIP` / `RAZORPAY` | 200 `refund_requested` | `REFUND_PENDING` | `IssueRefundAsync`; success → `REFUNDED` + ledger; fail → `REFUND_FAILED` |
| Rail `BILLPLZ` or offline (`BANK_TRANSFER` / `CASH` / `MANUAL_OFFLINE` / `COMPED`) without `mark_refunded` | **400** `MARK_REFUNDED_REQUIRED` | Unchanged | Nothing |
| Same rails with `mark_refunded: true` | 200 | `REFUNDED` (or pending then completed in-process) | **No** adapter. Publish `GatewayRefundCompleted` so ledger (and existing LHDN handler) still run |
| Missing gateway on an old `SYSTEM` row | 400 `GATEWAY_REQUIRED` until staff send `gateway_name` | Unchanged | Do **not** default STRIPE |
| Soft-disabled gateway config | Still refund (historical obligation) | Same as live | Same as today |

Do **not** cancel the subscription on refund. `11-subscriptions-lifecycle.md` is correct: refund ≠ cancel. Staff cancel separately (already on Subscribers).

Do **not** offer buyer self-serve refund. Portal `/legal/refund` is already honest (Lazuar is not MoR).

---

## 2. What exists (read, not redesigned)

### 2.1 HTTP

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints.cs`  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs`

```
POST /admin/commerce/transactions/{id}/refund
```

| Fact | Detail |
|------|--------|
| Auth | Parent group `RequireAuthorization("OrgAdmin")`. Same role as rotate Billplz secret (LP-166 later) |
| Body | Optional `RecordRefundRequestDto`: `amount?`, `gateway_name?`, `subscription_id?`, `tax_amount?` |
| Success | `200 { status: "refund_requested" }` after `mediator.Send` returns |
| Domain errors | `catch (InvalidOperationException)` → **`400 { status: message }`** (`StatusResponse`) |
| TypeSpec | `packages/api-spec/modules/commerce/admin-routes.tsp` documents 400 as **`ProblemDetails`**. Ops client reads `error.detail`. Live 400 has **no** `detail`. Dishonest contract |
| Amount bind | `req?.Amount is double a ? (decimal)a : null` — omitted amount means full `log.Amount` |

Ops callers today:

| Surface | Body | Notes |
|---------|------|-------|
| `TransactionDetailPanel.tsx` | `{}` | Full implied. Reason typed, **not sent**. On 200, **optimistically sets `status: "REFUNDED"`** |
| `SubscribersPage.tsx` | `{ subscription_id }` | Same. Payment list is `GET /transactions?search=email` (can collide) |

There is already an “Issue Refund” button. Tracker `LP-093 = N` is still right: the button is not a sellable refund console (see sibling file).

### 2.2 `RecordRefundCommand`

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs`

```
load log by id (IgnoreQueryFilters)
org mismatch → "Transaction log not found."
status == REFUNDED → reject
blank ExternalReference → reject
amount = request.Amount ?? log.Amount
amount <= 0 or amount > log.Amount → reject
currency = log.Currency or MYR
gateway = request.GatewayName or **"STRIPE"**
PublishAsync GatewayRefundRequested
// no SaveChanges
```

Does **not** mutate the log. Does **not** persist outbox. Does **not** look at `RecordedByName`. Does not know Billplz vs Stripe.

Event fields (real amounts — gap-06 “amount=0 / no publisher” is **stale**):

```
OrganizationId, SubscriptionId (Empty if omitted),
PaymentRecordId = log.Id,
GatewayTransactionId = log.ExternalReference,
Amount, Currency, GatewayName, TaxAmount
```

### 2.3 Payments execute

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs`

```
GetByTenantAndGateway(org, GatewayName)   // IgnoreQueryFilters; no IsActive check (soft-disable OK)
missing / empty ApiKey → GatewayRefundFailed
Amount <= 0 → GatewayRefundFailed
DecryptOrPlaintext(ApiKey)
adapter.IssueRefundAsync(key, GatewayTransactionId, Amount)
true  → GatewayRefundCompleted (RefundedFee = 0 always, Net = Amount)
false → GatewayRefundFailed ("Gateway adapter failed to issue refund.")
```

Payments **does** `PublishAsync` completed/failed onto `PaymentsEventBus`. That handler is invoked by the Payments inbox **after** a Commerce outbox row exists. Today the Commerce row never exists.

`RefundedFee = 0` is residual `LP-PAY-011` / refund-webhook enrichment. Do not block 091.

### 2.4 `IssueRefundAsync` (major units)

Port: `IPaymentGatewayAdapter.IssueRefundAsync(apiKey, transactionId, amount)` — no currency, no idempotency key, no capability flag.

| Adapter | Live behavior | Full refund? |
|---------|---------------|--------------|
| **Stripe** | `RefundCreateOptions { PaymentIntent = transactionId, Amount = (long)(amount * 100) }`. Success if `succeeded` or `pending`. StripeException → false + log | Yes, if `transactionId` is a `pi_…`. Zero-decimal currencies still `* 100` (not this ticket) |
| **CHIP** | `POST purchases/{id}/refund/` with `{ amount: ToMinorUnitsRounded }` when amount &gt; 0; empty body if 0 (never, Commerce rejects ≤ 0) | Yes. API yes; `payment.refunded` webhook registered and **unmapped** (`LP-PAY-022`) |
| **Razorpay** | `Payment.Fetch(id).Refund({ amount: ToMinorUnitsTruncating })` when amount &gt; 0 | Yes |
| **Billplz** | `Task.FromResult(false)` | **Never.** Payment Order is a **new disbursement**, not a bill reversal. Do not wrap it |

`PaymentGatewayCapabilities` today only has `SupportsOffSession` / `IsReminderOnlyGateway` (Stripe+CHIP). Razorpay is reminder-only for vault but **can** API-refund. New flag must not reuse off-session.

### 2.5 Downstream of `GatewayRefundCompleted`

| Consumer | What it does | 091 impact |
|----------|--------------|------------|
| Commerce `GatewayRefundCompletedIntegrationEventHandler` | Match log by `ExternalReference == GatewayTransactionId` **or** `== PaymentRecordId` **or** `Id == PaymentRecordId`. Then `TransitionToRefunded()` (binary). No-op if no row | Full flip even if amount was partial (092). Never calls `Order.Refund()` |
| Billing `GatewayRefundCompletedHandler` | Skip if `RefundedAmount <= 0`. Idempotent on `(GATEWAY_REFUND, PaymentRecordId.ToString())`. Tax: explicit `TaxAmount` else scale from original `GATEWAY_PAYMENT` by gateway tx id. Tests exist (full + 50% + explicit tax + second event no-op) | Second **full** retry is correctly no-op. Second **partial** is also no-op — **092 must change the reference id** |
| LHDN `GatewayRefundCompletedIntegrationEventHandler` | Load tax doc by `PaymentRecordId`. &lt;72h **cancel whole invoice**; ≥72h draft CN with placeholder buyer | A **partial** would cancel the invoice. 091 full is acceptable; 092 must gate on full. Do not rebuild LP-104 here |
| `GatewayRefundFailed` | **No subscribers** | Ops / log stay lying |

### 2.6 Transaction log write paths (no gateway column)

`CommerceTransactionLog`: `Amount`, `FeeAmount`, `NetAmount`, `Currency`, `Status`, customer, `ProductName`, `RecordedByName`, `ExternalReference`. Status created `CONFIRMED`; only transition is `REFUNDED`.

| Path | RecordedBy | ExternalReference | Refundable via adapter? |
|------|------------|-------------------|-------------------------|
| `GatewayPaymentCompleted` (open / subscription / custom link) | `"SYSTEM"` | Gateway tx id (`pi_`, bill id, purchase id, `pay_`) | Yes **if** we know which adapter |
| Admin record-payment | `BANK_TRANSFER` / `CASH` / `COMPED` | User ref or `MANUAL-{subId}` | **No** — mark only |
| Mark checkout paid offline | `MANUAL_OFFLINE` | `OFFLINE-{sessionId}` | **No** |

`GET /admin/commerce/transactions` maps `RecordedByName` → DTO `recorded_by_name`. TypeSpec has **no** `payment_method` / `gateway_name`. Query `payment_method` is **accepted and ignored**. Ops table reads `tx.payment_method` (undefined). That is LP-093.

### 2.7 Stale claims

| Claim | Live fact |
|-------|-----------|
| Gap-06: no publisher / amount 0 | Publisher + real amount exist; **unpublished** |
| Gap-06: event has no amount field | Event has `Amount` + `Currency` |
| Inventory: Stripe refund SHIPPED | Route exists; money loop dead |
| Tracker note: “No ops refund button” | Buttons exist; they are dishonest (093) |
| 13: “Billing will post non-zero refunds” | Only after Payments completed fires — never today |

### 2.8 Tests that exist vs missing

| Coverage | File | LP-091? |
|----------|------|---------|
| Foreign org | `CrossTenantIdorTests.RecordRefund_ForeignOrg_ThrowsNotFound` | Keep |
| Billing full / partial tax / idempotent | `GatewayRefundCompletedHandlerTests` | Keep; 092 changes reference id |
| Ledger matrix refund | `LedgerBalanceMatrixTests` | Keep |
| Adapter `IssueRefundAsync` | **none** | **Must add** (Stripe options, CHIP path, Billplz false) |
| `RecordRefund` persist / gateway default / mark | **none** | **Must add** (real `OutboxEventBus<CommerceDbContext>`) |
| `GatewayRefundRequested` handler | **none** | **Must add** |
| Commerce completed → `REFUNDED` | **none** | **Must add** |
| Failed consumer | **none** | **Must add** |

---

## 3. Gaps (in scope for LP-091)

| # | Gap | Why LP-091 fails |
|---|-----|------------------|
| G1 | `PublishAsync` without `SaveChangesAsync` | Refund never leaves the request. Same ADR as LP-132 / `docs/001-cross-module-communication.md`: one SaveChanges covers domain + outbox |
| G2 | Default `GatewayName = "STRIPE"` | Wrong rail; Billplz/CHIP look like “refund failed” or hit the wrong Stripe account |
| G3 | No `GatewayName` on the log | Cannot fix G2 without a column (or forcing every click to send `gateway_name`) |
| G4 | Billplz `false` + offline refs sent to adapters | Product pretends in-product money refund. Tracker 13 residual #2 |
| G5 | No `GatewayRefundFailed` consumer | Staff cannot see failure; optimistic UI is a lie |
| G6 | Completion is the first time the log moves, and it is binary | HTTP 200 is not pending; refresh shows CONFIRMED; 093 paints REFUNDED |
| G7 | Re-POST not blocked | After G1, double full refund at Stripe |
| G8 | 400 is `StatusResponse`, TypeSpec/ops expect `ProblemDetails.detail` | Failures look like “Refund failed” with empty description |
| G9 | No handler / persist / adapter tests | G1–G7 unasserted |

### Not LP-091 (do not touch)

| Item | Owner |
|------|--------|
| `PARTIALLY_REFUNDED`, remaining, second refund, ledger ref per attempt | LP-092 |
| Amount field, SOP copy, status badges, stop optimistic paint, reason persist, method column | LP-093 |
| Inbound `charge.refunded` / CHIP `payment.refunded` | reserved `LP-PAY-022` |
| Outbound `payment.refunded` catalog | LP-135 |
| M2M `POST /integrations/payments/…/refund` | after 091–093 honest |
| Billplz Payment Order as refund | **Refuse** (`13`, `LP-PAY-023` is SOP + mark, not PO) |
| Guest / portal refund button | Refuse (chargeback magnet; legal page already correct) |
| Auto-cancel subscription on refund | Not this product |
| Role split (cashier cannot refund) | LP-166 |
| Who-clicked audit | LP-167 |
| `RefundedFee` from Stripe / CHIP | `LP-PAY-011` / webhook |
| LHDN CN product / real buyer TIN | LP-104 |
| Settlement reports | LP-095 / `LP-PAY-019` |
| Void / uncapture | Not a MY FPX job |

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

### 5.1 Full amount

`effectiveAmount = request.Amount ?? log.Amount`.  
If `effectiveAmount != log.Amount` (091-only tree): **400** `PARTIAL_NOT_ENABLED`.  
If 092 is in the same series: treat `== remaining` as full; `< remaining` as 092.

### 5.2 API vs mark

```
gateway = ResolveGateway(...)
if RequiresMarkRefunded(gateway) && !request.MarkRefunded:
    400 MARK_REFUNDED_REQUIRED
if RequiresMarkRefunded && MarkRefunded:
    log.TransitionToRefunded(); log.RefundedAmount = log.Amount
    Publish GatewayRefundCompleted (fee 0, amount = log.Amount)
    SaveChanges
    return refunded
if SupportsApiRefund:
    log.Status = REFUND_PENDING
    Publish GatewayRefundRequested
    SaveChanges
    return refund_requested
else:
    400 GATEWAY_REFUND_UNSUPPORTED
```

### 5.3 Completion / failure

Commerce completed handler:

- Find log as today.
- `RefundedAmount += event.RefundedAmount` (091: becomes `Amount`).
- `TransitionToRefunded()` when `RefundedAmount >= Amount`.
- Clear pending.

Commerce **new** `GatewayRefundFailed` handler:

- Find log by `PaymentRecordId`.
- If `REFUND_PENDING` → `REFUND_FAILED`.
- Do not touch `RefundedAmount`.

### 5.4 Stripe / CHIP / Razorpay

Keep adapter signatures. 091 does not add currency to the port.

Billplz stays `false` as a **backstop**. Commerce must not call it.

---

## 6. Minimal code changes (091)

### Must

1. **`CommerceTransactionLog`** — `GatewayName`, `RefundedAmount`, `MarkRefundPending` / `MarkRefundFailed` / `ApplyRefund(amount)` (full → `REFUNDED`). Keep `TransitionToRefunded()` as `ApplyRefund` when remaining hits 0.
2. **EF + Commerce migration** — nullable gateway, `RefundedAmount` default 0. No extra unique index.
3. **Stamp `GatewayName`** on `LogTransactionAsync` / record-payment / mark-offline.
4. **`PaymentGatewayCapabilities.SupportsApiRefund` / `RequiresMarkRefunded`** + tests.
5. **`RecordRefundCommand` (+ DTO)** — `MarkRefunded` bool; persist; resolve gateway; pending guard; no STRIPE default.
6. **`TransactionEndpoints`** — map `mark_refunded`; 400 ProblemDetails.
7. **TypeSpec** `RecordRefundRequestDto.mark_refunded?`; `TransactionLogDto.gateway_name?`, `refunded_amount`.
8. **Commerce `GatewayRefundFailed` handler** + DI subscribe.
9. **Commerce completed handler** — `ApplyRefund` instead of blind `TransitionToRefunded`.
10. **`RecordRefundCommandHandler` `SaveChangesAsync` after every publish.**

### Should (tiny, same ticket)

11. Stripe refund idempotency key.
12. XML comment on Billplz `IssueRefundAsync`: not Payment Order.
13. `Order.Refund()` if the completed handler can find a one-time order for the same org + gateway tx (optional; skip if no stable link).

### Must not

- Parse inbound refund webhooks.
- Emit outbound `payment.refunded`.
- Change Billplz adapter to call Payment Order.
- Add remaining UI.
- Cancel subscriptions.
- Touch M2M checkout refund.

---

## 7. Tests (091)

Keep NSubstitute IDOR. Add persist tests next to `OutboundWebhookRequestedPersistTests`.

### 7.1 `RecordRefundCommandHandlerTests` (new) — real outbox

| Test | Assert |
|------|--------|
| `Handle_Publishes_And_Persists_Outbox` | After handle, `commerce.OutboxMessages` has one `GatewayRefundRequested` with `Amount == log.Amount`, `GatewayTransactionId == ExternalReference`; log `REFUND_PENDING` |
| `Handle_DoesNotDefaultStripe_WhenGatewayMissing` | throw `GATEWAY_REQUIRED`; **zero** outbox |
| `Handle_UsesLogGatewayName` | CHIP log → event `GatewayName=CHIP` |
| `Handle_Rejects_AlreadyRefunded` / `_Pending` | no second outbox |
| `Handle_Rejects_Billplz_WithoutMark` | 400-equivalent exception; no adapter event |
| `Handle_MarkRefunded_Billplz_PublishesCompleted_NotRequested` | outbox type completed; log `REFUNDED`; `RefundedAmount == Amount` |
| `Handle_Rejects_Offline_WithoutMark` | `MANUAL_OFFLINE` |
| `Handle_Rejects_PartialAmount` (if 091 ships alone) | exception |
| Keep IDOR | no publish |

### 7.2 `GatewayRefundRequestedIntegrationEventHandlerTests` (new)

| Test | Assert |
|------|--------|
| Missing config → `GatewayRefundFailed` | message contains configuration |
| Amount ≤ 0 → failed | |
| Adapter true → `GatewayRefundCompleted` with same amount, fee 0 | |
| Adapter false → failed | |
| Soft-disable config still calls adapter | (config present, `IsActive=false`) |

### 7.3 Commerce completed / failed (new)

| Test | Assert |
|------|--------|
| Completed matches by `ExternalReference` → `REFUNDED`, `RefundedAmount` | |
| Failed on `REFUND_PENDING` → `REFUND_FAILED` | |
| Failed on unknown id → no throw | |

### 7.4 Adapters

| Test | Assert |
|------|--------|
| Billplz `IssueRefundAsync` → `false` (lock) | |
| CHIP: amount &gt; 0 posts `{ amount: sen }` to `purchases/{id}/refund/` (HttpMessageHandler fake) | |
| Stripe: options PaymentIntent + Amount minor (if you can intercept Stripe.net; otherwise skip network and unit-test a thin wrapper). Do not require live Stripe | |

### 7.5 Capabilities

`SupportsApiRefund` true for Stripe/CHIP/Razorpay; false for Billplz/null. `RequiresMarkRefunded` inverse for Billplz/offline names.

No host e2e. No ops component test required on 091 (093).

---

## 8. Files to touch (when implementing 091)

| File | Change |
|------|--------|
| `Modules/Commerce/Domain/Entities/CommerceTransactionLog.cs` | columns + transitions |
| `Modules/Commerce/Infrastructure/CommerceDbContext.cs` | map |
| `Modules/Commerce/Infrastructure/Migrations/*_AddTransactionRefundFields.cs` | **new** |
| `Modules/Commerce/Contracts/Commands/RecordRefundCommand.cs` | `MarkRefunded` |
| `Modules/Commerce/Application/Commands/RecordRefundCommandHandler.cs` | persist + resolve + mark |
| `Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs` | ProblemDetails + flag |
| `Modules/Commerce/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` | `ApplyRefund` |
| `Modules/Commerce/Infrastructure/EventHandlers/GatewayRefundFailedIntegrationEventHandler.cs` | **new** |
| `Modules/Commerce/Infrastructure/DependencyInjection.cs` | subscribe failed |
| `Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler*.cs` | stamp gateway |
| `Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs` | stamp `OFFLINE` |
| `Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` | stamp `OFFLINE` |
| `Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` | refund flags |
| `Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` | optional idempotency |
| `Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` | comment only |
| `packages/api-spec/modules/commerce/models/subscriber.tsp` | DTO fields |
| `tests/.../RecordRefundCommandHandlerTests.cs` | **new** |
| `tests/.../GatewayRefundRequestedIntegrationEventHandlerTests.cs` | **new** |
| `tests/.../PaymentGatewayCapabilitiesTests.cs` | extend |
| `tests/.../BillplzGatewayAdapterTests.cs` | lock `false` |
| `tests/.../ChipCollectGatewayAdapterTests.cs` | refund POST |

`task gen` after TypeSpec. No ops TSX on this ticket if 093 follows immediately; if 091 is demoed alone, a one-line body `{ gateway_name }` is enough to exercise the API.

---

## 9. Acceptance (flip LP-091 to **Y** when)

1. `POST /admin/commerce/transactions/{id}/refund` with a Stripe/CHIP/Razorpay log commits **one** `commerce.OutboxMessages` row and sets `REFUND_PENDING`. Killing the process after 200 still leaves the row.
2. Payments worker calls `IssueRefundAsync` with the log’s `ExternalReference` and **full** amount. Success → log `REFUNDED`, `RefundedAmount == Amount`. Billing posts one `GATEWAY_REFUND`.
3. Adapter `false` or missing config → log `REFUND_FAILED`, **not** `REFUNDED`. Retry allowed.
4. **No** default `STRIPE`. Missing gateway → 400 `GATEWAY_REQUIRED`.
5. Billplz / offline without `mark_refunded` → 400 `MARK_REFUNDED_REQUIRED`. With the flag → `REFUNDED` and `GatewayRefundCompleted` **without** `IssueRefundAsync`.
6. Billplz adapter still returns `false`. No Payment Order client.
7. Tests in §7 green. Tracker LP-091 **P → Y**. Do **not** flip LP-092 / LP-093 / `LP-PAY-009` to shipped (009 stays partial until 092 + Billplz SOP UI + 022). Do **not** flip `LP-PAY-022`.

---

*Read-only analysis of the Commerce → Payments full-refund path as of 16 August 2026. No product code changed. Shared plan §4 is copied in W1-LP-092 and W1-LP-093.*
