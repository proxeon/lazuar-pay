# fb25 — Billplz missing currency after D15

**Track:** Fill Billplz · **Depends:** D15  
**Goal:** `RailTests.Billplz_missing_currency_does_not_pay` **if** D15 refuses omitted currency

---

- [ ] Paid HMAC form **without** currency field (happy path must include whatever key D15 reads)
- [ ] 400 `missing currency`, zero documents
- [ ] If Billplz never sends currency and A00 documents “always require form currency on fixtures only,” still 400 when omitted
- [ ] Exit: green or A00 note + this method still 400
