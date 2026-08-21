# P10 — Pay SPA / OIDC (parked)

**Do not start until C99.**  
**Analysis:** [02](../02-one-authn-tokens.md), 011/03 steps 1–2  
**Not part of connected.** Needs a Pay **browser origin**.

---

## P10.1 When this becomes real

- [x] Pay UI origins exist: `lazuar-pay-merchant` `:5178` and `lazuar-pay-checkout` `:5179`. Not Hub `:3003` / `:3004` pointed at 8081. OIDC still unwired.

## P10.2 One-side (API already exists; prefer Pay calling it)

- [ ] Register OIDC SPA via One `POST /tenants/{id}/apps` (or seed script in **One** only if Pay cannot self-register)
- [ ] Redirects on that app + login `REDIRECT_ALLOWLIST`
- [ ] PKCE; **access_token** as Bearer to Pay and One
- [ ] Login host `:5175`; not `:3005` product path; not `:5173`

## P10.3 Must not

- [ ] Password form in Pay
- [ ] `id_token` as Bearer
- [ ] Console-only client_id with no One app object
- [ ] Mix this PR with C13 whoami

## P10.4 One repo?

- [ ] Only if a **seed script** for `lazuar-pay` SPA is wanted as convenience — not required if Pay uses `POST …/apps`
- [ ] No One product feature for this
