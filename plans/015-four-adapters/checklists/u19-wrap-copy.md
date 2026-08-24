# U19 — Honest hosted_link / reminder copy

**Track:** Merchant UI · **Depends:** P16  
**Analysis:** [00](../00-what-must-be-done.md) §1 / §6.1  
**IDs:** NP-GW-007  
**Goal:** One sentence per rail. No silent debit.

---

## U19.1 Copy

- [ ] Stripe: hosted Checkout, cards on Stripe’s page, capability hosted_link
- [ ] CHIP: hosted CHIP page (FPX/wallets if enabled on the brand). Auto-debit later, not this program
- [ ] Billplz: reminder + hosted bill. We do not auto-debit
- [ ] Xendit: hosted invoice. Wallets on Xendit’s page. We do not auto-debit
- [ ] Razorpay: hosted payment link. Not e-mandate. We do not auto-debit
- [ ] All: receipt is Official Receipt, not an e-invoice (T18)

## U19.2 Exit

- [ ] Copy visible next to the field set
