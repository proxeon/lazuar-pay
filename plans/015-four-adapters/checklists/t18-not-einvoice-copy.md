# T18 — Honesty copy: not an e-invoice

**Track:** Tax · **Depends:** T14  
**Analysis:** [00](../00-what-must-be-done.md) §4 / §6  
**IDs:** NP-DOC-003  
**Goal:** Humans are told receipts are not tax invoices.

---

## T18.1 Surfaces

- [ ] Merchant receipts list/detail on `:5178`: Official Receipt, not Tax Invoice
- [ ] Checkout paid state on `:5179` already says Official Receipt — keep
- [ ] Optional one line on merchant or receipt: “Pay does not file SST or MyInvois.”

## T18.2 Must not

- [ ] Do not print VALID
- [ ] Do not print a QR that looks like MyInvois

## T18.3 Exit

- [ ] Copy matches T14
- [ ] Tax track done. Unblocked for S10 (schema may overlap T after A00)
