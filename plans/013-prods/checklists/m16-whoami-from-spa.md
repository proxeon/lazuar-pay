# M16 — Whoami from SPA

**Track:** Merchant · **Depends:** M15  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** After a user is present, Pay `GET /v1/whoami` is the session door.

---

## M16.1 Call

- [ ] After user present, `GET {VITE_PAY_API_URL}/v1/whoami`
- [ ] `Authorization: Bearer` + `pickApiBearerToken` (JWT `access_token` only)
- [ ] 401 → sign in again (`signinRedirect`), do not invent a Pay login

## M16.2 Do not hammer

- [ ] Once per load / identity refresh / org switch — not per table row, not a 2s poll
- [ ] Health stays anonymous; do not replace `/health` with whoami on an interval

## M16.3 Projection door

- [ ] Do **not** call One `GET /me` from the SPA **as** the Pay projection
- [ ] Pay whoami is the door (host already forwards to One)
- [ ] Optional: SPA may also call One later for tenants create (M19) — not this phase

## M16.4 Exit

- [ ] Authenticated shell can show whoami JSON (or a user/org chrome stub)
- [ ] Unblocked for M17
