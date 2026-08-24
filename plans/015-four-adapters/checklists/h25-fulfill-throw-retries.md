# H25 — Fulfill throw rolls back the event id

**Track:** Harden · **Depends:** H12  
**Analysis:** [00](../00-what-must-be-done.md) §3.3; [014/08](../../014-evals/08-webhooks-secrets-fulfillment.md)  
**IDs:** NP-GW-006  
**Goal:** Stripe retry can still pay if the first attempt died mid-fulfill.

---

## H25.1 Behaviour

- [x] If fulfill throws (DB blip, missing required row), the transaction **aborts**
- [x] `psp_webhook_events` row for that event_id is **not** committed
- [x] Handler returns **5xx** (so Stripe retries) — not 200 `{ duplicate: true }`
- [x] Second delivery can insert + fulfill

## H25.2 Test (best effort)

- [x] If you can inject a failing fulfill (test double): first POST 5xx, no document; second POST 200 + one `RCPT-`
- [x] If not injectable without a seam, document that H12 one-TX is the proof and skip a fake 5xx — do **not** fake it by SaveChanges-then-throw (that is the old bug)

## H25.3 Must not

- [x] Do not 200 duplicate after a failed fulfill
- [x] Do not add a repair worker as a substitute for rollback

## H25.4 Exit

- [x] Code path has one TX
- [x] Unblocked for C19 (CHIP uses the same handler TX)
