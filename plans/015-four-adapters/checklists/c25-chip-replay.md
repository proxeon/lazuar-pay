# C25 — CHIP replay no second RCPT-

**Track:** CHIP · **Depends:** C19, C20, H12  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** NP-GW-006  
**Goal:** Same as Stripe `WebhookTests` replay.

---

## C25.1

- [ ] POST signed `purchase.paid` twice
- [ ] First: 200, one document
- [ ] Second: 200 `{ duplicate: true }`, still one document, debit==credit unchanged

## C25.2 Exit

- [ ] Test green
