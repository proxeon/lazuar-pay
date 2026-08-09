# F03 — One-only middleware (remove dual-read)

**Goal:** Auth uses only `one.ApiCredentials`; dual Lhdn read and dual revoke gone.  
**Depends on:** F02 complete (legacy keys migrated or proven zero)  
**Calendar:** dual-read allowed until 2026-11-30; target One-only by 2026-12-15 (or earlier if F01 allows)

---

## F03.1 Preflight

- [ ] Confirm F02 residual legacy-only active count = 0 (or accepted risk signed off)
- [ ] Staging already One-only candidate verified

## F03.2 Code

- [ ] Remove Lhdn SQL branch from `ApiKeyAuthenticationMiddleware`
- [ ] Remove Lhdn revoke event subscription from host composition
- [ ] Ensure only One `ApiKeyRevokedIntegrationEvent` clears cache
- [ ] Block any residual insert into `lhdn.DeveloperApiKeys`
- [ ] Lhdn key HTTP routes: One façade only or deprecated

## F03.3 Tests

- [ ] Module/auth tests: One credential works
- [ ] Migrated hash still authenticates
- [ ] Revoke invalidates access
- [ ] No test seeds Lhdn-only keys as sole path

## F03.4 Docs

- [ ] One/Lhdn README: dual-read window closed + date
- [ ] Update `api-key-cutover-design.md` status = executed
- [ ] Update `FUTURE-WORK.md` FW-1 status

## F03.5 Exit

- [ ] Staging + prod One-only
- [ ] No dual-read code path
- [ ] Monitoring: no spike in 401s for API keys
