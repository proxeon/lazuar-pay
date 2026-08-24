# R19 — Event id: header X-Razorpay-Event-Id or captured:{pay_}

**Track:** Razorpay · **Depends:** R17  
**Analysis:** Hub `ResolveEventId`; 008 collision residual  
**IDs:** NP-GW-006  
**Goal:** Never bare `pay_` as EventId.

---

## R19.1

- [ ] Prefer header `X-Razorpay-Event-Id` if non-empty
- [ ] Else `captured:{paymentId}` for paid, `failed:{paymentId}` for failed
- [ ] Missing both header and payment id → 400 unusable
- [ ] Do not use checkout id as EventId

## R19.2 Exit

- [ ] Covered by R17/R18 fixtures
