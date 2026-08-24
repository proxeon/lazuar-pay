# B28 — Billplz hermetic tests

**Track:** Billplz · **Depends:** B15, B19–B22  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** —  
**Goal:** Clone C32 shape for form HMAC.

---

## B28.1 Must exist

- [ ] Empty body 400
- [ ] Bad HMAC 400
- [ ] Extra-fields HMAC variant (B19)
- [ ] paid form → `RCPT-` + replay
- [ ] unpaid ignore
- [ ] localhost PublicBaseUrl start 400/503 without network (B15)
- [ ] Mocked `POST …/bills` → redirect_url (when PublicBaseUrl is https public **or** test injects a stub after bypassing B15 with a fake https origin)

## B28.2 Test PublicBaseUrl

- [ ] Tests set `Pay:PublicBaseUrl=https://pay.test.example` so create can run against HttpMessageHandler without hitting B15

## B28.3 Exit

- [ ] `task pay:test` green
- [ ] Unblocked for B29, U13
