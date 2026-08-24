# B22 — Billplz event id paid:{billId}

**Track:** Billplz · **Depends:** B20  
**Analysis:** [00](../00-what-must-be-done.md) §5.2; Hub `{PAYMENT_COMPLETED}:{billId}`  
**IDs:** NP-GW-006  
**Goal:** Missing bill id is unusable. Do not invent Guids.

---

## B22.1

- [x] Form field `id` = bill id
- [x] Missing → 400 unusable, no fulfill
- [x] Paid EventId = `paid:{billId}`
- [x] Persist `ProviderSessionId` if empty

## B22.2 Exit

- [x] Covered by B20 fixture
