# S30 — Sample monorepo packaging (workspace + turbo)

**Track:** Sample packaging · **Analysis:** `../07-monorepo-packaging.md`  
**Depends on:** S00  
**Goal:** Opt-in workspace member without breaking product CI/turbo.

---

## S30.1 Workspace

- [x] Edit `pnpm-workspace.yaml`: add `"examples/*"`
- [x] Do **not** put sample under `apps/`
- [x] No nested lockfile inside sample

## S30.2 Root scripts (turbo filters)

- [x] Root `package.json` build/dev/lint/test/check-types exclude examples, e.g. `--filter=!@examples/*` (adjust name when known)
- [x] Optional convenience: `"example:cashier": "pnpm --filter @examples/hub-cashier-next dev"`
- [x] Confirm product `pnpm build` / `pnpm dev` no longer need sample present to succeed when sample has `build` script

## S30.3 CI

- [x] **No** new job required to build sample by default
- [x] After first sample package.json exists: `pnpm install` updates root lockfile (commit with S31)
- [x] Sample must not appear in contracts dirty-check paths
- [x] No docker-bake / GHCR matrix entry

## S30.4 Root discoverability

- [x] Optional: one bullet under root README project structure for `examples/` — deferred to **S60**
- [x] Create `examples/README.md` index (one paragraph + link to hub-cashier-next when scaffolded)

## S30.5 Exit

- [x] Filters documented in this checklist README / examples README
- [x] Product turbo path safe even after sample `build` script exists
