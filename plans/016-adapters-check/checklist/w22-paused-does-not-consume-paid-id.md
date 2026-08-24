# W22 — Paused paid webhook does not consume the paid event id

**Track:** One HMAC · **Depends:** W21  
**Analysis:** decisions.md Pause row  
**IDs:** —  
**Goal:** After unsuspend, PSP retry can still fulfill. Consuming `paid:{id}` would lose the cash.

---

## W22.1 Decision (frozen)

- [ ] Verify + parse as usual
- [ ] If checkout org is paused **and** parse is a paid (not ignored) event:
  - HTTP **409** or **403** (pick one; prefer **409** so Stripe/CHIP retry)
  - **Do not** insert `psp_webhook_events` for that paid `EventId`
  - **Do not** call fulfill (W21 is belt)

## W22.2 Ignored events while paused

- [ ] Setup / preauth / unpaid / SETTLED may still insert their **ignored** grain (they must never pay). Optional. If easier, skip insert too — do not pay either way

## W22.3 Must not

- [ ] Do not 200 `{ ok: true }`
- [ ] Do not 200 `{ duplicate: true }`
- [ ] Do not insert `paused:{eventId}` as a substitute if that blocks the real paid id — **do not consume the paid id**

## W22.4 Exit

- [ ] W24 asserts Documents 0, PspWebhookEvents 0 for that paid EventId, HTTP 409/403
- [ ] After SQL-clear pause, same payload 200 + one `RCPT-`
