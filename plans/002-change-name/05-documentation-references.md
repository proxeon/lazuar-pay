# 05 — Documentation References Impact Analysis

**Plan:** `plans/002-change-name`  
**Scope:** Documentation only (markdown / VitePress MD). No app code, package.json, Docker, or CI configs.  
**Date of scan:** 2026-08-08  
**Workspace:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`

## Proposed renames (directory / app identity)

| Old name | New name |
|----------|----------|
| `developers-page` | `lazuar-spec` |
| `ops-page` | `lazuar-ops` |
| `portal-page` | `lazuar-portal` |
| `superadmin-page` | `lazuar-admin` |

## Classification legend (used per file / section)

| Class | Meaning |
|-------|---------|
| **must update** | Living onboarding, product guides, or active SOPs that engineers will follow today. Broken paths/filter names after rename will confuse. |
| **historical ADR keep-as-is** | Architecture Decision Record capturing a decision at a point in time. Prefer leave original text; optionally add a short superseding note if the ADR is still used as an implementation guide. |
| **optional** | Snapshot / gap audit / completed checklist. Paths are evidence of past state. Update only if the doc remains an active working map; otherwise leave or add a one-line “paths renamed” banner. |

## Search method

1. Recursive ripgrep over `*.md` / `*.mdx` for exact tokens:  
   `developers-page`, `ops-page`, `portal-page`, `superadmin-page`
2. Secondary greps for related prose: `developers page`, `Developer Hub`, `developers.lazuar.com`, `ops.lazuar.com`, `portal.lazuar.com`, `admin.lazuar.com`, product-path language (`/docs`)
3. Explicit checks of requested focus areas:
   - Root `README.md`
   - `docs/**`
   - `apps/*/README.md`
   - `apps/lazuar-docs/**`
   - `plans/**`
   - `docs/architecture-decision-log/**`
   - `docs/001-gaps/**`
   - `CLAUDE.md` / `AGENTS.md` (root and under apps)
   - `idea/**`
   - `script/second-app-proof.md`
4. **No matches** in: root `CLAUDE.md`/`AGENTS.md` (none present at root), `idea/**`, `script/second-app-proof.md` (exact app-folder tokens), `apps/lazuar-api/docs/**`, `packages/**/*.md`, most ADR files without frontend app names.

Approximate hit volume for the four exact tokens in `*.md`/`*.mdx`: **~180+ lines** across **~35 files** (concentrated in `docs/001-gaps/` and a handful of ADRs + root/lazuar-docs).

---

## Naming fitness note: `developers-page` → `lazuar-spec`

### What the product is (from docs)

Across gap analysis, ADR 007, and lazuar-docs, **`developers-page` is consistently described as the Developer Hub / API reference shell**:

- Next.js app rendering **Scalar OpenAPI** product references.
- Loads YAML from `packages/api-spec/dist/{product}/openapi.yaml`.
- Production mount: `hub.lazuar.com/docs*` (path `/docs`, not hostname `developers.lazuar.com` in current deploy).
- **Not** the TypeSpec source-of-truth package (that is `packages/api-spec`).
- **Not** the credential/console surface (Ops “Developer” nav is webhooks/API keys).
- Product prose also calls it: “Developers hub”, “Developer Hub”, “Scalar developers page”, “developers-page”.

Representative product-purpose quotes:

From `docs/001-gaps/04-developers-page-dx.md`:

```text
| Developer Hub app | .../apps/developers-page | Next.js 16 app rendering Scalar OpenAPI references |
...
The hub is a **thin documentation shell**, not an integration console:
1. **Landing** (`apps/developers-page/app/page.tsx`) — four product cards linking to module references.
2. **Four Scalar routes** — each is a Next.js Route Handler that loads a YAML string and passes it to `@scalar/nextjs-api-reference`
```

From `docs/architecture-decision-log/007-product-scoped-api-references.md`:

```text
Instead of rendering one global API page, we generate distinct OpenAPI artifacts for each business domain
and serve them on isolated routes within our `developers-page` Next.js application
(e.g., `developers.lazuar.com/one`, `developers.lazuar.com/community`).
```

From `apps/lazuar-docs/README.md`:

```text
| `apps/developers-page` | Live Scalar OpenAPI |
```

### Does `lazuar-spec` make sense in documentation?

**Partial fit, with a real collision risk.**

| Argument for `lazuar-spec` | Argument against / risk |
|----------------------------|-------------------------|
| App’s primary job is **serving OpenAPI specs** (Scalar). “Spec” = the published contract surface. | **`packages/api-spec` already exists** and is documented everywhere as the TypeSpec **source of truth**. New readers will confuse `apps/lazuar-spec` (UI) with `packages/api-spec` (TypeSpec). |
| Aligns with monorepo `lazuar-*` app naming (`lazuar-api`, `lazuar-docs`, proposed `lazuar-ops`, etc.). | “Spec” does not communicate **developers / hub / Scalar / docs UX**. Product language in docs is “Developers hub”, not “spec app”. |
| Distinguishes from VitePress **guides** (`lazuar-docs`) vs **live OpenAPI** (this app). | ADR 007 title and body talk about “Developer Hub Segmentation”; renaming to “spec” requires intentional prose updates so marketing/onboarding still say “Developers” where user-facing. |
| Deploy path remains `/docs` — product URL need not change with folder rename. | Gap filename `04-developers-page-dx.md` and dozens of historical quotes embed the old name; historical keep-as-is is fine, but living docs must stop saying `pnpm --filter developers-page`. |

**Documentation recommendation if rename proceeds:**

1. In living docs, always pair the new name with role language:  
   **`lazuar-spec` (Developer Hub / Scalar OpenAPI)** — never bare “spec” next to `api-spec` without disambiguation.
2. Add an explicit comparison table (already half-exists in lazuar-docs README):

   | Package/app | Role |
   |-------------|------|
   | `packages/api-spec` | TypeSpec sources + `task gen` outputs |
   | `apps/lazuar-spec` | Scalar UI that **renders** generated OpenAPI |
   | `apps/lazuar-docs` | VitePress human guides |

3. Prefer user-facing labels remain **“Developers” / “API Reference”**; reserve `lazuar-spec` for monorepo path / `pnpm --filter` only.
4. Optionally document the rename in a short ADR or in `plans/002-change-name` rather than rewriting historical gap reports.

**Verdict:** Naming is **acceptable for monorepo identity** if docs disambiguate from `packages/api-spec`. It is **weaker as a product name** than “developers” / “dev hub”. Documentation impact is higher than for `ops`/`portal`/`admin` renames because of the `api-spec` collision.

---

## Global inventory of markdown files with exact-token hits

### A. Must-update (living onboarding / product docs)

| Absolute path | Tokens present | Notes |
|---------------|----------------|-------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md` | `ops-page`, `portal-page`, `superadmin-page` (not `developers-page`) | Structure tree + ports; omits developers hub entirely (pre-existing gap). |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/README.md` | `developers-page` | Relationship table. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/index.md` | `developers-page` | Status blurb. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/reference/openapi.md` | `developers-page` + `pnpm --filter developers-page` | Runnable commands — **hard break** if not updated. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/guide/how-to-maintain.md` | `developers-page` | Publish checklist. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/integrations/payments-cashier.md` | prose “Scalar developers page” (not exact folder token) | Soft rename / wording only. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/contracts/openapi-vs-minimal-api.md` | all four | Living contract honesty rules. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/001-backend/001-backend-solidification-checklist.md` | `ops-page`, `portal-page`, `developers-page` | Active checklist still referenced from gaps README. |

### B. Historical ADR — keep-as-is (recommended); optional footnote

| Absolute path | Tokens | Title / role |
|---------------|--------|--------------|
| `.../docs/architecture-decision-log/007-product-scoped-api-references.md` | `developers-page` heavily | Defines Developer Hub pattern |
| `.../docs/architecture-decision-log/013-frontend-module-implementation.md` | `ops-page` in title + paths | **Active SOP** for adding ops modules — borderline; see note below |
| `.../docs/architecture-decision-log/014-apps.md` | `ops-page` once (frontend module path pattern) | Large apps catalog (historical ambition) |
| `.../docs/architecture-decision-log/016-platform-domain-strategy.md` | `ops-page`, `portal-page` (Caddy example service names) | Domain strategy |
| `.../docs/architecture-decision-log/017-portal-frontend-architecture.md` | `portal-page` | Portal vertical-slice SOP |
| `.../docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md` | `ops-page`, `portal-page` | Marketplace strategy |
| `.../docs/architecture-decision-log/022-remove-community-vault-modules.md` | `ops-page`, `portal-page`, `superadmin-page` | Removal phases |
| `.../docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` | `ops-page`, `portal-page` | UI lobotomy |

**ADR 013 / 017 special case:** These are written as **current implementation SOPs** (“Open `apps/ops-page/src/App.tsx`”). Classification:

- Pure historical decision body → **historical ADR keep-as-is**
- Path-bearing checklist steps still followed day-to-day → treat path lines as **must update** *or* add a banner:  
  `> Path note (2026 rename): apps/ops-page → apps/lazuar-ops`  
  without rewriting the whole ADR.

### C. Gap analyses & product-owner maps — optional (snapshots)

All under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/`. These are labeled “full uncondensed subagent analysis” / dated evaluations. Paths often still say `lazuar-hub`. Prefer **optional** bulk find-replace only if the team still navigates code from these reports.

| File | Tokens | Density |
|------|--------|---------|
| `04-developers-page-dx.md` | all four heavily | Highest density; **filename** contains `developers-page` |
| `03-api-auth-credentials.md` | all four | High |
| `13-typespec-api-contracts.md` | all four | High |
| `19-frontend-backend-integration.md` | `ops-page`, `portal-page`, `superadmin-page` | High (absolute paths) |
| `18-outbound-customer-webhooks.md` | `ops-page`, `developers-page` | Medium |
| `07-commerce-module.md` | `ops-page`, `portal-page` | Medium |
| `09-lhdn-module.md` | `ops-page`, `developers-page` | Medium |
| `20-architecture-intent-vs-implementation.md` | `ops-page`, `portal-page`, `developers-page` | Medium |
| `16-testing-coverage.md` | all four | Medium |
| `00-what-we-need-to-do-next.md` | links to `04-developers-page-dx.md` | Filename links |
| `README.md` | `developers-page`, filename links | Index |
| `01-dunning-engine.md` | `ops-page` | Low (2 path rows) |
| `06-payments-module.md` | `ops-page` | Low |
| `08-communications-module.md` | `ops-page` | Low |
| `11-ops-crm-messaging.md` | `ops-page` | Low |

**Gap files with no exact-token hits** (scan):  
`02-payment-webhooks.md`, `05-billing-module.md`, `10-one-identity-module.md`, `12-buildingblocks-host.md`, `14-tenant-isolation.md`, `15-event-driven-architecture.md`, `17-background-workers.md`, `21-phase-c-acceptance-notes.md` — no mandatory doc work for this rename.

### D. App READMEs / agent stubs — optional or structural

| Absolute path | Hits | Classification |
|---------------|------|----------------|
| `apps/developers-page/README.md` | none of the four tokens (stock create-next-app) | **optional** product rewrite when app renames; file **moves** with folder |
| `apps/developers-page/AGENTS.md` | none | **moves with folder**; Next.js agent rules only |
| `apps/developers-page/CLAUDE.md` | none (`@AGENTS.md`) | **moves with folder** |
| `apps/ops-page/README.md` | none (title “# Ops ”) | **optional** when folder renames |
| `apps/superadmin-page/README.md` | none (title “# Ops ”) | **optional**; stale title already |
| `apps/portal-page/README.md` | none (stock Next.js) | **optional** |
| Root `CLAUDE.md` / `AGENTS.md` | **do not exist** | n/a |
| `idea/**` | no hits | n/a |
| `script/second-app-proof.md` | no exact app-folder tokens | n/a (mentions “Hub Ops” as product surface, not `ops-page`) |
| `docs/payments-integration-quickstart.md` | no exact folder tokens; “Developers hub `/payments`” | **optional** prose consistency |
| `deploy/prod/README.md` | no exact `-page` tokens; uses `ops` / `portal` / `superadmin` / “developer API docs” | Out of strict scope; soft product names already match intent of rename |

### E. Plans

| Absolute path | Hits | Classification |
|---------------|------|----------------|
| `plans/001-backend/001-backend-solidification-checklist.md` | yes | **must update** (still the solidification checklist) |
| `plans/001-backend/README.md` | no app tokens | n/a |
| `plans/002-change-name/` | this file only (so far) | n/a |

---

# Per-file analysis (uncondensed)

---

## 1. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md`

**Classification:** **must update**

**Tokens:** `ops-page`, `portal-page`, `superadmin-page`. **Does not mention `developers-page`** (known gap called out in `04-developers-page-dx.md`).

### Relevant sections (quoted)

**Key Separation:**

```markdown
**Key Separation:**
- **`ops-page` (Admin):** The AWS-style superapp. Internal staff use this to configure products, view the financial ledger, construct Dunning campaigns, and manage operations. 
- **`portal-page` (Checkout):** The headless cash register. Highly optimized, distraction-free SSR Next.js app that processes transactions and grants access.
```

**Project Structure:**

```markdown
├── apps/
│   ├── lazuar-api/       # The Brain (.NET Modular Monolith) -> api.lazuar.com
│   ├── ops-page/         # The Back-Office (Vite CSR)        -> ops.lazuar.com
│   ├── portal-page/      # The Cash Register (Next.js SSR)   -> portal.lazuar.com
│   └── superadmin-page/  # The Global Control Plane          -> admin.lazuar.com
```

**Port mapping:**

```markdown
| App | Port | Access URL | Description |
|-----|------|------------|-------------|
| `lazuar-api` | 8080 | `http://localhost:8080` | .NET 10 Modular Monolith |
| `ops-page` | 3003 | `http://localhost:3003` | Superapp Console (Admin) |
| `portal-page`| 3004 | `http://localhost:3004` | Universal Checkout & Dashboard |
| `superadmin` | 3005 | `http://localhost:3005` | Platform Infrastructure Admin |
```

Note: table shortens `superadmin-page` to `` `superadmin` `` already (inconsistent).

**Architecture diagram** uses hostnames only (`portal.lazuar.com`) — no folder rename impact.

### Required edits after rename

| Location | Old | New |
|----------|-----|-----|
| Key Separation bullets | `` `ops-page` `` / `` `portal-page` `` | `` `lazuar-ops` `` / `` `lazuar-portal` `` |
| Tree | `ops-page/`, `portal-page/`, `superadmin-page/` | `lazuar-ops/`, `lazuar-portal/`, `lazuar-admin/` |
| Ports table | same | same mapping |
| **Add missing row** for developers hub | (absent) | e.g. `lazuar-spec` port **3002**, description “Scalar OpenAPI / Developer Hub”, prod path `/docs` |

Also keep `packages/api-spec` as-is; do not rename package when renaming the app to `lazuar-spec`.

---

## 2. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/README.md`

**Classification:** **must update**

### Quote

```markdown
## Relationship to other docs

| Location | Audience |
|----------|----------|
| **This app** | Product + integrator guides (refine → publish) |
| `docs/*.md` (repo root) | Engineering ADRs, gap analysis, quickstarts |
| `apps/developers-page` | Live Scalar OpenAPI |
| Aura `apps/aura-docs` | Salon **product how-to** (not Hub integrator) |
```

### Edit

- Row → `` `apps/lazuar-spec` `` with note “Live Scalar OpenAPI (Developer Hub)”.
- Optionally expand table with `packages/api-spec` vs `apps/lazuar-spec` disambiguation (recommended because of naming collision).

---

## 3. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/index.md`

**Classification:** **must update**

### Quote

```markdown
## Status

These guides are **drafts for refinement**. Runtime APIs live in the monorepo; Scalar OpenAPI is under **developers-page** (`/payments`). Update guides as contracts change.
```

### Edit

- Prefer: “Scalar OpenAPI is under **`lazuar-spec`** (Developer Hub) at `/payments`…”  
  Keep `/payments` route language; it is an app route, not a monorepo folder.

---

## 4. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/reference/openapi.md`

**Classification:** **must update** (commands will break)

### Quote

```markdown
## Developers page (Scalar)

Run **developers-page** in the monorepo:

```bash
# typical local
pnpm --filter developers-page dev
```

Useful routes (when running):

| Path | Content |
|------|---------|
| `/payments` | Payments OpenAPI Scalar |
| `/payments-cashier` | Link/card into cashier narrative |
| `/auth` | API keys & scopes copy |
| `/webhooks` | Webhook UI notes |

Point production docs site nav at your deployed developers host when publishing.
```

### Edit

- Heading can stay “Developers page (Scalar)” (product language) **or** become “Developer Hub (`lazuar-spec`)”.
- `pnpm --filter developers-page` → `pnpm --filter lazuar-spec` (must match `package.json` `name` after rename).
- Last sentence: “deployed developers host” remains valid product language.

**Also** upper table correctly lists `packages/api-spec/` — do **not** change those rows to `lazuar-spec`.

---

## 5. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/guide/how-to-maintain.md`

**Classification:** **must update**

### Quote

```markdown
## Publishing later

- Set `base` in `.vitepress/config.ts` if served under a subpath.  
- Point nav “Developers (Scalar)” at production developers-page URL.  
- Promote pages from draft → stable when contracts freeze.
```

### Edit

- “production developers-page URL” → “production Developer Hub (`lazuar-spec` / `/docs`) URL”.
- Nav label “Developers (Scalar)” can stay (user-facing).

Related sources table lists `packages/api-spec/` only — no change.

---

## 6. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/integrations/payments-cashier.md`

**Classification:** **optional** (no exact folder token)

### Quote

```markdown
## OpenAPI

Scalar developers page **Payments** product · TypeSpec `packages/api-spec/docs-payments.tsp` · `packages/api-spec/dist/payments/openapi.yaml`.
```

### Edit (if polishing)

- “Scalar developers page” → “Scalar Developer Hub (`lazuar-spec`)” for consistency.
- Keep TypeSpec paths on `packages/api-spec`.

---

## 7. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/contracts/openapi-vs-minimal-api.md`

**Classification:** **must update**

### Quote

```markdown
If you add a **product** route used by ops-page, portal-page, developers-page, or SDKs, it **must** land in TypeSpec before UI wiring.
```

```markdown
| UI island | Status | Reactivation |
|-----------|--------|--------------|
| `ops-page` invoicing module (quotes / tax invoices / credit notes) | Code present; **no routes** in `App.tsx` | Uncomment `[MVP-HIDE]` routes + sidebar (Phase D.3) |
| `ops-page` `BillingProfilePage` | Unrouted | Same |
| `ops-page` Ops chat (`OpsChatWorkspace`, stream client) | Unrouted; API + OpenAPI exist | Mount `/ops/chat` when productizing |
```

### Edit

- First rule: `lazuar-ops`, `lazuar-portal`, `lazuar-spec`, or SDKs.
- Table rows: `` `lazuar-ops` invoicing module `` etc.
- This is a living “do not regress” contract doc — treat as **must**.

---

## 8. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/payments-integration-quickstart.md`

**Classification:** **optional**

### Quote

```markdown
**OpenAPI:** Scalar → Developers hub `/payments` · TypeSpec `packages/api-spec/docs-payments.tsp` · generated `packages/api-spec/dist/payments/openapi.yaml`.
```

Uses product language “Developers hub”, not folder name. No mandatory path rewrite. Optional: parenthetical `` (`apps/lazuar-spec`) `` once.

Also says “Ops → Payment settings” as product UI, not `ops-page`.

---

## 9. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/script/second-app-proof.md`

**Classification:** **no action** for exact rename tokens

Mentions “Open Hub Ops for `$WORKSPACE_ID`” as a human step — product surface name, not monorepo folder. No `ops-page` / `developers-page` strings.

---

## 10. `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/001-backend/001-backend-solidification-checklist.md`

**Classification:** **must update** (still linked from `docs/001-gaps/README.md` as the implementation checklist)

### Quotes

```markdown
## B.3 Ops Developer console — API keys

**Apps:** `ops-page`
...
- [x] Deep link to developers docs (`/docs/lhdn` etc.)
```

```markdown
## B.5 Ops Developer console — webhooks UX

**Apps:** `ops-page`
```

```markdown
## B.6 Developers-page as integration hub

**Apps:** `developers-page`, `packages/api-spec`
```

```markdown
- [x] Developers hub explains auth + one happy path without reading ADRs
  - Residual (content review): `/auth` + quickstart exist in `apps/developers-page` (keys vs JWT, Ops path, curl happy path). Spot-check for copy drift only.
```

```markdown
- `MessageDeliveryLog` admin UI not wired in ops-page (API only)
```

```markdown
| TypeSpec / gen / docs | 0, B, C | api-spec + developers-page |
| Ops / portal UI | B, C | ops-page + portal-page |
```

### Edit guidance

- Replace app folder tokens with new names.
- Section title “B.6 Developers-page as integration hub” → “B.6 lazuar-spec (Developer Hub) as integration hub” **or** keep product title and only change the `**Apps:**` line.
- Keep `` `packages/api-spec` `` distinct from `` `lazuar-spec` `` in the B.6 apps line to avoid implying they are the same thing.

---

## 11. Architecture Decision Log

### 11.1 `docs/architecture-decision-log/007-product-scoped-api-references.md`

**Classification:** **historical ADR keep-as-is** (core decision text); **optional** path annotation for implementers

**Status in file:** Accepted (June 2026)

**Quotes:**

```markdown
Instead of rendering one global API page, we generate distinct OpenAPI artifacts for each business domain and serve them on isolated routes within our `developers-page` Next.js application (e.g., `developers.lazuar.com/one`, `developers.lazuar.com/community`).
```

```markdown
### Step 3: Create the Next.js Scalar Route
In the `developers-page` Next.js app, create an API route that reads the newly generated YAML file and renders the highly-optimized Scalar HTML engine.

**File:** `apps/developers-page/app/vault/route.ts`
```

```markdown
**File:** `apps/developers-page/app/page.tsx`
```

**Notes:**

- Decision content (product-scoped OpenAPI) is independent of monorepo folder name.
- Implementation guide still used when adding products → if kept active, either update paths to `apps/lazuar-spec/...` **or** add:

  ```markdown
  > **Path rename (plan 002):** `apps/developers-page` → `apps/lazuar-spec`. Production host is `hub.lazuar.com/docs`, not `developers.lazuar.com`.
  ```

- Examples still reference Vault/Community (stale product surface per ADR 022) — pre-existing staleness, out of rename scope.

### 11.2 `docs/architecture-decision-log/013-frontend-module-implementation.md`

**Classification:** **historical ADR keep-as-is** for decision; **must update OR annotate** for path steps if still the SOP for ops frontend

**Title:** `# ADR 013: Frontend Module Implementation (ops-page)`

**Quotes:**

```markdown
**Context:** As the Lazuar platform grows, we will introduce new business verticals (e.g., `Funnel`, `Vault`, `CRM`) to the `ops-page` Super App.
...
This document outlines the standard operating procedure for adding a completely new module to the `ops-page` frontend.
```

```text
apps/ops-page/src/modules/funnel/
```

```markdown
1.  Open `apps/ops-page/src/App.tsx`.
...
1.  Open `apps/ops-page/src/components/Sidebar.tsx`.
```

**Recommendation:** Add rename banner at top; either leave historical title or retitle to “lazuar-ops (formerly ops-page)” only if the team wants ADRs to track current paths. Do not silently rewrite history without a superseding ADR.

### 11.3 `docs/architecture-decision-log/014-apps.md`

**Classification:** **historical ADR keep-as-is**

**Quote:**

```text
ops-page/src/modules/{appName}/
├── pages/
```

Only one hit found in this large file (frontend module pattern). File is catalogued as historical ambition in root README watermark. Leave as-is.

### 11.4 `docs/architecture-decision-log/016-platform-domain-strategy.md`

**Classification:** **historical ADR keep-as-is**

**Quotes:**

```text
ops.lazuar.com {
    reverse_proxy ops-page:3000
}

portal.lazuar.com {
    reverse_proxy portal-page:3000
}
```

Also documents hostnames `ops.lazuar.com`, `portal.lazuar.com` — **hostnames are not part of this folder rename** (unless a separate DNS rename is planned). Caddy service names `ops-page` / `portal-page` would change only if Docker service names change (infra, not this doc plan alone).

**Optional:** footnote that container names may become `lazuar-ops` / `lazuar-portal` after deploy rename.

### 11.5 `docs/architecture-decision-log/017-portal-frontend-architecture.md`

**Classification:** **historical ADR keep-as-is** (decision); **annotate if still SOP**

**Quotes:**

```markdown
**Context:** Frontend Codebase Organization (`portal-page`)
...
We are adopting a **Vertical Slice Architecture (Module-Driven Design)** for the `portal-page` codebase.
```

```text
apps/portal-page/
├── src/
│   ├── app/
```

Same treatment as ADR 013.

### 11.6 `docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md`

**Classification:** **historical ADR keep-as-is**

**Quotes:**

```markdown
keeping our core transactional engine (`portal-page`) completely blind and deterministic.
...
When a creator chooses to "Publish to Marketplace" from their `ops-page` (Vite) dashboard...
...
The Next.js/Remix `portal-page` remains purely transactional and blazing fast.
...
[CREATOR] -> ops-page (Vite) -> Enters Markdown / Metadata
...
[BUYER]   -> portal-page (SSR) -> Executes Transaction (Blind Checkout)
```

Decision narrative; not day-to-day path navigation. Keep-as-is.

### 11.7 `docs/architecture-decision-log/022-remove-community-vault-modules.md`

**Classification:** **historical ADR keep-as-is**

**Quotes:**

```markdown
- **ops-page:** Community Spaces & Vault entries removed from the sidebar `MODULES` array and `App.tsx` routes.
- **portal-page:** Community (Telegram/Zoom) and Vault (Digital Vault downloads) sections removed...
...
   - ops-page: `modules/vault/`, `modules/community/pages/SpacesPage.tsx`, ...
   - portal-page: `modules/community/components/CommunityPortalView.tsx`, ...
...
(e.g. `superadmin-page/src/lib/prompt-library.ts`)
```

Records a completed/ongoing cleanup decision with evidence paths. Leave historical names; they document what was touched under old names.

### 11.8 `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md`

**Classification:** **historical ADR keep-as-is**

**Quotes:**

```markdown
**1. Creator Dashboard (`ops-page`)**
...
**2. Buyer Checkout (`portal-page`)**
```

Decision record of what was hidden. Keep-as-is.

### 11.9 ADRs with **no** exact app-folder hits (scan)

Including but not limited to: `001`–`006`, `009`–`012`, `015` (uses hostnames `portal.lazuar.com` / `ops.lazuar.com` only), `019`–`021`. No rename text edits required for folder tokens.

---

## 12. Gap analyses (`docs/001-gaps/`)

General policy for this tree:

- Each report is a **time-stamped audit** (headers: “Full uncondensed subagent analysis — do not summarize”).
- Many absolute paths still say `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/...` (repo rename already stale).
- **Default classification: optional / historical keep-as-is.**
- **Exception:** if someone is still implementing from a report’s “file map” tables, they will need mental translation after rename; a one-line banner on the directory README is cheaper than rewriting 20 reports.

### 12.1 `docs/001-gaps/README.md`

**Classification:** **optional** (index); links to filename `04-developers-page-dx.md`

**Quotes:**

```markdown
Product-owner starter concerns (dunning, webhooks/API, developers-page, integration credentials vs JWT) are covered in depth in the reports listed below.
```

```markdown
| 04 | [04-developers-page-dx.md](./04-developers-page-dx.md) | Developers hub = docs not integration console; credential UX missing |
```

```markdown
### 3. Developers-page focuses on backend API, not integration APIs
→ Primary: **04**, also **13**, **03**, **19**
```

```markdown
2. `03-api-auth-credentials.md` + `04-developers-page-dx.md` + `18-outbound-customer-webhooks.md`
```

**Filename decision:**

| Option | Pros | Cons |
|--------|------|------|
| Keep `04-developers-page-dx.md` | Historical identity; no broken deep links in chat/PRs | Name drifts from monorepo |
| Rename to `04-lazuar-spec-dx.md` or `04-developers-hub-dx.md` | Aligns with product language | Must update all links in README, `00-…`, checklist evidence lines |

**Recommendation:** **keep filename** (historical). Optional body-banner only.

### 12.2 `docs/001-gaps/00-what-we-need-to-do-next.md`

**Classification:** **optional**

**Quotes:**

```markdown
**Evidence:** `18-outbound-customer-webhooks.md`, `04-developers-page-dx.md`, `10-one-identity-module.md`.
...
**Evidence:** `03-api-auth-credentials.md`, `04-developers-page-dx.md`, ...
...
| [04-developers-page-dx.md](./04-developers-page-dx.md) | Developer hub / DX |
```

Only filename / theme references, not `apps/developers-page` paths in the grepped lines. Keep-as-is unless renaming the gap file.

### 12.3 `docs/001-gaps/04-developers-page-dx.md`

**Classification:** **optional / historical keep-as-is** (primary developers-page evidence document)

**Highest density of all four tokens.** Selected representative quotes (not exhaustive — file is large and nearly every section references the hub or ops Developer nav):

**Surface table:**

```markdown
| Developer Hub app | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/developers-page` | Next.js 16 app rendering Scalar OpenAPI references |
```

**What the hub does:**

```markdown
1. **Landing** (`apps/developers-page/app/page.tsx`) — four product cards linking to module references.
...
3. **Spec loader** (`apps/developers-page/lib/openapi.ts`) — resolves monorepo path vs Docker `OPENAPI_SPEC_ROOT`.
```

**Adjacent ops surfaces:**

```markdown
### Adjacent “developer” surfaces (ops-page)

Ops console has a **Developer** nav group (`apps/ops-page/src/components/Sidebar.tsx`):
```

**Non-goals:**

```markdown
### Explicit non-goals of current developers-page (evidence)
...
- Root monorepo README **omits `developers-page`** from project structure (lists api, ops, portal, superadmin only).
```

**Pipeline diagram ends with:**

```text
└─► developers-page Scalar routes (+ Docker COPY into image)
```

**Credential matrix excerpt:**

```markdown
| ops-page Developer | **No** | Outbound webhooks + delivery logs only |
| ops-page Payment Settings | No | **BYOK third-party** gateway secrets (Stripe/Billplz/etc.) |
| ops-page Email Settings | No | **Resend** provider key |
| portal-page | No | Buyer checkout/portal only |
| superadmin-page | No evidence of Lazuar API key vault | — |
| developers-page | No | Read-only docs |
```

**Recommendations sections** repeatedly say “Ship API Keys UI in ops-page”, “Make developers-page an integration hub”, section headers `### apps/developers-page/`, file maps listing:

```markdown
| `docker-bake.hcl` | `developers-page` target, `NEXT_BASE_PATH=/docs`. |
| `mprocs-dev.yaml` | Local developers-page process. |
| Root `README.md` | Omits developers-page from structure; ... |
```

**If updating (optional full pass):** every path and filter name; also refresh absolute `lazuar-hub` roots to `lazuar-pay`. Not required for rename ship if living docs are fixed.

**Product-purpose note:** This file is the best single source describing that developers-page is **docs/Scalar**, not credentials. After rename to `lazuar-spec`, a reader of this historical report must mentally map; a banner at the top of the file would help:

```markdown
> **Rename map (plan 002):** `developers-page` → `lazuar-spec`, `ops-page` → `lazuar-ops`, `portal-page` → `lazuar-portal`, `superadmin-page` → `lazuar-admin`.
```

### 12.4 `docs/001-gaps/03-api-auth-credentials.md`

**Classification:** **optional**

**Quotes:**

```markdown
1. Frontend stores `ops_active_workspace_id` and sends `X-Tenant-Id` on non-`/one/` requests (`apps/ops-page/src/lib/api-client.ts`).
```

```markdown
App: `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/developers-page`
```

```markdown
**Conclusion:** Product owner assessment is accurate — developers-page is backend API docs, not the integration credentials flow people expect (Stripe-like “create key → use key on API”).
```

```markdown
- developers-page can remain public docs + deep link “Manage keys in console.”
...
14. developers-page: “Authentication” guide per product + link to console; optional authenticated key page later.
```

**File map:**

```markdown
| `apps/developers-page/app/page.tsx` | Module cards → Scalar only. |
| `apps/developers-page/app/lhdn/route.ts` | LHDN OpenAPI Scalar. |
| `apps/developers-page/lib/openapi.ts` | Loads dist YAML. |
| `apps/ops-page/src/lib/api-client.ts` | Cookie session + X-Tenant-Id. |
| `apps/ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx` | Webhooks only. |
| `apps/ops-page/src/App.tsx` / `Sidebar.tsx` | Developer = webhooks + logs. |
```

```markdown
- **developers-page does not generate credentials**; ops Developer is webhooks.
```

### 12.5 `docs/001-gaps/13-typespec-api-contracts.md`

**Classification:** **optional**

**Quotes:**

```markdown
**Sources:** `packages/api-spec/**`, ... `apps/developers-page`, `task gen` pipeline.
```

```markdown
ADR 005 claims `dist/` is gitignored/transient; **`dist/**/openapi.yaml` is present and used by developers-page**.
```

```markdown
| Product | docs-*.tsp | package.json build | dist artifact | developers-page route | Landing card |
```

```markdown
`.../apps/developers-page/app/page.tsx` exposes only One, Ops, Billing, LHDN.
```

```markdown
- `apps/ops-page`
- `apps/superadmin-page`
- `apps/portal-page` (checkout + community portal)
...
**Not consumed by:** messaging UI (n/a), developers-page (reads YAML directly).
```

```markdown
| — | `POST /chat/stream` (SSE) | **Impl-only** (ops-page uses it) |
```

```markdown
| `apps/developers-page/**` | Solid loader pattern; missing commerce; trusts dist YAML. |
```

Also cites portal-page code fences for CommunityPortalView — historical path evidence.

### 12.6 `docs/001-gaps/19-frontend-backend-integration.md`

**Classification:** **optional**

**Quotes:**

```markdown
Cross-cutting read of `apps/ops-page/src/`, `apps/portal-page/src/`, `apps/superadmin-page/src/`, ...
```

```markdown
| Typed client | `openapi-fetch` + `@repo/api-types-ts` in `.../apps/ops-page/src/lib/api-client.ts` |
```

```markdown
| **ops-page** | HttpOnly cookie `lazuar_auth` | ... |
| **superadmin-page** | HttpOnly cookie `lazuar_admin_auth` | ... |
| **portal-page (SSR)** | Forwards `lazuar_auth` if present | ... |
| **portal-page (customer)** | Portal **token query param** | ... |
```

Extensive absolute path file maps under `.../ops-page/`, `.../portal-page/`, `.../superadmin-page/` (api-client, payment settings, dunning pages, etc.). All evidence paths; optional bulk replace.

### 12.7 `docs/001-gaps/18-outbound-customer-webhooks.md`

**Classification:** **optional**

**Quotes:**

```markdown
**No UI** in ops-page for LHDN webhook management (API/SDK only).
...
### One / Developer (ops-page)
...
| Public docs / developers-page content | Product OpenAPI via Scalar; **no webhook guide** / examples in repo docs |
...
   - Document verification algorithm next to developers-page.
...
Filter per endpoint. Document on developers-page with sample signatures.
```

Absolute paths:

```markdown
| `.../apps/ops-page/src/modules/commerce/components/ProductForm.tsx` | ... |
| `.../apps/ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx` | ... |
| `.../apps/ops-page/src/modules/workspace/pages/DeliveryLogsPage.tsx` | ... |
| `.../apps/ops-page/src/components/Sidebar.tsx` | Developer nav |
```

### 12.8 `docs/001-gaps/07-commerce-module.md`

**Classification:** **optional**

**Quotes:**

```markdown
- **ops-page:** dashboard, products, subscribers, transactions, coupons, dunning, custom checkouts (“quotes”)
- **portal-page:** public product checkout success, portal list, cancel plan (calls missing APIs)
```

```markdown
| POST | `/{tenantSlug}/portal/cancel` | **Missing** | Yes | **portal-page Cancel Plan** |
...
| `cancelPortalSubscription` | Cancel Plan broken in portal-page |
```

```markdown
| `apps/ops-page/.../SubscribersPage.tsx` | ... |
| `apps/portal-page/.../portal/page.tsx` | Cancel Plan → missing API |
| `apps/ops-page` commerce pages | ... |
```

### 12.9 `docs/001-gaps/09-lhdn-module.md`

**Classification:** **optional**

**Quotes:**

```markdown
**Scope:** ... ops-page invoicing UI, and Architecture ADRs 009–011 / 020–021.
...
| Developers portal | `apps/developers-page/app/lhdn/` |
...
- Product docs: `docs-lhdn.tsp` + developers-page Scalar at `/lhdn`.
...
| `ops-page/.../TaxInvoiceDetailPanel.tsx` | ... |
| `developers-page/app/lhdn/route.ts` | OpenAPI reference for LHDN product |
```

### 12.10 `docs/001-gaps/20-architecture-intent-vs-implementation.md`

**Classification:** **optional**

**Quotes:**

```markdown
| Dunning campaigns CRUD + defaults | **Yes** | `DunningCampaign` aggregate, admin endpoints, ops-page builder |
```

```markdown
3. **Developer hub omits Commerce** despite `docs-commerce.tsp` existing; ... `developers-page` only lists One, Ops, Billing, LHDN.
```

```markdown
11. Wire **`docs-commerce.tsp`** into `api-spec` build + developers-page route `/commerce`.
...
17. Delete frontend orphans (`portal-page` community modules, telegram fields).
...
Not primary backend solidification; ops-page dunning UI is live.
```

Also quotes ADR 007 `developers.lazuar.com/...` as intent language (hostname, not folder).

### 12.11 `docs/001-gaps/16-testing-coverage.md`

**Classification:** **optional**

**Quotes:**

```markdown
| `apps/ops-page` | No `test` script; no vitest/jest/playwright |
| `apps/portal-page` | No test tooling |
| `apps/superadmin-page` | No test tooling |
| `apps/developers-page` | No tests |
```

```markdown
| `ops-page` | Vite/React | **None** | ... |
| `portal-page` | Next.js | **None** | ... |
| `superadmin-page` | Vite/React | **None** | ... |
| `developers-page` | Next.js | **None** | ... | OpenAPI docs only |
```

### 12.12 `docs/001-gaps/01-dunning-engine.md`

**Classification:** **optional**

**Quotes:**

```markdown
| List page | `apps/ops-page/src/modules/commerce/pages/DunningCampaignsPage.tsx` |
| Routes | `apps/ops-page/src/App.tsx` |
```

Only two exact path hits in this large file (other dunning content is backend-focused).

### 12.13 `docs/001-gaps/06-payments-module.md`

**Classification:** **optional**

**Quotes:**

```markdown
| **UI (ops)** | `apps/ops-page/.../PaymentSettingsPage.tsx` | BYOK credential vault UI |
...
| `apps/ops-page/.../PaymentSettingsPage.tsx` | Multi-gateway BYOK UX; client-side Billplz validation. |
```

### 12.14 `docs/001-gaps/08-communications-module.md`

**Classification:** **optional**

**Quotes:**

```markdown
**Related:** ... ops-page templates/email settings, TypeSpec contracts.
...
### Frontend (ops-page)
...
| `apps/ops-page/.../TemplatesPage.tsx` | ... |
| `apps/ops-page/.../EmailSettingsPage.tsx` | ... |
| `apps/ops-page/.../dunning/DunningStepEditor.tsx` | ... |
```

### 12.15 `docs/001-gaps/11-ops-crm-messaging.md`

**Classification:** **optional**

**Quotes:**

```markdown
... cross-module consumers (Commerce, Communications, One, Billing, ops-page).
...
| **Frontend** | `apps/ops-page` consumes stream, conversations, execute-action, resolve | Primary consumer |
...
| **API maturity** | High (used by ops-page) | None | Token endpoint only |
...
| `apps/ops-page/src/hooks/use-chat-stream.ts` | Real Ops consumer |
```

Note: “Ops” as **backend module name** must not be confused with `ops-page` app rename — only the **app** becomes `lazuar-ops`; Modules/Ops stays.

---

## 13. App-local markdown under `apps/*-page`

### 13.1 `apps/developers-page/README.md`

**Classification:** **optional** (stock Next.js boilerplate; **no** monorepo path tokens)

Will **move** with directory rename to `apps/lazuar-spec/README.md`. Opportunity to replace boilerplate with product README stating:

- Role: Developer Hub / Scalar OpenAPI
- Relation to `packages/api-spec` and `apps/lazuar-docs`
- Local: `pnpm --filter lazuar-spec dev`
- Prod: `/docs` on hub host

### 13.2 `apps/developers-page/AGENTS.md` / `CLAUDE.md`

**Classification:** **moves with folder**; no token hits

Content is Next.js version warning only — no rename text.

### 13.3 `apps/ops-page/README.md` / `apps/superadmin-page/README.md`

**Classification:** **optional**

Both currently:

```markdown
# Ops 
```

(stub). After rename, titles should become “Lazuar Ops” / “Lazuar Admin” respectively; superadmin stub currently mislabeled “Ops”.

### 13.4 `apps/portal-page/README.md`

**Classification:** **optional** (stock Next.js; no tokens)

---

## 14. Hostnames vs folder names (documentation clarity)

Several docs mix **DNS product hosts** with **monorepo folder names**. Only folder names are in this rename proposal unless infra also renames services.

| Kind | Examples | Affected by this rename? |
|------|----------|---------------------------|
| Folder / app id | `apps/ops-page`, `pnpm --filter developers-page` | **Yes** |
| Docker/Caddy service | `reverse_proxy ops-page:3000` (ADR 016) | Only if compose service renames (infra plan) |
| Public hostname | `ops.lazuar.com`, `portal.lazuar.com`, `admin.lazuar.com` | **No** (unless separate DNS plan) |
| Path mount | `/docs` for developers hub (deploy/prod README) | **No** (good — product URL stable) |
| Product prose | “Developers hub”, “Hub Ops” | **Optional** reword only |

`deploy/prod/README.md` already uses soft names (`ops`, `portal`, `superadmin`, “developer API docs”) without `-page` — low conflict after rename.

---

## 15. Collision risks specific to documentation

### 15.1 `lazuar-spec` vs `packages/api-spec`

Documented co-occurrence is frequent, e.g. checklist:

```markdown
**Apps:** `developers-page`, `packages/api-spec`
```

After rename, naive readers may parse `lazuar-spec` + `api-spec` as duplicates. Living docs must keep them adjacent with role labels.

### 15.2 `lazuar-ops` vs backend `Modules/Ops`

Gap docs say both “ops-page” (frontend) and “Ops module” (backend). After rename, prefer:

- App: `lazuar-ops` / “Ops console”
- Backend: `Modules/Ops` / “Ops agent”

Avoid shortening both to bare “ops” in the same paragraph.

### 15.3 `lazuar-portal` vs product word “portal” (buyer portal routes)

`portal-page` is the app; `/portal` is also a path prefix in deploy. Docs should keep:

- App: `lazuar-portal`
- Routes: `/{tenantSlug}/portal/...`, deploy path `/portal`

### 15.4 `lazuar-admin` vs cookie `lazuar_admin_auth` / path `/admin/*`

Gap 19 documents cookie `lazuar_admin_auth` and deploy path `/admin/*`. Folder rename to `lazuar-admin` is **aligned** with those names — good for docs consistency.

---

## 16. Priority matrix for documentation workstream

### P0 — update before or with folder rename (broken commands / onboarding)

1. Root `README.md` — structure, key separation, ports; **add** `lazuar-spec`
2. `apps/lazuar-docs/docs/reference/openapi.md` — `pnpm --filter …`
3. `apps/lazuar-docs/README.md`, `docs/index.md`, `guide/how-to-maintain.md`
4. `docs/contracts/openapi-vs-minimal-api.md`
5. `plans/001-backend/001-backend-solidification-checklist.md` app references

### P1 — active SOP ADRs (annotate or path-fix)

6. ADR 013 (ops module SOP paths)
7. ADR 017 (portal structure paths)
8. ADR 007 (implementation guide paths) — banner + path examples

### P2 — optional historical cleanup

9. Entire `docs/001-gaps/**` token replace **or** single rename-map banner on `docs/001-gaps/README.md`
10. Remaining ADRs 014, 016, 018, 022, 023 — leave or footnote
11. App README rewrites when folders move
12. Soft prose: “developers page” → “Developer Hub” in payments-cashier + quickstart

### Explicitly out of documentation-only scope (do not treat as done by this file)

- `package.json` names, `pnpm-workspace.yaml`, `turbo.json`, `mprocs-dev.yaml`
- `docker-bake.hcl`, Dockerfiles, Caddyfile service names
- Source imports, `basePath`, env samples
- Renaming gap file `04-developers-page-dx.md` (optional, not required)

---

## 17. Suggested living-docs language after rename

Use this glossary in README / lazuar-docs:

| Monorepo path | User-facing name | Role |
|---------------|------------------|------|
| `apps/lazuar-api` | Lazuar API | Modular monolith |
| `apps/lazuar-ops` | Ops / Console | Creator + staff superapp (Vite) |
| `apps/lazuar-portal` | Portal / Checkout | Buyer checkout + portal (Next) |
| `apps/lazuar-admin` | Admin / Superadmin | Platform control plane (Vite) |
| `apps/lazuar-spec` | Developers / API Reference | Scalar OpenAPI hub |
| `apps/lazuar-docs` | Hub Docs | VitePress integrator guides |
| `packages/api-spec` | API Spec (TypeSpec) | Contract SSoT + `task gen` |

**Anti-pattern to avoid in docs:** calling `lazuar-spec` “the API spec package” or calling `packages/api-spec` “the developers page”.

---

## 18. Files explicitly searched with **zero** exact-token hits (focus areas)

Recorded for completeness so the rename workstream does not re-scan blindly:

| Path | Result |
|------|--------|
| Root `CLAUDE.md` / `AGENTS.md` | Files not present |
| `idea/**` | No matches |
| `script/second-app-proof.md` | No exact folder tokens |
| `apps/lazuar-api/docs/**` | No matches |
| `packages/**/*.md` | No matches |
| `docs/api-versioning.md` | No matches |
| `docs/lhdn/**` | No matches |
| `docs/xml/**` | No matches |
| Most ADRs (001–006, 009–012, 015, 019–021) | No folder tokens (some use hostnames only) |
| Gap reports 02, 05, 10, 12, 14, 15, 17, 21 | No folder tokens |
| `deploy/prod/README.md` | Soft names only (ops/portal/admin/docs) |
| `plans/001-backend/README.md` | No folder tokens |

---

## 19. Summary counts (documentation only)

| Classification | Approx. files |
|----------------|---------------|
| **must update** | ~8 living files (README, lazuar-docs set, contracts, checklist) |
| **historical ADR keep-as-is** (default) | 8 ADRs with hits; 2 of those also **annotate** if used as SOP |
| **optional** gap / snapshot | ~15 gap files with hits + soft-prose integrator docs |
| **moves with folder, no token work** | app AGENTS/CLAUDE/stock READMEs |
| **no action** | idea, second-app-proof, many ADRs/gaps without tokens |

**Biggest product-docs risk:** not the ops/portal/admin renames (straightforward), but **`developers-page` → `lazuar-spec`** colliding with **`packages/api-spec`** in every sentence that discusses the TypeSpec → OpenAPI → Scalar pipeline.

**Biggest broken-command risk:** `pnpm --filter developers-page` in `apps/lazuar-docs/docs/reference/openapi.md`.

**Biggest historical surface:** `docs/001-gaps/04-developers-page-dx.md` and related credential/typespec gap reports — leave as archaeology or banner-map.

---

## 20. Recommended one-banner for historical trees (copy-paste)

For `docs/001-gaps/README.md` and/or top of ADRs that keep old paths:

```markdown
> **Monorepo app rename (plan 002-change-name):**  
> `developers-page` → `lazuar-spec` (Developer Hub / Scalar) ·  
> `ops-page` → `lazuar-ops` ·  
> `portal-page` → `lazuar-portal` ·  
> `superadmin-page` → `lazuar-admin`.  
> TypeSpec SSoT remains `packages/api-spec` (unchanged).  
> Historical paths below may still use pre-rename names.
```

---

*End of documentation-only impact analysis. No application code or config was modified for this report.*
