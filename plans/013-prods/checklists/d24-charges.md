# D24 — `charges`

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** Attempt / capture row. **Not** the journal.

---

## D24.1 Table

- [x] `charges`: `id`, `org_id`, `checkout_id`, `provider`, `provider_ref`, `amount`, `currency`, `status`
- [x] `org_id` is One tenant id
- [x] One table — not Hub `ChargeAttemptLogs` **plus** `TransactionLogs` as two SoTs

## D24.2 Not the journal

- [x] Do not put debit/credit lines on this table
- [x] Journal is D26 (`journal_entries` + `journal_lines`)
- [x] Same-handler insert with paid + journal is F, not this file

## D24.3 Refuse

- [x] No `TaxInvoiceId`
- [x] No Stripe Billing subscription id as this row’s SoT
- [x] No MediatR `GatewayPaymentCompleted` outbox to create the row later

## D24.4 Exit

- [x] Table exists; it is not named `ledger_*`
- [x] Unblocked for D25
