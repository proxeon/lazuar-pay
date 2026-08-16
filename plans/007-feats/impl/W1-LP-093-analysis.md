# W1-LP-093 — Refund UI in ops

**Date:** 16 August 2026  
**Status:** Analysis only — **do not implement from this file**  
**Tracker:** [00-checklist-tracker.md](../00-checklist-tracker.md) `LP-093` (Wave 1, Lazuar **N**)  
**Implement list:** [00-implement-ids.md](../00-implement-ids.md) “Refund UI in ops”  
**Siblings (same program):** [W1-LP-091-analysis.md](./W1-LP-091-analysis.md) persist + full + mark-refunded + failed consumer; [W1-LP-092-analysis.md](./W1-LP-092-analysis.md) remaining / `PARTIALLY_REFUNDED`  
**Evidence:** [17-merchant-dashboard-analytics.md](../17-merchant-dashboard-analytics.md) Transactions §; live `TransactionDetailPanel.tsx` + `SubscribersPage.tsx` + `TransactionsPage.tsx`

**This ticket is not** LP-091/092 backend (except TypeSpec fields those tickets already add), LP-097 (CSV), LP-166 (cashier cannot refund), LP-167 (who clicked), `LP-PAY-017` (webhook replay), inbound refund webhooks, or a buyer portal refund button.

§4 is the **same shared plan** as 091/092. This file owns the **merchant console**: a button that tells the truth.

**Land 091 (+ 092 if we show an amount box).** An amount field against a binary `REFUNDED` log is how we got here.

---

## 0. Verdict

Tracker `LP-093 = N` and the checklist footnote “No ops refund button” are **half stale**. There **is** a refund control in two places. It is not a sellable refund UI.

| Surface | What staff see | What the API gets | After 200 |
|---------|----------------|-------------------|-----------|
| Commerce → **Transaction Logs** → detail “Issue Refund” | “Full refund of **RM {amount}**. Cannot be undone.” Optional reason | **`{}`** | Toast success; **optimistic `status: "REFUNDED"`**; reason discarded |
| Commerce → **Subscribers** → payment ledger “Refund” | “Full refund… cannot be undone.” Optional reason | `{ subscription_id }` only | Same; reason discarded |

That is why inventory `01` §19 can say “Ops Transactions page” while the tracker stays **N**. A button that (a) never persists the request (091 G1), (b) defaults the rail to Stripe, (c) offers Billplz/cash the same “Process Refund”, and (d) paints refunded before money moves, is **worse than no button**.

Other honesty holes on the same pages (in scope when they touch refund):

- List “method” column reads `tx.payment_method`. `TransactionLogDto` has **`recorded_by_name` only**. The cell is **undefined** at runtime (`17` already recorded this).
- Status filter is `CONFIRMED | REFUNDED`. No pending / partial / failed.
- Method filter `ONLINE_GATEWAY | BANK_TRANSFER | …` is sent as `payment_method` and **ignored by SQL**.
- Detail breakdown never shows `refunded_amount` / remaining.
- 400 body is `{ status: "…" }` while the client throws `error.detail` → toast “Refund failed” with an empty description.

**LP-093 is: one honest refund modal on the transaction (and the same modal on the subscriber ledger). Amount + remaining. API vs mark-refunded by rail. No optimistic REFUNDED. Reason sent. Status badges that match the machine. Method/gateway visible. Do not build roles, CSV, or a refund inbox.**

---

## 1. Product contract for this ID

Sellable sentence after this ticket:

> In ops, an admin opens a confirmed (or partially refunded) transaction and sees whether Lazuar can refund it **at the processor** or only **mark it** after they did the work in Billplz / at the desk. They type an amount up to remaining, optionally a reason, and confirm. The row shows **Refund pending** until the worker finishes, then **Refunded** or **Partially refunded** or **Refund failed** with the error toastable from the next GET. The UI never claims a Billplz bill was reversed in-product.

| Rail / method | Primary action | Copy |
|---------------|----------------|------|
| Stripe / CHIP / Razorpay | **Refund at {gateway}** | Money leaves the merchant’s processor account. Pending until confirmed. |
| Billplz | **Mark refunded** (default). No “Process Refund” that calls the API | “Billplz has no bill-refund API. Refund the bill in the Billplz dashboard, then mark it here.” Link out is optional (no deep link required). |
| `BANK_TRANSFER` / `CASH` / `MANUAL_OFFLINE` | **Mark refunded** | “This was logged offline. Mark only after you returned the money.” |
| `COMPED` / amount 0 | **Hide** refund | Nothing to return |
| `REFUND_PENDING` | **Disable** | “Refund in progress” |
| `REFUNDED` | Badge only | Strike amount as today |
| `REFUND_FAILED` | Retry (API or mark, same rules) | Show last failure if we add `detail` on GET; else “Retry refund” |
| `GATEWAY_REQUIRED` (legacy `SYSTEM` row, null gateway) | Gateway `<select>` Stripe / CHIP / Razorpay / Billplz **before** submit | Do not silently pick Stripe |

Amount field (requires 092):

- Default = **remaining** (full).
- Min &gt; 0, max = remaining, step 0.01.
- Helper: “Already refunded RM X · remaining RM Y”.
- If 092 has not landed, hide the field and only offer full (091). Do **not** ship a free-typed amount that the API will 400.

Success:

- API path: toast **“Refund requested”** (not “Refunded”). Leave status `REFUND_PENDING` from GET (invalidate queries; **do not** set `REFUNDED` locally).
- Mark path: toast **“Marked refunded”**; status from response / refetch.

Failure:

- Show `error.detail` (091 switches 400 to ProblemDetails).

Do **not** auto-cancel the subscription from this modal. A one-line “Cancel the subscription separately if access should stop” is enough.

Do **not** add a refund button on `lazuar-admin`, portal, or M2M.

---

## 2. What exists (ops, 16 Aug 2026)

### 2.1 Routes

| Page | Path (ops) | Files |
|------|------------|-------|
| Transaction Logs | Commerce → Transactions | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/TransactionsPage.tsx` |
| Detail / refund modal | side panel + overlay | `.../components/TransactionDetailPanel.tsx` |
| Subscriber payments | Member console “Payment Ledger” | `.../pages/SubscribersPage.tsx` |

`POST /admin/commerce/transactions/{id}/refund` — OrgAdmin. See 091 §2.1.

Credit Notes page (`invoicing/CreditNotesPage.tsx`) is **unrouted** (ADR 023). Not this ticket.

### 2.2 `TransactionDetailPanel` (verbatim behavior)

```tsx
client.POST("/admin/commerce/transactions/{id}/refund", {
  params: { path: { id: transaction.id } },
  body: {},
});
// onSuccess:
onUpdate(transaction ? { ...transaction, status: "REFUNDED" } : null);
```

`isRefundable = status === "CONFIRMED" && amount > 0`.  
`isRefunded = status === "REFUNDED"` → amber strike + badge.

Reason state `refundReason` is never put on the body. `RecordRefundRequestDto` has no `reason` field today (091/093 add it).

Comment in the file already admits: *“Status flips to REFUNDED when GatewayRefundCompleted is processed; optimistically mark requested.”* Then it marks **REFUNDED**, not requested.

### 2.3 `SubscribersPage` ledger

Payments loaded via `GET /admin/commerce/transactions?search={customer_email}` — not “payments for this subscription”. Two customers sharing an email, or a drifted log email, mis-attribute. Residual; do not rebuild the query unless it is cheap. Refund button: `CONFIRMED && amount > 0`. Body `{ subscription_id: selectedSub.id }`. Same full-only modal.

### 2.4 `TransactionsPage` list

- Amount amber if `REFUNDED`.
- Badge: CONFIRMED emerald / else amber (so `REFUND_PENDING` would already look “refund-ish” if we only change the backend — **must** add badge variants).
- `tx.payment_method` is not on the DTO → blank method line.
- Filters: status ALL/CONFIRMED/REFUNDED; method ALL/ONLINE_GATEWAY/BANK_TRANSFER/CASH/COMPED. SQL ignores method. Search is name/email only (not ref).

### 2.5 DTO vs UI

TypeSpec `TransactionLogDto` (`packages/api-spec/modules/commerce/models/subscriber.tsp`):

`id, amount, fee_amount, net_amount, currency, status, created_at, customer_name, customer_email, product_name?, recorded_by_name, external_reference?`

091/092 add `gateway_name?`, `refunded_amount`. 093 should also expose:

- `remaining_amount` (or compute `amount - refunded_amount` in TS — prefer server if we want one rounding)
- `supports_api_refund` **or** let the client call the same capability rules (`STRIPE|CHIP|RAZORPAY` vs rest). Prefer **server flag** so Razorpay vs Billplz cannot drift.
- `reason` is write-only.

`payment_method`: either add it as an alias of `recorded_by_name` or change the React to `recorded_by_name`. Do the latter + show `gateway_name` as its own line. Stop lying that RecordedBy is a rail.

### 2.6 Error mapping

Generated 400 is `Core.ProblemDetails`. Live endpoint returns `StatusResponse`. 091 must flip the endpoint; 093 only needs `error.detail` once that exists. If 093 ships first, read `error.detail ?? error.status ?? String(error)`.

---

## 3. Gaps (in scope for LP-093)

| # | Gap | Why LP-093 fails |
|---|-----|------------------|
| G1 | Optimistic `REFUNDED` | Staff believe money moved; 091 proves it often did not |
| G2 | Body `{}` / no amount / no gateway / no reason | Cannot partial (092); cannot backfill gateway; reason is theater |
| G3 | Same CTA for Billplz / cash / Stripe | Dishonest (inventory: “UI may offer refund; gateway will fail”) |
| G4 | `isRefundable` only `CONFIRMED` | After 092, partials cannot refund the rest; after 091, `REFUND_FAILED` cannot retry |
| G5 | List/detail ignore pending / partial / failed | New statuses look like generic amber |
| G6 | `payment_method` undefined; method filter dead | Console looks unfinished next to a refund action |
| G7 | Duplicate modal code (detail vs subscribers) | Two places to get G1–G4 wrong |

### Not LP-093

| Item | Owner |
|------|--------|
| Persist / capabilities / mark-refunded API / ProblemDetails | LP-091 |
| Remaining math / `PARTIALLY_REFUNDED` / ledger keys | LP-092 |
| CSV of transactions | LP-097 |
| Staff role “cannot refund” | LP-166 |
| Audit “who refunded” | LP-167 |
| Webhook replay | `LP-PAY-017` |
| Credit-note UI un-hide | Wave 2 / ADR 023 |
| Real-time toast from `GatewayRefundFailed` without refresh | Nice; invalidate + refetch is enough |
| Buyer portal refund | Refuse |

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

## 5. Recommended UI (this ID)

### 5.1 One component

Extract `RefundModal` used by `TransactionDetailPanel` and `SubscribersPage`. Props: `transaction` (DTO), `subscriptionId?`, `onClose`, `onSettled`.

Do not add a third entry point (dashboard, admin).

### 5.2 Modal contents

1. Header: “Refund” + gateway / method chip.  
2. Money: original, already refunded, **remaining** (mono, RM).  
3. Amount input default remaining (092). Disabled at remaining if you want a “full only” checkbox — not required; defaulting remaining **is** full.  
4. Reason optional → `reason` on body (persist last reason on log).  
5. Gateway select **only** when `gateway_name` is null.  
6. Primary button:
   - API rail: “Refund RM {n} via {gateway}”
   - Mark rail: “Mark RM {n} refunded”
7. Secondary: Cancel.  
8. Amber warning: API = money movement, irreversible at the processor. Mark = bookkeeping, only click after the dashboard/desk refund.

### 5.3 When to show the trigger

```
remaining = amount - (refunded_amount ?? 0)
canAct = remaining > 0 && status ∈ { CONFIRMED, PARTIALLY_REFUNDED, REFUND_FAILED }
pending = status === REFUND_PENDING
```

Pending: show disabled “Refund in progress”.  
Refunded: badge only (keep today’s amber).  
Partial: badge + “Refund rest”.

### 5.4 Badges (list + detail + subscriber ledger)

| Status | Style |
|--------|--------|
| `CONFIRMED` | emerald (today) |
| `REFUND_PENDING` | blue / zinc “Pending refund” |
| `PARTIALLY_REFUNDED` | amber “Partial · RM {refunded} back” |
| `REFUNDED` | amber “Refunded” (today) |
| `REFUND_FAILED` | rose “Refund failed” + retry |

Amount column: do not strike the **original** on partial; strike only when fully `REFUNDED`. Optional second line `− RM {refunded}`.

### 5.5 List filters

- Status: add `REFUND_PENDING`, `PARTIALLY_REFUNDED`, `REFUND_FAILED`.
- Method filter: **either** wire SQL to `RecordedByName` / `GatewayName` **or** remove the dead select. Prefer: filter `gateway_name` (`ALL | STRIPE | CHIP | BILLPLZ | RAZORPAY | OFFLINE`). Honest and useful next to refund.
- Method column: `recorded_by_name` + `gateway_name` if present. Never `tx.payment_method` unless TypeSpec adds it.

### 5.6 Refetch

`onSuccess`: `invalidateQueries(["commerce-transactions"])`, `["commerce-payments"]`, `["financial-summary"]`. **Do not** `onUpdate({ status: "REFUNDED" })`. For pending, `onUpdate({ status: "REFUND_PENDING" })` is allowed if the 200 is `refund_requested`.

Optional: `refetchInterval` 2s while the open panel is `REFUND_PENDING` (max ~30s). Should, not must.

### 5.7 TypeSpec (093-owned bits)

If 091/092 already added money fields, 093 only:

```
model RecordRefundRequestDto {
  amount?: float64;
  gateway_name?: string;
  subscription_id?: string;
  tax_amount?: float64;
  mark_refunded?: boolean;   // 091
  reason?: string;           // 093
}

model TransactionLogDto {
  ...
  gateway_name?: string;
  refunded_amount: float64;
  remaining_amount: float64;
  supports_api_refund: boolean;
}
```

`supports_api_refund` from `PaymentGatewayCapabilities.SupportsApiRefund(gateway_name)` in `CommerceQueryService.Transactions`.

Map `reason` → `log.RefundReason` in the command (091 column).

---

## 6. Minimal code changes (093)

### Must

1. **`RefundModal.tsx`** (new) — §5.2.  
2. **`TransactionDetailPanel.tsx`** — use modal; stop empty body; stop optimistic REFUNDED; remaining-aware CTA.  
3. **`SubscribersPage.tsx`** — same modal; keep `subscription_id`.  
4. **`TransactionsPage.tsx`** — badges; status filter; method column = `recorded_by_name` / `gateway_name`; fix or replace method filter.  
5. **Query DTO mapping** — `gateway_name`, `refunded_amount`, `remaining_amount`, `supports_api_refund`.  
6. **TypeSpec + `task gen`**.  
7. **Command** accepts `reason` if 091 left it off.

### Should

8. Poll pending while panel open.  
9. Helper text that cancel is a separate action.  
10. `CreditNotesPage` untouched (hidden).

### Must not

- New ops route / “Refunds” nav item. Transaction Logs **is** the inbox.
- Admin app refund.
- Portal refund.
- CSV (097).
- Role gating (166).

---

## 7. Tests (093)

Ops has no component test harness worth extending. Prove the **contract** the UI needs:

| Test | Where |
|------|--------|
| Query maps `gateway_name`, `refunded_amount`, `remaining_amount`, `supports_api_refund` | `CommerceQueryService` test or thin mapper test if one exists; else a new `CommerceTransactionQueryTests` with Dapper is heavy — a unit test on a static mapper is enough if you extract one |
| `RecordRefund` persists `reason` | 091 handler tests |
| TypeSpec honesty: `payment_method` no longer read from a missing field | manual / grep `tx.payment_method` = 0 in ops |

No Playwright required. Manual demo checklist in §9.

---

## 8. Files to touch (when implementing 093)

| File | Change |
|------|--------|
| `apps/lazuar-ops/src/modules/commerce/components/RefundModal.tsx` | **new** |
| `apps/lazuar-ops/src/modules/commerce/components/TransactionDetailPanel.tsx` | wire modal |
| `apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx` | wire modal; drop inline refund form |
| `apps/lazuar-ops/src/modules/commerce/pages/TransactionsPage.tsx` | badges + filters + method column |
| `Modules/Commerce/Infrastructure/Services/CommerceQueryService.Transactions.cs` | new fields; optional real method/gateway filter |
| `packages/api-spec/modules/commerce/models/subscriber.tsp` | DTO |
| `RecordRefundCommand` / handler | `Reason` |
| generated `api-types-ts` via `task gen` | |

No Payments adapter files. No LHDN.

---

## 9. Acceptance (flip LP-093 to **Y** when)

1. Transaction Logs detail and Subscriber ledger use the **same** modal. Neither sends `{}`. Neither sets `REFUNDED` on 200 for the API path.
2. Stripe/CHIP/Razorpay: CTA is “Refund via {gateway}”. Default amount = remaining. Optional reason is stored.
3. Billplz / offline: **no** “Process Refund” that hits `IssueRefundAsync`. CTA is mark-refunded with SOP copy. 091 API still 400s if someone crafts a POST without `mark_refunded`.
4. Badges distinguish pending / partial / full / failed. List filter can select them.
5. Method column shows a real DTO field (`recorded_by_name` and/or `gateway_name`). `tx.payment_method` is gone.
6. After a 091 API refund, refresh (or poll) shows `REFUND_PENDING` then `REFUNDED` / `PARTIALLY_REFUNDED` / `REFUND_FAILED` — not an optimistic lie.
7. Tracker LP-093 **N → Y**. Flip only after 091 (and 092 if the amount box is shown). Checklist footnote “No ops refund button” is deleted in the same tracker edit.

**Manual demo (attach to done note, not this file):** one Stripe (or CHIP) full refund, one partial, one Billplz mark, one failed (wrong gateway / Billplz API click via crafted request) showing `REFUND_FAILED`.

---

*Read-only analysis of ops refund UI as of 16 August 2026. No product code changed. Shared plan §4 matches W1-LP-091 and W1-LP-092.*
