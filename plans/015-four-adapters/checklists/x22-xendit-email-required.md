# X22 — Xendit start requires email

**Track:** Xendit · **Depends:** P19, P20  
**Analysis:** Hub `TryResolveEmail` on GenerateCheckout  
**IDs:** NP-BUY-001  
**Goal:** Invoice needs a buyer email.

---

## X22.1

- [x] Missing / placeholder email → 400
- [x] Do not send `customer@example.com`

## X22.2 Exit

- [x] Test on start
