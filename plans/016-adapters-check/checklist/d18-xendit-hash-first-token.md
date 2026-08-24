# D18 — Xendit callback token: hash first, then fixed-time

**Track:** Units · **Depends:** A00  
**Analysis:** [`../07-xendit-crosscheck.md`](../07-xendit-crosscheck.md) Hub 073; live length-check then FixedTimeEquals on raw tokens  
**IDs:** —  
**Goal:** Timing leak on token length. Shared-secret model still allows stolen-token PAID; hash-first matches Hub without changing Xendit’s protocol.

---

## D18.1 Live today

- [ ] UTF-8 bytes of provided vs expected; length mismatch fails fast

## D18.2 Change

- [ ] SHA-256 (or HMAC with a constant) both sides, then `FixedTimeEquals` on the digests (equal length)
- [ ] Empty/missing header still 400 invalid signature

## D18.3 Must not

- [ ] Do not log the token
- [ ] Do not change SETTLED-not-paid

## D18.4 Exit

- [ ] fx11 still 400 on wrong token
- [ ] Comment cites Hub 073 judgment
