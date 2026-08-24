# K12 — No GrabPay / TnG / FPX tiles on checkout

**Track:** Checkout UI · **Depends:** X20  
**Analysis:** [00](../00-what-must-be-done.md) §6.2  
**IDs:** NP-GW-007  
**Goal:** Hosted_link pixel. Processor draws wallets.

---

## K12.1

- [ ] Grep `lazuar-pay-checkout/src` for grab, tng, touch, boost, duitnow, fpx, shopee — none as buttons
- [ ] No card PAN (K17)
- [ ] `locks.test.ts` may add those strings as forbidden

## K12.2 Exit

- [ ] Grep / lock test
