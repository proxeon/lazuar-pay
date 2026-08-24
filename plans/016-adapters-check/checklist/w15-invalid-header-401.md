# W15 — Missing / garbage signature header is 401

**Track:** One HMAC · **Depends:** W11  
**Analysis:** 014 P0-4; no tests today  
**IDs:** —  
**Goal:** Unsigned Plane A is not a pause switch.

---

## W15.1

- [ ] Missing `X-Lazuar-Signature` → 401
- [ ] Empty / whitespace → 401
- [ ] `deadbeef` / `v1=nope` without `t=` → 401
- [ ] Do not insert `OneWebhookEvents` on 401

## W15.2 Exit

- [ ] `OneWebhookTests.Missing_signature_is_401`
- [ ] Unblocked for W16
