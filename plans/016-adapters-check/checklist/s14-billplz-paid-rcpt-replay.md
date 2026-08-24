# S14 — Strengthen Billplz paid: RCPT + replay; no localhost here

**Track:** Strengthen · **Depends:** A00  
**Analysis:** 09 §9.5; method name currently lies  
**IDs:** B28  
**Goal:** Edit `Billplz_paid_form_and_localhost_blocked`. Optionally rename to `Billplz_paid_form_sandbox_start` **if** you touch the method.

---

## S14.1 Add

- [ ] `Documents.Single().Number` starts with `RCPT-`
- [ ] Replay same form+HMAC → body `duplicate`, still one document

## S14.2 Must not

- [ ] Do **not** assert localhost in this method (fb15 is that test)
- [ ] Do not keep claiming B15 from this name after rename

## S14.3 Exit

- [ ] Green
- [ ] Unblocked for fb15
