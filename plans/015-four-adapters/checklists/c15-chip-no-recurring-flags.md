# C15 — No force_recurring / skip_capture in this program

**Track:** CHIP · **Depends:** C12  
**Analysis:** [00](../00-what-must-be-done.md) §5.1; parked-offsession  
**IDs:** NP-GW-007  
**Goal:** Hosted_link only. Hub used those flags for vault. We do not.

---

## C15.1

- [x] Create payload must **not** contain `force_recurring`
- [x] Must **not** contain `skip_capture`
- [x] Do not send `$0` purchases to vault a card
- [x] C17 mock asserts absence of those keys

## C15.2 Must not

- [x] Do not copy Hub `if (setupFutureUsage)` block

## C15.3 Exit

- [x] Payload grep/test
- [x] Unblocked for parked-offsession to stay parked
