# Phase 4 — Analysis: lockfile regen + living docs

**Date:** 2026-08-09  
**Branch:** `chore/rename-frontend-apps-lazuar-prefix`  
**Status:** Analysis only — **do not implement from this doc alone** (implementer follows the edit table + steps).  
**Prior:** Phase 3 done (mprocs/Taskfile). Apps already renamed on disk; package `"name"` fields already `lazuar-*`.

---

## Naming map (locked)

| Old folder / package / filter | New |
|-------------------------------|-----|
| `developers-page` / `apps/developers-page` | **`lazuar-developers`** / `apps/lazuar-developers` |
| `ops-page` / `apps/ops-page` | **`lazuar-ops`** / `apps/lazuar-ops` |
| `portal-page` / `apps/portal-page` | **`lazuar-portal`** / `apps/lazuar-portal` |
| `superadmin-page` / `apps/superadmin-page` | **`lazuar-admin`** / `apps/lazuar-admin` |

> Checklist uses **`lazuar-developers`** (not `lazuar-spec`) to avoid confusion with `packages/api-spec` / `@repo/api-spec`.

**Unchanged (do not “fix” in Phase 4):**

- GHCR image names (`lazuar-hub-*`)
- Prod compose service names / Caddy public paths (`/`, `/portal`, `/docs`, `/admin`)
- Backend modules (`Modules/Ops`, `/api/v1/ops`, etc.)
- Host examples like `ops.lazuar.com` unless already editing that line for the app name (optional polish only)

---

## Scope of this phase

| In scope (must update) | Out of scope |
|------------------------|--------------|
| Root `README.md` | `docs/001-gaps/**` (historical gap snapshots) |
| `apps/lazuar-docs/**` living MD | `docs/architecture-decision-log/**` (ADRs; Phase 7 optional) |
| `docs/contracts/**` | `plans/002-change-name/**` inventory/analyses (historical of rename itself) |
| `plans/001-backend/**` living checklist app labels | Hand-editing `pnpm-lock.yaml` body |
| `pnpm-lock.yaml` via **`pnpm install` only** | App source, Docker, mprocs (already done Phases 1–3) |

---

## A. `pnpm-lock.yaml` — regenerate, do not hand-edit

### Current stale importers (proof)

Importer keys still point at **old** app paths. Already-correct importers coexist for `lazuar-api` / `lazuar-docs`:

| Line | Importer key (today) | Expected after `pnpm install` |
|------|----------------------|-------------------------------|
| 21 | `apps/developers-page:` | `apps/lazuar-developers:` |
| 61 | `apps/lazuar-api: {}` | unchanged |
| 63 | `apps/lazuar-docs:` | unchanged |
| 72 | `apps/ops-page:` | `apps/lazuar-ops:` |
| 217 | `apps/portal-page:` | `apps/lazuar-portal:` |
| 317 | `apps/superadmin-page:` | `apps/lazuar-admin:` |

There are **no** `apps/lazuar-developers:`, `apps/lazuar-ops:`, `apps/lazuar-portal:`, or `apps/lazuar-admin:` importer keys yet.

### Steps (implementer)

From monorepo root (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`):

```bash
# 1) Optional cleanup if local installs were created under old paths
#    (only if these dirs still exist as orphans — they should not after git mv)
# rm -rf apps/developers-page apps/ops-page apps/portal-page apps/superadmin-page

# 2) Regenerate lockfile importers from workspace packages + package.json names
pnpm install

# 3) Prove importers flipped
rg -n '^  apps/(developers-page|ops-page|portal-page|superadmin-page):' pnpm-lock.yaml
# → expect NO matches

rg -n '^  apps/lazuar-(developers|ops|portal|admin):' pnpm-lock.yaml
# → expect four importer keys

# 4) Sanity: filters resolve by package name
pnpm --filter lazuar-developers exec node -e "console.log('ok developers')"
pnpm --filter lazuar-ops exec node -e "console.log('ok ops')"
pnpm --filter lazuar-portal exec node -e "console.log('ok portal')"
pnpm --filter lazuar-admin exec node -e "console.log('ok admin')"
```

**Rules:**

- **Do not** search-replace importer keys in `pnpm-lock.yaml` by hand.
- Commit the lockfile **with** the living-doc edits in the same Phase 4 change set (or same PR).
- If `pnpm install` rewrites unrelated package versions, prefer the minimal intentional diff; if pnpm upgrades broadly, note it in the PR body.

### Exit criteria (lockfile)

- [ ] `pnpm install` exits 0
- [ ] No `apps/*-page:` importers remain
- [ ] Four `apps/lazuar-{developers,ops,portal,admin}:` importers present
- [ ] `pnpm --filter lazuar-*` works for all four frontends

---

## B. Root `README.md` — exact line edits

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md`  
**Hits:** 7 lines still use `ops-page` / `portal-page` / `superadmin-page`. Developers hub is **missing** from structure + ports.

### B.1 Product bullets (Key Separation)

| Line | Current | Replace with |
|------|---------|--------------|
| 46 | `- **\`ops-page\` (Admin):** The AWS-style superapp. …` | `- **\`lazuar-ops\` (Admin):** The AWS-style superapp. …` |
| 47 | `- **\`portal-page\` (Checkout):** The headless cash register. …` | `- **\`lazuar-portal\` (Checkout):** The headless cash register. …` |

**Optional (same block, if adding glossary coverage):** after line 47, add one bullet for admin + developers, e.g.:

```markdown
- **`lazuar-admin` (Platform):** Global control plane for tenants/workspaces.
- **`lazuar-developers` (API docs):** Scalar OpenAPI hub (local port 3002; prod path `/docs`).
```

### B.2 Project structure tree

| Line | Current | Replace with |
|------|---------|--------------|
| 97 | `│   ├── ops-page/         # The Back-Office (Vite CSR)        -> ops.lazuar.com` | `│   ├── lazuar-ops/        # The Back-Office (Vite CSR)        -> ops.lazuar.com` |
| 98 | `│   ├── portal-page/      # The Cash Register (Next.js SSR)   -> portal.lazuar.com` | `│   ├── lazuar-portal/     # The Cash Register (Next.js SSR)   -> portal.lazuar.com` |
| 99 | `│   └── superadmin-page/  # The Global Control Plane          -> admin.lazuar.com` | `│   ├── lazuar-admin/      # The Global Control Plane          -> admin.lazuar.com` |

**Recommended structure expansion (same fenced block)** so tree matches `ls apps/`:

```md
.
├── apps/
│   ├── lazuar-api/          # The Brain (.NET Modular Monolith) -> api.lazuar.com
│   ├── lazuar-ops/          # The Back-Office (Vite CSR)        -> ops.lazuar.com
│   ├── lazuar-portal/       # The Cash Register (Next.js SSR)   -> portal.lazuar.com
│   ├── lazuar-admin/        # The Global Control Plane          -> admin.lazuar.com
│   ├── lazuar-developers/   # Scalar OpenAPI hub                -> hub …/docs
│   └── lazuar-docs/         # VitePress product guides
│
├── packages/
│   ├── api-spec/            # TypeSpec definitions (Single Source of Truth)
│   ├── api-types-dotnet/    # Auto-generated C# Models
│   └── api-types-ts/        # Auto-generated TypeScript Interfaces
│
└── docs/                    # Architecture Decision Logs (ADR)
```

Host suffixes (`ops.lazuar.com` etc.) may remain as marketing/product host examples; Phase 4 does **not** require switching them to `hub.lazuar.com` path mode.

### B.3 Port table

| Line | Current | Replace with |
|------|---------|--------------|
| 142 | `\| \`ops-page\` \| 3003 \| \`http://localhost:3003\` \| Superapp Console (Admin) \|` | `\| \`lazuar-ops\` \| 3003 \| \`http://localhost:3003\` \| Superapp Console (Admin) \|` |
| 143 | `\| \`portal-page\`\| 3004 \| \`http://localhost:3004\` \| Universal Checkout & Dashboard \|` | `\| \`lazuar-portal\` \| 3004 \| \`http://localhost:3004\` \| Universal Checkout & Dashboard \|` |
| 144 | `\| \`superadmin\` \| 3005 \| \`http://localhost:3005\` \| Platform Infrastructure Admin \|` | `\| \`lazuar-admin\` \| 3005 \| \`http://localhost:3005\` \| Platform Infrastructure Admin \|` |

**Note on line 144:** table currently says `` `superadmin` `` (not `superadmin-page`). Still update to **`lazuar-admin`** so package/folder identity matches reality.

**Recommended:** insert developers row after `lazuar-api`:

```markdown
| `lazuar-developers` | 3002 | `http://localhost:3002` | Scalar OpenAPI hub |
```

### B.4 Glossary blurb (§4.3 checklist)

Add a short note near Project Structure or Ports (one place only), e.g.:

```markdown
**Monorepo app names:** `lazuar-ops`, `lazuar-portal`, `lazuar-admin`, `lazuar-developers`.
TypeSpec SSoT remains `packages/api-spec`. GHCR images remain `lazuar-hub-*`. Public hub paths unchanged (`/`, `/portal`, `/docs`, `/admin`).
```

---

## C. `apps/lazuar-docs/**` — exact line edits

### C.1 `apps/lazuar-docs/README.md`

| Line | Current | Replace with |
|------|---------|--------------|
| 44 | `\| \`apps/developers-page\` \| Live Scalar OpenAPI \|` | `\| \`apps/lazuar-developers\` \| Live Scalar OpenAPI \|` |

Other filter commands in this file already use `lazuar-docs` — leave them.

### C.2 `apps/lazuar-docs/docs/reference/openapi.md` — **command must work**

| Line | Current | Replace with |
|------|---------|--------------|
| 15 | `Run **developers-page** in the monorepo:` | `Run **lazuar-developers** in the monorepo:` |
| 19 | `pnpm --filter developers-page dev` | `pnpm --filter lazuar-developers dev` |

Heading “Developers page (Scalar)” (line 13) is product prose — **optional** rename to “Developers hub (Scalar)” / “lazuar-developers (Scalar)”; not required for filter honesty.

### C.3 `apps/lazuar-docs/docs/index.md`

| Line | Current | Replace with |
|------|---------|--------------|
| 40 | `… Scalar OpenAPI is under **developers-page** (\`/payments\`). …` | `… Scalar OpenAPI is under **lazuar-developers** (\`/payments\`). …` |

### C.4 `apps/lazuar-docs/docs/guide/how-to-maintain.md`

| Line | Current | Replace with |
|------|---------|--------------|
| 39 | `- Point nav “Developers (Scalar)” at production developers-page URL.` | `- Point nav “Developers (Scalar)” at production lazuar-developers (hub `/docs`) URL.` |

Alternate (shorter, if avoiding package name in ops prose):

```markdown
- Point nav “Developers (Scalar)” at the production Scalar/docs host URL (`/docs`).
```

`lazuar-docs` filter commands on lines 31–33 are already correct.

### C.5 No other living commands under lazuar-docs

Scoped search for the four old tokens under `apps/lazuar-docs` finds **only** the five lines above (plus already-correct `lazuar-docs` filters). No other files need Phase 4 edits.

---

## D. `docs/contracts/**` — exact line edits

### D.1 `docs/contracts/openapi-vs-minimal-api.md`

| Line | Current | Replace with |
|------|---------|--------------|
| 57 | `… used by ops-page, portal-page, developers-page, or SDKs, …` | `… used by lazuar-ops, lazuar-portal, lazuar-developers, or SDKs, …` |
| 65 | `\| \`ops-page\` invoicing module (quotes / tax invoices / credit notes) \| …` | `\| \`lazuar-ops\` invoicing module (quotes / tax invoices / credit notes) \| …` |
| 66 | `\| \`ops-page\` \`BillingProfilePage\` \| …` | `\| \`lazuar-ops\` \`BillingProfilePage\` \| …` |
| 67 | `\| \`ops-page\` Ops chat (\`OpsChatWorkspace\`, stream client) \| …` | `\| \`lazuar-ops\` Ops chat (\`OpsChatWorkspace\`, stream client) \| …` |

This file is a **living** contract SOP (critical-path rules still enforce UI wiring discipline). Treat all four lines as **must-change**.

No other files under `docs/contracts/` matched the old app tokens.

---

## E. `plans/001-backend/**` — living checklist labels

### E.1 Classification

`plans/001-backend/001-backend-solidification-checklist.md` is still an **active execution map** (residual `[ ]` items, phase status notes). Update **app identity labels** so residual work points at real folders/packages. This is **not** a filter-command file, but checklist §4.2 explicitly includes it.

`plans/001-backend/README.md` — **no** `*-page` hits; no edit.

### E.2 Exact line edits — `plans/001-backend/001-backend-solidification-checklist.md`

| Line | Current | Replace with |
|------|---------|--------------|
| 293 | `**Apps:** \`ops-page\`` | `**Apps:** \`lazuar-ops\`` |
| 351 | `**Apps:** \`ops-page\`` | `**Apps:** \`lazuar-ops\`` |
| 366 | `**Apps:** \`developers-page\`, \`packages/api-spec\`` | `**Apps:** \`lazuar-developers\`, \`packages/api-spec\`` |
| 432 | `… exist in \`apps/developers-page\` (keys vs JWT, …` | `… exist in \`apps/lazuar-developers\` (keys vs JWT, …` |
| 531 | `- \`MessageDeliveryLog\` admin UI not wired in ops-page (API only)` | `- \`MessageDeliveryLog\` admin UI not wired in lazuar-ops (API only)` |
| 730 | `\| TypeSpec / gen / docs \| 0, B, C \| api-spec + developers-page \|` | `\| TypeSpec / gen / docs \| 0, B, C \| api-spec + lazuar-developers \|` |
| 731 | `\| Ops / portal UI \| B, C \| ops-page + portal-page \|` | `\| Ops / portal UI \| B, C \| lazuar-ops + lazuar-portal \|` |

**Optional heading polish (same section, not required):**

| Line | Current | Optional |
|------|---------|----------|
| 364 | `## B.6 Developers-page as integration hub` | `## B.6 lazuar-developers as integration hub` |

Do **not** rewrite completed checkbox history prose beyond the token renames above.

---

## F. Historical / deferred (optional note only — **not required** for Phase 4)

These still contain `developers-page|ops-page|portal-page|superadmin-page` but are **snapshots / ADRs / rename inventory**, not living onboarding commands. Leave for Phase 7 (docs archaeology) unless a PR author wants a one-line banner.

| Area | Why skip in Phase 4 |
|------|---------------------|
| `docs/001-gaps/**` | Gap analyses dated to pre-rename tree; filename `04-developers-page-dx.md` is historical |
| `docs/architecture-decision-log/**` (e.g. 007, 012–014, 017) | Point-in-time ADRs |
| `plans/002-change-name/01–11*.md`, `phase-*-analysis/done.md` | Rename program records |
| Backend comment strings already handled in Phase 1 (optional) | Not living docs |
| App-local path banners if any remain | Cosmetic; not Phase 4 living docs |

Quick inventory greps (for PR body “allowed remaining”):

```bash
rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
  --glob '!**/node_modules/**' --glob '!**/.next/**' --glob '!**/dist/**' \
  docs/001-gaps docs/architecture-decision-log plans/002-change-name
```

---

## G. Implementer checklist (ordered)

1. **Docs first or lockfile first** — either order is fine; prefer **docs + then `pnpm install`** so a single commit can carry both.
2. Apply **§B–E** string updates exactly (and recommended README structure/port rows if accepting them).
3. Run **§A** `pnpm install` from repo root; verify importer keys.
4. Verify living commands:

   ```bash
   rg -n 'developers-page|ops-page|portal-page|superadmin-page' \
     README.md apps/lazuar-docs docs/contracts plans/001-backend
   # → expect no matches after Phase 4

   rg -n 'pnpm --filter (developers-page|ops-page|portal-page|superadmin-page)' \
     --glob '!**/node_modules/**'
   # → expect no matches anywhere living
   ```

5. Optional smoke: `pnpm --filter lazuar-docs build` if docs site changed; not a hard gate for rename honesty.
6. Mark `plans/002-change-name/11-implementation-checklist.md` Phase 4 boxes when implementing (not part of this analysis write).

---

## H. Phase 4 exit criteria (copy from checklist)

- [ ] `pnpm install` clean; lockfile importers use `apps/lazuar-*` for the four frontends
- [ ] New contributor can find apps by new names in root `README.md` (tree + ports)
- [ ] No living command uses `--filter developers-page` (etc.) — especially `apps/lazuar-docs/docs/reference/openapi.md`
- [ ] `docs/contracts/openapi-vs-minimal-api.md` and `plans/001-backend/001-backend-solidification-checklist.md` app labels updated
- [ ] Historical gap/ADR leftovers listed as **allowed remaining** in PR body (not blocking)

---

## I. Edit summary count

| Surface | Files | Required line-level hits |
|---------|-------|---------------------------|
| `pnpm-lock.yaml` | 1 | regen only (4 stale importer keys → 4 new) |
| Root `README.md` | 1 | 7 required renames (+ recommended structure/ports/glossary) |
| `apps/lazuar-docs/**` | 4 | 5 required lines |
| `docs/contracts/**` | 1 | 4 required lines |
| `plans/001-backend/**` | 1 | 7 required lines |
| **Total required** | **8 surfaces** | **~23 content lines + lockfile regen** |

No implementation performed in this analysis document.
