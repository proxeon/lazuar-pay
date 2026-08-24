# X18 — Event id paid:{invoiceId}

**Track:** Xendit · **Depends:** X15  
**Analysis:** Hub `{mapped}:{invoiceId}`  
**IDs:** NP-GW-006  
**Goal:** Missing invoice id unusable. No default MYR (X19).

---

## X18.1

- [x] Invoice `id` required
- [x] Paid EventId `paid:{invoiceId}`
- [x] Missing id → 400 unusable

## X18.2 Exit

- [x] Covered by X15
