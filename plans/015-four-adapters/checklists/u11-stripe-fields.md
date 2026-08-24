# U11 — Stripe fields: sk_ + whsec_

**Track:** Merchant UI · **Depends:** U10, P12  
**Analysis:** [00](../00-what-must-be-done.md) §6.1  
**IDs:** NP-GW-001  
**Goal:** Stop training merchants that `sk_test_` is the only secret.

---

## U11.1

- [ ] Inputs: API key (`sk_test_` / `sk_live_`), webhook signing secret (`whsec_`)
- [ ] Labels say Dashboard **endpoint** signing secret, not the API key
- [ ] `autoComplete="off"`
- [ ] Save calls PUT `{ provider: "stripe", secret, webhook_secret }`

## U11.2 Exit

- [ ] Two fields visible for stripe
