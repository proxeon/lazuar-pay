# W11 — Parse `t=` / `v1=` header

**Track:** One HMAC · **Depends:** W10  
**Analysis:** One `TryParseHeader`  
**IDs:** —  
**Goal:** Length-mismatch with a real One header is why live suspend never fires.

---

## W11.1 Live today

- [ ] Pay compares the **entire** header string to uppercase hex of HMAC(body)
- [ ] Real header `t=1710000000,v1=abc…` never matches a 64-char hex string

## W11.2 Change

- [ ] Split on comma, trim, parse `t=` as unix int and `v1=` as hex
- [ ] Missing `t` or `v1` → not verified (W15)

## W11.3 Must not

- [ ] Do not HMAC the header itself
- [ ] Do not require extra keys (`v0`, `h=`)

## W11.4 Exit

- [ ] Parser unit or W23 vector
- [ ] Unblocked for W12
