# Q15 — CORS still denies ops

**Track:** CI / isolation · **Depends:** K14  
**Analysis:** [10](../10-ci-observability-decommission.md) §8.4  
**Goal:** Pay CORS stays 5178+5179. Ops/portal stay out.

---

## Q15.1 CorsTests

- [ ] `http://localhost:5178` allowed
- [ ] `http://localhost:5179` allowed
- [ ] `http://localhost:3003` **denied** (ops)
- [ ] `http://localhost:3004` **denied** (portal) — add if missing
- [ ] Existing Health allow tests still pass

## Q15.2 Must not

- [ ] Do not “temporarily” add ops `:3003` to demo
- [ ] Do not add `:3004` / `:5173` / `:3005`
- [ ] Do not `AllowCredentials` for a Hub cookie

## Q15.3 Exit

- [ ] CorsTests cover 5178/5179 allow and 3003/3004 deny
- [ ] Q track complete; **unblocked for B99**
