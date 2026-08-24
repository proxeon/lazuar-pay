# X17 — PENDING / EXPIRED / FAILED ignore

**Track:** Xendit · **Depends:** X15  
**Analysis:** Hub `MapStatus` FAILED/EXPIRED  
**IDs:** —  
**Goal:** Not a receipt.

---

## X17.1

- [ ] PENDING, EXPIRED, FAILED, `invoice.expired`, `invoice.failed` → 200 ignored
- [ ] No `RCPT-`
- [ ] Do not consume `paid:{id}` grain

## X17.2 Exit

- [ ] One fixture (EXPIRED) green
