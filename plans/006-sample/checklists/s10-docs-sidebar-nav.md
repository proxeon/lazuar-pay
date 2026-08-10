# S10 — Docs sidebar & nav (IA shell)

**Track:** Docs IA · **Analysis:** `../08-docs-information-architecture.md`  
**Depends on:** S00  
**Goal:** Wire VitePress navigation **before** or **with** empty stubs; avoid orphan pages.

---

## S10.1 Config edit

- [x] Open `apps/lazuar-docs/docs/.vitepress/config.ts`
- [x] **Start** group: keep Introduction, Product lines, Concepts
- [x] Add under Start (or Architecture): **Architecture: who does what** → `/guide/architecture-who-does-what`
- [x] **Integrations** order:
  - [x] Overview
  - [ ] **Payment flow** → `/integrations/payment-flow` (deferred — S21; Option A: no stub)
  - [x] Payments cashier
  - [x] **Hub vs DIY** after cashier (S12)
  - [x] Provision → Create checkout → Webhooks → API keys → Environments
  - [ ] **Run sample app** → `/integrations/run-sample-app` (only if page exists **or** stub with draft status — see S10.3) (deferred — S50)
  - [x] Second-app checklist
  - [x] Aura reference (after second-app)
- [x] Optional top nav: do **not** add Sample until S50 is real (prefer sidebar only)

## S10.2 Stub policy (pick one and stick)

- [x] **Option A (recommended):** Add sidebar links only when page markdown exists in same PR as S11/S21/S50
- [ ] **Option B:** Create stub pages with H1 + “Status: draft — content in S11/S21/S50” so nav never 404s

## S10.3 Verify

- [ ] `pnpm --filter lazuar-docs dev` — every new sidebar item loads (or stub) (optional; build used)
- [x] `pnpm --filter lazuar-docs build` green
- [x] No broken internal links introduced

## S10.4 Exit

- [x] Config committed; IA order matches checklist README track map *(implementer: no git commit requested — config on branch)*
- [x] No “Run sample app” link to empty path without stub
