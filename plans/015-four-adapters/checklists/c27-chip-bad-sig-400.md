# C27 — CHIP bad RSA signature 400

**Track:** CHIP · **Depends:** C18  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-GW-004  
**Goal:** Invalid `X-Signature` is 400, not 500.

---

## C27.1

- [x] Valid JSON body, garbage `X-Signature` → 400
- [x] Missing header → 400
- [x] No `RCPT-`

## C27.2 Exit

- [x] Test green
