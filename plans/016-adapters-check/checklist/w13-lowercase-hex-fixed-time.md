# W13 — Lowercase hex, fixed-time compare

**Track:** One HMAC · **Depends:** W12  
**Analysis:** One `Convert.ToHexString(hash).ToLowerInvariant()`  
**IDs:** —  
**Goal:** `Convert.ToHexString` without lower is uppercase. One emits lowercase.

---

## W13.1

- [ ] Expected `v1` is lowercase hex
- [ ] Compare with `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes of normalized hex (or on decoded bytes if you decode both)
- [ ] Length mismatch → false (do not throw)

## W13.2 Must not

- [ ] Do not `==` on strings
- [ ] Do not accept mixed-case by accident without normalizing **both** sides first

## W13.3 Exit

- [ ] W23 vector uses lowercase `v1`
