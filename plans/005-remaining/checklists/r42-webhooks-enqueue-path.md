# R42 — Enqueue LHDN lifecycle into One dispatcher (A1)

**Track:** Webhooks · **Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`  
**Depends on:** R40, R41 recommended  
**Do not:** Dual-fire fire-and-forget + One in production without explicit dual-delivery decision

---

## R42.1 Implement enqueue

- [ ] On LHDN validated/invalid (etc.), publish `OutboundWebhookRequestedIntegrationEvent` (or chosen A1 shape)
- [ ] Payload per R40 (envelope vs raw)
- [ ] Org/endpoint resolution matches One dispatcher expectations
- [ ] Correlation ids

## R42.2 Optional dual-sign

- [ ] If R40 requires dual-verify: implement dual headers / dual body rules
- [ ] Golden signature tests

## R42.3 Tests

- [ ] Event → outbox row(s)
- [ ] Fan-out filters by EnabledEvents
- [ ] Dispatcher still delivers One platform events unchanged

## R42.4 Exit

- [ ] Staging: LHDN event produces durable outbox delivery
