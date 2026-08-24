# X23 — Xendit hermetic tests

**Track:** Xendit · **Depends:** X12–X19  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-LAT-002 (partial until R25 too)  
**Goal:** Clone C32 for callback-token + invoice JSON.

---

## X23.1 Must exist

- [x] Empty body 400
- [x] Bad callback token 400
- [x] PAID → `RCPT-` + replay
- [x] SETTLED after PAID still one doc (X16)
- [x] EXPIRED ignore
- [x] Mocked create invoice → `redirect_url`
- [x] Missing email 400

## X23.2 Exit

- [x] `task pay:test` green
- [x] Unblocked for U14
