# K19 — `success_url` is not paid (NP-CHK-002)

**Track:** Buyer page · **Depends:** K16  
**Analysis:** [05](../05-checkout-frontend.md) §4.4; `examples/hub-cashier-next` judgment  
**Goal:** Return URL is marketing / verifying. Webhook writes `paid`. Do not grant access on landing.  
**011:** NP-CHK-002

---

## K19.1 Copy

- [ ] After PSP return: wait for **payment confirmation** (verifying spinner)
- [ ] Poll public `GET /v1/pay/{token}` until `status=paid` **or** timeout
- [ ] Timeout: retry / “still confirming” — **not** “you’re in” / member / download unlocked
- [ ] Paid pixel only when public GET says `paid`

## K19.2 Contract

- [ ] `success_url` must **not** grant access (NP-FUL-002 is the Pay row, F11+)
- [ ] Query `?payment=success` / Stripe session id is **not** paid
- [ ] Steal `examples/hub-cashier-next` judgment: success_url is not paid — do not copy the app

## K19.3 Must not

- [ ] No Hub portal redirect that drops tokens
- [ ] No treating `mode=setup` / amount 0 as paid (NP-GW-008) if that session appears

## K19.4 Exit

- [ ] Verifying → paid | timeout pixels exist
- [ ] Do not re-flip NP-CHK-002 (already stored on fixture); this phase is honesty of the page
- [ ] Buyer track UI honesty unblocked for B99
