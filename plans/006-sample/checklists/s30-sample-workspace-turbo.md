# S30 — Sample monorepo packaging (workspace + turbo)

**Track:** Sample packaging · **Analysis:** `../07-monorepo-packaging.md`  
**Depends on:** S00  
**Goal:** Opt-in workspace member without breaking product CI/turbo.

---

## S30.1 Workspace

- [ ] Edit `pnpm-workspace.yaml`: add `"examples/*"`
- [ ] Do **not** put sample under `apps/`
- [ ] No nested lockfile inside sample

## S30.2 Root scripts (turbo filters)

- [ ] Root `package.json` build/dev/lint/test/check-types exclude examples, e.g. `--filter=!@examples/*` (adjust name when known)
- [ ] Optional convenience: `"example:cashier": "pnpm --filter @examples/hub-cashier-next dev"`
- [ ] Confirm product `pnpm build` / `pnpm dev` no longer need sample present to succeed when sample has `build` script

## S30.3 CI

- [ ] **No** new job required to build sample by default
- [ ] After first sample package.json exists: `pnpm install` updates root lockfile (commit with S31)
- [ ] Sample must not appear in contracts dirty-check paths
- [ ] No docker-bake / GHCR matrix entry

## S30.4 Root discoverability

- [ ] Optional: one bullet under root README project structure for `examples/`
- [ ] Create `examples/README.md` index (one paragraph + link to hub-cashier-next when scaffolded)

## S30.5 Exit

- [ ] Filters documented in this checklist README / examples README
- [ ] Product turbo path safe even after sample `build` script exists
