# F13 — Balanced journal, same transaction

**Track:** Fulfillment · **Depends:** F11, D26  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-MON-001  
**Goal:** Insert `journal_entries` + `journal_lines` in the same DB transaction as `paid`. Reject unbalanced.

---

## F13.1 Write

- [ ] Same `BEGIN` as the checkout `paid` update
- [ ] Unique grain `(org_id, reference_type, reference_id)`; `reference_type = gateway_payment`
- [ ] `reference_id` = provider payment id (not the checkout create key)

## F13.2 Balance guard

- [ ] Read `apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs` `ValidateBalanced` — **read, do not copy project**
- [ ] Steal: empty lines throw; net `Amount` **per currency** must be 0
- [ ] Reject unbalanced (throw / roll back). Do not insert
- [ ] Line currency must match header (v1 MYR)

## F13.3 Shape (v1)

- [ ] `asset_cash` + net; `expense_gateway_fee` + fee **only if** the PSP sent fee > 0 (`unknown` ≠ 0)
- [ ] `revenue_gross` − (amount − tax); `liability_tax_payable` − tax only if tax > 0
- [ ] No AR, deferred, `invoice_issued`, Hub SaaS / credit accounts

## F13.4 Must not

- [ ] No `TaxInvoiceId` column. No LHDN statuses on the journal
- [ ] No ProjectReference to `apps/lazuar-api` / `Modules.Billing`

## F13.5 Exit

- [ ] Unbalanced path does not commit `paid`
- [ ] Unblocked for F14, F16, F18, F22
