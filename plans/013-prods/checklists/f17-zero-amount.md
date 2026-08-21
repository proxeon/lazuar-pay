# F17 — Amount ≤ 0 is not paid

**Track:** Fulfillment · **Depends:** F10  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**Goal:** Amount ≤ 0 does not mint `RCPT-`, does not insert ACTIVE, does not write a GMV journal. Align G22.

---

## F17.1 Skip

- [x] If `amount_paid <= 0` or setup-only: return without booking (HTTP 200 to the PSP is fine; no money rows)
- [x] Align [G22](./g22-setup-not-paid.md): setup / setup-intent is not paid
- [x] 009 / live Hub GMV skip: `$0` is not GMV — read `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` (`AmountPaid <= 0`); **read, do not copy project**

## F17.2 Must not

- [x] Do not burn a `RCPT-` sequence
- [x] Do not insert `subscriptions` ACTIVE
- [x] Do not insert `journal_entries` / lines (empty journal is not a journal — F13)
- [x] Do not title a zero row Official Receipt

## F17.3 Exit

- [x] Test: ≤0 event leaves journal line count 0, no `RCPT-`, checkout not `paid`
- [x] Aligns with G22
