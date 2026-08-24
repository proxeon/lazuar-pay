# C21 — purchase.preauthorized is not paid

**Track:** CHIP · **Depends:** C19, H15, H19  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-GW-008  
**Goal:** Hub mapped preauthorized + token to `PAYMENT_COMPLETED`. Steal the vault extract later. **Do not steal the event name.**

---

## C21.1

- [ ] `event_type == purchase.preauthorized` → 200 `{ ignored: "preauthorized" }`
- [ ] Zero documents, checkout remains `open`
- [ ] Even if JSON contains `recurring_token` / `is_recurring_token`
- [ ] Do not call `FulfillPaidAsync`

## C21.2 Test

- [ ] Signed preauthorized fixture with a token field still does not mint `RCPT-`

## C21.3 Must not

- [ ] Do not copy Hub `ExtractVaultIds` into a paid path
- [ ] Off-session stays parked

## C21.4 Exit

- [ ] Test green
