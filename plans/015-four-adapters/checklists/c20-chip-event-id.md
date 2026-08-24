# C20 — CHIP event id paid:{purchaseId}

**Track:** CHIP · **Depends:** C19  
**Analysis:** [00](../00-what-must-be-done.md) §5.1; Hub used `{mapped}:{id}` after EventId collisions  
**IDs:** NP-GW-006  
**Goal:** Fail-then-pay must not share a unique grain.

---

## C20.1

- [ ] Paid unique id = `paid:{purchaseId}` (or `PAYMENT_COMPLETED:{id}` — pick **one** prefix and stick; prefer `paid:`)
- [ ] Purchase id from C23
- [ ] Do **not** use bare purchase id as EventId (008/009 history)
- [ ] Failure events if inserted must use `failed:{purchaseId}` — this program **ignores** failure (C22) so prefer **not** to consume `paid:` grain

## C20.2 Exit

- [ ] Unique key uses namespaced id
- [ ] Unblocked for C25
