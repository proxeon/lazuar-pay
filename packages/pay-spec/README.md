# @repo/pay-spec

TypeSpec for the **focused Pay host** (`apps/lazuar-pay`, port 8081).

Not [`packages/api-spec`](../api-spec/) (old modular API on 8080). Do not import One, LHDN, or `/public/commerce` routes here.

```bash
task pay:spec
# or
pnpm --filter @repo/pay-spec build
node scripts/check-pay-openapi-honesty.mjs
```

OpenAPI lands in `dist/openapi.yaml` (gitignored; compile before reading). Grow `main.tsp` when a Pay `/v1` door exists. Honesty scrape is `scripts/check-pay-openapi-honesty.mjs` in CI job `pay` — not Hub `task gen` / `honesty-allowlist.yaml`. Unversioned `/health` and `/ready` stay host-only.
