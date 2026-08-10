# 07 — Monorepo packaging for the sample app

**Status:** analysis complete 2026-08-10  
**Goal:** House `examples/hub-cashier-next` in the pnpm monorepo for convenience **without** coupling it to product CI, turbo default builds, shared API types, or Docker product images.

---

## 1. Current monorepo facts

### `pnpm-workspace.yaml`

```yaml
packages:
  - "apps/*"
  - "packages/*"
```

No `examples/*` today. No `examples/` directory.

### Root `package.json`

- `packageManager`: `pnpm@11.5.2`  
- Scripts: `turbo run build|dev|lint|test|check-types`  
- Docs helpers: `docs:dev|build|preview` filter `lazuar-docs`

### `turbo.json`

- Tasks: `build`, `test`, `lint`, `check-types`, `dev`  
- No package filters in turbo.json itself — filtering is at CLI  

### CI (`.github/workflows/ci.yml`)

Jobs observed:

1. **contracts** — pnpm install, `task gen`, honesty script, dirty check on generated clients  
2. **dotnet** — restore/build/test API solutions  

**No** `turbo run build` over all apps in current CI file. Product frontends may be built elsewhere (`ghcr.yml`) or not gated.

### Docker

Product apps have Dockerfiles (`lazuar-api`, portal, ops, admin, developers). **Sample must not** get a production Dockerfile.

### API types package

`packages/api-types-ts` — private workspace package generated from OpenAPI; used by portal. **Sample must not depend on it** so integrators can copy sample out of monorepo with plain fetch.

---

## 2. Target packaging decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Path | `examples/hub-cashier-next` | Clear, copy-friendly |
| Workspace | Add `"examples/*"` | Single `pnpm install` from root |
| Turbo build inclusion | **Exclude** from default root product filters | Sample breakage must not fail release builds |
| CI | **No** required build/test of sample | Optional path later |
| Shared packages | **None** required | Plain fetch |
| Dockerfile | **None** | Local/dev only |
| Publish npm | **No** | Private example |

---

## 3. `pnpm-workspace.yaml` change

```yaml
packages:
  - "apps/*"
  - "packages/*"
  - "examples/*"
```

Optional later:

```yaml
  - "examples/sample-apps/*"
```

Only if multiple samples need nesting — avoid until needed.

---

## 4. Sample `package.json` shape

```json
{
  "name": "hub-cashier-next",
  "private": true,
  "version": "0.0.0",
  "scripts": {
    "dev": "next dev -H 0.0.0.0 -p 3005",
    "build": "next build",
    "start": "next start -p 3005",
    "lint": "echo \"no lint configured\"",
    "check-types": "tsc --noEmit"
  },
  "dependencies": {
    "next": "16.2.9",
    "react": "19.2.4",
    "react-dom": "19.2.4"
  },
  "devDependencies": {
    "@types/node": "^20",
    "@types/react": "^19",
    "@types/react-dom": "^19",
    "typescript": "^5"
  }
}
```

### Explicit non-dependencies

Do **not** add:

- `@repo/api-types-ts`  
- `@repo/ui`  
- `@repo/eslint-config` (optional later)  
- `stripe`, `billplz`, any gateway SDK  
- `openapi-fetch` (optional teaching variant only — not MVP)

---

## 5. Turbo filters — exclude examples

### Problem

Root `pnpm build` → `turbo run build` discovers all workspace packages with a `build` script, including sample → sample type errors could fail local full builds.

### Mitigations (pick combination)

#### A. Root script filters (recommended)

Change root scripts to product filters:

```json
{
  "scripts": {
    "build": "turbo run build --filter=./apps/* --filter=./packages/*",
    "lint": "turbo run lint --filter=./apps/* --filter=./packages/*",
    "check-types": "turbo run check-types --filter=./apps/* --filter=./packages/*",
    "test": "turbo run test --filter=./apps/* --filter=./packages/*",
    "dev": "turbo run dev --filter=./apps/*",
    "sample:dev": "pnpm --filter hub-cashier-next dev",
    "sample:build": "pnpm --filter hub-cashier-next build"
  }
}
```

#### B. `turbo.json` package configuration

```json
{
  "tasks": {
    "build": {
      "dependsOn": ["^build"]
    }
  }
}
```

Turbo 2 supports package-level `turbo.json` in sample:

```json
// examples/hub-cashier-next/turbo.json
{
  "extends": ["//"],
  "tasks": {
    "build": {
      "cache": false
    }
  }
}
```

This does **not** auto-exclude from root pipeline — still prefer CLI filters.

#### C. Omit `build` script on sample

Weak: still want local `pnpm build` for smoke. Prefer A.

### Dev convenience

```bash
pnpm --filter hub-cashier-next dev
# or
pnpm sample:dev
```

Do **not** add sample to `mprocs-dev.yaml` by default (noise). Optional commented entry OK.

---

## 6. CI policy

| Check | Include sample? |
|-------|-----------------|
| contracts / `task gen` | No |
| dotnet tests | No |
| frontend turbo build | No (if introduced later, keep filter without examples) |
| Optional future job `sample-smoke` | Manual/`workflow_dispatch` only |

**Rule:** A red sample never blocks main Hub merges.

Document in sample README: “CI does not build this app; run `pnpm sample:build` locally before docs demos.”

---

## 7. File tree (target)

```text
examples/
  hub-cashier-next/
    package.json
    tsconfig.json
    next.config.ts
    next-env.d.ts
    .env.example
    .gitignore            # if not covered by root
    README.md
    turbo.json            # optional
    app/
      layout.tsx
      page.tsx
      globals.css
      orders/[orderId]/page.tsx
      pay/success/page.tsx
      pay/cancel/page.tsx
      api/orders/route.ts
      api/orders/[orderId]/route.ts
      api/checkout/route.ts
      api/webhooks/hub/route.ts
    lib/
      hub.ts
      orders-store.ts
      webhook-verify.ts
      types.ts
    scripts/
      provision-and-print-env.sh
```

Root changes:

```text
pnpm-workspace.yaml          # + examples/*
package.json                 # filters + sample:dev scripts
.gitignore                   # ensure .env.local patterns
plans/006-sample/            # this analysis (already)
apps/lazuar-docs/...         # run-sample-app page
```

**No:**

```text
examples/hub-cashier-next/Dockerfile
deploy/**/sample*
```

---

## 8. TypeScript config

Minimal standalone:

```json
{
  "compilerOptions": {
    "target": "ES2017",
    "lib": ["dom", "dom.iterable", "esnext"],
    "allowJs": false,
    "skipLibCheck": true,
    "strict": true,
    "noEmit": true,
    "esModuleInterop": true,
    "module": "esnext",
    "moduleResolution": "bundler",
    "resolveJsonModule": true,
    "isolatedModules": true,
    "jsx": "preserve",
    "incremental": true,
    "plugins": [{ "name": "next" }],
    "paths": { "@/*": ["./*"] }
  },
  "include": ["next-env.d.ts", "**/*.ts", "**/*.tsx", ".next/types/**/*.ts"],
  "exclude": ["node_modules"]
}
```

Do **not** extend `@repo/typescript-config` unless we accept coupling; standalone is easier to copy out.

---

## 9. Copy-out story (integrator)

Docs should say:

> You can copy `examples/hub-cashier-next` to a new repo. Replace monorepo scripts with local `pnpm install && pnpm dev`. No Hub packages required.

That is why plain `fetch` + duplicated verify helper is intentional (not a DRY failure).

---

## 10. Interaction with `task gen` / honesty

Sample does not import generated clients → contract regeneration cannot break sample compile.  
If someone later adds `@repo/api-types-ts`, they re-introduce coupling — **forbid in code review**.

---

## 11. Root README blurb (optional D06)

```markdown
### Sample integrator app

See `examples/hub-cashier-next` and VitePress **Run the sample**.
Not part of product CI.
```

---

## 12. Implementation checklist

- [ ] Create `examples/hub-cashier-next`  
- [ ] Extend `pnpm-workspace.yaml`  
- [ ] Adjust root turbo filters / `sample:*` scripts  
- [ ] Root `.gitignore` for sample envs if needed  
- [ ] Confirm `pnpm install` links package  
- [ ] Confirm `pnpm build` (root) does not require sample green  
- [ ] No Dockerfile  
- [ ] No `@repo/*` dependencies  
