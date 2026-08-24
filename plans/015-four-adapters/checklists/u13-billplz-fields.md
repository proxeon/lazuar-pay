# U13 — Billplz fields

**Track:** Merchant UI · **Depends:** U10, B11  
**Analysis:** [00](../00-what-must-be-done.md) §5.2 / §6.1  
**IDs:** —  
**Goal:** Secret, Collection ID, X-Signature secret, test|live.

---

## U13.1

- [ ] API secret
- [ ] Collection ID
- [ ] X-Signature secret
- [ ] Environment select `test` | `live`
- [ ] Copy: callback must be public https; localhost will fail (B15/B29)
- [ ] Webhook URL hint: `/v1/webhooks/billplz/{orgId}`

## U13.2 Exit

- [ ] Fields match B11
