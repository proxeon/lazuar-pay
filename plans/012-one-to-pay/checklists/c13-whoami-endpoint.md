# C13 — `GET /v1/whoami`

**Track:** Whoami · **Depends:** C12  
**Analysis:** [03](../03-pay-host-seams.md), [10](../10-dogfood-and-tests.md)  
**Goal:** First authenticated Pay door. Forwards Bearer to One `GET /me`.

---

## C13.1 Route

- [x] `GET /v1/whoami` on the focused host
- [x] **Not** `GET /v1/me`, **not** `GET /one/auth/me`, **not** `GET /api/v1/me`
- [x] Require `Authorization` header; if missing/blank → 401 (detail in C14)

## C13.2 Forward

- [x] HTTP to One: `GET {BaseUrl}/me` (if BaseUrl already includes `/api/v1`) or `GET {BaseUrl}/api/v1/me` — must match C10 lock
- [x] Copy `Authorization` verbatim
- [x] If request has `X-Lazuar-Tenant-Id`, forward as hint only (do not authorize from it)
- [x] One 200 + body → map via C12 → Pay 200 JSON
- [x] Do not persist users/tenants to a Pay database

## C13.3 What the handler must not do

- [x] No password lookup
- [x] No JWT signature validation of Zitadel keys in Pay (One already did; Pay trusts One’s 200/401)
- [x] No calling `/me` more than **once** per whoami request
- [x] No logging the full Bearer token

## C13.4 Exit

- [x] Happy path implemented
- [x] Error mapping may still be stubby if C14 is the next commit — **prefer C13+C14 same commit** if small
- [x] Unblocked for C14 (or C14 done in same tip)
