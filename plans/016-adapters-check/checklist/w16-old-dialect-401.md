# W16 — Body-only uppercase hex is 401

**Track:** One HMAC · **Depends:** W12  
**Analysis:** live Pay dialect; 014 P0-4  
**IDs:** —  
**Goal:** The old verifier must not still accept its own forgeries.

---

## W16.1

- [ ] Header = `Convert.ToHexString(HMACSHA256(secret, body))` (uppercase, no `t=`) → **401**
- [ ] This is the current production Pay compute — after W12 it must fail

## W16.2 Must not

- [ ] Do not keep a “compat” body-only path
- [ ] Do not accept both dialects

## W16.3 Exit

- [ ] `OneWebhookTests.Body_only_uppercase_hex_is_401`
- [ ] Unblocked for W23
