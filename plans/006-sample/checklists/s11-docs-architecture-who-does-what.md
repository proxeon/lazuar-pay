# S11 — Architecture: who does what (matrices page)

**Track:** Docs IA · **Analysis:** `../02-responsibility-matrices.md`  
**Depends on:** S00; S10 nav entry same PR or already present  
**Goal:** One page with M1–M7 (or M1–M5+M7 full; M6 Billplz/Stripe).

---

## S11.1 Create page

- [x] Create `apps/lazuar-docs/docs/guide/architecture-who-does-what.md`
- [x] H1: `Architecture: who does what`
- [x] Audience blurb: Payments M2M only; domain stays in app
- [x] Status: draft

## S11.2 Actors section

- [x] Define App / Hub / Gateway / Human OrgAdmin / Guest (table from analysis 02 §0)

## S11.3 Matrices (paste from analysis 02 — full tables)

- [x] **M1** Create payment (checkout) — step rows App/Hub/Gateway
- [x] **M2** Webhook path — hop 1 inbound (gateway→Hub) + hop 2 outbound (Hub→app)
- [x] **M3** Secrets & credentials (sk_, whsec_, provision secret, BYOK vault)
- [x] **M4** Multi-tenant BYOK (workspace isolation)
- [x] **M5** Errors & recovery (`PAYMENTS_NOT_CONFIGURED`, hops, retries)
- [x] **M6** Billplz vs Stripe vs Hub quirk columns (metadata, signatures)
- [x] **M7** Anti-patterns (success_url unlock, DIY processor verify, keys in browser, …)

## S11.4 Teaching callouts

- [x] App verifies **Hub** signatures, not Billplz/Stripe
- [x] Hub is **not** MoR for guest GMV under BYOK
- [x] Placeholders only: `sk_test_…`, `whsec_…` — no real secrets

## S11.5 Related links

- [x] Link: product-lines, concepts, payment-flow (if exists), payments-cashier, webhooks, api-keys, second-app-checklist
- [x] Placeholder for run-sample-app (if not yet): “coming in S50” or omit

## S11.6 Exit

- [x] Sidebar entry works
- [x] `pnpm --filter lazuar-docs build` green
- [x] Matrices not only one-liners (full step lists for M1–M2 at minimum)
