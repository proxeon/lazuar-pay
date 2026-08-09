# How to maintain these guides

## Where content lives

```text
apps/lazuar-docs/
  docs/                 ← Markdown sources (this site)
  docs/.vitepress/      ← VitePress config
```

Related monorepo sources of truth:

| Concern | Source |
|---------|--------|
| OpenAPI / TypeSpec | `packages/api-spec/` |
| Live payments quickstart (engineers) | `docs/payments-integration-quickstart.md` |
| Curl harness | `script/second-app-proof.md` |
| Architecture ADRs | `docs/architecture-decision-log/` |

## Workflow

1. Change API or product behavior in the same PR as guide updates when practical.  
2. Prefer examples that match **snake_case** JSON (Hub ASP.NET default).  
3. Never commit live `sk_` / `whsec_` secrets into docs.  
4. Mark experimental paths with **Status: draft** in the page.  

## Local commands

```bash
# from monorepo root
pnpm --filter lazuar-docs dev      # http://localhost:5180
pnpm --filter lazuar-docs build
pnpm --filter lazuar-docs preview
```

## Publishing later

- Set `base` in `.vitepress/config.ts` if served under a subpath.  
- Point nav “Developers (Scalar)” at production lazuar-developers (hub `/docs`) URL.  
- Promote pages from draft → stable when contracts freeze.
