# X16 — SETTLED does not second-journal

**Track:** Xendit · **Depends:** X15  
**Analysis:** Hub maps SETTLED to `PAYMENT_COMPLETED` (dangerous if PAID already used a different event id)  
**IDs:** NP-GW-006  
**Goal:** Fulfill **PAID only**. SETTLED → 200 ignored.

---

## X16.1

- [ ] Do **not** copy Hub mapping SETTLED → PAYMENT_COMPLETED
- [ ] SETTLED / `invoice.settled` → `{ ignored: "settled" }`
- [ ] If PAID already inserted `paid:{invoiceId}`, SETTLED must not mint a second receipt even if you mistakenly fulfill — unique + status≠open saves you; still do not call fulfill

## X16.2 Test

- [ ] PAID then SETTLED → still one document

## X16.3 Exit

- [ ] Test green
