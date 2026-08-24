# R15 — Discard SetupFutureUsage

**Track:** Razorpay · **Depends:** R13  
**Analysis:** Hub discards it and always mints a payment link  
**IDs:** NP-GW-007  
**Goal:** No registration / mandate links.

---

## R15.1

- [ ] Do not send `max_amount` 10× tricks or card-registration payloads
- [ ] Always payment link for hosted_link

## R15.2 Exit

- [ ] Payload is a payment link
