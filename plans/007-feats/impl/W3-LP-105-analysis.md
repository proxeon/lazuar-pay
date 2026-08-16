# W3-LP-105 — Payment terms / due date / AR reminders

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-105`. Tracker: *Payment terms / due date / AR reminders* — Lazuar **N**.  
**Not this ID:** Subscription dunning (`LP-070`–`079`). Quotes un-hide (`LP-102`). Tax invoice (`LP-103`). Portal document history (`LP-175`). Stripe Invoicing / Net 30 as a second product.

**Invariant:** A **custom checkout / quote** can have a **due date**. Before/on/after that date we email the existing pay URL. This is AR for one-off bills, not card retries. Do not reuse `DunningEngineJob`.

---

## 0. Scope lock

In scope:

- `CheckoutSession.DueAt` on **ad-hoc** sessions  
- Ops create-quote: Net 7 / 15 / 30 or custom date  
- Small `InvoiceReminderJob` (or a claim in Commerce) for `OPEN` + due window  
- Template `invoice.due` / `invoice.overdue` (or one template + offset)

Out of scope:

- AR aging dashboard  
- Partial payments  
- Recurring `send_invoice`  
- Using PAST_DUE campaigns on quotes  
- Un-hiding the quotes module (that is LP-102 — **this ticket is blocked** for merchant UI until that lands)

---

## 1. Verdict

Custom checkout has `ExpiresAt` (link death), not a commercial due date. `InvoiceIssuedIntegrationEvent` has a `DueDate` field and **no publisher**. Subscription dunning is a different object. HitPay/Stripe “Net 30 + remind” is not a campaign builder.

Ship only after LP-102 remounts quotes. Until then, an API-only due date is **P**.

---

## 2. Current files

| Path | Role |
|------|------|
| `CreateCustomCheckoutRequestDto` | `expires_at`, no `due_at` |
| `CheckoutSession.ExpiresAt` | 24h default / quote expiry |
| `CreateQuoteModal.tsx` | Lines only |
| `DunningEngineJob` | Subscriptions |
| `InvoiceIssuedIntegrationEvent` | Orphan `DueDate` |
| Communications catalog | No invoice-due template |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No due date column |
| G2 | No reminder worker for OPEN custom sessions |
| G3 | Quotes UI hidden (LP-102) |
| G4 | No template |

---

## 4. Recommended model

```
CreateCustomCheckout { ..., due_at?: utc, terms?: "due_on_receipt"|"net_7"|"net_15"|"net_30" }
  DueAt = due_at ?? now + terms
  ExpiresAt = max(ExpiresAt, DueAt + 14d)  // link must outlive due

InvoiceReminderJob hourly:
  OPEN custom sessions
  offsets: -3, 0, +3 from DueAt
  unique (SessionId, DayOffset) like ReminderDispatchLog
  email existing public pay URL (LP-102)
```

Do **not** mark the session `PAST_DUE`. Status stays `OPEN` until paid/expired. Ops badge “overdue” is display-only.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `CheckoutSession` + migration | `DueAt?` |
| Create custom command + TypeSpec | `due_at` / `terms` |
| `CreateQuoteModal` | Terms select (after LP-102) |
| New job + dispatch log | 3 offsets |
| `DefaultMessageTemplates` | One invoice reminder |
| Tests | Offset idempotency; skip COMPLETED |

Must not: dunning campaign targeting quotes; `InvoiceIssued` publisher (stub TIN).

---

## 6. Tests

| Case | Expect |
|------|--------|
| Net 30 create | `DueAt ≈ now+30d` |
| Day 0 due, OPEN | One email with pay URL |
| Replay same hour | Still one log |
| COMPLETED | No mail |
| Product (non-custom) session | Job ignores |

---

## 7. Acceptance

1. Merchant sets Net 30 on a quote; buyer is emailed on due with the same `/pay/{id}` link.  
2. Paying completes the session; reminders stop.  
3. Subscription dunning unchanged.  
4. No aging report required.

Tracker **N → Y** after 1–2 **and** LP-102 UI. Else **P**.

---

## 8. Order

After LP-102. Then: column → template → job → modal.

Do **not** implement from this file.
