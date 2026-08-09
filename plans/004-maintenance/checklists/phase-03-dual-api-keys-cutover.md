# Phase 03 — Dual API keys cutover (One as SSoT)

**Depends on:** Phase 00.1 decision (must choose end-state A or dated B).  
**Goal:** Single mint/list/revoke path for platform API credentials.  
**Risk:** Auth breakage for integrators still on LHDN developer keys — migrate carefully.  
**Locked:** dual-read until **2026-11-30**; One-only target **2026-12-15** (`decisions.md` §00.1).

---

## 03.1 Inventory (before code)

- [x] Map middleware dual-read: `ApiKeyAuthenticationMiddleware` (or equivalent) SQL to `one.ApiCredentials` + `lhdn.DeveloperApiKeys` — see `phase-03-analysis.md`
- [x] List Lhdn Application commands/ports that mint/list/revoke DeveloperApiKeys
- [x] List One endpoints for `/api-keys` (or equivalent) mint/list/revoke
- [x] List dual `ApiKeyRevokedIntegrationEvent` subscriptions in `Program.cs`
- [x] List IMemoryCache keys / invalidation paths for both stores
- [x] Count/estimate remaining rows in `lhdn.DeveloperApiKeys` (dev/staging/prod) — **method documented**; live counts are ops (no DB in this interim)

## 03.2 Migration design (write before PR)

- [x] Document key migration algorithm (hash format, scopes, tenant mapping) — `api-key-cutover-design.md`
- [x] Document who cannot be migrated automatically
- [x] Document dual-read window behavior (read order: One first vs Lhdn first) — **One first, then Lhdn**
- [x] Document post-cutover failure mode (401 with clear message)
- [x] Get Phase 00 sign-off that design matches decision — design aligns with locked 00.1

## 03.3 Prefer One for all new keys (if not already)

- [x] Ensure Lhdn “generate key” UI/API façades call **One** commands if still minting Lhdn rows — **already One via `IApiCredentialService`**
- [x] Block new inserts into `lhdn.DeveloperApiKeys` if product allows (feature flag or code path removal) — mint path already dead (`AddDeveloperApiKey` unused by application)
- [x] Tests: mint via One → auth succeeds for LHDN scopes — covered by existing ModuleTests (`GenerateAndListApiCredentialsTests`, `ApiKeyAuthenticationTests`, Lhdn façade tests)

## 03.4 Migrate existing keys

- [ ] One-off migrator job or SQL script (prefer idempotent job in One or ops script) — **after 2026-11-30 gate only if still needed; implement when staging/prod row inventory is known**
- [ ] Dry-run on staging
- [ ] Migrate staging; verify auth
- [ ] Migrate production with runbook
- [ ] Verify revoke on One invalidates cached auth

## 03.5 Remove dual-read (after migration complete)

- [ ] Middleware reads **only** One credentials — **after 2026-11-30** (target by 2026-12-15); dual-read intentionally kept in interim
- [ ] Remove Lhdn DeveloperApiKey lookup SQL — **after 2026-11-30**
- [ ] Collapse revoke handlers to One event only — **after 2026-11-30**
- [ ] Remove dual subscribe in `Program.cs` — **after 2026-11-30**
- [ ] Deprecate/remove Lhdn domain aggregates/commands for keys (or leave read-only table one release) — **after 2026-11-30**
- [ ] TypeSpec: LHDN key routes either removed or documented as One-backed façade — **after 2026-11-30** / Phase 05

## 03.6 Optional table drop (later PR)

- [ ] After monitoring window: migration to drop `lhdn.DeveloperApiKeys` (or rename archive) — **≥ 30 days after One-only in prod**
- [ ] Update architecture tests if they asserted dual path

## 03.7 Tests

- [ ] Unit/module: One credential auth for LHDN-scoped routes — existing coverage noted; expand if migrator lands
- [ ] Revoke invalidates access — existing handler tests; re-verify post-cutover
- [ ] No test still seeds only Lhdn DeveloperApiKeys as sole path — **after dual-read removal**
- [ ] Architecture tests updated if boundaries change — **after dual-read removal**

## 03.8 Exit criteria

- [ ] No dual-read in middleware — **after 2026-11-30**
- [ ] No dual revoke event subscription — **after 2026-11-30**
- [ ] Staging + prod migrators complete (or zero rows left)
- [ ] Integrator notes updated (hub docs / api-spec descriptions)

### Interim complete (2026-08-09)

Inventory (03.1), design (03.2), and “prefer One for new keys” (03.3) done. Dual-read middleware + dual revoke subscribe **retained** until after **2026-11-30**. See `phase-03-done.md`.
