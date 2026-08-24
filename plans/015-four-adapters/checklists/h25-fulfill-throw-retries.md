# H25 — Fulfill throw rolls back the event id

**Track:** Harden · **Depends:** H12  
**Analysis:** [00](../00-what-must-be-done.md) §3.3; [014/08](../../014-evals/08-webhooks-secrets-fulfillment.md)  
**IDs:** NP-GW-006  
**Goal:** Stripe retry can still pay if the first attempt died mid-fulfill.

---

## H25.1 Behaviour

- [ ] If fulfill throws (DB blip, missing required row), the transaction **aborts**
- [ ] `psp_webhook_events` row for that event_id is **not** committed
- [ ] Handler returns **5xx** (so Stripe retries) — not 200 `{ duplicate: true }`
- [ ] Second delivery can insert + fulfill

## H25.2 Test (best effort)

- [ ] If you can inject a failing fulfill (test double): first POST 5xx, no document; second POST 200 + one `RCPT-`
- [ ] If not injectable without a seam, document that H12 one-TX is the proof and skip a fake 5xx — do **not** fake it by SaveChanges-then-throw (that is the old bug)

## H25.3 Must not

- [ ] Do not 200 duplicate after a failed fulfill
- [ ] Do not add a repair worker as a substitute for rollback

## H25.4 Exit

- [ ] Code path has one TX
- [ ] Unblocked for C19 (CHIP uses the same handler TX)
