# H15 — Ignored events must not pay

**Track:** Harden · **Depends:** H12  
**Analysis:** [00](../00-what-must-be-done.md) §3.3  
**IDs:** NP-GW-008  
**Goal:** Unique grain is honest for setup / ignore vs paid.

---

## H15.1 Policy

- [ ] Events that will **never** fulfill (Stripe `mode=setup`, amount 0, unknown type): **200** `{ ignored: "…" }`
- [ ] Decide and document: either
  - (A) insert unique `(org, provider, event_id)` as ignored so Stripe retry no-ops, **or**
  - (B) do not insert, so a later “same id” cannot block a real paid event
- [ ] Stripe event ids are unique per event object — (A) is safe for Stripe. CHIP fail-then-pay **must** namespace (C20) so FAILED and PAID do not share a grain
- [ ] Do not fulfill on ignored path

## H15.2 Live Stripe handler

- [ ] Keep `mode=setup` / `AmountTotal` null or 0 → ignored (already in `WebhookEndpoints`)
- [ ] Do not map `setup_intent.succeeded` to paid (Hub `PAYMENT_COMPLETED` amount 0 — **refuse**)
- [ ] Do not listen `customer.subscription.*` as SoT

## H15.3 Exit

- [ ] Policy written in the handler comment (two sentences max)
- [ ] Unblocked for H19, H20
