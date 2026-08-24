# B12 — Billplz sandbox vs www from environment

**Track:** Billplz · **Depends:** B11, S12  
**Analysis:** [00](../00-what-must-be-done.md) §5.2; Hub `BillplzPublicBase.IsProductionApi`  
**IDs:** —  
**Goal:** `test` → sandbox API. `live` → www. Do not infer from `lazuar.com`.

---

## B12.1

- [ ] `test` → `https://www.billplz-sandbox.com/api/v3/`
- [ ] `live` → `https://www.billplz.com/api/v3/`
- [ ] POST bills to `{host}bills` (Hub concatenated `endpoint + "bills"`)
- [ ] Do not use `Contains("lazuar.com")` to pick live

## B12.2 Exit

- [ ] Host selection unit-testable
- [ ] Unblocked for B13
