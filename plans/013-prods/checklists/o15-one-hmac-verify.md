# O15 — One HMAC verify

**Track:** One extras · **Depends:** O14  
**Analysis:** [08](../08-one-identity-production.md) §7.4  
**Goal:** Bad signature never applies an event.

---

## O15.1 Secret

- [ ] Pay env holds the One webhook secret (`whsec_…`) — not a Zitadel PAT
- [ ] Never `VITE_*`; never log the secret
- [ ] Verify HMAC on the **raw** body (do not re-serialize JSON first)

## O15.2 Fail closed

- [ ] Missing / bad HMAC → **401** or **403**
- [ ] Empty body → 4xx (do not 200)
- [ ] Do not apply `tenant.suspended` (or anything) on a failed verify

## O15.3 Tests (hermetic)

- [ ] Good signature → 2xx (handler may no-op unknown types)
- [ ] Bad signature → 401/403
- [ ] No live One / Zitadel in `task pay:test`

## O15.4 Must not

- [ ] Zitadel PAT as the HMAC key
- [ ] Dual-use G19 PSP secret / Stripe signing secret

## O15.5 Exit

- [ ] Hermetic verify tests green
- [ ] Unblocked for O16
