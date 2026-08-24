# B24 — No Billplz refund API

**Track:** Billplz · **Depends:** B10  
**Analysis:** [00](../00-what-must-be-done.md) §5.2; Hub IssueRefund returns false  
**IDs:** parked-refunds  
**Goal:** Payment Order is a disbursement, not a reversal.

---

## B24.1

- [ ] Do not add `IssueRefund` on BillplzHosted
- [ ] Do not POST Billplz payment orders
- [ ] Merchant copy may say refunds are later / mark in dashboard (U19)

## B24.2 Exit

- [ ] No refund method
