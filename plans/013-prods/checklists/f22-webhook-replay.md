# F22 — Webhook replay no-ops

**Track:** Fulfillment · **Depends:** F13, G21  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**Goal:** Second identical `event_id` → HTTP 200, journal line count unchanged, one `RCPT-`.

---

## F22.1 Behaviour

- [x] Replay of the same `(org_id, provider, event_id)` after commit: **HTTP 200**
- [x] Journal line count unchanged
- [x] Exactly one `RCPT-` (sequence does not increment again)
- [x] One ACTIVE (or one paid one-off), not two

## F22.2 Test (required)

- [x] Hermetic: POST twice with the same `event_id`
- [x] Assert status 200, line count stable, receipt count = 1
- [x] Fake PSP; no live Stripe/CHIP network
- [x] Runs under `task pay:test`

## F22.3 Must not

- [x] Do not return 500 after commit (PSP would retry; unique keys still no-op, but 200 is the contract)
- [x] Do not allocate a second number because PDF failed (number is in the TX)

## F22.4 Exit

- [x] Replay test green
