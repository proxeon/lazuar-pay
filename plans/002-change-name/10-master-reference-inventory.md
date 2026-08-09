# Master reference inventory — frontend app renames

**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Inventory date:** 2026-08-08  
**Scope:** Exhaustive content search for old app names (excluding `node_modules`, and treating `bin/`, `obj/`, `.next/`, `dist/` build trees as non-source). No application code was modified for this inventory.

## Proposed renames

| Old name (dir / package / compose key) | New name |
|----------------------------------------|----------|
| `developers-page` | `lazuar-spec` |
| `ops-page` | `lazuar-ops` |
| `portal-page` | `lazuar-portal` |
| `superadmin-page` | `lazuar-admin` |

## Search methodology

| Pattern | Result |
|---------|--------|
| `developers-page` | Matches (see §1) |
| `ops-page` | Matches (see §2) |
| `portal-page` | Matches (see §3) |
| `superadmin-page` | Matches (see §4) |
| `developers_page` / `ops_page` / `portal_page` / `superadmin_page` | **Zero matches** in repo (excluding node_modules) |
| `package.json` `"name"` fields | Equal to directory basenames (see Package identity) |
| GHCR image names | `lazuar-hub-{ops,portal,superadmin,developers}` — **no `-page` suffix** (see §5) |
| Prod Docker service names | Short names `ops`, `portal`, `superadmin`, `developers` (see §6) |

**Exclusions applied mentally / by tool:** `**/node_modules/**`. Build artifacts under `bin/`, `obj/`, `.next/`, `dist/` were not treated as rename sources (none of the name strings appear as unique content hits outside path-based Docker COPY of source trees).

---

## Package identity (from each app `package.json`)

| App directory | `"name"` field | Matches proposed old name? | Action on rename |
|---------------|----------------|----------------------------|------------------|
| `apps/developers-page` | `developers-page` | Yes | **must-change** (name + dir) |
| `apps/ops-page` | `ops-page` | Yes | **must-change** (name + dir) |
| `apps/portal-page` | `portal-page` | Yes | **must-change** (name + dir) |
| `apps/superadmin-page` | `superadmin-page` | Yes | **must-change** (name + dir) |

`pnpm-workspace.yaml` uses `apps/*` (no hard-coded package names).  
Root `package.json`, `turbo.json`, `Taskfile.yml`: **no** hard-coded `*-page` package names.

---

## Filesystem (directories that are the apps)

These directories **are** the rename targets (git mv / rename):

| Path | New path | Action |
|------|----------|--------|
| `apps/developers-page/` | `apps/lazuar-spec/` | **must-change** |
| `apps/ops-page/` | `apps/lazuar-ops/` | **must-change** |
| `apps/portal-page/` | `apps/lazuar-portal/` | **must-change** |
| `apps/superadmin-page/` | `apps/lazuar-admin/` | **must-change** |

Historical path strings inside some gap docs still say `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/...` (old monorepo root name) — treat as **historical** docs content, still update if keeping docs accurate.

---

## §1 — `developers-page` → `lazuar-spec`

### §1.1 Must-change / operational references

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `apps/developers-page/package.json` | `developers-page` | `"name": "developers-page",` | config | **must-change** |
| `apps/developers-page/Dockerfile` | `developers-page` | `COPY apps/developers-page/package.json apps/developers-page/` | docker | **must-change** |
| `apps/developers-page/Dockerfile` | `developers-page` | `RUN pnpm install --filter ./apps/developers-page... --filter @repo/api-spec... --frozen-lockfile` | docker | **must-change** |
| `apps/developers-page/Dockerfile` | `developers-page` | `COPY apps/developers-page apps/developers-page` | docker | **must-change** |
| `apps/developers-page/Dockerfile` | `developers-page` | `RUN pnpm --filter ./apps/developers-page build` | docker | **must-change** |
| `apps/developers-page/Dockerfile` | `developers-page` | `COPY --from=build ... /app/apps/developers-page/.next/standalone ./` | docker | **must-change** |
| `apps/developers-page/Dockerfile` | `developers-page` | `COPY --from=build ... /app/apps/developers-page/.next/static ./apps/developers-page/.next/static` | docker | **must-change** |
| `apps/developers-page/Dockerfile` | `developers-page` | `COPY --from=build ... /app/apps/developers-page/public ./apps/developers-page/public` | docker | **must-change** |
| `apps/developers-page/Dockerfile` | `developers-page` | `CMD ["node", "apps/developers-page/server.js"]` | docker | **must-change** |
| `docker-bake.hcl` | `developers-page` | `targets = ["api", "portal-page", "ops-page", "superadmin-page", "developers-page"]` | docker | **must-change** |
| `docker-bake.hcl` | `developers-page` | `target "developers-page" {` | docker | **must-change** |
| `docker-bake.hcl` | `developers-page` | `dockerfile = "apps/developers-page/Dockerfile"` | docker | **must-change** |
| `mprocs-dev.yaml` | `developers-page` | `developers-page:` | config | **must-change** |
| `mprocs-dev.yaml` | `developers-page` | `shell: cd apps/developers-page && pnpm dev` | config | **must-change** |
| `.github/workflows/ghcr.yml` | `developers-page` | `dockerfile: apps/developers-page/Dockerfile` | ci | **must-change** |
| `pnpm-lock.yaml` | `developers-page` | `apps/developers-page:` | lockfile | **must-change** (regenerate via install after rename) |

**Note:** Root `docker-compose.yml` and `docker-compose.ghcr.yml` do **not** define a `developers-page` service today (developers is only in prod deploy compose as short name `developers`). Bake + GHCR workflow **do** build the image.

### §1.2 Docs / narrative references (developers-page)

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `README.md` | *(no `developers-page` string)* | (structure omits developers hub) | docs | **review** (add new name if documenting structure) |
| `apps/lazuar-docs/README.md` | `developers-page` | `\| \`apps/developers-page\` \| Live Scalar OpenAPI \|` | docs | **review** / docs-only |
| `apps/lazuar-docs/docs/reference/openapi.md` | `developers-page` | `Run **developers-page** in the monorepo:` | docs | **review** / docs-only |
| `apps/lazuar-docs/docs/reference/openapi.md` | `developers-page` | `pnpm --filter developers-page dev` | docs | **must-change** if filter name changes (command must work) |
| `apps/lazuar-docs/docs/index.md` | `developers-page` | `Scalar OpenAPI is under **developers-page** (\`/payments\`)` | docs | docs-only |
| `apps/lazuar-docs/docs/guide/how-to-maintain.md` | `developers-page` | `Point nav “Developers (Scalar)” at production developers-page URL.` | docs | docs-only |
| `docs/001-gaps/00-what-we-need-to-do-next.md` | `developers-page` | `` `04-developers-page-dx.md` `` | docs | **historical** / docs-only |
| `docs/001-gaps/00-what-we-need-to-do-next.md` | `developers-page` | `\| [04-developers-page-dx.md](./04-developers-page-dx.md) \|` | docs | **historical** (filename itself) |
| `docs/001-gaps/03-api-auth-credentials.md` | `developers-page` | `App: \`.../apps/developers-page\`` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `developers-page` | `developers-page is backend API docs...` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `developers-page` | `developers-page can remain public docs...` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `developers-page` | `14. developers-page: “Authentication” guide...` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `developers-page` | `\| \`apps/developers-page/app/page.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `developers-page` | `\| \`apps/developers-page/app/lhdn/route.ts\` \|` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `developers-page` | `\| \`apps/developers-page/lib/openapi.ts\` \|` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `developers-page` | `developers-page does not generate credentials` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | *(filename + many body hits — primary gap report)* | docs | **historical** (consider leave filename; body paths **review**) |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `.../apps/developers-page` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `Landing (\`apps/developers-page/app/page.tsx\`)` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `Spec loader (\`apps/developers-page/lib/openapi.ts\`)` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `Explicit non-goals of current developers-page` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `omits \`developers-page\` from project structure` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `└─► developers-page Scalar routes` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `\`\`\`1:14:apps/developers-page/app/billing/route.ts` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `\| developers-page \| No \| Read-only docs \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `No MDX/guides under \`developers-page\`` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `developers-page currently documents **backend API**` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `Make developers-page an integration hub` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `so developers-page never ships Ops chat` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `### \`apps/developers-page/\`` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `Defines developers-page pattern` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `\| \`docker-bake.hcl\` \| \`developers-page\` target` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `developers-page` | `Local developers-page process` | docs | docs-only |
| `docs/001-gaps/09-lhdn-module.md` | `developers-page` | `\| Developers portal \| \`apps/developers-page/app/lhdn/\` \|` | docs | docs-only |
| `docs/001-gaps/09-lhdn-module.md` | `developers-page` | `developers-page Scalar at \`/lhdn\`` | docs | docs-only |
| `docs/001-gaps/09-lhdn-module.md` | `developers-page` | `\| \`developers-page/app/lhdn/route.ts\` \|` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `developers-page` | `\`apps/developers-page\`, \`task gen\` pipeline` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `developers-page` | `used by developers-page` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `developers-page` | `developers-page route \| Landing card` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `developers-page` | `.../apps/developers-page/app/page.tsx` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `developers-page` | `developers-page (reads YAML directly)` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `developers-page` | `\| \`apps/developers-page/**\` \|` | docs | docs-only |
| `docs/001-gaps/16-testing-coverage.md` | `developers-page` | `\| \`apps/developers-page\` \| No tests \|` | docs | docs-only |
| `docs/001-gaps/16-testing-coverage.md` | `developers-page` | `\| \`developers-page\` \| Next.js \| **None** \|` | docs | docs-only |
| `docs/001-gaps/18-outbound-customer-webhooks.md` | `developers-page` | `Public docs / developers-page content` | docs | docs-only |
| `docs/001-gaps/18-outbound-customer-webhooks.md` | `developers-page` | `next to developers-page` | docs | docs-only |
| `docs/001-gaps/18-outbound-customer-webhooks.md` | `developers-page` | `Document on developers-page with sample signatures` | docs | docs-only |
| `docs/001-gaps/20-architecture-intent-vs-implementation.md` | `developers-page` | `\`developers-page\` only lists One, Ops, Billing, LHDN` | docs | docs-only |
| `docs/001-gaps/20-architecture-intent-vs-implementation.md` | `developers-page` | `developers-page route \`/commerce\`` | docs | docs-only |
| `docs/001-gaps/README.md` | `developers-page` | `developers-page, integration credentials` | docs | docs-only |
| `docs/001-gaps/README.md` | `developers-page` | `[04-developers-page-dx.md](./04-developers-page-dx.md)` | docs | **historical** (filename link) |
| `docs/architecture-decision-log/007-product-scoped-api-references.md` | `developers-page` | `within our \`developers-page\` Next.js application` | docs | docs-only |
| `docs/architecture-decision-log/007-product-scoped-api-references.md` | `developers-page` | `In the \`developers-page\` Next.js app` | docs | docs-only |
| `docs/architecture-decision-log/007-product-scoped-api-references.md` | `developers-page` | `**File:** \`apps/developers-page/app/vault/route.ts\`` | docs | docs-only |
| `docs/architecture-decision-log/007-product-scoped-api-references.md` | `developers-page` | `**File:** \`apps/developers-page/app/page.tsx\`` | docs | docs-only |
| `docs/contracts/openapi-vs-minimal-api.md` | `developers-page` | `ops-page, portal-page, developers-page, or SDKs` | docs | docs-only |
| `plans/001-backend/001-backend-solidification-checklist.md` | `developers-page` | `**Apps:** \`developers-page\`, \`packages/api-spec\`` | docs | docs-only / **historical** |
| `plans/001-backend/001-backend-solidification-checklist.md` | `developers-page` | `exist in \`apps/developers-page\`` | docs | docs-only |
| `plans/001-backend/001-backend-solidification-checklist.md` | `developers-page` | `api-spec + developers-page` | docs | docs-only |

### §1.3 Related filename (not a package string, but rename-adjacent)

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `docs/001-gaps/04-developers-page-dx.md` | filename contains `developers-page` | (file name) | docs | **historical** — optional rename to `04-lazuar-spec-dx.md` + update links |

### §1.4 developers-page — occurrence count (content lines)

Approximately **75** matching lines for pattern `developers-page` (first full ripgrep pass).

---

## §2 — `ops-page` → `lazuar-ops`

### §2.1 Must-change / operational references

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `apps/ops-page/package.json` | `ops-page` | `"name": "ops-page",` | config | **must-change** |
| `apps/ops-page/Dockerfile` | `ops-page` | `COPY apps/ops-page/package.json apps/ops-page/` | docker | **must-change** |
| `apps/ops-page/Dockerfile` | `ops-page` | `RUN pnpm install --filter ./apps/ops-page... --frozen-lockfile` | docker | **must-change** |
| `apps/ops-page/Dockerfile` | `ops-page` | `COPY apps/ops-page apps/ops-page` | docker | **must-change** |
| `apps/ops-page/Dockerfile` | `ops-page` | `RUN pnpm --filter ./apps/ops-page build` | docker | **must-change** |
| `apps/ops-page/Dockerfile` | `ops-page` | `COPY --from=build ... /app/apps/ops-page/dist ./dist` | docker | **must-change** |
| `docker-bake.hcl` | `ops-page` | `targets = [..., "ops-page", ...]` | docker | **must-change** |
| `docker-bake.hcl` | `ops-page` | `target "ops-page" {` | docker | **must-change** |
| `docker-bake.hcl` | `ops-page` | `dockerfile = "apps/ops-page/Dockerfile"` | docker | **must-change** |
| `docker-compose.yml` | `ops-page` | `ops-page:` (service key) | docker | **must-change** |
| `docker-compose.yml` | `ops-page` | `dockerfile: apps/ops-page/Dockerfile` | docker | **must-change** |
| `docker-compose.ghcr.yml` | `ops-page` | `ops-page:` | docker | **must-change** |
| `mprocs-dev.yaml` | `ops-page` | `ops-page:` | config | **must-change** |
| `mprocs-dev.yaml` | `ops-page` | `shell: cd apps/ops-page && pnpm dev` | config | **must-change** |
| `.github/workflows/ghcr.yml` | `ops-page` | `dockerfile: apps/ops-page/Dockerfile` | ci | **must-change** |
| `pnpm-lock.yaml` | `ops-page` | `apps/ops-page:` | lockfile | **must-change** (regenerate) |

### §2.2 Code comments / path headers inside apps

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `apps/ops-page/src/hooks/use-chat-stream.ts` | `ops-page` | `// apps/ops-page/src/hooks/use-chat-stream.ts` | code | **review** (comment-only) |
| `apps/ops-page/src/hooks/use-debounce.ts` | `ops-page` | `// apps/ops-page/src/hooks/use-debounce.ts` | code | **review** |
| `apps/ops-page/src/components/OpsChatWorkspace.tsx` | `ops-page` | `// apps/ops-page/src/components/OpsChatWorkspace.tsx` | code | **review** |
| `apps/ops-page/src/components/chat/ChatMessageBubble.tsx` | `ops-page` | `// apps/ops-page/src/components/chat/ChatMessageBubble.tsx` | code | **review** |
| `apps/ops-page/src/components/chat/MarkdownContent.tsx` | `ops-page` | `// apps/ops-page/src/components/chat/MarkdownContent.tsx` | code | **review** |
| `apps/ops-page/src/components/forms/AutoForm.tsx` | `ops-page` | `// apps/ops-page/src/components/chat/AutoForm.tsx` | code | **review** |
| `apps/ops-page/src/types/chat.ts` | `ops-page` | `// apps/ops-page/src/types/chat.ts` | code | **review** |
| `apps/superadmin-page/src/hooks/use-debounce.ts` | `ops-page` | `// apps/ops-page/src/hooks/use-debounce.ts` | code | **review** (stale copy-from-ops header) |
| `apps/superadmin-page/src/types/chat.ts` | `ops-page` | `// apps/ops-page/src/types/chat.ts` | code | **review** (stale copy-from-ops header) |

### §2.3 Backend comments (word “ops-page” as product surface)

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs` | `ops-page` | `// Ensure superadmin can open ops-page (memberships drive /me/entitlements...)` | code | **review** |
| `apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs` | `ops-page` | `// Platform superadmins can operate any active workspace (ops-page requires ≥1 entitlement).` | code | **review** |

### §2.4 Docs / narrative references (ops-page)

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `README.md` | `ops-page` | `**\`ops-page\` (Admin):** The AWS-style superapp...` | docs | docs-only |
| `README.md` | `ops-page` | `│   ├── ops-page/         # The Back-Office...` | docs | docs-only |
| `README.md` | `ops-page` | `\| \`ops-page\` \| 3003 \| ...` | docs | docs-only |
| `docs/001-gaps/01-dunning-engine.md` | `ops-page` | `\| List page \| \`apps/ops-page/src/modules/commerce/pages/DunningCampaignsPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/01-dunning-engine.md` | `ops-page` | `\| Routes \| \`apps/ops-page/src/App.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `ops-page` | `(\`apps/ops-page/src/lib/api-client.ts\`)` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `ops-page` | `\| \`apps/ops-page/src/lib/api-client.ts\` \|` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `ops-page` | `\| \`apps/ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/03-api-auth-credentials.md` | `ops-page` | `\| \`apps/ops-page/src/App.tsx\` / \`Sidebar.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `### Adjacent “developer” surfaces (ops-page)` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `(\`apps/ops-page/src/components/Sidebar.tsx\`)` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `\| ops-page Developer \| **No** \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `\| ops-page Payment Settings \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `\| ops-page Email Settings \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `**No ops-page UI** was found for certificate upload` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `ops-page, internal APIs` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `**Ship API Keys UI in ops-page**` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `\| \`apps/ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `\| \`apps/ops-page/src/modules/workspace/pages/DeliveryLogsPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `\| \`apps/ops-page/src/App.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `\| \`apps/ops-page/src/components/Sidebar.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `\| \`apps/ops-page/src/modules/workspace/pages/PaymentSettingsPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `ops-page` | `\| \`apps/ops-page/src/modules/workspace/pages/EmailSettingsPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/06-payments-module.md` | `ops-page` | `\| **UI (ops)** \| \`apps/ops-page/.../PaymentSettingsPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/06-payments-module.md` | `ops-page` | `\| \`apps/ops-page/.../PaymentSettingsPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/07-commerce-module.md` | `ops-page` | `**ops-page:** dashboard, products, subscribers...` | docs | docs-only |
| `docs/001-gaps/07-commerce-module.md` | `ops-page` | `\| \`apps/ops-page/.../SubscribersPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/07-commerce-module.md` | `ops-page` | `\| \`apps/ops-page\` commerce pages \|` | docs | docs-only |
| `docs/001-gaps/08-communications-module.md` | `ops-page` | `ops-page templates/email settings` | docs | docs-only |
| `docs/001-gaps/08-communications-module.md` | `ops-page` | `### Frontend (ops-page)` | docs | docs-only |
| `docs/001-gaps/08-communications-module.md` | `ops-page` | `\| \`apps/ops-page/.../TemplatesPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/08-communications-module.md` | `ops-page` | `\| \`apps/ops-page/.../EmailSettingsPage.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/08-communications-module.md` | `ops-page` | `\| \`apps/ops-page/.../dunning/DunningStepEditor.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/09-lhdn-module.md` | `ops-page` | `ops-page invoicing UI` | docs | docs-only |
| `docs/001-gaps/09-lhdn-module.md` | `ops-page` | `\| \`ops-page/.../TaxInvoiceDetailPanel.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/11-ops-crm-messaging.md` | `ops-page` | `Billing, ops-page).` | docs | docs-only |
| `docs/001-gaps/11-ops-crm-messaging.md` | `ops-page` | `\| **Frontend** \| \`apps/ops-page\` consumes stream...` | docs | docs-only |
| `docs/001-gaps/11-ops-crm-messaging.md` | `ops-page` | `High (used by ops-page)` | docs | docs-only |
| `docs/001-gaps/11-ops-crm-messaging.md` | `ops-page` | `\| \`apps/ops-page/src/hooks/use-chat-stream.ts\` \|` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `ops-page` | `- \`apps/ops-page\`` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `ops-page` | `**Impl-only** (ops-page uses it)` | docs | docs-only |
| `docs/001-gaps/16-testing-coverage.md` | `ops-page` | `\| \`apps/ops-page\` \| No \`test\` script...` | docs | docs-only |
| `docs/001-gaps/16-testing-coverage.md` | `ops-page` | `\| \`ops-page\` \| Vite/React \| **None** \|` | docs | docs-only |
| `docs/001-gaps/18-outbound-customer-webhooks.md` | `ops-page` | `**No UI** in ops-page for LHDN webhook management` | docs | docs-only |
| `docs/001-gaps/18-outbound-customer-webhooks.md` | `ops-page` | `### One / Developer (ops-page)` | docs | docs-only |
| `docs/001-gaps/18-outbound-customer-webhooks.md` | `ops-page` | `.../apps/ops-page/src/modules/commerce/components/ProductForm.tsx` | docs | docs-only |
| `docs/001-gaps/18-outbound-customer-webhooks.md` | `ops-page` | `.../apps/ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx` | docs | docs-only |
| `docs/001-gaps/18-outbound-customer-webhooks.md` | `ops-page` | `.../apps/ops-page/src/modules/workspace/pages/DeliveryLogsPage.tsx` | docs | docs-only |
| `docs/001-gaps/18-outbound-customer-webhooks.md` | `ops-page` | `.../apps/ops-page/src/components/Sidebar.tsx` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `Cross-cutting read of \`apps/ops-page/src/\`...` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../apps/ops-page/src/lib/api-client.ts` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `\| **ops-page** \| HttpOnly cookie \`lazuar_auth\` \|` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../apps/ops-page/src/components/PaymentSettingsModal.tsx` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../apps/ops-page/src/modules/workspace/components/PaymentSettingsModal.tsx` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../ops-page/src/modules/workspace/pages/PaymentSettingsPage.tsx` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../ops-page/src/modules/workspace/pages/EmailSettingsPage.tsx` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../ops-page/src/modules/workspace/pages/DeliveryLogsPage.tsx` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../ops-page/src/modules/commerce/pages/DunningCampaignsPage.tsx` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../ops-page/src/modules/commerce/pages/CampaignBuilderPage.tsx` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../ops-page/src/modules/commerce/components/dunning/*` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `ops-page` | `.../ops-page/src/modules/commerce/pages/SubscribersPage.tsx` | docs | docs-only |
| `docs/001-gaps/20-architecture-intent-vs-implementation.md` | `ops-page` | `ops-page builder` | docs | docs-only |
| `docs/001-gaps/20-architecture-intent-vs-implementation.md` | `ops-page` | `ops-page dunning UI is live` | docs | docs-only |
| `docs/architecture-decision-log/013-frontend-module-implementation.md` | `ops-page` | `# ADR 013: Frontend Module Implementation (ops-page)` | docs | docs-only |
| `docs/architecture-decision-log/013-frontend-module-implementation.md` | `ops-page` | `to the \`ops-page\` Super App` | docs | docs-only |
| `docs/architecture-decision-log/013-frontend-module-implementation.md` | `ops-page` | `module to the \`ops-page\` frontend` | docs | docs-only |
| `docs/architecture-decision-log/013-frontend-module-implementation.md` | `ops-page` | `apps/ops-page/src/modules/funnel/` | docs | docs-only |
| `docs/architecture-decision-log/013-frontend-module-implementation.md` | `ops-page` | `Open \`apps/ops-page/src/App.tsx\`` | docs | docs-only |
| `docs/architecture-decision-log/013-frontend-module-implementation.md` | `ops-page` | `Open \`apps/ops-page/src/components/Sidebar.tsx\`` | docs | docs-only |
| `docs/architecture-decision-log/014-apps.md` | `ops-page` | `ops-page/src/modules/{appName}/` | docs | docs-only |
| `docs/architecture-decision-log/016-platform-domain-strategy.md` | `ops-page` | `reverse_proxy ops-page:3000` | docs | **review** (historical Caddy example; prod uses short name `ops`) |
| `docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md` | `ops-page` | `from their \`ops-page\` (Vite) dashboard` | docs | docs-only |
| `docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md` | `ops-page` | `[CREATOR] -> ops-page (Vite) ->` | docs | docs-only |
| `docs/architecture-decision-log/022-remove-community-vault-modules.md` | `ops-page` | `**ops-page:** Community Spaces & Vault entries removed` | docs | docs-only |
| `docs/architecture-decision-log/022-remove-community-vault-modules.md` | `ops-page` | `ops-page: \`modules/vault/\`...` | docs | docs-only |
| `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` | `ops-page` | `**1. Creator Dashboard (\`ops-page\`)**` | docs | docs-only |
| `docs/contracts/openapi-vs-minimal-api.md` | `ops-page` | `ops-page, portal-page, developers-page` | docs | docs-only |
| `docs/contracts/openapi-vs-minimal-api.md` | `ops-page` | `\| \`ops-page\` invoicing module...` | docs | docs-only |
| `docs/contracts/openapi-vs-minimal-api.md` | `ops-page` | `\| \`ops-page\` \`BillingProfilePage\` \|` | docs | docs-only |
| `docs/contracts/openapi-vs-minimal-api.md` | `ops-page` | `\| \`ops-page\` Ops chat...` | docs | docs-only |
| `plans/001-backend/001-backend-solidification-checklist.md` | `ops-page` | `**Apps:** \`ops-page\`` | docs | docs-only / historical |
| `plans/001-backend/001-backend-solidification-checklist.md` | `ops-page` | `not wired in ops-page` | docs | docs-only |
| `plans/001-backend/001-backend-solidification-checklist.md` | `ops-page` | `ops-page + portal-page` | docs | docs-only |

### §2.5 ops-page — occurrence count

Approximately **113** matching lines for pattern `ops-page`.

---

## §3 — `portal-page` → `lazuar-portal`

### §3.1 Must-change / operational references

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `apps/portal-page/package.json` | `portal-page` | `"name": "portal-page",` | config | **must-change** |
| `apps/portal-page/Dockerfile` | `portal-page` | `COPY apps/portal-page/package.json apps/portal-page/` | docker | **must-change** |
| `apps/portal-page/Dockerfile` | `portal-page` | `RUN pnpm install --filter ./apps/portal-page... --frozen-lockfile` | docker | **must-change** |
| `apps/portal-page/Dockerfile` | `portal-page` | `COPY apps/portal-page apps/portal-page` | docker | **must-change** |
| `apps/portal-page/Dockerfile` | `portal-page` | `RUN pnpm --filter ./apps/portal-page build` | docker | **must-change** |
| `apps/portal-page/Dockerfile` | `portal-page` | `COPY --from=build ... /app/apps/portal-page/.next/standalone ./` | docker | **must-change** |
| `apps/portal-page/Dockerfile` | `portal-page` | `COPY --from=build ... /app/apps/portal-page/.next/static ./apps/portal-page/.next/static` | docker | **must-change** |
| `apps/portal-page/Dockerfile` | `portal-page` | `COPY --from=build ... /app/apps/portal-page/public ./apps/portal-page/public` | docker | **must-change** |
| `apps/portal-page/Dockerfile` | `portal-page` | `CMD ["node", "apps/portal-page/server.js"]` | docker | **must-change** |
| `docker-bake.hcl` | `portal-page` | `targets = [..., "portal-page", ...]` | docker | **must-change** |
| `docker-bake.hcl` | `portal-page` | `target "portal-page" {` | docker | **must-change** |
| `docker-bake.hcl` | `portal-page` | `dockerfile = "apps/portal-page/Dockerfile"` | docker | **must-change** |
| `docker-compose.yml` | `portal-page` | `portal-page:` | docker | **must-change** |
| `docker-compose.yml` | `portal-page` | `dockerfile: apps/portal-page/Dockerfile` | docker | **must-change** |
| `docker-compose.ghcr.yml` | `portal-page` | `portal-page:` | docker | **must-change** |
| `mprocs-dev.yaml` | `portal-page` | `portal-page:` | config | **must-change** |
| `mprocs-dev.yaml` | `portal-page` | `shell: cd apps/portal-page && pnpm dev` | config | **must-change** |
| `.github/workflows/ghcr.yml` | `portal-page` | `dockerfile: apps/portal-page/Dockerfile` | ci | **must-change** |
| `pnpm-lock.yaml` | `portal-page` | `apps/portal-page:` | lockfile | **must-change** (regenerate) |

### §3.2 Code path-header comments

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `apps/portal-page/src/app/page.tsx` | `portal-page` | `// apps/portal-page/src/app/page.tsx` | code | **review** |
| `apps/portal-page/src/app/not-found.tsx` | `portal-page` | `// apps/portal-page/src/app/not-found.tsx` | code | **review** |
| `apps/portal-page/src/modules/core/lib/server-client.ts` | `portal-page` | `// apps/portal-page/src/modules/core/lib/server-client.ts` | code | **review** |
| `apps/portal-page/src/modules/checkout/components/PromoCodeInput.tsx` | `portal-page` | `// apps/portal-page/src/modules/checkout/components/PromoCodeInput.tsx` | code | **review** |
| `apps/portal-page/src/modules/checkout/components/CheckoutLayout.tsx` | `portal-page` | `// apps/portal-page/src/modules/checkout/components/CheckoutLayout.tsx` | code | **review** |
| `apps/portal-page/src/modules/checkout/components/IdentityBanner.tsx` | `portal-page` | `// apps/portal-page/src/modules/checkout/components/IdentityBanner.tsx` | code | **review** |

### §3.3 Docs / narrative references (portal-page)

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `README.md` | `portal-page` | `**\`portal-page\` (Checkout):** The headless cash register...` | docs | docs-only |
| `README.md` | `portal-page` | `│   ├── portal-page/      # The Cash Register...` | docs | docs-only |
| `README.md` | `portal-page` | `\| \`portal-page\`\| 3004 \| ...` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `portal-page` | `\| portal-page \| No \| Buyer checkout/portal only \|` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `portal-page` | `\| \`apps/portal-page/**\` \|` | docs | docs-only |
| `docs/001-gaps/07-commerce-module.md` | `portal-page` | `**portal-page:** public product checkout success...` | docs | docs-only |
| `docs/001-gaps/07-commerce-module.md` | `portal-page` | `**portal-page Cancel Plan**` | docs | docs-only |
| `docs/001-gaps/07-commerce-module.md` | `portal-page` | `Cancel Plan broken in portal-page` | docs | docs-only |
| `docs/001-gaps/07-commerce-module.md` | `portal-page` | `\| \`apps/portal-page/.../portal/page.tsx\` \|` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `portal-page` | `- \`apps/portal-page\` (checkout + community portal)` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `portal-page` | `.../apps/portal-page/src/modules/community/components/CommunityPortalView.tsx` | docs | docs-only |
| `docs/001-gaps/16-testing-coverage.md` | `portal-page` | `\| \`apps/portal-page\` \| No test tooling \|` | docs | docs-only |
| `docs/001-gaps/16-testing-coverage.md` | `portal-page` | `\| \`portal-page\` \| Next.js \| **None** \|` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `portal-page` | `\`apps/portal-page/src/\`` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `portal-page` | `\| **portal-page (SSR)** \|` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `portal-page` | `\| **portal-page (customer)** \|` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `portal-page` | `.../apps/portal-page/src/modules/core/lib/server-client.ts` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `portal-page` | `.../apps/portal-page/src/modules/checkout/lib/api.ts` | docs | docs-only |
| `docs/001-gaps/20-architecture-intent-vs-implementation.md` | `portal-page` | `Delete frontend orphans (\`portal-page\` community modules...)` | docs | docs-only |
| `docs/architecture-decision-log/016-platform-domain-strategy.md` | `portal-page` | `reverse_proxy portal-page:3000` | docs | **review** (historical; prod uses short name `portal`) |
| `docs/architecture-decision-log/017-portal-frontend-architecture.md` | `portal-page` | `Frontend Codebase Organization (\`portal-page\`)` | docs | docs-only |
| `docs/architecture-decision-log/017-portal-frontend-architecture.md` | `portal-page` | `for the \`portal-page\` codebase` | docs | docs-only |
| `docs/architecture-decision-log/017-portal-frontend-architecture.md` | `portal-page` | `apps/portal-page/` | docs | docs-only |
| `docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md` | `portal-page` | `core transactional engine (\`portal-page\`)` | docs | docs-only |
| `docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md` | `portal-page` | `\`portal-page\` remains purely transactional` | docs | docs-only |
| `docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md` | `portal-page` | `link directly to our \`portal-page\` checkout` | docs | docs-only |
| `docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md` | `portal-page` | `[BUYER]   -> portal-page (SSR) ->` | docs | docs-only |
| `docs/architecture-decision-log/022-remove-community-vault-modules.md` | `portal-page` | `**portal-page:** Community (Telegram/Zoom)...` | docs | docs-only |
| `docs/architecture-decision-log/022-remove-community-vault-modules.md` | `portal-page` | `portal-page: \`modules/community/...\`` | docs | docs-only |
| `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` | `portal-page` | `**2. Buyer Checkout (\`portal-page\`)**` | docs | docs-only |
| `docs/contracts/openapi-vs-minimal-api.md` | `portal-page` | `ops-page, portal-page, developers-page` | docs | docs-only |
| `plans/001-backend/001-backend-solidification-checklist.md` | `portal-page` | `ops-page + portal-page` | docs | docs-only |

### §3.4 portal-page — occurrence count

Approximately **56** matching lines for pattern `portal-page`.

---

## §4 — `superadmin-page` → `lazuar-admin`

### §4.1 Must-change / operational references

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `apps/superadmin-page/package.json` | `superadmin-page` | `"name": "superadmin-page",` | config | **must-change** |
| `apps/superadmin-page/Dockerfile` | `superadmin-page` | `COPY apps/superadmin-page/package.json apps/superadmin-page/` | docker | **must-change** |
| `apps/superadmin-page/Dockerfile` | `superadmin-page` | `RUN pnpm install --filter ./apps/superadmin-page... --frozen-lockfile` | docker | **must-change** |
| `apps/superadmin-page/Dockerfile` | `superadmin-page` | `COPY apps/superadmin-page apps/superadmin-page` | docker | **must-change** |
| `apps/superadmin-page/Dockerfile` | `superadmin-page` | `RUN pnpm --filter ./apps/superadmin-page build` | docker | **must-change** |
| `apps/superadmin-page/Dockerfile` | `superadmin-page` | `COPY --from=build ... /app/apps/superadmin-page/dist ./dist` | docker | **must-change** |
| `docker-bake.hcl` | `superadmin-page` | `targets = [..., "superadmin-page", ...]` | docker | **must-change** |
| `docker-bake.hcl` | `superadmin-page` | `target "superadmin-page" {` | docker | **must-change** |
| `docker-bake.hcl` | `superadmin-page` | `dockerfile = "apps/superadmin-page/Dockerfile"` | docker | **must-change** |
| `docker-compose.yml` | `superadmin-page` | `superadmin-page:` | docker | **must-change** |
| `docker-compose.yml` | `superadmin-page` | `dockerfile: apps/superadmin-page/Dockerfile` | docker | **must-change** |
| `docker-compose.ghcr.yml` | `superadmin-page` | `superadmin-page:` | docker | **must-change** |
| `mprocs-dev.yaml` | `superadmin-page` | `superadmin-page:` | config | **must-change** |
| `mprocs-dev.yaml` | `superadmin-page` | `shell: cd apps/superadmin-page && pnpm dev` | config | **must-change** |
| `.github/workflows/ghcr.yml` | `superadmin-page` | `dockerfile: apps/superadmin-page/Dockerfile` | ci | **must-change** |
| `pnpm-lock.yaml` | `superadmin-page` | `apps/superadmin-page:` | lockfile | **must-change** (regenerate) |

### §4.2 Docs / narrative references (superadmin-page)

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `README.md` | `superadmin-page` | `│   └── superadmin-page/  # The Global Control Plane...` | docs | docs-only |
| `docs/001-gaps/04-developers-page-dx.md` | `superadmin-page` | `\| superadmin-page \| No evidence of Lazuar API key vault \|` | docs | docs-only |
| `docs/001-gaps/13-typespec-api-contracts.md` | `superadmin-page` | `- \`apps/superadmin-page\`` | docs | docs-only |
| `docs/001-gaps/16-testing-coverage.md` | `superadmin-page` | `\| \`apps/superadmin-page\` \| No test tooling \|` | docs | docs-only |
| `docs/001-gaps/16-testing-coverage.md` | `superadmin-page` | `\| \`superadmin-page\` \| Vite/React \| **None** \|` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `superadmin-page` | `\`apps/superadmin-page/src/\`` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `superadmin-page` | `\| **superadmin-page** \| HttpOnly cookie \`lazuar_admin_auth\` \|` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `superadmin-page` | `.../apps/superadmin-page/src/lib/api-client.ts` | docs | docs-only |
| `docs/001-gaps/19-frontend-backend-integration.md` | `superadmin-page` | `.../superadmin-page/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx` | docs | docs-only |
| `docs/architecture-decision-log/022-remove-community-vault-modules.md` | `superadmin-page` | `e.g. \`superadmin-page/src/lib/prompt-library.ts\`` | docs | docs-only |

### §4.3 superadmin-page — occurrence count

Approximately **25** matching lines for pattern `superadmin-page`.

---

## §5 — GHCR image fragments (already short; not `*-page`)

These image names **do not contain** `developers-page` / `ops-page` / etc. They are related product names used at the registry layer.

| File path | Pattern matched | Sample line | Category | Action |
|-----------|-----------------|-------------|----------|--------|
| `docker-bake.hcl` | `lazuar-hub-ops` | `ghcr.io/proxeon/lazuar-hub-ops` (comment + tags) | docker | **review** — already maps to “ops”; no `-page`. Optional rebrand to `lazuar-ops` image name is a **separate** decision |
| `docker-bake.hcl` | `lazuar-hub-portal` | `${REGISTRY}/lazuar-hub-portal:${TAG}` | docker | **review** (same) |
| `docker-bake.hcl` | `lazuar-hub-superadmin` | `${REGISTRY}/lazuar-hub-superadmin:${TAG}` | docker | **review** (same) |
| `docker-bake.hcl` | `lazuar-hub-developers` | `${REGISTRY}/lazuar-hub-developers:${TAG}` | docker | **review** (same; not `lazuar-spec` today) |
| `docker-compose.yml` | `lazuar-hub-ops` / `portal` / `superadmin` | `image: ghcr.io/proxeon/lazuar-hub-ops:local` | docker | **review** |
| `docker-compose.ghcr.yml` | `lazuar-hub-ops` / `portal` / `superadmin` | `image: ghcr.io/proxeon/lazuar-hub-ops:${TAG:-latest}` | docker | **review** — **no developers image service** in this file |
| `deploy/prod/docker-compose.yml` | `lazuar-hub-ops` | `image: ghcr.io/proxeon/lazuar-hub-ops:${VERSION:-latest}` | docker | **review** |
| `deploy/prod/docker-compose.yml` | `lazuar-hub-portal` | `image: ghcr.io/proxeon/lazuar-hub-portal:${VERSION:-latest}` | docker | **review** |
| `deploy/prod/docker-compose.yml` | `lazuar-hub-superadmin` | `image: ghcr.io/proxeon/lazuar-hub-superadmin:${VERSION:-latest}` | docker | **review** |
| `deploy/prod/docker-compose.yml` | `lazuar-hub-developers` | `image: ghcr.io/proxeon/lazuar-hub-developers:${VERSION:-latest}` | docker | **review** |
| `.github/workflows/ghcr.yml` | matrix image names | `name: lazuar-hub-portal` / `ops` / `superadmin` / `developers` | ci | **review** — image names independent of app dir; **dockerfile paths must change** with app rename |

**Implication:** Renaming app directories to `lazuar-*` does **not** require GHCR image renames for builds to work, as long as Dockerfiles and matrix `dockerfile:` paths update. Renaming GHCR packages is optional and has pull-path / deploy impact.

---

## §6 — Docker service names that differ from `*-page` (false-positive care)

### §6.1 Production (`deploy/prod/docker-compose.yml` + Caddyfile)

| Service key | Image | Container name | Caddy upstream | Contains `*-page`? | Action for app rename |
|-------------|-------|----------------|----------------|--------------------|------------------------|
| `ops` | `lazuar-hub-ops` | `hub-ops` | `ops:3000` | No | **ignore** for string `ops-page`; only change if deliberately renaming prod services |
| `portal` | `lazuar-hub-portal` | `hub-portal` | `portal:3000` | No | **ignore** for `portal-page` |
| `superadmin` | `lazuar-hub-superadmin` | `hub-superadmin` | `superadmin:3000` | No | **ignore** for `superadmin-page` |
| `developers` | `lazuar-hub-developers` | `hub-developers` | `developers:3000` | No | **ignore** for `developers-page` |

`deploy/prod/Caddyfile` samples:

| File path | Pattern | Sample line | Category | Action |
|-----------|---------|-------------|----------|--------|
| `deploy/prod/Caddyfile` | service host `portal` | `reverse_proxy portal:3000` | docker | **ignore** (not `portal-page`) |
| `deploy/prod/Caddyfile` | service host `developers` | `reverse_proxy developers:3000` | docker | **ignore** (not `developers-page`) |
| `deploy/prod/Caddyfile` | service host `superadmin` | `reverse_proxy superadmin:3000` | docker | **ignore** |
| `deploy/prod/Caddyfile` | service host `ops` | `reverse_proxy ops:3000` | docker | **ignore** |
| `deploy/prod/README.md` | short names | `\| \`/\` \| ops \|` etc. | docs | **ignore** / docs-only if renaming prod services |

### §6.2 Local compose service keys (these ARE `*-page`)

| File | Service keys |
|------|----------------|
| `docker-compose.yml` | `ops-page`, `portal-page`, `superadmin-page` (**no** developers service) |
| `docker-compose.ghcr.yml` | `ops-page`, `portal-page`, `superadmin-page` (**no** developers service) |

### §6.3 High-risk false positives (do **not** bulk-replace)

| Token | Why dangerous | Recommendation |
|-------|---------------|----------------|
| bare `ops` | Backend module `Modules/Ops`, API routes `/ops`, chat product, Caddy service | Only replace `ops-page` and paths/packages |
| bare `portal` | URL basePath `/portal`, commerce portal APIs, Caddy handle `/portal*` | Only replace `portal-page` |
| bare `developers` | Prod service + GHCR image `lazuar-hub-developers` | Only replace `developers-page` |
| bare `superadmin` | Role/claims, cookie `lazuar_admin_auth`, prod service | Only replace `superadmin-page` |
| `ops_active_workspace_id` | localStorage key in ops client | **ignore** — not app package name; snake partial is not `ops_page` |
| `Modules/Ops` / product “Ops” OpenAPI | Domain naming | **ignore** |

---

## §7 — Snake_case patterns

| Pattern | Matches |
|---------|---------|
| `developers_page` | **0** |
| `ops_page` | **0** |
| `portal_page` | **0** |
| `superadmin_page` | **0** |

---

## §8 — Files with **no** `*-page` hit (confirmed non-participants for package string)

| File / area | Notes |
|-------------|-------|
| `pnpm-workspace.yaml` | `apps/*` only |
| `turbo.json` | No app names |
| Root `package.json` | No app names |
| `Taskfile.yml` | GHCR registry helpers only; no `*-page` strings |
| `.github/workflows/ci.yml` | Dotnet + contracts only |
| `scripts/**` | No hits |
| `script/**` | No hits |
| `idea/**` | No hits |
| Per-app `README.md` | Generic templates / “# Ops” stub — no package name strings |

---

## §9 — Aggregate counts

### By pattern (approx. content match lines, excl. node_modules)

| Pattern | Approx. line hits |
|---------|-------------------|
| `developers-page` | ~75 |
| `ops-page` | ~113 |
| `portal-page` | ~56 |
| `superadmin-page` | ~25 |
| Combined unique-ish content hits | ~269 |
| Snake_case variants | 0 |

### Unique files containing any of the four hyphenated names (source + docs + config)

**Must-change operational files (unique):**

1. `apps/developers-page/` (directory)
2. `apps/ops-page/` (directory)
3. `apps/portal-page/` (directory)
4. `apps/superadmin-page/` (directory)
5. `apps/developers-page/package.json`
6. `apps/ops-page/package.json`
7. `apps/portal-page/package.json`
8. `apps/superadmin-page/package.json`
9. `apps/developers-page/Dockerfile`
10. `apps/ops-page/Dockerfile`
11. `apps/portal-page/Dockerfile`
12. `apps/superadmin-page/Dockerfile`
13. `docker-bake.hcl`
14. `docker-compose.yml`
15. `docker-compose.ghcr.yml`
16. `mprocs-dev.yaml`
17. `.github/workflows/ghcr.yml`
18. `pnpm-lock.yaml` (regenerate)

**Code comments only (unique):**

- `apps/ops-page/src/hooks/use-chat-stream.ts`
- `apps/ops-page/src/hooks/use-debounce.ts`
- `apps/ops-page/src/components/OpsChatWorkspace.tsx`
- `apps/ops-page/src/components/chat/ChatMessageBubble.tsx`
- `apps/ops-page/src/components/chat/MarkdownContent.tsx`
- `apps/ops-page/src/components/forms/AutoForm.tsx`
- `apps/ops-page/src/types/chat.ts`
- `apps/portal-page/src/app/page.tsx`
- `apps/portal-page/src/app/not-found.tsx`
- `apps/portal-page/src/modules/core/lib/server-client.ts`
- `apps/portal-page/src/modules/checkout/components/PromoCodeInput.tsx`
- `apps/portal-page/src/modules/checkout/components/CheckoutLayout.tsx`
- `apps/portal-page/src/modules/checkout/components/IdentityBanner.tsx`
- `apps/superadmin-page/src/hooks/use-debounce.ts` (stale ops-page header)
- `apps/superadmin-page/src/types/chat.ts` (stale ops-page header)
- `apps/lazuar-api/Modules/One/Infrastructure/Workers/SystemGenesisBootstrapperJob.cs`
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs`

**Docs / plans (unique files with hits):**

- `README.md`
- `apps/lazuar-docs/README.md`
- `apps/lazuar-docs/docs/reference/openapi.md`
- `apps/lazuar-docs/docs/index.md`
- `apps/lazuar-docs/docs/guide/how-to-maintain.md`
- `docs/001-gaps/00-what-we-need-to-do-next.md`
- `docs/001-gaps/01-dunning-engine.md`
- `docs/001-gaps/03-api-auth-credentials.md`
- `docs/001-gaps/04-developers-page-dx.md` (+ filename)
- `docs/001-gaps/06-payments-module.md`
- `docs/001-gaps/07-commerce-module.md`
- `docs/001-gaps/08-communications-module.md`
- `docs/001-gaps/09-lhdn-module.md`
- `docs/001-gaps/11-ops-crm-messaging.md`
- `docs/001-gaps/13-typespec-api-contracts.md`
- `docs/001-gaps/16-testing-coverage.md`
- `docs/001-gaps/18-outbound-customer-webhooks.md`
- `docs/001-gaps/19-frontend-backend-integration.md`
- `docs/001-gaps/20-architecture-intent-vs-implementation.md`
- `docs/001-gaps/README.md`
- `docs/architecture-decision-log/007-product-scoped-api-references.md`
- `docs/architecture-decision-log/013-frontend-module-implementation.md`
- `docs/architecture-decision-log/014-apps.md`
- `docs/architecture-decision-log/016-platform-domain-strategy.md`
- `docs/architecture-decision-log/017-portal-frontend-architecture.md`
- `docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md`
- `docs/architecture-decision-log/022-remove-community-vault-modules.md`
- `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md`
- `docs/contracts/openapi-vs-minimal-api.md`
- `plans/001-backend/001-backend-solidification-checklist.md`

---

## §10 — Shortlist: files that **MUST change** for rename to work

These are required for package resolution, Docker builds, CI image builds, and local multi-app process manager. **Without them, rename breaks.**

| # | Path | Why |
|---|------|-----|
| 1 | `apps/developers-page/` → `apps/lazuar-spec/` | Directory is workspace package root |
| 2 | `apps/ops-page/` → `apps/lazuar-ops/` | Directory is workspace package root |
| 3 | `apps/portal-page/` → `apps/lazuar-portal/` | Directory is workspace package root |
| 4 | `apps/superadmin-page/` → `apps/lazuar-admin/` | Directory is workspace package root |
| 5 | `apps/*/package.json` (`name` fields) | pnpm package identity / `--filter` by name |
| 6 | `apps/developers-page/Dockerfile` | Hard-coded `apps/developers-page` paths + filters + CMD |
| 7 | `apps/ops-page/Dockerfile` | Hard-coded `apps/ops-page` paths + filters |
| 8 | `apps/portal-page/Dockerfile` | Hard-coded `apps/portal-page` paths + filters + CMD |
| 9 | `apps/superadmin-page/Dockerfile` | Hard-coded `apps/superadmin-page` paths + filters |
| 10 | `docker-bake.hcl` | Target names + dockerfile paths for all four frontends |
| 11 | `docker-compose.yml` | Service keys + dockerfile paths (`ops`/`portal`/`superadmin`) |
| 12 | `docker-compose.ghcr.yml` | Service keys for three frontends |
| 13 | `mprocs-dev.yaml` | Proc keys + `cd apps/...` shells for all four |
| 14 | `.github/workflows/ghcr.yml` | `dockerfile: apps/*-page/Dockerfile` for four matrix entries |
| 15 | `pnpm-lock.yaml` | Importer keys `apps/*-page` — regenerate after rename |

**Also treat as must-change if published as runnable docs commands:**

| Path | Why |
|------|-----|
| `apps/lazuar-docs/docs/reference/openapi.md` | Contains `pnpm --filter developers-page dev` — fails after package rename if not updated |

### Explicitly **not** required for rename-to-work (unless rebranding deploy topology)

| Path | Why skip for minimal rename |
|------|------------------------------|
| `deploy/prod/docker-compose.yml` | Service keys already short (`ops`, `portal`, …); images already short |
| `deploy/prod/Caddyfile` | Uses short service hostnames |
| GHCR image names (`lazuar-hub-*`) | Independent of app folder; path to Dockerfile is the coupling |
| All `docs/**`, ADRs, gap reports, `plans/001-backend/**` | Narrative only |
| Path-header comments in TS/CS files | Cosmetic |
| `README.md` structure tree | Docs-only (still recommended) |

---

## §11 — Docs-only shortlist (safe to defer)

All of the following can ship after (or without) the functional rename; they do not block `pnpm install`, Docker build, or GHCR:

- Entire `docs/001-gaps/**` (including historical filename `04-developers-page-dx.md`)
- Entire `docs/architecture-decision-log/**` hits
- `docs/contracts/openapi-vs-minimal-api.md`
- `plans/001-backend/001-backend-solidification-checklist.md`
- Root `README.md` marketing/structure table (except if onboarding depends on exact dir names — still not a build break)
- `apps/lazuar-docs/docs/index.md`, `how-to-maintain.md`, `README.md` (except filter command above)
- Backend comment strings in `SystemGenesisBootstrapperJob.cs` / `Endpoints.cs`
- Stale `// apps/ops-page/...` headers inside superadmin files

---

## §12 — Suggested rename execution order (inventory-only guidance)

1. `git mv` the four app directories.
2. Update four `package.json` `"name"` fields.
3. Update four Dockerfiles path/filter/CMD strings.
4. Update `docker-bake.hcl` targets + dockerfile paths.
5. Update `docker-compose.yml` + `docker-compose.ghcr.yml` service keys + dockerfile paths.
6. Update `mprocs-dev.yaml`.
7. Update `.github/workflows/ghcr.yml` dockerfile matrix paths.
8. `pnpm install` to refresh `pnpm-lock.yaml` importer paths.
9. Fix `apps/lazuar-docs/docs/reference/openapi.md` filter command.
10. Optionally sweep docs + path comments.
11. Optionally decide whether GHCR image names should become `lazuar-ops` / `lazuar-portal` / `lazuar-admin` / `lazuar-spec` (deploy + pull impact; **not** required for monorepo path rename).

---

## §13 — Gap notes discovered during inventory

1. **Local compose lacks developers:** `docker-compose.yml` / `docker-compose.ghcr.yml` do not define a developers/`developers-page` service, but bake + GHCR + prod deploy do build/run it. Rename should not assume local compose parity.
2. **Prod service names already short:** `ops` / `portal` / `superadmin` / `developers` — closer to desired product names than `*-page`; do not confuse with app package rename.
3. **GHCR names already short but not identical to proposed app names:** e.g. `lazuar-hub-developers` vs proposed app `lazuar-spec`; `lazuar-hub-superadmin` vs proposed `lazuar-admin`.
4. **Historical monorepo path** `lazuar-hub` appears in many gap docs absolute paths; orthogonal to this rename but will confuse path updates.
5. **No snake_case package identifiers** exist today — no extra snake_case migration surface.

---

*End of master inventory. Generated by exhaustive ripgrep of the monorepo for the four old app names, package identity, GHCR fragments, snake_case variants, and Docker service naming.*
