# U12 — CHIP fields: Bearer, Brand ID, PEM

**Track:** Merchant UI · **Depends:** U10, C11  
**Analysis:** [00](../00-what-must-be-done.md) §5.1 / §6.1  
**IDs:** —  
**Goal:** Dashboard paste. No registrar.

---

## U12.1

- [x] Secret key (Bearer)
- [x] Brand ID (`public_merchant_id`)
- [x] Webhook public key PEM (textarea)
- [x] Copy: paste PEM from CHIP dashboard; Pay does not auto-register webhooks
- [x] Webhook URL to copy: `https://{public}/v1/webhooks/chip/{orgId}` (use `Pay` origin, not Hub)

## U12.2 Exit

- [x] Three fields + URL hint
