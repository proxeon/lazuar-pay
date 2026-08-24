# B26 — Billplz start requires email

**Track:** Billplz · **Depends:** P19, P20  
**Analysis:** Hub `TryResolveEmail`  
**IDs:** NP-BUY-001  
**Goal:** Collection bills need a buyer email.

---

## B26.1

- [ ] Missing email → 400
- [ ] Placeholder email → 400
- [ ] Name: Hub used local-part of email — acceptable fallback if name blank

## B26.2 Exit

- [ ] Test on start
