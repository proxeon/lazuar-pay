# U13 — Billplz fields

**Track:** Merchant UI · **Depends:** U10, B11  
**Analysis:** [00](../00-what-must-be-done.md) §5.2 / §6.1  
**IDs:** —  
**Goal:** Secret, Collection ID, X-Signature secret, test|live.

---

## U13.1

- [x] API secret
- [x] Collection ID
- [x] X-Signature secret
- [x] Environment select `test` | `live`
- [x] Copy: callback must be public https; localhost will fail (B15/B29)
- [x] Webhook URL hint: `/v1/webhooks/billplz/{orgId}`

## U13.2 Exit

- [x] Fields match B11
