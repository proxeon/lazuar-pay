# 005 Remaining — detailed implementation checklists

**Status:** Ready to execute  
**Date:** 2026-08-09  
**Style:** Many **small** phase files (not fat catch-alls). One phase ≈ one PR (or smaller).  
**How-to analyses:** parent `../01-…`–`../10-…`  
**Prior scaffold:** `../../004-maintenance/checklists-future/` (F00–F16) — this folder **supersedes for execution detail**; keep F-map for orientation.

## Rule: not one mega-PR

| Do | Don’t |
|----|--------|
| One phase / one leak / one ownership move per PR | Land keys + webhooks + BB + SQL in one branch tip |
| Parallel **tracks** after R00 | Parallel **inside** Keys (F03 before F02) |
| Honor product/calendar gates | Remove dual-read before migration |

## Track map

```text
R00 Align
  ├─ Track Keys:     R01 → R02 → R03 → R04 → (wait) → R05
  ├─ Track SQL:      R10 → R11 → R12 → R13 → R14 → R15 → R16 → R17
  ├─ Track TypeSpec: R20 → R21 → R22 → R23 → R24 → R25
  ├─ Track BB:       R30 → R31 → R32 → R33 → R34 → R35
  ├─ Track Webhooks: R40 → R41 → R42 → R43
  ├─ Track Polish:   R50 → R51 → R52 → R53
  └─ Track Extract:  R60 (default SKIP)
R99 Definition of done
```

## Phase index

### Program

| ID | File | Intent |
|----|------|--------|
| R00 | [`r00-wave-align.md`](./r00-wave-align.md) | Which tracks this wave |
| R99 | [`r99-definition-of-done.md`](./r99-definition-of-done.md) | Close remaining program |

### Track Keys (bullet 1 · analysis 01)

| ID | File | Intent |
|----|------|--------|
| R01 | [`r01-keys-code-inventory.md`](./r01-keys-code-inventory.md) | Refresh dual-read / mint map |
| R02 | [`r02-keys-data-inventory.md`](./r02-keys-data-inventory.md) | Staging/prod row counts |
| R03 | [`r03-keys-migrator-implement.md`](./r03-keys-migrator-implement.md) | Idempotent migrator |
| R04 | [`r04-keys-migrate-staging-prod.md`](./r04-keys-migrate-staging-prod.md) | Run migration |
| R05 | [`r05-keys-one-only-middleware.md`](./r05-keys-one-only-middleware.md) | Remove dual-read |
| R06 | [`r06-keys-table-drop.md`](./r06-keys-table-drop.md) | Drop Lhdn table (≥30d) |

### Track SQL (bullet 4 · analysis 06)

| ID | File | Intent |
|----|------|--------|
| R10 | [`r10-sql-inventory-refresh.md`](./r10-sql-inventory-refresh.md) | Re-grep, ticket table |
| R11 | [`r11-sql-l01-document-published.md`](./r11-sql-l01-document-published.md) | L-01 Communications |
| R12 | [`r12-sql-l02-platform-superadmin.md`](./r12-sql-l02-platform-superadmin.md) | L-02 Payments→one |
| R13 | [`r13-sql-l03-arrears-update.md`](./r13-sql-l03-arrears-update.md) | L-03 Commerce arrears |
| R14 | [`r14-sql-l05-document-lookup-crm.md`](./r14-sql-l05-document-lookup-crm.md) | L-05 CommerceDocumentLookup |
| R15 | [`r15-sql-l04-dead-template-sql.md`](./r15-sql-l04-dead-template-sql.md) | L-04 delete dead SQL |
| R16 | [`r16-sql-l06-metrics-handoff.md`](./r16-sql-l06-metrics-handoff.md) | L-06 → metrics track R35 |
| R17 | [`r17-sql-l07-apikey-handoff.md`](./r17-sql-l07-apikey-handoff.md) | L-07 → keys track R05 |

### Track TypeSpec Wave B (bullet 6 · analysis 08)

| ID | File | Intent |
|----|------|--------|
| R20 | [`r20-tsp-dual-dto-products.md`](./r20-tsp-dual-dto-products.md) | Product create/update DTOs |
| R21 | [`r21-tsp-dual-dto-refund.md`](./r21-tsp-dual-dto-refund.md) | Record refund DTO |
| R22 | [`r22-tsp-broadcast-preview-status.md`](./r22-tsp-broadcast-preview-status.md) | Preview/status honesty |
| R23 | [`r23-tsp-billing-pdf-honesty.md`](./r23-tsp-billing-pdf-honesty.md) | Signed PDF |
| R24 | [`r24-tsp-payments-security-schemes.md`](./r24-tsp-payments-security-schemes.md) | Docs security |
| R25 | [`r25-tsp-path-honesty-ci.md`](./r25-tsp-path-honesty-ci.md) | OpenAPI ⊆ Minimal + allowlist |

### Track BuildingBlocks (bullet 3 · analyses 03–05)

| ID | File | Intent |
|----|------|--------|
| R30 | [`r30-bb-port-hygiene.md`](./r30-bb-port-hygiene.md) | Ports in Application |
| R31 | [`r31-bb-llm-factory-to-ops.md`](./r31-bb-llm-factory-to-ops.md) | Factory/policies/title → Ops |
| R32 | [`r32-bb-agent-tools-to-ops-contracts.md`](./r32-bb-agent-tools-to-ops-contracts.md) | AgentTool + prompt port |
| R33 | [`r33-bb-magic-link-to-commerce.md`](./r33-bb-magic-link-to-commerce.md) | Magic link shapes |
| R34 | [`r34-bb-email-messaging-to-messaging.md`](./r34-bb-email-messaging-to-messaging.md) | Email/IMessagingService |
| R35 | [`r35-bb-metrics-plugins.md`](./r35-bb-metrics-plugins.md) | Contributors + schema reg |

### Track Webhooks (bullet 2 · analysis 02)

| ID | File | Intent |
|----|------|--------|
| R40 | [`r40-webhooks-product-lock.md`](./r40-webhooks-product-lock.md) | Signing/payload/routing |
| R41 | [`r41-webhooks-registry-backfill.md`](./r41-webhooks-registry-backfill.md) | Lhdn → One endpoints |
| R42 | [`r42-webhooks-enqueue-path.md`](./r42-webhooks-enqueue-path.md) | A1 enqueue to One outbox |
| R43 | [`r43-webhooks-retire-fire-and-forget.md`](./r43-webhooks-retire-fire-and-forget.md) | Remove fire-and-forget |

### Track Polish (bullet 6 · analysis 09)

| ID | File | Intent |
|----|------|--------|
| R50 | [`r50-polish-testsupport-batch.md`](./r50-polish-testsupport-batch.md) | TestSupport N tests |
| R51 | [`r51-polish-lhdn-gateway-partials.md`](./r51-polish-lhdn-gateway-partials.md) | LhdnGatewayAdapter |
| R52 | [`r52-polish-llm-stream-partial.md`](./r52-polish-llm-stream-partial.md) | LLM stream split |
| R53 | [`r53-polish-gateway-common-outbox-di.md`](./r53-polish-gateway-common-outbox-di.md) | GatewayCommon + outbox DI pilot |

### Track Extract (bullet 5 · analysis 07)

| ID | File | Intent |
|----|------|--------|
| R60 | [`r60-extract-gate-only.md`](./r60-extract-gate-only.md) | Default SKIP |

## Suggested first wave (default R00)

1. R00  
2. Parallel: **R01–R02** (keys invent), **R10** (SQL invent), **R20–R21** (easy TypeSpec), **R30** (BB ports)  
3. Then: **R03–R04** keys migrate, **R11–R15** SQL fixes, **R31+** BB  
4. Then: **R05** One-only (after migrate)  
5. Webhooks only if R40 locked: R41–R43  
6. R50–R53 polish opportunistic  
7. R06 after 30d; R99 close-out  

## PR hygiene (every phase)

- [ ] Read linked analysis section first  
- [ ] Single intent in PR title  
- [ ] Tests / `task gen` as needed  
- [ ] Architecture tests if boundaries move  
- [ ] Update `../FUTURE-WORK.md` status when a track finishes  
- [ ] No outbox type renames without migration note  
