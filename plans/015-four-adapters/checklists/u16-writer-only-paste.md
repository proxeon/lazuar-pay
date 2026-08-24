# U16 — Hide paste unless owner/admin

**Track:** Merchant UI · **Depends:** U10, H18  
**Analysis:** [00](../00-what-must-be-done.md) §6.1  
**IDs:** NP-GW-009  
**Goal:** Live `canWriteMoney` already hides Stripe paste. Keep for all rails.

---

## U16.1

- [x] `canWriteMoney(role)` owner|admin
- [x] Member sees U17 metadata, not inputs
- [x] API still 403 if they curl (H18)

## U16.2 Exit

- [x] Chrome matches H18
