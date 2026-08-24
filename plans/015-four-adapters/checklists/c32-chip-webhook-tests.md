# C32 — CHIP hermetic webhook bundle

**Track:** CHIP · **Depends:** C19–C27  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-GW-003  
**Goal:** One test class (or file) a later implementer can clone for Billplz.

---

## C32.1 Must exist

- [x] Empty body 400 (C26)
- [x] Bad / missing RSA 400 (C27)
- [x] `purchase.paid` → `RCPT-` + replay duplicate (C19, C25)
- [x] `purchase.preauthorized` no pay (C21)
- [x] Missing currency no pay (C24)
- [x] Cross-org bind (H13) still holds for chip path

## C32.2 Exit

- [x] `task pay:test` green without network
- [x] NP-GW-003 **may** flip when a human also dogfoods CHIP (A99). Tests alone do not close B99
- [x] Unblocked for B10 / U12
