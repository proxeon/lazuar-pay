# X19 — Xendit missing currency: do not default MYR

**Track:** Xendit · **Depends:** X15  
**Analysis:** Hub refuses missing invoice currency  
**IDs:** —  
**Goal:** Same as C24.

---

## X19.1

- [x] Missing `currency` → do not fulfill
- [x] Must match checkout currency
- [x] Do not invent MYR

## X19.2 Exit

- [x] Test fixture
