# 006 Sample — detailed implementation checklists

**Status:** S00 frozen · ready for S10 / S20 / S30  
**Date:** 2026-08-10  
**Style:** Many **small** phase files (not fat catch-alls). One phase ≈ one PR (or a tightly scoped commit).  
**How-to analyses:** parent `../01-…`–`../10-…`  
**Freeze artifact:** [`../wave-decisions.md`](../wave-decisions.md)  
**Program idea:** diagrams + responsibility matrices in `lazuar-docs` + Next.js sample under `examples/` proving Hub multi-app cashier without Aura.

## Rule: not one mega-PR

| Do | Don’t |
|----|--------|
| One phase intent per PR | Land docs IA + full sample + e2e in one tip |
| Parallel **tracks** after S00 | Parallel **inside** sample (webhook before order model) |
| Freeze path/port/envelope first | Invent envelope shape that disagrees with runtime |
| Keep Payments M2M only | Smuggle Commerce / LHDN / Paddle into sample |

## Locked decisions (from S00 — reaffirm when executing)

**Full freeze:** [`../wave-decisions.md`](../wave-decisions.md) · checklist [`s00-align-freeze.md`](./s00-align-freeze.md) **complete**

| Topic | Lock |
|-------|------|
| Surface | **Payments M2M cashier only** (not Commerce, LHDN, Paddle) |
| Sample `external_product` | **not** `aura` (e.g. `demo-app` / `sample-shop`) |
| Fulfillment | signed Hub webhook only (never `success_url` alone) |
| Sample path | `examples/hub-cashier-next` |
| Package name | `@examples/hub-cashier-next` |
| Hub API base in docs/sample | `http://localhost:8080/api/v1` (**8080**, not 8090) |
| Sample port | **3020** (CORS already lists it; avoid 3002–3005 product apps) |
| Docs site port | **5180** |
| Webhook payload | **Envelope + `data`** (runtime), not flat TypeSpec alone |
| HTTP client | plain `fetch` — no `@repo/api-types-ts` |
| Dockerfile / GHCR | **none** |
| CI product turbo build sample | **no** (filter exclude) |
| Diagram primary format | Mermaid preferred; ASCII fallback; prose summary always |

## Track map

```text
S00 Align & freeze
  │
  ├─ Track Docs IA / ownership ──── S10 → S11 → S12 → S13 → S14
  ├─ Track Docs diagrams ────────── S20 → S21 → S22 → S23 → S24 → S25
  ├─ Track Sample packaging ─────── S30 → S31
  ├─ Track Sample app ───────────── S40 → S41 → S42 → S43 → S44 → S45 → S46
  ├─ Track Runbook & proof ──────── S50 → S51 → S52 → S53
  └─ Track Polish ───────────────── S60 → S61
S99 Definition of done
```

**Parallel green-lights after S00**

| Band | Phases |
|------|--------|
| A | S10–S14 (docs ownership) ∥ S20–S21 (diagram plugin + flow page) ∥ S30 (workspace) |
| B | After S11: S12–S14; after S21: S22–S25; after S30: S31 → S40 |
| C | Sample S40–S46 serial inside track |
| D | S50+ after S42+S45 minimum (checkout + webhook working) |

## Phase index

### Program

| ID | File | Intent |
|----|------|--------|
| S00 | [`s00-align-freeze.md`](./s00-align-freeze.md) | Lock path, ports, envelope, non-goals |
| S99 | [`s99-definition-of-done.md`](./s99-definition-of-done.md) | Close 006 program honestly |

### Track Docs — IA & ownership

| ID | File | Intent |
|----|------|--------|
| S10 | [`s10-docs-sidebar-nav.md`](./s10-docs-sidebar-nav.md) | VitePress sidebar/nav only |
| S11 | [`s11-docs-architecture-who-does-what.md`](./s11-docs-architecture-who-does-what.md) | New ownership matrices page (M1–M7) |
| S12 | [`s12-docs-hub-vs-diy.md`](./s12-docs-hub-vs-diy.md) | Optional why-Hub vs DIY (no insecure tutorials) |
| S13 | [`s13-docs-homepage-and-cross-links.md`](./s13-docs-homepage-and-cross-links.md) | Home Start here + bidirectional links |
| S14 | [`s14-docs-dead-link-cleanup.md`](./s14-docs-dead-link-cleanup.md) | Remove/fix `script/second-app-proof.md` refs |

### Track Docs — flow diagrams

| ID | File | Intent |
|----|------|--------|
| S20 | [`s20-docs-mermaid-or-ascii-decision.md`](./s20-docs-mermaid-or-ascii-decision.md) | Enable Mermaid **or** commit ASCII-only |
| S21 | [`s21-docs-payment-flow-page.md`](./s21-docs-payment-flow-page.md) | Canonical payment-flow page (E2E SSoT) |
| S22 | [`s22-docs-diagrams-provision-checkout.md`](./s22-docs-diagrams-provision-checkout.md) | Diagrams on provision + create-checkout |
| S23 | [`s23-docs-diagrams-webhooks.md`](./s23-docs-diagrams-webhooks.md) | Hops + handler + fulfillment state |
| S24 | [`s24-docs-diagrams-product-env-second-app.md`](./s24-docs-diagrams-product-env-second-app.md) | Product-lines, environments, second-app |
| S25 | [`s25-docs-diagram-maintenance-policy.md`](./s25-docs-diagram-maintenance-policy.md) | how-to-maintain diagram rules |

### Track Sample packaging

| ID | File | Intent |
|----|------|--------|
| S30 | [`s30-sample-workspace-turbo.md`](./s30-sample-workspace-turbo.md) | pnpm `examples/*` + turbo exclude |
| S31 | [`s31-sample-scaffold-next.md`](./s31-sample-scaffold-next.md) | Next app shell, ports, package.json |

### Track Sample app

| ID | File | Intent |
|----|------|--------|
| S40 | [`s40-sample-env-and-readme.md`](./s40-sample-env-and-readme.md) | `.env.example` + app README |
| S41 | [`s41-sample-order-domain.md`](./s41-sample-order-domain.md) | Toy order model + store |
| S42 | [`s42-sample-checkout-route.md`](./s42-sample-checkout-route.md) | Hub create checkout + redirect |
| S43 | [`s43-sample-pay-ui-pages.md`](./s43-sample-pay-ui-pages.md) | Pay / success / cancel UI |
| S44 | [`s44-sample-webhook-verify-lib.md`](./s44-sample-webhook-verify-lib.md) | Signature helper + unit vector |
| S45 | [`s45-sample-webhook-route-fulfill.md`](./s45-sample-webhook-route-fulfill.md) | Route + idempotent unlock |
| S46 | [`s46-sample-error-and-security-pass.md`](./s46-sample-error-and-security-pass.md) | Error map, no secret leakage |

### Track Runbook & proof

| ID | File | Intent |
|----|------|--------|
| S50 | [`s50-docs-run-sample-app-page.md`](./s50-docs-run-sample-app-page.md) | VitePress run-sample-app guide |
| S51 | [`s51-harness-restore.md`](./s51-harness-restore.md) | Curl harness restore under plans/scripts |
| S52 | [`s52-second-app-checklist-update.md`](./s52-second-app-checklist-update.md) | Point checklist at sample |
| S53 | [`s53-manual-e2e-evidence.md`](./s53-manual-e2e-evidence.md) | Local e2e evidence template + one run |

### Track Polish

| ID | File | Intent |
|----|------|--------|
| S60 | [`s60-polish-root-readme-ports.md`](./s60-polish-root-readme-ports.md) | Root README / optional mprocs |
| S61 | [`s61-polish-docs-port-honesty.md`](./s61-polish-docs-port-honesty.md) | Fix remaining 8090 → 8080 drift |

## Analysis map

| Phases | Primary analyses |
|--------|------------------|
| S00 | `../README.md`, `../10-program-sequencing.md` |
| S10–S14 | `../02`, `../08`, `../09` |
| S20–S25 | `../01`, `../08` |
| S30–S31 | `../03`, `../07` |
| S40–S46 | `../03`, `../04`, `../05`, `../06` |
| S50–S53 | `../06`, `../10` |
| S60–S61, S99 | `../10` |

## Suggested first execution order

1. **S00** freeze  
2. Parallel: **S10** + **S20** + **S30**  
3. **S11** then **S12–S14**  
4. **S21** then **S22–S25**  
5. **S31** → **S40** → **S41** → **S42** → **S43** → **S44** → **S45** → **S46**  
6. **S50**–**S53**  
7. **S60**–**S61** → **S99**
