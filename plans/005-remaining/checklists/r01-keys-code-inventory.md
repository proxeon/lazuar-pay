# R01 — Keys code inventory

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md`  
**Goal:** Confirm current dual-read / mint / revoke map before data work.  
**No production cutover in this phase.**

---

## R01.1 Middleware

- [ ] Open `ApiKeyAuthenticationMiddleware` (or current path under host Middleware)
- [ ] Document: One lookup first SQL (table/columns)
- [ ] Document: Lhdn dual-read second SQL
- [ ] Document: 401 body on miss
- [ ] Document: cache key format + TTL
- [ ] Confirm cutover date comments still present

## R01.2 Mint / list / revoke

- [ ] One `IApiCredentialService` (or equivalent) is only mint path
- [ ] Lhdn `/api-keys` is façade over One (no insert into `DeveloperApiKeys`)
- [ ] Aura provision mints One credentials only
- [ ] Revoke: One event publisher exists
- [ ] Dual subscribe for Lhdn revoke still in composition? (note location)

## R01.3 Dead write paths

- [ ] Grep `DeveloperApiKey` / `AddDeveloperApiKey` / insert into `lhdn.DeveloperApiKeys`
- [ ] List any residual write capability (should be none for app mint)

## R01.4 Tests that encode dual-read

- [ ] List tests that seed Lhdn-only keys
- [ ] List tests for dual revoke handlers
- [ ] Note which must change in R05

## R01.5 Exit

- [ ] Short inventory note in PR / `plans/005-remaining/r01-notes.md`
- [ ] No behavior change required (docs-only OK if already accurate)
