# C21 — purchase.preauthorized is not paid

**Track:** CHIP · **Depends:** C19, H15, H19  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-GW-008  
**Goal:** Hub mapped preauthorized + token to `PAYMENT_COMPLETED`. Steal the vault extract later. **Do not steal the event name.**

---

## C21.1

- [x] `event_type == purchase.preauthorized` → 200 `{ ignored: "preauthorized" }`
- [x] Zero documents, checkout remains `open`
- [x] Even if JSON contains `recurring_token` / `is_recurring_token`
- [x] Do not call `FulfillPaidAsync`

## C21.2 Test

- [x] Signed preauthorized fixture with a token field still does not mint `RCPT-`

## C21.3 Must not

- [x] Do not copy Hub `ExtractVaultIds` into a paid path
- [x] Off-session stays parked

## C21.4 Exit

- [x] Test green
