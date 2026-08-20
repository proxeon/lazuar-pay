# C15 — Health never calls One

**Track:** Whoami · **Depends:** C13  
**Analysis:** [03](../03-pay-host-seams.md) (no middleware on `/me`)  
**Goal:** Probes stay cheap and One-free.

---

## C15.1 Routes

- [x] `GET /health` still `{ status: "ok" }` without Authorization
- [x] `GET /v1/health` still `{ status: "ok" }` without Authorization
- [x] Neither handler uses `IHttpClientFactory` / One client

## C15.2 Middleware

- [x] No global “call `/me` on every request” middleware
- [x] If auth middleware exists, it **must skip** `/health` and `/v1/health`

## C15.3 Test

- [x] Existing HealthTests still pass
- [x] New test: health handlers succeed even when the One `HttpClient` is configured to throw if used (handler that fails on any send)

## C15.4 Exit

- [x] C15.3 test exists
- [x] Unblocked for C16
