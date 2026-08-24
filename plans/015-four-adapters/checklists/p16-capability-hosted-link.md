# P16 — capability hosted_link for all five

**Track:** Provider door · **Depends:** P14  
**Analysis:** [00](../00-what-must-be-done.md) §1 / §5  
**IDs:** NP-GW-007  
**Goal:** JSON does not claim off-session or e-mandate.

---

## P16.1

- [ ] PUT and GET return `capability: "hosted_link"` for stripe, chip, billplz, xendit, razorpay
- [ ] Do not return `vaulted` / `off_session` / `emandate` in this program
- [ ] Merchant copy (U19) explains Billplz/Xendit/Razorpay = reminder + hosted page; CHIP vault is later

## P16.2 Must not

- [ ] Do not port `PaymentGatewayCapabilities.SupportsOffSession` as a live JSON flag that implies auto-debit works
- [ ] Do not set `SupportsEmandate` true

## P16.3 Exit

- [ ] Capability string locked
- [ ] Unblocked for U19
