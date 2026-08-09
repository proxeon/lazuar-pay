# R17 — L-07 API key dual-read handoff

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** SQL  
**Checklist:** `checklists/r17-sql-l07-apikey-handoff.md`  
**Analysis:** `06-cross-schema-sql-leaks.md` L-07, `01-api-key-one-only-cutover.md`  
**Scope this pass:** Confirm dual-read **already removed** by **R05**. **No** separate SQL PR. **No** app code.

---

## Summary

| Concern | State |
|---------|--------|
| Dual-read (`one` + `lhdn`) | **Removed** by R05 |
| Middleware | **One-only** — `one."ApiCredentials"` via keyed `OneSqlConnectionFactory` |
| `LhdnLookupSql` / `lhdn."DeveloperApiKeys"` | **Gone** from middleware |
| L-07 leak | **Fixed** (R05) |
| This phase | Docs / checklist handoff only |
| Separate SQL fix PR | **Not required** |

---

## Confirmation (2026-08-09)

**File:** `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`

| Check | Result |
|-------|--------|
| Lookup SQL | `FROM one."ApiCredentials"` only (`OneLookupSql`) |
| `LhdnLookupSql` | **Absent** |
| `DeveloperApiKeys` in middleware | **Absent** |
| Lookup path | `LookupCredentialAsync` → `OneSqlConnectionFactory` only |
| Lhdn-only keys | **401** (by design after R05) |
| Remarks | Document One-only / R05; table drop still R06 |

**Classification:** L-07 dual-read boundary debt is **closed**. Residual host hardcode of `one.ApiCredentials` is allowed composition-root auth (not a multi-schema leak).

---

## Why no SQL PR under R17

Original R17 checklist assumed dual-read still intentional until R05. **R05 already landed** on this branch:

- Code + tests for One-only middleware  
- Revoke subscribe One-only  
- Notes: `r05-notes.md`

R17 therefore only **records** the handoff:

| Track | Role |
|-------|------|
| Keys R01–R05 | **Owns** the dual-read cutover (done in R05 code) |
| SQL R17 | Confirm dual-read removed; link exit to R05; no Contracts-port “fix” |
| Keys R06 | Table drop / archive (≥30d One-only in prod) — **out of L-07** |

Do **not** “fix” L-07 by moving Lhdn key SQL into a module without the Keys cutover — that path is obsolete; cutover already happened in R05.

---

## Residual (not L-07)

| Item | Owner | Note |
|------|-------|------|
| Deploy One-only to staging/prod | R05.5 | Gated on inventory Q8 `active_legacy_only = 0` |
| Drop `lhdn.DeveloperApiKeys` | R06 | After One-only soak |
| Host migrator store dual-schema SQL | R03 tooling | Not request-path dual-read |

---

## Files (docs only)

| Action | Path |
|--------|------|
| Notes | `plans/005-remaining/r17-notes.md` |
| Checklist | `plans/005-remaining/checklists/r17-sql-l07-apikey-handoff.md` |
| Live status | `plans/005-remaining/cross-schema-leaks-live.md` — L-07 fixed (R05) |
| Keys notes | `plans/005-remaining/r05-notes.md` |
| FULL-CHECKLIST | R17 section checked |

---

## Exit

| Criterion | Result |
|-----------|--------|
| Dual-read removed from middleware | **Yes** (R05) |
| Tracked under Keys R05 | **Yes** |
| Linked to R05; no separate SQL PR | **Yes** |
| L-07 live status | **fixed (R05)** |

**R17 complete.** L-07 closed by R05; SQL track has no further dual-read work.
