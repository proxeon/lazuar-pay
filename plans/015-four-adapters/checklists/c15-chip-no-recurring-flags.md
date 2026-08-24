# C15 — No force_recurring / skip_capture in this program

**Track:** CHIP · **Depends:** C12  
**Analysis:** [00](../00-what-must-be-done.md) §5.1; parked-offsession  
**IDs:** NP-GW-007  
**Goal:** Hosted_link only. Hub used those flags for vault. We do not.

---

## C15.1

- [ ] Create payload must **not** contain `force_recurring`
- [ ] Must **not** contain `skip_capture`
- [ ] Do not send `$0` purchases to vault a card
- [ ] C17 mock asserts absence of those keys

## C15.2 Must not

- [ ] Do not copy Hub `if (setupFutureUsage)` block

## C15.3 Exit

- [ ] Payload grep/test
- [ ] Unblocked for parked-offsession to stay parked
