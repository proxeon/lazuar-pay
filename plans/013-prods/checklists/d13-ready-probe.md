# D13 — Ready probe (Postgres only)

**Track:** Database · **Depends:** D12  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** Liveness stays dumb. Ready 503s if Postgres is down. **Never One.**

---

## D13.1 Liveness (keep)

- [ ] `GET /health` still `{ status: "ok" }` without Authorization
- [ ] `GET /v1/health` still `{ status: "ok" }` without Authorization
- [ ] Neither queries One
- [ ] Neither queries Postgres (keep them dumb)

## D13.2 Ready (add)

- [ ] `GET /ready` — **503** if Pay Postgres is down / DSN unusable; **200** if `SELECT 1` (or equivalent) works
- [ ] Ready **must never** call One
- [ ] Do not add Hub `/health/ready` outbox-lag or `/health/metrics`
- [ ] Do not rate-limit `/health`

## D13.3 Tests / later Docker

- [ ] Existing HealthTests still pass, including `Health_does_not_call_one`
- [ ] New: ready does not call One (`ThrowOnSend` still 200/503 from Postgres only)
- [ ] Docker HEALTHCHECK later hits **8081** (`/health` or `/ready`), **never 8080** — do not implement the image here

## D13.4 Exit

- [ ] Health still skips One; ready is Postgres only
- [ ] Unblocked for D14 if not already started
