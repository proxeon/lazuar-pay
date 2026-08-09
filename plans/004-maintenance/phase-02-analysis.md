# Phase 02 — Analysis (Community / Vault documentation honesty)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Scope:** Docs + light comments only. No schema drops, no module re-adds.

## Findings → actions

| Artifact | Issue | Action taken |
| :--- | :--- | :--- |
| `packages/api-spec/README.md` | Documented deleted `auth`/`community` modules + Community refund sample | Full rewrite for live modules + `docs-*.tsp` (ADR 007) + `task gen` |
| `docs/001`–`002` | Community used as canonical examples | Rewrote to Commerce / One / CRM |
| `docs/003` | `CommunityPlan` / `CommunitySubscription` as live invariants | Obsolete banner + Commerce as current target language |
| `docs/004` | Active seed steps for `community.Plans/Subscriptions/PaymentRecords` | Obsolete banner; steps retargeted to one/crm/communications/commerce/billing; deleted tables struck through |
| `docs/005` | Community entities + `tenant.Organizations` | Commerce entities; `one.Organizations` |
| `docs/006` | Runnable `community.*` ReminderDispatchLogs SQL + `CommunityLifecycleJob` | Webhook sections kept; §4 marked obsolete with DO NOT RUN SQL |
| `docs/007`–`008` | — | No Community/Vault mentions |
| Messaging / CRM / Billing / Payments / One READMEs | Taught Community/Vault as live consumers | Pointed at Commerce / Communications |
| `AppOptions.ClientUrl` XML doc | “Community Enrollment” | Portal / public checkout language |
| `messaging/models.tsp` | “Templates migrated to Community” | Communications ownership |
| Template preview mock URL | `community.lazuar.com/checkout` | `portal.lazuar.com/checkout` |
| `DefaultMessageTemplates.OrphanNames` | Community* strings still present | Documented intentional retain until ops cleanup (ADR 022) — names **not** deleted |
| `DropLegacySchemas` migration | Must remain | Confirmed present under One migrations |
| Optional live schema drop (02.7) | Ops/DBA | **N/A** this PR; migration already exists |

## Decisions

- **Prefer banners over archive moves** for Community-heavy migration playbooks (003–006) so historical context remains discoverable but clearly non-operational.
- **Do not remove OrphanNames** Community* strings until product/ops confirms legacy-cleanup on all tenants.
- **No production schema work** in this phase (checklist 02.7).

## Out of scope

- Regenerating TypeSpec clients (`task gen`) — comment-only tsp change
- Dropping residual `community`/`vault` schemas on shared DBs if any still exist
- Rewriting ADR history docs that intentionally document Community/Vault removal
- Frontend / portal copy still using legacy paths
