# R25 — Razorpay hermetic tests

**Track:** Razorpay · **Depends:** R13–R21  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-LAT-002  
**Goal:** Clone C32 for HMAC JSON + payment.captured.

---

## R25.1 Must exist

- [ ] Empty body 400
- [ ] Bad signature 400
- [ ] `payment.captured` → `RCPT-` + replay
- [ ] `payment.failed` ignore
- [ ] Fixture with `tax` still two journal lines (R21)
- [ ] Mocked payment_links → `short_url`
- [ ] No `Razorpay.Api` in csproj (R14)

## R25.2 Exit

- [ ] `task pay:test` green
- [ ] NP-LAT-002 **may** flip when Xendit **and** Razorpay hosted_link exist and U19 labels reminder-only
- [ ] Unblocked for U15
