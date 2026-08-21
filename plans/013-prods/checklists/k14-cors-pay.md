# K14 — CORS on `/v1/pay/*` for `:5179`

**Track:** Buyer page · **Depends:** K10  
**Analysis:** [05](../05-checkout-frontend.md) §3.7  
**Goal:** Checkout origin can GET/POST/OPTIONS public pay. Still deny Hub 3003/3004.

---

## K14.1 Allow

- [x] `GET /v1/pay/{token}` from Origin `http://localhost:5179` → `Access-Control-Allow-Origin` that origin
- [x] `POST /v1/pay/{token}/start` same
- [x] `OPTIONS` preflight on `/v1/pay/*` succeeds for 5179 (`AllowAnyHeader` / `AllowAnyMethod` already the host shape)
- [x] Keep `http://127.0.0.1:5179` twin if other Pay CORS twins exist

## K14.2 Deny (extend CorsTests)

- [x] Origin `http://localhost:3003` (ops) → **no** ACAO on `/v1/pay/*`
- [x] Origin `http://localhost:3004` (portal) → **no** ACAO — do **not** add 3004 “to dual-run”
- [x] Existing `Health_does_not_allow_ops_origin` still passes

## K14.3 Must not

- [x] No `AllowCredentials` to make Hub cookies work
- [x] Do not add `:5179` to One CORS or login `REDIRECT_ALLOWLIST` (that is NP-CHK-007)

## K14.4 Exit

- [x] CorsTests cover public pay GET/POST/OPTIONS + deny 3003 and 3004
- [x] Unblocked for K15 browser fetch
