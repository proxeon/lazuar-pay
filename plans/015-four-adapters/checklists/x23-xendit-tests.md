# X23 — Xendit hermetic tests

**Track:** Xendit · **Depends:** X12–X19  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-LAT-002 (partial until R25 too)  
**Goal:** Clone C32 for callback-token + invoice JSON.

---

## X23.1 Must exist

- [ ] Empty body 400
- [ ] Bad callback token 400
- [ ] PAID → `RCPT-` + replay
- [ ] SETTLED after PAID still one doc (X16)
- [ ] EXPIRED ignore
- [ ] Mocked create invoice → `redirect_url`
- [ ] Missing email 400

## X23.2 Exit

- [ ] `task pay:test` green
- [ ] Unblocked for U14
