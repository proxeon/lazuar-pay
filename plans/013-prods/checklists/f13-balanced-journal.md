# F13 — Balanced journal, same transaction

**Track:** Fulfillment · **Depends:** F11, D26  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-MON-001  
**Goal:** Insert `journal_entries` + `journal_lines` in the same DB transaction as `paid`. Reject unbalanced.

---

## F13.1 Write

- [x] Same `BEGIN` as the checkout `paid` update
- [x] Unique grain `(org_id, reference_type, reference_id)`; `reference_type = gateway_payment`
- [x] `reference_id` = provider payment id (not the checkout create key)

## F13.2 Balance guard

- [x] Read `apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs` `ValidateBalanced` — **read, do not copy project**
- [x] Steal: empty lines throw; net `Amount` **per currency** must be 0
- [x] Reject unbalanced (throw / roll back). Do not insert
- [x] Line currency must match header (v1 MYR)

## F13.3 Shape (v1)

- [x] `asset_cash` + net; `expense_gateway_fee` + fee **only if** the PSP sent fee > 0 (`unknown` ≠ 0)
- [x] `revenue_gross` − (amount − tax); `liability_tax_payable` − tax only if tax > 0
- [x] No AR, deferred, `invoice_issued`, Hub SaaS / credit accounts

## F13.4 Must not

- [x] No `TaxInvoiceId` column. No LHDN statuses on the journal
- [x] No ProjectReference to `apps/lazuar-api` / `Modules.Billing`

## F13.5 Exit

- [x] Unbalanced path does not commit `paid`
- [x] Unblocked for F14, F16, F18, F22
