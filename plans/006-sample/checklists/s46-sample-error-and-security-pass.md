# S46 — Sample error & security pass

**Track:** Sample app · **Analysis:** `../04`, `../05`, `../03` risks  
**Depends on:** S42–S45  
**Goal:** Hardening pass before runbook claims green.

---

## S46.1 Security grep

- [ ] No `NEXT_PUBLIC_` containing sk_ or whsec
- [ ] No Billplz/Stripe SDK dependencies in package.json
- [ ] No processor secret env vars required for runtime pay path
- [ ] Webhook route does not log full secrets or full sk_
- [ ] Server-only modules for hub client + verify

## S46.2 Body integrity

- [ ] No middleware rewrites webhook body
- [ ] No JSON.stringify re-serialize before verify
- [ ] Edge runtime not used for webhook

## S46.3 Product teaching

- [ ] Success page copy still warns not to trust redirect
- [ ] README security checklist present
- [ ] Sample badge “not production” visible

## S46.4 Error paths smoke

- [ ] Missing env secrets fail-fast with clear message on checkout route
- [ ] Invalid amount rejected before or by Hub with readable UI error

## S46.5 Exit

- [ ] Ready for S50 docs and S53 e2e evidence
