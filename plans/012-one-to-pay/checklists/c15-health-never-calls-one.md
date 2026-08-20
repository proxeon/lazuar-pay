# C15 — Health never calls One

**Track:** Whoami · **Depends:** C13  
**Analysis:** [03](../03-pay-host-seams.md) (no middleware on `/me`)  
**Goal:** Probes stay cheap and One-free.

---

## C15.1 Routes

- [ ] `GET /health` still `{ status: "ok" }` without Authorization
- [ ] `GET /v1/health` still `{ status: "ok" }` without Authorization
- [ ] Neither handler uses `IHttpClientFactory` / One client

## C15.2 Middleware

- [ ] No global “call `/me` on every request” middleware
- [ ] If auth middleware exists, it **must skip** `/health` and `/v1/health`

## C15.3 Test

- [ ] Existing HealthTests still pass
- [ ] New test: health handlers succeed even when the One `HttpClient` is configured to throw if used (handler that fails on any send)

## C15.4 Exit

- [ ] C15.3 test exists
- [ ] Unblocked for C16
