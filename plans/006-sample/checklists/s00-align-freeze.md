# S00 — Align & freeze

**Track:** Program · **Analysis:** `../README.md`, `../10-program-sequencing.md`  
**Goal:** Lock decisions so docs and sample do not diverge.  
**No product code in this phase.**  
**Decisions artifact:** [`../wave-decisions.md`](../wave-decisions.md)

---

## S00.1 Product scope

- [x] Confirm surface is **Payments M2M cashier only** (not Commerce, LHDN, Paddle)
- [x] Confirm sample `external_product` default is **not** `aura` (e.g. `sample-shop` or `demo-app`)
- [x] Confirm fulfillment rule: **signed Hub webhook only** (never success_url alone)
- [x] Confirm sample holds **no** Billplz/Stripe long-term secrets

## S00.2 Placement & packaging

- [x] Sample path locked: `examples/hub-cashier-next`
- [x] Package name locked: `@examples/hub-cashier-next` (or documented alternate)
- [x] Sample **not** under product `apps/`
- [x] No Dockerfile / GHCR for sample
- [x] CI: sample **not** required for product turbo green

## S00.3 Ports & base URLs

- [x] Hub API base for docs/sample: `http://localhost:8080/api/v1` (**8080**)
- [x] Sample dev port: **3020**
- [x] Docs site port noted: **5180**
- [x] Note: some existing guides say 8090 — fix later in S61, do not re-lock 8090

## S00.4 Contract honesty

- [x] Outbound webhook payload is **envelope** `{ id, event_type, created_at, data }` (runtime)
- [x] Payment fields live under `data.*` (checkout_id, metadata, amount, …)
- [x] Signature: `X-Lazuar-Signature: t=<unix>,v1=<hex>` over `{t}.{raw_body}`
- [x] Secret for HMAC is full `whsec_…` string (**do not strip** prefix)
- [x] Client HTTP: plain `fetch` + local types (no `@repo/api-types-ts`)

## S00.5 Process

- [x] One phase ≈ one PR (confirm)
- [x] 005 ops residuals (keys migrate, table drop) **do not** block 006
- [x] Analysis folder `plans/006-sample/01–10` is how-to SSoT for implementers

## S00.6 Exit

- [x] This checklist complete or explicitly amended with signed change
- [x] Team unblocked for S10 / S20 / S30
