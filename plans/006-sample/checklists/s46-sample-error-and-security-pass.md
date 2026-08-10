# S46 — Sample error & security pass

**Track:** Sample app · **Analysis:** `../04`, `../05`, `../03` risks  
**Depends on:** S42–S45  
**Goal:** Hardening pass before runbook claims green.

---

## S46.1 Security grep

- [x] No `NEXT_PUBLIC_` containing sk_ or whsec
- [x] No Billplz/Stripe SDK dependencies in package.json
- [x] No processor secret env vars required for runtime pay path
- [x] Webhook route does not log full secrets or full sk_
- [x] Server-only modules for hub client + verify

## S46.2 Body integrity

- [x] No middleware rewrites webhook body
- [x] No JSON.stringify re-serialize before verify
- [x] Edge runtime not used for webhook

## S46.3 Product teaching

- [x] Success page copy still warns not to trust redirect
- [x] README security checklist present
- [x] Sample badge “not production” visible

## S46.4 Error paths smoke

- [x] Missing env secrets fail-fast with clear message on checkout route
- [x] Invalid amount rejected before or by Hub with readable UI error

## S46.5 Exit

- [x] Ready for S50 docs and S53 e2e evidence
