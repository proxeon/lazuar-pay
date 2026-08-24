# U21 — Show which rail is active

**Track:** Merchant UI · **Depends:** P14  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** Staff can see GET `provider` after save.

---

## U21.1

- [ ] After GET, show `Active: chip` (or stripe/…) and last4
- [ ] `configured: false` empty state
- [ ] Saving a different rail updates the label (P13)

## U21.2 Exit

- [ ] Label matches GET
