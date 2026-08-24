# X18 — Event id paid:{invoiceId}

**Track:** Xendit · **Depends:** X15  
**Analysis:** Hub `{mapped}:{invoiceId}`  
**IDs:** NP-GW-006  
**Goal:** Missing invoice id unusable. No default MYR (X19).

---

## X18.1

- [ ] Invoice `id` required
- [ ] Paid EventId `paid:{invoiceId}`
- [ ] Missing id → 400 unusable

## X18.2 Exit

- [ ] Covered by X15
