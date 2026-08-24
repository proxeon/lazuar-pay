# Q11 — pay-spec webhook honesty

**Track:** Q · **Depends:** P21  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-API-002  
**Goal:** Spec already has `POST /v1/webhooks/{provider}/{orgId}`. Document allowed provider names.

---

## Q11.1

- [x] Comment or enum: `stripe` | `chip` | `billplz` | `xendit` | `razorpay`
- [x] Keep Plane A `POST /v1/one/webhooks` separate
- [x] Do not import Hub 152-op catalog

## Q11.2 Exit

- [x] Spec comment matches P10
