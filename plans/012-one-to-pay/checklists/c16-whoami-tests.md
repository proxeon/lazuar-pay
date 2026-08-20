# C16 — Hermetic whoami tests

**Track:** Whoami · **Depends:** C14, C15  
**Analysis:** [10](../10-dogfood-and-tests.md)  
**Goal:** `task pay:test` proves whoami without One/Zitadel.

---

## C16.1 Factory

- [ ] `WebApplicationFactory<Program>` replaces One `HttpMessageHandler`
- [ ] Fake inspects method/path: `GET` … `/me` (or `/api/v1/me` consistent with C10)
- [ ] Fake asserts incoming `Authorization` equals what the test sent

## C16.2 Cases (one test each)

- [ ] 200: One returns a fixture `/me` → Pay whoami maps `active_org_id` and `tenants[].id`
- [ ] 200: One returns empty `tenants` → Pay 200 with empty list
- [ ] 401: no Authorization → Pay 401; fake must **not** be called
- [ ] 401: One returns 401 → Pay 401
- [ ] 503: One handler throws / delays past timeout → Pay 503
- [ ] 503: One returns 500 → Pay 503
- [ ] Health: `/health` 200 while fake throws if called (may live in C15)

## C16.3 Hygiene

- [ ] Tests do not read live network
- [ ] Tests do not skip on “One not running”
- [ ] `task pay:test` runs them

## C16.4 Exit

- [ ] All C16.2 cases green
- [ ] Authz track (C20) unblocked
- [ ] Unblocked for C17
