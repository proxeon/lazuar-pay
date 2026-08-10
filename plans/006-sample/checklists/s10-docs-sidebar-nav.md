# S10 — Docs sidebar & nav (IA shell)

**Track:** Docs IA · **Analysis:** `../08-docs-information-architecture.md`  
**Depends on:** S00  
**Goal:** Wire VitePress navigation **before** or **with** empty stubs; avoid orphan pages.

---

## S10.1 Config edit

- [ ] Open `apps/lazuar-docs/docs/.vitepress/config.ts`
- [ ] **Start** group: keep Introduction, Product lines, Concepts
- [ ] Add under Start (or Architecture): **Architecture: who does what** → `/guide/architecture-who-does-what`
- [ ] **Integrations** order:
  - [ ] Overview
  - [ ] **Payment flow** → `/integrations/payment-flow`
  - [ ] Payments cashier
  - [ ] Provision → Create checkout → Webhooks → API keys → Environments
  - [ ] **Run sample app** → `/integrations/run-sample-app` (only if page exists **or** stub with draft status — see S10.3)
  - [ ] Second-app checklist
  - [ ] Aura reference
- [ ] Optional top nav: do **not** add Sample until S50 is real (prefer sidebar only)

## S10.2 Stub policy (pick one and stick)

- [ ] **Option A (recommended):** Add sidebar links only when page markdown exists in same PR as S11/S21/S50
- [ ] **Option B:** Create stub pages with H1 + “Status: draft — content in S11/S21/S50” so nav never 404s

## S10.3 Verify

- [ ] `pnpm --filter lazuar-docs dev` — every new sidebar item loads (or stub)
- [ ] `pnpm --filter lazuar-docs build` green
- [ ] No broken internal links introduced

## S10.4 Exit

- [ ] Config committed; IA order matches checklist README track map
- [ ] No “Run sample app” link to empty path without stub
