# B19 — HMAC with-extra then without-extra

**Track:** Billplz · **Depends:** B18  
**Analysis:** Hub `ExtraFields` = `paid_at`, `transaction_id`, `transaction_status`; `AlwaysExclude` = `x_signature`  
**IDs:** NP-GW-004  
**Goal:** Billplz dashboard versions disagree on extra fields. Hub tries both. Steal that.

---

## B19.1

- [ ] First compute HMAC including extra fields (except `x_signature`)
- [ ] If mismatch, recompute **excluding** `paid_at`, `transaction_id`, `transaction_status`
- [ ] If both mismatch → 400
- [ ] Always exclude `x_signature` from the source string

## B19.2 Test

- [ ] Fixture that only verifies with extra-excluded still 200
- [ ] Fixture that verifies with extra included still 200
- [ ] Wrong secret both ways → 400

## B19.3 Exit

- [ ] Tests green
