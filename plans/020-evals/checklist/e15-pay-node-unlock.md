# E15 — Sample unlocks a toy row

**Track:** E · **Depends:** E14  
**Goal:** Prove second-app fulfillment is **the app’s** row, not One membership.

**Why:** 011/012: buyers are not Zitadel humans. If the sample `POST /members` on One, we re-taught the Hub lie.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Pay’s paid row |
| IsolationTests | No Pay members table |
| E14 | Verified event |

**Current (`6d730d15`):** N/A.

---

## E15.1

- [ ] In-memory map `checkout_id → unlocked`
- [ ] On verified `payment.completed`, set unlocked
- [ ] GET `/unlocked/:checkoutId` for demo
- [ ] Do not call One `POST /members`

## E15.2 Exit

- [ ] Unblocked for E16
