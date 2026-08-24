# H19 — Hermetic Stripe mode=setup is not paid

**Track:** Harden · **Depends:** H12, H15  
**Analysis:** [00](../00-what-must-be-done.md) §3.6; 011 fail lock “Setup session counted as paid”  
**IDs:** NP-GW-008  
**Goal:** The ignore branch is a test, not a comment. 013 G22.3 claimed this and did not ship the fixture.

---

## H19.1 Fixture

- [x] Signed `checkout.session.completed` JSON with `"mode": "setup"` and `amount_total` 0 or null
- [x] `client_reference_id` = an **open** checkout id with amount > 0 (trap: do not pay it)
- [x] Assert HTTP 200
- [x] Assert body contains `ignored` / `setup`
- [x] Assert `Documents.Count == 0` and checkout still `open`

## H19.2 Must not

- [x] Do not steal Hub `EventType: "PAYMENT_COMPLETED"` for setup
- [x] Do not create `mode=setup` in `StripeHosted` in this program (hosted_link only)

## H19.3 Exit

- [x] Test in `WebhookTests` (or sibling) green
- [x] Unblocked for H20
