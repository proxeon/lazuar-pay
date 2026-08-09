# Phase 03 — Done (interim)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `docs(api): API key dual-read cutover design (phase 03 interim)`

## What this interim delivers

Phase 03 full cutover is **date-gated** (dual-read allowed until **2026-11-30**). This PR only lands analysis, design, and low-risk SSoT documentation — **not** dual-read removal.

### Documents

1. `plans/004-maintenance/phase-03-analysis.md` — full dual-path inventory (middleware, Lhdn façades, One SSoT, revoke events, cache keys, tests, row-count gate)
2. `plans/004-maintenance/api-key-cutover-design.md` — migration algorithm outline, One-then-Lhdn read order, cutover dates, post-cutover 401, non-migratable cases
3. Checklist `checklists/phase-03-dual-api-keys-cutover.md` — 03.1–03.3 checked; 03.4–03.8 left open with **after 2026-11-30** notes

### Low-risk code / README (SSoT clarity only)

- `ApiKeyAuthenticationMiddleware` — cutover dates on dual-read `LookupCredentialAsync`
- `Program.cs` — dual revoke subscribe comment with dates
- `ApiKeyRevokedIntegrationEventHandler` — dual-window summary
- `Modules/One/README.md` — §8 platform credentials + dual-read window
- `Modules/Lhdn/README.md` — §6 developer keys façade + dual-read window

### Confirmed without code change

- **New key mint already prefers One:** Lhdn generate/list/revoke commands and `/lhdn/api-keys` endpoints call `IApiCredentialService` → `one.ApiCredentials`. No application path mints `DeveloperApiKey` rows.

## Explicitly deferred (full cutover)

| Item | When |
| :--- | :--- |
| Migrate existing `lhdn.DeveloperApiKeys` rows into One | 03.4 — after staging/prod inventory |
| Remove dual-read middleware + Lhdn lookup SQL | **after 2026-11-30** (target by 2026-12-15) |
| Collapse revoke to One event only | **after 2026-11-30** |
| Drop/archive `lhdn.DeveloperApiKeys` | ≥ 30 days after One-only in prod (03.6) |

**LOCKED:** Do **not** remove dual-read middleware before the dated window unless prod active legacy row count is zero and ops signs off.

## Next

- Ops: run `COUNT(*)` on staging/prod `lhdn.DeveloperApiKeys`  
- Schedule 03.4 migrator when counts known  
- Phase 04+ per `plans/004-maintenance/checklists/`
