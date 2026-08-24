# fb12 — Billplz HMAC with extra fields paid

**Track:** Fill Billplz · **Depends:** S14  
**Analysis:** 09 method 21; B19; extras `paid_at`, `transaction_id`, `transaction_status`  
**Goal:** `RailTests.Billplz_hmac_with_extra_fields_paid`

---

- [ ] Form includes extras **and** `paid=true`. HMAC `ComputeHmac(..., excludeExtra: false)`
- [ ] Query `checkout_id`
- [ ] 200, one `RCPT-`
- [ ] Use production `BillplzWebhook.ComputeHmac` — do not copy Hub
- [ ] Exit: green
