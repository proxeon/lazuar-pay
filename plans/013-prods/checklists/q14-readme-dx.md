# Q14 — README dogfood DX

**Track:** CI / isolation · **Depends:** M26, K22  
**Analysis:** [10](../10-ci-observability-decommission.md) §6  
**Goal:** Getting started is Pay + One, not Hub `task dev`.

---

## Q14.1 Host README (`apps/lazuar-pay/README.md`)

- [x] Dogfood loop: `task pay:dev`, `task pay:merchant`, `task pay:checkout`, One on **8080**
- [x] Do **not** start with `task dev` / `task fe` / `pnpm dev`
- [x] Fingerprint One: `GET /api/v1/` `name=lazuar-one-api`
- [x] Hub `task dev` / compose `api` **off** while One owns 8080

## Q14.2 Root note (optional but preferred)

- [x] Root README (or a short pointer) does not claim `task dev` is Pay
- [x] P60 sentence stays: do not set ops/portal `VITE_API_URL` to 8081; new UIs are `:5178` / `:5179`

## Q14.3 Must not

- [x] Document `:3003` / `:3004` / `:3005` / `:5173` as merchant/checkout
- [x] Alias `task dev` → Hub “for old times”

## Q14.4 Exit

- [x] An engineer can start Bar B without Hub DX
- [x] Unblocked for Q15
