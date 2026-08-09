# R42 — Enqueue LHDN lifecycle into One dispatcher (A1)

**Track:** Webhooks · **Analysis:** `../02-lhdn-webhooks-one-dispatcher.md`  
**Depends on:** R40, R41 recommended  
**Do not:** Dual-fire fire-and-forget + One in production without explicit dual-delivery decision  
**Notes:** `../r42-notes.md`

---

## R42.1 Implement enqueue

- [x] On LHDN validated/invalid (etc.), publish `OutboundWebhookRequestedIntegrationEvent` (or chosen A1 shape)
- [x] Payload per R40 (envelope vs raw) — **data-only** snake_case JsonElement; One wraps envelope
- [x] Org/endpoint resolution matches One dispatcher expectations — `TargetUrl: null` fan-out; `EventType: invoice.valid|invoice.invalid`
- [x] Correlation ids — event `Id` (Guid v7) on integration event; One outbox uses its own delivery id

## R42.2 Optional dual-sign

- [x] If R40 requires dual-verify: implement dual headers / dual body rules — **skipped** (not easy / ops dual-sign window not set; no dual-fire)
- [x] Golden signature tests — **N/A** (dual-sign skipped)

## R42.3 Tests

- [x] Event published with correct type (`invoice.valid` / `invoice.invalid`) and data payload fields
- [x] No `IWebhookSenderService` on this path (handler deps = event bus + link service only)
- [x] Fan-out filters by EnabledEvents — **unchanged** One `OutboundWebhookEventHandlers` / existing `OutboundWebhookTests`
- [x] Dispatcher still delivers One platform events unchanged — no One handler code change

## R42.4 Exit

- [ ] Staging: LHDN event produces durable outbox delivery
