# R02 — Keys data inventory

**Track:** Keys · **Analysis:** `../01-api-key-one-only-cutover.md`, `../../004-maintenance/api-key-cutover-design.md`  
**Goal:** Staging/prod counts; early-cutover decision.

---

## R02.1 Queries (run staging then prod)

- [ ] Count active `lhdn."DeveloperApiKeys"` where `IsActive = true`
- [ ] Count active `one."ApiCredentials"` where `IsActive = true`
- [ ] Count **active_legacy_only**: Lhdn active hash **not** in One
- [ ] Count inactive Lhdn rows (migrate all vs active-only — record choice: ________)
- [ ] Sample scopes not in One allowlist (quarantine list)

## R02.2 Record results

| Env | Active Lhdn | Active One | Active legacy-only | Notes |
|-----|-------------|------------|--------------------|-------|
| Staging | | | | |
| Prod | | | | |

## R02.3 Decision

- [ ] If prod **active_legacy_only = 0**: mark **accelerate** → R03 may be no-op / verify-only; R05 can proceed after staging One-only smoke
- [ ] If prod **active_legacy_only > 0**: R03 migrator required before R05
- [ ] Sign-off for accelerate: ________ (name/date) if used

## R02.4 Exit

- [ ] Numbers committed in plan note or design doc appendix
- [ ] R03 go / no-go clear
