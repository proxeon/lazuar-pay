# fr16 — Razorpay EventId prefers header

**Track:** Fill Razorpay · **Depends:** J15  
**Analysis:** 09 method 50; R19  
**Goal:** `RailTests.Razorpay_event_id_prefers_header`

---

- [ ] Captured body pay_1, header `X-Razorpay-Event-Id: evt_header_1`
- [ ] After 200, `PspWebhookEvents` has `EventId == "evt_header_1"` not `pay_1` and not `captured:pay_1`
- [ ] Exit: green
