# U12 — CHIP fields: Bearer, Brand ID, PEM

**Track:** Merchant UI · **Depends:** U10, C11  
**Analysis:** [00](../00-what-must-be-done.md) §5.1 / §6.1  
**IDs:** —  
**Goal:** Dashboard paste. No registrar.

---

## U12.1

- [ ] Secret key (Bearer)
- [ ] Brand ID (`public_merchant_id`)
- [ ] Webhook public key PEM (textarea)
- [ ] Copy: paste PEM from CHIP dashboard; Pay does not auto-register webhooks
- [ ] Webhook URL to copy: `https://{public}/v1/webhooks/chip/{orgId}` (use `Pay` origin, not Hub)

## U12.2 Exit

- [ ] Three fields + URL hint
