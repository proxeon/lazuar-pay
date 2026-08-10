# Wave decisions — sample + docs program 006

**Date:** 2026-08-10  
**Branch:** `chore/sample-006`  
**Phase:** S00 complete  
**Analysis:** `README.md`, `10-program-sequencing.md`, `checklists/s00-align-freeze.md`  
**How-to SSoT:** `plans/006-sample/01`–`10` (implementers use checklists for phase DoD)

---

## Track selection

| Track | In this wave? | Phases | Notes |
|-------|---------------|--------|-------|
| Program | YES | S00, S99 | Freeze + close-out |
| Docs IA / ownership | YES | S10–S14 | After S00 |
| Docs diagrams | YES | S20–S25 | After S00; S20 decides Mermaid plugin |
| Sample packaging | YES | S30–S31 | After S00 |
| Sample app | YES | S40–S46 | Serial after S31 |
| Runbook & proof | YES | S50–S53 | After checkout + webhook minimum |
| Polish | YES | S60–S61 | Ports honesty |

---

## Delivery

- Long-lived branch: `chore/sample-006`
- One phase ≈ one PR (or tightly scoped commit)
- No product code in S00
- 005 ops residuals (keys migrate, table drop clocks) **do not** block 006

---

## Locked decisions (S00 freeze)

### Product scope

| Topic | Lock |
|-------|------|
| Surface | **Payments M2M cashier only** — not Commerce, LHDN, or Paddle |
| Sample `external_product` default | **Not** `aura` — use `demo-app` / `sample-shop` / `hub-cashier-sample` |
| Fulfillment | **Signed Hub webhook only** — never unlock on `success_url` alone |
| Gateway secrets | Sample holds **no** Billplz/Stripe long-term secrets |

### Placement & packaging

| Topic | Lock |
|-------|------|
| Sample path | `examples/hub-cashier-next` |
| Package name | `@examples/hub-cashier-next` |
| Location | **Not** under product `apps/` |
| Dockerfile / GHCR | **none** |
| CI product turbo | Sample **excluded** (not required for product green) |

### Ports & base URLs

| Topic | Lock |
|-------|------|
| Hub API base (docs/sample) | `http://localhost:8080/api/v1` (**8080**) |
| Sample dev port | **3020** |
| Docs site port | **5180** |
| 8090 in existing guides | Drift only — fix in **S61**; do **not** re-lock 8090 |

### Contract honesty

| Topic | Lock |
|-------|------|
| Outbound webhook payload | **Envelope + `data`**: `{ id, event_type, created_at, data }` (runtime) |
| Payment fields | Under `data.*` (checkout_id, metadata, amount, …) |
| Signature header | `X-Lazuar-Signature: t=<unix>,v1=<hex>` over `{t}.{raw_body}` |
| HMAC secret | Full `whsec_…` string (**do not strip** prefix) |
| Client HTTP | plain `fetch` + local types — **no** `@repo/api-types-ts` |

### Diagrams

| Topic | Lock |
|-------|------|
| Primary format | **Mermaid preferred** if plugin enabled |
| Fallback | **ASCII** always acceptable; prose summary always |

---

## Ordered start list

1. **S00** (done) — this file + checklists aligned  
2. Parallel band A: **S10** ∥ **S20** ∥ **S30**  
3. After S11: S12–S14; after S21: S22–S25; after S30: **S31** → S40  
4. Sample serial: S40 → S41 → S42 → S43 → S44 → S45 → S46  
5. Runbook: S50–S53 (after S42+S45 minimum)  
6. Polish: S60–S61 → **S99**

---

## Serial rules (hard)

- No sample app work before S00 freeze (done)  
- S31 after S30 (workspace/turbo before scaffold)  
- Sample track S40–S46 serial (no webhook before order model / env)  
- S50+ after checkout + webhook working  
- Docs tracks may parallel sample packaging after S00  

---

## Supersedes analysis drift

Older analysis drafts may say sample port **3005** or package name without scope.  
**S00 lock wins:** port **3020**, package **`@examples/hub-cashier-next`**.  
Implementers follow **checklists/** + this file over unamended 01–10 prose where they conflict.

---

## Exit (S00)

- [x] Decisions recorded in this file  
- [x] `checklists/s00-align-freeze.md` complete  
- [x] `checklists/README.md` locked table matches  
- [x] Team unblocked for S10 / S20 / S30  
