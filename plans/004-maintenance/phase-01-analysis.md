# Phase 01 — Analysis (secrets & dead residue)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Scope:** Zero-risk deletes + secret jar removal only.

## Findings → actions

| Artifact | Risk | Action taken |
|----------|------|--------------|
| `scripts/lhdn_sandbox/cookies.txt` | Secret (JWT cookie jar) | Deleted; gitignored |
| `packages/api-types-dotnet/Generated/Models.cs` | Dead (not compiled) | Deleted; `Generated/` removed |
| ADR 005 path `Generated/Models.cs` | Stale docs | Pointed at `Lazuar.ApiContracts.cs` |
| `UblStrategyTests.cs` | No-op (fully commented) | Deleted |
| `lhdn-golden-master.json` + EmbeddedResource | Orphan fixture | Deleted + csproj line removed |
| `script/second-app-proof.md` | Orphan harness note | Deleted; empty `script/` removed |
| Empty `Lazuar.slnx` Folder nodes | Clutter | Pruned |

## Decisions

- **History scrub** for cookies JWT: **out of scope** for this PR (current tree only).
- **Session rotation** for `sysadmin@lazuars.io`: ops practice outside the repo.
- **Doc/UI mentions** of `script/second-app-proof.md` left in place (payments quickstart, lazuar-docs, developers page) — not blocking; optional follow-up.

## Out of scope

- Regenerating UBL golden tests
- Broader gap-doc rewrites that still mention deleted paths
- Full solution-wide refactors
