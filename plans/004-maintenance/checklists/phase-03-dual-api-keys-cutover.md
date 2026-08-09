# Phase 03 — Dual API keys cutover (One as SSoT)

**Depends on:** Phase 00.1 decision (must choose end-state A or dated B).  
**Goal:** Single mint/list/revoke path for platform API credentials.  
**Risk:** Auth breakage for integrators still on LHDN developer keys — migrate carefully.

---

## 03.1 Inventory (before code)

- [ ] Map middleware dual-read: `ApiKeyAuthenticationMiddleware` (or equivalent) SQL to `one.ApiCredentials` + `lhdn.DeveloperApiKeys`
- [ ] List Lhdn Application commands/ports that mint/list/revoke DeveloperApiKeys
- [ ] List One endpoints for `/api-keys` (or equivalent) mint/list/revoke
- [ ] List dual `ApiKeyRevokedIntegrationEvent` subscriptions in `Program.cs`
- [ ] List IMemoryCache keys / invalidation paths for both stores
- [ ] Count/estimate remaining rows in `lhdn.DeveloperApiKeys` (dev/staging/prod)

## 03.2 Migration design (write before PR)

- [ ] Document key migration algorithm (hash format, scopes, tenant mapping)
- [ ] Document who cannot be migrated automatically
- [ ] Document dual-read window behavior (read order: One first vs Lhdn first)
- [ ] Document post-cutover failure mode (401 with clear message)
- [ ] Get Phase 00 sign-off that design matches decision

## 03.3 Prefer One for all new keys (if not already)

- [ ] Ensure Lhdn “generate key” UI/API façades call **One** commands if still minting Lhdn rows
- [ ] Block new inserts into `lhdn.DeveloperApiKeys` if product allows (feature flag or code path removal)
- [ ] Tests: mint via One → auth succeeds for LHDN scopes

## 03.4 Migrate existing keys

- [ ] One-off migrator job or SQL script (prefer idempotent job in One or ops script)
- [ ] Dry-run on staging
- [ ] Migrate staging; verify auth
- [ ] Migrate production with runbook
- [ ] Verify revoke on One invalidates cached auth

## 03.5 Remove dual-read (after migration complete)

- [ ] Middleware reads **only** One credentials
- [ ] Remove Lhdn DeveloperApiKey lookup SQL
- [ ] Collapse revoke handlers to One event only
- [ ] Remove dual subscribe in `Program.cs`
- [ ] Deprecate/remove Lhdn domain aggregates/commands for keys (or leave read-only table one release)
- [ ] TypeSpec: LHDN key routes either removed or documented as One-backed façade

## 03.6 Optional table drop (later PR)

- [ ] After monitoring window: migration to drop `lhdn.DeveloperApiKeys` (or rename archive)
- [ ] Update architecture tests if they asserted dual path

## 03.7 Tests

- [ ] Unit/module: One credential auth for LHDN-scoped routes
- [ ] Revoke invalidates access
- [ ] No test still seeds only Lhdn DeveloperApiKeys as sole path
- [ ] Architecture tests updated if boundaries change

## 03.8 Exit criteria

- [ ] No dual-read in middleware
- [ ] No dual revoke event subscription
- [ ] Staging + prod migrators complete (or zero rows left)
- [ ] Integrator notes updated (hub docs / api-spec descriptions)
