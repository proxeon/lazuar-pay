# X22 — Xendit start requires email

**Track:** Xendit · **Depends:** P19, P20  
**Analysis:** Hub `TryResolveEmail` on GenerateCheckout  
**IDs:** NP-BUY-001  
**Goal:** Invoice needs a buyer email.

---

## X22.1

- [ ] Missing / placeholder email → 400
- [ ] Do not send `customer@example.com`

## X22.2 Exit

- [ ] Test on start
