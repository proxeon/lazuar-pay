# W12 — HMAC over `{unix}.{body}`

**Track:** One HMAC · **Depends:** W11  
**Analysis:** One `ComputeHeaderValue`  
**IDs:** —  
**Goal:** Body-only HMAC is the 014 dialect. Kill it.

---

## W12.1

- [ ] Signed payload = `$"{timestamp}.{body}"` with the **raw** request body string (same bytes One signed)
- [ ] HMAC-SHA256 UTF-8 secret, UTF-8 payload
- [ ] Do not JSON-minify / re-serialize before verify

## W12.2 Must not

- [ ] Do not HMAC body alone
- [ ] Do not HMAC pretty-printed JSON

## W12.3 Exit

- [ ] W23 uses `{unix}.{body}`
- [ ] Unblocked for W13
