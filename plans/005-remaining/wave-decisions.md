# Wave decisions — remaining program 005

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Phase:** R00 complete  
**Analysis:** `10-program-sequencing-and-risks.md`, `checklists/r00-wave-align.md`  
**Decisions source:** `../004-maintenance/decisions.md`

## Track selection

| Track | In this wave? | Phases |
|-------|---------------|--------|
| Keys | YES | R01–R06 |
| SQL | YES | R10–R17 |
| TypeSpec | YES | R20–R25 |
| BuildingBlocks | YES | R30–R35 |
| Webhooks | YES | R40–R43 (R40 = product defaults from 00.2 A + analysis 02) |
| Polish | YES | R50–R53 |
| Extract | NO | R60 skip |

## Delivery

- Long-lived branch: `chore/remaining-005`
- One phase ≈ one commit; push after each
- Dual-read keys not removed before R04 migrate complete
- No second Lhdn webhook stack (00.2 B rejected)

## Freezes still in force

- Keys dual-read until 2026-11-30 (early cutover OK if active legacy = 0)
- RevenueRecognitionJob parked (00.3)
- WhatsApp / multi-channel frozen (00.4)
- No new modules without R60 product gate (00.6)

## Ordered start list

1. R00 (done)
2. Parallel band: R01→R02, R10, R20–R21, R30, R40, (R50 if idle)
3. Then: R03→R04, R11–R15, R31+, R41 after R40, TypeSpec remainder
4. Then: R05 (after migrate), R42→R43, R35 (after R16 handoff / R10)
5. R06 after ≥30d One-only in prod
6. R99 close-out when selected tracks complete/deferred with dates
7. R60 not started

## Webhooks R40 seed defaults (full locks in R40 artifact)

- End-state A; reject B
- Signing: One t=,v1=; dual-verify window if needed
- Payload: platform envelope + LHDN data
- Routing: migrate to TenantWebhookEndpoints + EnabledEvents
- Design: A1 OutboundWebhookRequestedIntegrationEvent
- Artifact path for R40: `plans/005-remaining/webhook-convergence-decisions.md`

## Calendar anchors

| Milestone | Date |
|-----------|------|
| Dual-read allowed until | 2026-11-30 |
| One-only target | 2026-12-15 |
| Table drop | ≥30d after One-only prod |

## Serial rules (hard)

- Keys: R01 → R02 → R03 → R04 → R05 → (≥30d) → R06
- Webhooks: R40 → R41 → R42 → R43
- SQL: R10 before R11–R15; R16 handoff R35; R17 handoff R05
- No R05 before R04 residual 0
- No second Lhdn webhook stack
