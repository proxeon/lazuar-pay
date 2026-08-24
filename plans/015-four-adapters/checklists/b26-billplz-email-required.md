# B26 — Billplz start requires email

**Track:** Billplz · **Depends:** P19, P20  
**Analysis:** Hub `TryResolveEmail`  
**IDs:** NP-BUY-001  
**Goal:** Collection bills need a buyer email.

---

## B26.1

- [x] Missing email → 400
- [x] Placeholder email → 400
- [x] Name: Hub used local-part of email — acceptable fallback if name blank

## B26.2 Exit

- [x] Test on start
