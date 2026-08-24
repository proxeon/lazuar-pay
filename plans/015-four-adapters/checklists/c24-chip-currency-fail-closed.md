# C24 — CHIP missing currency: do not default MYR

**Track:** CHIP · **Depends:** C19  
**Analysis:** [00](../00-what-must-be-done.md) decisions; Hub `TryNormalizeCurrency`  
**IDs:** —  
**Goal:** Refuse inventing MYR.

---

## C24.1

- [ ] Read `purchase.currency`
- [ ] Missing / not 3-letter → do not fulfill; 400 unusable
- [ ] If present, must match checkout currency (H14) case-insensitive
- [ ] Do not `currency = "MYR"` as fallback

## C24.2 Exit

- [ ] Test fixture without currency does not mint `RCPT-`
