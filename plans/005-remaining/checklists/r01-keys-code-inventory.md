# R01 — Keys code inventory

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md`  
**Goal:** Confirm current dual-read / mint / revoke map before data work.  
**No production cutover in this phase.**  
**Notes:** `../r01-notes.md` (2026-08-09)

---

## R01.1 Middleware

- [x] Open `ApiKeyAuthenticationMiddleware` (or current path under host Middleware)
- [x] Document: One lookup first SQL (table/columns)
- [x] Document: Lhdn dual-read second SQL
- [x] Document: 401 body on miss
- [x] Document: cache key format + TTL
- [x] Confirm cutover date comments still present

## R01.2 Mint / list / revoke

- [x] One `IApiCredentialService` (or equivalent) is only mint path
- [x] Lhdn `/api-keys` is façade over One (no insert into `DeveloperApiKeys`)
- [x] Aura provision mints One credentials only
- [x] Revoke: One event publisher exists
- [x] Dual subscribe for Lhdn revoke still in composition? (note location)

## R01.3 Dead write paths

- [x] Grep `DeveloperApiKey` / `AddDeveloperApiKey` / insert into `lhdn.DeveloperApiKeys`
- [x] List any residual write capability (should be none for app mint)

## R01.4 Tests that encode dual-read

- [x] List tests that seed Lhdn-only keys
- [x] List tests for dual revoke handlers
- [x] Note which must change in R05

## R01.5 Exit

- [x] Short inventory note in PR / `plans/005-remaining/r01-notes.md`
- [x] No behavior change required (docs-only OK if already accurate)
