# Examples

Integrator-facing sample apps that live in the monorepo for convenience but are **not** product apps.

They are workspace members (`examples/*` in `pnpm-workspace.yaml`) so a single root `pnpm install` links them. Default product turbo scripts at the repo root use `--filter=!@examples/*`, so sample build/lint/typecheck failures never block product `pnpm build` / `pnpm dev` / `pnpm lint` / `pnpm test` / `pnpm check-types`.

| Package | Path | Port | Start |
|---------|------|------|-------|
| `@examples/hub-cashier-next` | [`hub-cashier-next`](./hub-cashier-next) | **3020** | `pnpm example:cashier` or `pnpm --filter @examples/hub-cashier-next dev` |

No Dockerfile, no GHCR matrix entry, and no required CI job for samples. Copy-out friendly: samples intentionally avoid `@repo/*` packages and payment-gateway SDKs.
