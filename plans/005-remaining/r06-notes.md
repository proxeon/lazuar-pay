# R06 — Drop/archive `lhdn.DeveloperApiKeys` notes

**Status:** **DEFERRED** (2026-08-09)  
**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Track:** Keys  
**Checklist:** `checklists/r06-keys-table-drop.md`  
**Depends on:** R05 **live in prod** ≥ **30 days** (or signed waiver)  
**Analysis:** `01-api-key-one-only-cutover.md` § F04 / PR-D  
**Scope this pass:** **Docs only** — record deferral, residual inventory, preflight greps. **Do not** drop the table now.

---

## Summary

| Concern | State |
|---------|--------|
| Status | **DEFERRED** — clock has **not** started |
| R05 code on branch | **One-only** — middleware no longer reads `lhdn.DeveloperApiKeys` |
| R05 deploy staging/prod | **Pending** (see `r05-notes.md` DEPLOY gate) |
| 30-day soak clock | Starts when R05 is **live in prod**, not when One-only code lands on branch |
| Table `lhdn.DeveloperApiKeys` | **Still required** until soak + R06 execute |
| Lhdn domain / EF residue | Still present — inventory below for later drop |
| This pass | No app code, no migration, no commit of drop work |

**Invariant:** R05 One-only middleware on a feature branch is **not** the same as One-only in prod. The table remains the dual-era residue store and must stay until after the production monitoring window (or explicit waiver).

---

## Why deferred

1. **Clock gate:** R06 is calendar-gated on R05 **prod** One-only for ≥ **30 days** (analysis § F04; decisions dual-read calendar / early path still wait 30d after prod One-only).
2. **Deploy not done:** R05.5 / R05.6 exit are still open (`r05-notes.md`). No prod One-only date to start the clock.
3. **R04 residual:** Prod `active_legacy_only` (Q8) still pending ops; premature table drop is irrelevant until migrate + One-only are honest in prod.
4. **Rollback surface:** While dual-read rollback from a pre-R05 commit remains a possible recovery path for 401 spikes, keeping the table (and residual EF map) is cheap insurance. After R06 drop, restore is backup/archive only.

---

## Clock (when it starts)

| Milestone | Condition | Clock |
|-----------|-----------|--------|
| R05 code on branch | Done (middleware One-only) | **Does not start** R06 clock |
| R05 staging deploy + smoke | Pending | Staging soak optional; not the gate |
| **R05 prod live** | One-only middleware in **prod** | **Start** 30-day clock — record date in checklist R06.1 |
| Day 30 (or signed waiver) | No unexplained key-auth regressions | R06.1 preflight may proceed |
| R06 execute | Separate PR (drop **or** archive-rename) | Table gone/archived; FW-1 fully closed |

**Fill when known:**

| Field | Value |
|-------|--------|
| Prod One-only live date | _pending R05.5/R05.6_ |
| Clock start | Same as above |
| Earliest R06 execute (default) | live date + **30 days** |
| Waiver (if any) | _none_ |

---

## Explicit: do **NOT** drop now

Do **not** in this pass or any PR before the clock gate:

- EF migration that **drops** or renames `lhdn.DeveloperApiKeys`
- Remove `DeveloperApiKey` aggregate / DbSet / repo methods “because middleware is One-only”
- Delete R03 migrator SQL that still reads the legacy table (until R04 fully executed and job is retired)
- Claim FW-1 fully closed in `FUTURE-WORK.md` while table + residue remain
- Ship drop in the same PR as R05 middleware (analysis: prefer separate F04/R06 after soak)

Table is **still required** until soak even though app auth no longer reads it.

---

## Residual file inventory (drop later)

Paths relative to `apps/lazuar-api/` unless noted. Refresh with greps in § Preflight before the R06 PR.

### Domain / ports / infrastructure (keep until R06)

| Artifact | Path | R06 action |
|----------|------|------------|
| Aggregate | `Modules/Lhdn/Domain/Aggregates/DeveloperApiKey.cs` | Delete if unused after table drop |
| Scope helper xref | `Modules/Lhdn/Domain/ApiKeyScopes.cs` | Keep if façades still use scope constants; drop aggregate xref only |
| Repo ports | `Modules/Lhdn/Application/Ports/ILhdnRepository.cs` — `Get/List/AddDeveloperApiKey` | Remove methods |
| Repo impl | `Modules/Lhdn/Infrastructure/Repositories/LhdnRepository.cs` | Remove methods |
| DbSet + EF | `Modules/Lhdn/Infrastructure/LhdnDbContext.cs` — `DeveloperApiKeys` | Remove DbSet + `Entity<>` config |
| Lhdn revoke event type | `Modules/Lhdn/Contracts/Events/ApiKeyRevokedIntegrationEvent.cs` | Delete if no residual outbox need (confirm empty / aged out) |

### Migrations / table

| Artifact | Path | R06 action |
|----------|------|------------|
| Create table | `Modules/Lhdn/Infrastructure/Migrations/20260627124829_InitialLhdnSchema.cs` | Historical — leave; new migration drops/renames |
| Scopes/hint | `Modules/Lhdn/Infrastructure/Migrations/20260803171454_AddDeveloperApiKeyScopesAndKeyHint.cs` | Historical |
| Snapshot + designers | `LhdnDbContextModelSnapshot.cs` + `*.Designer.cs` with `DeveloperApiKey` | Updated by new drop/archive migration |
| Live table | `lhdn."DeveloperApiKeys"` | **Drop** or **rename to archive schema** (product choice still open) |

### R03 migrator (retire after R04 done + no longer needed)

| Artifact | Path | R06-era action |
|----------|------|----------------|
| Hosted job + store | `src/Lazuar.Api/Jobs/ApiKeyMigration/*` | Remove or permanently disable after prod migrate verified and ops no longer need re-run |
| DI / Program wire | `src/Lazuar.Api/Program.cs` (R03 comment + registration) | Remove with job |
| Tests | `tests/Lazuar.ModuleTests/One/LegacyApiKeyMigratorTests.cs` | Remove with job |

Migrator **reads** `lhdn.DeveloperApiKeys` by design. Do not delete migrator solely because middleware is One-only if R04 staging/prod may still need a re-run.

### Intentionally **not** residual for table drop

| Artifact | Notes |
|----------|--------|
| Lhdn HTTP `/api-keys` façades | One-backed; product surface — out of R06 unless product deprecates |
| One `ApiCredentials` / mint/list/revoke | SSoT — keep |
| Middleware One-only path | R05 — already done on branch |
| Host One-only revoke subscribe | R05 — already done on branch |
| `Modules/One/Domain/ApiCredential.cs` xmldoc mentioning former Lhdn shape | Docs only |

### Comments / docs (update on execute)

| Location | Note |
|----------|------|
| `ModuleRegistrationExtensions.cs` | Mentions “Table drop … is R06” |
| `ApiKeyAuthenticationTests` comment | R05 regression wording OK to keep |
| One/Lhdn README, `api-key-cutover-design.md`, `FUTURE-WORK.md` FW-1 | Close fully after R06 |
| Plan notes R01–R05 | Historical; link from this file |

---

## Preflight greps (run before R06.2 — not now)

From repo root / `apps/lazuar-api` as appropriate. Expect **hits** today on residue; R06 exit expects app **runtime** paths clean of read/write.

```bash
# Auth path must stay clean (R05 invariant — should already be empty)
rg 'DeveloperApiKeys|LhdnLookupSql' apps/lazuar-api/src/Lazuar.Api/Middleware

# Full residual map (expect domain/repo/EF/migrator hits until R06)
rg 'DeveloperApiKey' apps/lazuar-api --glob '*.cs'

# SQL / table name
rg 'lhdn\."DeveloperApiKeys"|DeveloperApiKeys' apps/lazuar-api --glob '*.{cs,sql}'

# Lhdn revoke event (host must not re-subscribe; type may remain until outbox clear)
rg 'Modules\.Lhdn\.Contracts\.Events\.ApiKeyRevoked|Lhdn.*ApiKeyRevoked' apps/lazuar-api --glob '*.cs'

# Migrator still referencing legacy table
rg 'DeveloperApiKeys|LegacyDeveloperApiKey' apps/lazuar-api/src/Lazuar.Api/Jobs
```

**Preflight checklist when un-deferring:**

1. One-only prod date + ≥30d (or waiver) recorded in R06.1.
2. Middleware grep: **no** `DeveloperApiKeys` / `LhdnLookupSql`.
3. App product path: no mint/list/revoke against Lhdn table (façades remain One-only).
4. Outbox: no residual Lhdn `ApiKeyRevoked` events that still need the type for deserialization (or accept type leave-behind).
5. Ops: backup/PITR note; choose **drop** vs **archive-rename**.
6. After migration: re-run greps; architecture / module tests green.

---

## Planned execute shape (when unblocked)

Align with analysis PR-D / F04:

| Include | Exclude |
|---------|---------|
| EF migration: drop **or** rename to archive | Dual-read reintroduction |
| Remove dead `DeveloperApiKey` domain/repo/DI/DbSet | Product feature work |
| Optional: retire R03 migrator job + tests | Same PR as unrelated keys work |
| Docs: FW-1 fully closed in `FUTURE-WORK.md` | Claiming done without greps/tests |

**Title idea:** `chore(lhdn): drop/archive DeveloperApiKeys after One-only soak (R06 / FW-1 F04)`

---

## Checklist ticks (this pass)

| Item | State |
|------|--------|
| R06 overall | **DEFERRED** 2026-08-09 |
| R06.1 Preflight | Unchecked — clock not started |
| R06.2 Migration | Unchecked — do not run |
| R06.3 Exit | Unchecked |

---

## Explicit non-goals (this docs pass)

- Do **not** drop or archive `lhdn.DeveloperApiKeys`.
- Do **not** remove Lhdn domain/EF residue “early.”
- Do **not** start or invent a 30-day clock without prod One-only date.
- Do **not** claim R05 prod cutover or R04.3 from this note.
- No application code changes; no commit required for this note alone.
