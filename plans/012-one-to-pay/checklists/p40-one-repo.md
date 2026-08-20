# P40 — Changes in `lazuar-one` (parked / rare)

**C-phases: zero One product PRs.**  
**Analysis:** conversation lock + [02](../02-one-authn-tokens.md)

---

## P40.1 Not required for connected

- [ ] `GET /me` already exists
- [ ] `authz/check` already exists
- [ ] Pay HttpClient does not need One CORS

## P40.2 Allowed later (config / seed, not a new IdP)

- [ ] Optional seed of a Pay OIDC app (P10) — prefer `POST …/apps` from Pay
- [ ] Redirect / CORS allowlist when a Pay **browser** origin exists
- [ ] Staff VIEWER as a **One membership role** — only if product wants NP-ONE-021 in One; until then Pay enforces money-route roles itself (C24)

## P40.3 Forbidden “for Pay”

- [ ] Checkout / ledger / receipts in One
- [ ] Pay routes on One TypeSpec
- [ ] FGA `payment` / `document` without a written Pay check call
- [ ] Giving Pay a Zitadel PAT or OpenFGA admin token
- [ ] Holding whoami on One staging-proof / SMTP / npm publish
