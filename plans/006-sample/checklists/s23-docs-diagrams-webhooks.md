# S23 — Diagrams: webhooks

**Track:** Docs diagrams · **Analysis:** `../01` SEQ-WEBHOOK-*, STM-FULFILLMENT; `../05` envelope  
**Depends on:** S20  
**Goal:** Make two hops + verify/fulfill impossible to miss.

---

## S23.1 Hops (`integrations/webhooks.md`)

- [ ] Diagram hop 1: Gateway → Hub inbound (processor public URL)
- [ ] Diagram hop 2: Hub → Your app outbound (signed POST)
- [ ] Explicit: browser checkout_url is **not** a hop

## S23.2 Handler sequence

- [ ] Raw body → parse signature → skew → HMAC → dedupe → unlock
- [ ] Status semantics: 2xx ACK, 401 bad sig, 422 mapping, 5xx retry
- [ ] Headers listed: X-Lazuar-Signature, Event, Delivery-Id, Webhook-Id

## S23.3 Envelope honesty

- [ ] Document runtime body shape: `{ id, event_type, created_at, data: { … } }`
- [ ] Note order correlation via `data.metadata.order_id` (not top-level order_id)
- [ ] Events: `payment.completed` / `payment.failed` (refunds maturing)

## S23.4 Fulfillment state

- [ ] App-owned: pending → unlocked on verified completed; replay no double credit
- [ ] Never unlock on success_url alone

## S23.5 Signature algorithm (prose + formula)

- [ ] `signed = "{t}." + raw_body`
- [ ] `v1 = hex_lower(HMAC-SHA256(full_whsec_secret, signed))`
- [ ] Full `whsec_` secret (prefix not stripped)
- [ ] Skew ~300s; constant-time compare

## S23.6 Exit

- [ ] Docs build green
- [ ] Links to architecture M2 + environments hops
