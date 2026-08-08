# lazuar-docs

VitePress site for **Lazuar Hub** product and **integrator** guides.

## Run

From monorepo root:

```bash
pnpm --filter lazuar-docs dev
# http://localhost:5180
```

Or:

```bash
cd apps/lazuar-docs
pnpm dev
```

```bash
pnpm --filter lazuar-docs build
pnpm --filter lazuar-docs preview
```

## Layout

```text
docs/
  index.md                 Home
  guide/                   Concepts, product lines
  integrations/            How to connect any app
  reference/               Errors, events, OpenAPI
  public/                  Favicon etc.
  .vitepress/config.ts     Sidebar / nav
```

## Relationship to other docs

| Location | Audience |
|----------|----------|
| **This app** | Product + integrator guides (refine → publish) |
| `docs/*.md` (repo root) | Engineering ADRs, gap analysis, quickstarts |
| `apps/lazuar-developers` | Live Scalar OpenAPI |
| Aura `apps/aura-docs` | Salon **product how-to** (not Hub integrator) |

## Status

Draft. Safe to expand freely; freeze before public marketing.
