# P16 — capability hosted_link for all five

**Track:** Provider door · **Depends:** P14  
**Analysis:** [00](../00-what-must-be-done.md) §1 / §5  
**IDs:** NP-GW-007  
**Goal:** JSON does not claim off-session or e-mandate.

---

## P16.1

- [x] PUT and GET return `capability: "hosted_link"` for stripe, chip, billplz, xendit, razorpay
- [x] Do not return `vaulted` / `off_session` / `emandate` in this program
- [x] Merchant copy (U19) explains Billplz/Xendit/Razorpay = reminder + hosted page; CHIP vault is later

## P16.2 Must not

- [x] Do not port `PaymentGatewayCapabilities.SupportsOffSession` as a live JSON flag that implies auto-debit works
- [x] Do not set `SupportsEmandate` true

## P16.3 Exit

- [x] Capability string locked
- [x] Unblocked for U19
