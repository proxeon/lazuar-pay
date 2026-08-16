# W1-LP-144 — Integration guides (how to integrate, not module dump)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 `LP-144`. Tracker: *Integration guides (not Scalar dump)* — Lazuar **P**.  
**Not this ID:** Event catalog SSoT (`LP-135` — VitePress `events.md`). Official Payments SDK (`LP-138`). Sample app already exists (`LP-140` **Y**). Do not delete Scalar.

**Invariant:** A new integrator can follow **task-shaped** VitePress pages (provision → key → checkout → verify webhook → unlock) without opening a module-shaped OpenAPI dump first. Scalar is **reference**, not onboarding.

---

## 0. Scope lock

In scope:

- VitePress `apps/lazuar-docs` as the how-to home
- Developers hub homepage information architecture
- One **Commerce hosted-link** how-to (today all “how to integrate” is M2M cashier)
- Un-draft / honesty pass on existing cashier guides

Out of scope:

- Rewriting every Scalar product (`/lhdn`, `/billing`, `/ops`)
- New SDK
- LP-135 catalog rewrite (link to it; don’t duplicate)
- Marketing pricing page (`LP-006`)

---

## 1. Verdict

Plan 006 already shipped a **real** VitePress how-to tree for **Payments M2M**. The cell is **P** because:

1. Home + Developers hub still **lead with module Scalar cards**.  
2. Guides are watermarked **Draft**.  
3. There is **no** “sell a Hub product link” guide (the merchant CaaS path).  
4. Some env copy is **stale** (Billplz “contains `lazuar.com`” — see LP-182).

| Surface | Job | Status |
|---------|-----|--------|
| `lazuar-docs/docs/integrations/*` | How to | **Y** for cashier; draft |
| `lazuar-docs/docs/guide/architecture-who-does-what.md` | Ownership | **Y** |
| `examples/hub-cashier-next` | Runnable | **Y** |
| `lazuar-developers` homepage | Start here + **6 Scalar products** | Module dump |
| `docs/payments-integration-quickstart.md` (repo root) | Engineering SSoT | Exists; not the public IA |

---

## 2. Current files

| Path | Role |
|------|------|
| `apps/lazuar-docs/docs/index.md` | Hero → cashier / who-does-what; **Status: drafts** |
| `apps/lazuar-docs/docs/integrations/index.md` | Numbered guide map (good) |
| `…/payment-flow.md`, `payments-cashier.md`, `create-checkout.md`, `webhooks.md`, `provision.md`, `api-keys.md`, `environments.md`, `run-sample-app.md`, `second-app-checklist.md`, `hub-vs-diy.md`, `aura-reference.md` | Task pages |
| `apps/lazuar-docs/docs/.vitepress/config.ts` | Sidebar is already task-shaped |
| `apps/lazuar-developers/app/page.tsx` | 4 guide cards + 6 OpenAPI products (Commerce/Billing/One/Ops/LHDN/Payments) |
| `apps/lazuar-developers/app/payments-cashier/page.tsx`, `quickstart/page.tsx`, `auth/page.tsx` | Hybrid guide+code |
| `apps/lazuar-docs/README.md` | “Draft. Safe to expand.” |

---

## 3. Gaps

### G1 — Two front doors, Scalar is louder

Someone hitting `/docs` on developers (prod `/docs`) sees OpenAPI tiles. VitePress is `:5180` / separate host. No “you are in the wrong building” if they wanted how-to.

### G2 — Commerce hosted path undocumented as a job

Merchant path: Ops signup → BYOK → Resend → product → share `/{slug}/checkout/{product}` → webhook `order.completed` / `subscription.activated`. That is the CaaS sale. Guides assume Aura/M2M.

### G3 — Draft watermark + stale env sentence

`environments.md` still says Billplz host follows `ApiBaseUrl` **contains `lazuar.com`**. Code is `ProductionHosts` allowlist + `App:BillplzEnvironment` (`BillplzPublicBase`). Honesty hole (pair with LP-182).

### G4 — Root `docs/payments-integration-quickstart.md` vs VitePress

Two cashier SSoTs.

**Not gaps**

- Sample app (keep).  
- ADR 007 product-scoped OpenAPI (keep as reference).

---

## 4. Minimal changes

### 4.1 Must — Developers hub is a **directory**, not a dump

`apps/lazuar-developers/app/page.tsx`:

1. **Start here** (full width): link to VitePress integrations index (env `NEXT_PUBLIC_DOCS_URL` or `/guides` proxy). Copy: “How to integrate — start in the guides. OpenAPI is the schema.”  
2. Keep **one** Payments cashier card + **one** LHDN quickstart.  
3. Collapse Billing / Ops / One Scalar under “Reference (advanced)” or a single “OpenAPI” section labeled **not onboarding**.  
4. Commerce card text: “Hosted checkout is a **guide**, not this Scalar admin tree” + link to new VitePress page.

### 4.2 Must — Commerce hosted how-to (VitePress)

New `apps/lazuar-docs/docs/integrations/hosted-checkout.md`:

Job steps (no module dump):

1. Create workspace (signup)  
2. Paste BYOK + Resend (email **gates** product create)  
3. Create product → copy link  
4. Buyer pays  
5. Fulfill on `order.completed` / `subscription.activated` (link LP-135 catalog)  
6. Do **not** trust success URL (LP-024)

Add to sidebar under Integrations.

### 4.3 Must — un-draft the cashier path

- Remove “These guides are **drafts for refinement**” from `index.md` **or** replace with “v1 — Payments cashier + hosted checkout.”  
- Footer in `config.ts`: drop “Internal / draft” once the two paths are honest.  
- Point root `docs/payments-integration-quickstart.md` at VitePress with a 10-line stub (do not maintain two long guides).

### 4.4 Should

- Fix Billplz environment sentence (or leave a “see LP-182” note if that PR lands first).  
- Ops API Keys page already links `VITE_DOCS_URL` — point it at `/integrations/` not Scalar.

### 4.5 Do not

- Delete Scalar routes.  
- Copy the entire admin OpenAPI into VitePress.  
- Write a Chargebee-length concept encyclopedia.

---

## 5. Tests

No API tests. Check:

| Check | How |
|-------|-----|
| `pnpm --filter lazuar-docs build` | New page in sidebar, no dead links |
| Developers homepage | First screen is how-to, not 6 modules |
| Grep “drafts for refinement” | Gone or scoped |

Manual: stranger can finish hosted-checkout.md + cashier map without opening `/ops` Scalar.

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Two hosts confuse bookmarks | Homepage + nav “Guides” vs “API reference” |
| Stale Billplz sentence survives | Same PR as LP-182 or fix here if LP-182 slips |

---

## 7. Acceptance

1. Integrator how-to starts in VitePress; Scalar is labeled reference.  
2. There is a hosted Commerce link guide with the six steps in §4.2.  
3. Draft watermark is gone from the v1 path.  
4. Root quickstart does not fork the cashier story.  
5. Tracker **P → Y**.

---

## 8. Implement order

1. Hosted-checkout.md + sidebar  
2. Developers homepage IA  
3. Un-draft + stub root quickstart  
