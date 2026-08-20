# C16 — Hermetic whoami tests

**Track:** Whoami · **Depends:** C14, C15  
**Analysis:** [10](../10-dogfood-and-tests.md)  
**Goal:** `task pay:test` proves whoami without One/Zitadel.

---

## C16.1 Factory

- [x] `WebApplicationFactory<Program>` replaces One `HttpMessageHandler`
- [x] Fake inspects method/path: `GET` … `/me` (or `/api/v1/me` consistent with C10)
- [x] Fake asserts incoming `Authorization` equals what the test sent

## C16.2 Cases (one test each)

- [x] 200: One returns a fixture `/me` → Pay whoami maps `active_org_id` and `tenants[].id`
- [x] 200: One returns empty `tenants` → Pay 200 with empty list
- [x] 401: no Authorization → Pay 401; fake must **not** be called
- [x] 401: One returns 401 → Pay 401
- [x] 503: One handler throws / delays past timeout → Pay 503
- [x] 503: One returns 500 → Pay 503
- [x] Health: `/health` 200 while fake throws if called (may live in C15)

## C16.3 Hygiene

- [x] Tests do not read live network
- [x] Tests do not skip on “One not running”
- [x] `task pay:test` runs them

## C16.4 Exit

- [x] All C16.2 cases green
- [x] Authz track (C20) unblocked
- [x] Unblocked for C17
