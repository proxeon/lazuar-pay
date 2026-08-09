# R05 — One-only middleware (remove dual-read)

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md` § F03  
**Depends on:** R04 complete (or accelerate waiver from R02)  
**Notes:** `../r05-notes.md`

---

## R05.1 Preflight

- [ ] Prod/staging migration residual accepted
- [ ] Staging already running candidate build with One-only if possible

## R05.2 Code

- [x] Remove Lhdn SQL branch from `ApiKeyAuthenticationMiddleware`
- [x] Remove dual Lhdn revoke subscription from host composition
- [x] Keep One revoke → cache eviction only
- [x] Assert no app path inserts `lhdn.DeveloperApiKeys`
- [x] Lhdn key HTTP: One façade only (or 410/deprecate if product wants)

## R05.3 Tests

- [x] Update/remove tests that relied on Lhdn-only dual-read
- [x] One credential auth green
- [x] Lhdn-only seed (if any test left) expects **401**
- [x] Architecture / module tests green

## R05.4 Docs

- [x] One/Lhdn README: dual-read closed + date
- [x] `api-key-cutover-design.md` / FUTURE-WORK FW-1: One-only live
- [x] Integrator note if any public dual-read messaging existed

## R05.5 Deploy / monitor

- [ ] Deploy staging → smoke
- [ ] Deploy prod → watch API key 401s
- [ ] Rollback plan: re-enable dual-read commit (document)

## R05.6 Exit

- [ ] One-only in prod
- [ ] Start 30-day clock for R06
