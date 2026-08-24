# fr11 — Razorpay bad signature is 400

**Track:** Fill Razorpay · **Depends:** S16  
**Analysis:** 09 method 45; R16  
**Goal:** `RailTests.Razorpay_bad_signature_is_400`

---

- [ ] Valid captured JSON, `X-Razorpay-Signature: deadbeef`
- [ ] 400, zero documents
- [ ] Exit: green
