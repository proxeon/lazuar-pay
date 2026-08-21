# F22 — Webhook replay no-ops

**Track:** Fulfillment · **Depends:** F13, G21  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**Goal:** Second identical `event_id` → HTTP 200, journal line count unchanged, one `RCPT-`.

---

## F22.1 Behaviour

- [ ] Replay of the same `(org_id, provider, event_id)` after commit: **HTTP 200**
- [ ] Journal line count unchanged
- [ ] Exactly one `RCPT-` (sequence does not increment again)
- [ ] One ACTIVE (or one paid one-off), not two

## F22.2 Test (required)

- [ ] Hermetic: POST twice with the same `event_id`
- [ ] Assert status 200, line count stable, receipt count = 1
- [ ] Fake PSP; no live Stripe/CHIP network
- [ ] Runs under `task pay:test`

## F22.3 Must not

- [ ] Do not return 500 after commit (PSP would retry; unique keys still no-op, but 200 is the contract)
- [ ] Do not allocate a second number because PDF failed (number is in the TX)

## F22.4 Exit

- [ ] Replay test green
