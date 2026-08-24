# @repo/pay-spec

TypeSpec for the **focused Pay host** (`apps/lazuar-pay`, port 8081).

Not [`packages/api-spec`](../api-spec/) (old modular API on 8080). Do not import One, LHDN, or `/public/commerce` routes here.

```bash
task pay:spec
# or
pnpm --filter @repo/pay-spec build
```

OpenAPI lands in `dist/openapi.yaml` (gitignored). Grow `main.tsp` when a Pay `/v1` door exists. Not Hub `task gen`.
