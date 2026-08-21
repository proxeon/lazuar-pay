# K14 — CORS on `/v1/pay/*` for `:5179`

**Track:** Buyer page · **Depends:** K10  
**Analysis:** [05](../05-checkout-frontend.md) §3.7  
**Goal:** Checkout origin can GET/POST/OPTIONS public pay. Still deny Hub 3003/3004.

---

## K14.1 Allow

- [ ] `GET /v1/pay/{token}` from Origin `http://localhost:5179` → `Access-Control-Allow-Origin` that origin
- [ ] `POST /v1/pay/{token}/start` same
- [ ] `OPTIONS` preflight on `/v1/pay/*` succeeds for 5179 (`AllowAnyHeader` / `AllowAnyMethod` already the host shape)
- [ ] Keep `http://127.0.0.1:5179` twin if other Pay CORS twins exist

## K14.2 Deny (extend CorsTests)

- [ ] Origin `http://localhost:3003` (ops) → **no** ACAO on `/v1/pay/*`
- [ ] Origin `http://localhost:3004` (portal) → **no** ACAO — do **not** add 3004 “to dual-run”
- [ ] Existing `Health_does_not_allow_ops_origin` still passes

## K14.3 Must not

- [ ] No `AllowCredentials` to make Hub cookies work
- [ ] Do not add `:5179` to One CORS or login `REDIRECT_ALLOWLIST` (that is NP-CHK-007)

## K14.4 Exit

- [ ] CorsTests cover public pay GET/POST/OPTIONS + deny 3003 and 3004
- [ ] Unblocked for K15 browser fetch
