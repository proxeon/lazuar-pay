# Phase 00 — Analysis: recommended locked decisions

**Role:** Phase 00 ANALYZER (recommendations only — **not** product sign-off)  
**Date:** 2026-08-09  
**Branch context:** `chore/backend-maintenance-004`  
**Inputs:**  
- [`phase-00-align-decisions.md`](./phase-00-align-decisions.md) (questions to answer)  
- [`../04-module-boundaries-modularization.md`](../04-module-boundaries-modularization.md)  
- [`../10-maintenance-questions-roadmap.md`](../10-maintenance-questions-roadmap.md)  
- Checklist map: Phase 03 (keys), 04 (webhooks), 16 (extract/merge), 17 (deferred jobs)  

**Output purpose:** Give an implementer enough **explicit text** to write  
`plans/004-maintenance/decisions.md` (or fill the blanks in `phase-00-align-decisions.md`) **without inventing product policy**.  

**Status of this file:** **RECOMMENDED defaults for Lazuar Pure CaaS MVP** (ADR 019/023).  
Team must promote these to **LOCKED** by committing `decisions.md` (or checking off 00.1–00.6) before Phase 03/04 dual-path work.

**Production-safe posture (summary):** Prefer **one long-term SSoT**, **dated dual-read / freeze windows**, **park liminal features**, **no new modules**, **no merge/extract without product trigger**.

---

## How the implementer should use this

1. Copy each **LOCKED draft** block into `plans/004-maintenance/decisions.md` under matching `00.x` headings.  
2. Replace status `RECOMMENDED` → `LOCKED` only after human product/eng agreement.  
3. Fill calendar dates if the team wants different cutovers; do not leave “someday.”  
4. Phase 01 may start **before** frontend work; it does **not** require Phase 03 cutover complete.  
5. Do **not** implement dual-path removals until Phase 00 is committed.

---

## 00.1 API credentials end-state

### Evidence (analysis)

| Fact | Source |
|------|--------|
| Platform store is One `ApiCredential` / `one.ApiCredentials` | Report 04 §5.2C, §7.2; roadmap 10 §1.1 |
| Legacy store is Lhdn `DeveloperApiKey` / `lhdn.DeveloperApiKeys` | Same; middleware dual-read |
| Host dual-subscribes `ApiKeyRevokedIntegrationEvent` from **both** One and Lhdn | Report 04 §6.2; Phase 03 checklist |
| Dual-read forever = security + cache-invalidation tax | Roadmap 10 T0.1 / §1.1 |
| Extracting a “Developer” module is **later**, not maintenance | Report 04 §9.3 — finish migration first |

### Recommended decision

| Checklist item | Recommendation |
|----------------|----------------|
| Long-term mint/list/revoke store | **One `ApiCredentials` only** |
| LHDN `DeveloperApiKeys` role until cutover | **Legacy dual-read only** (auth may still validate legacy rows) |
| Cutover posture | **B — Keep dual-read with a calendar end date**, then execute Phase 03 migrate → single-read |
| Scopes model | **Yes** — LHDN product scopes live on One credentials (no parallel scope system) |
| Revoke event end-state after cutover | **One `ApiKeyRevokedIntegrationEvent` only** |

### Cutover posture detail (dated strategy — production-safe)

**Chosen: Option B (dated dual-read), leading into Option A’s end-state (delete LHDN store + dual middleware).**

Do **not** delete dual-read on day one of maintenance. Sequence:

| Step | Action | Owner phase |
|------|--------|-------------|
| 1 | **SSoT for writes:** all new mint/list/revoke → One only; block or façade-stop new `lhdn.DeveloperApiKeys` inserts | Phase 03.3 |
| 2 | Dual-read remains in `ApiKeyAuthenticationMiddleware` (prefer **One first**, then Lhdn legacy) | Phase 03.2 design |
| 3 | Inventory + migrate remaining legacy rows (staging → prod) with runbook | Phase 03.1, 03.4 |
| 4 | Remove Lhdn lookup + collapse revoke to One event only | Phase 03.5 |
| 5 | Optional table drop / archive after monitoring window | Phase 03.6 |

**Recommended dual-read calendar (implementer: put exact date in decisions.md):**

| Milestone | Recommended date | Meaning |
|-----------|------------------|---------|
| Dual-read **allowed until** | **2026-11-30** | Auth may still hit `lhdn.DeveloperApiKeys` |
| Target dual-read **removed by** | **2026-12-15** | Middleware One-only; dual revoke subscription gone |
| Table drop / archive | **≥ 30 days after** One-only in prod | Phase 03.6 |

If prod row count is zero earlier, cutover may move **forward** (never leave dual-read open “because it still works”).

### Rationale

- **Production-safe:** avoids mass 401 for integrators still on LHDN-minted keys.  
- **Honest end-state:** One is SSoT; LHDN store is not a permanent second product.  
- Aligns with report 04 P0 “finish API credential unification” and roadmap T0.1.  
- Rejects permanent dual-store and rejects “extract Developer module first.”

### LOCKED draft (copy into decisions.md)

```markdown
## 00.1 API credentials

- **SSoT (long-term):** One `ApiCredentials` is the only mint/list/revoke store.
- **Legacy:** `lhdn.DeveloperApiKeys` is dual-read only until cutover; no new permanent product surface.
- **Cutover posture:** B — dual-read until **2026-11-30**; target One-only middleware + One revoke event by **2026-12-15** (Phase 03).
- **Scopes:** LHDN scopes are modeled on One credentials (yes).
- **Revoke after cutover:** One `ApiKeyRevokedIntegrationEvent` only.
- **Read order during dual-read:** One first, then Lhdn legacy (document in Phase 03.2).
```

### Explicit non-decisions (do not invent)

- Hash/format migration algorithm details → Phase 03.2 design doc, not Phase 00.  
- Whether LHDN HTTP routes remain as One-backed façades → Phase 03.5 + TypeSpec honesty (Phase 05).

---

## 00.2 Outbound webhooks end-state

### Evidence (analysis)

| Fact | Source |
|------|--------|
| One has durable stack: `WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob` + signing | Report 04 §5.2C; roadmap 10 §1.1; Phase 04 |
| LHDN customer webhooks use fire-and-forget (`WebhookSenderService` class of path) | Roadmap residual B.4.4; Phase 04.1 |
| Improving **both** forever is forbidden debt | Roadmap 10 §1.1 “Do not improve both forever” |
| Extract Webhooks module is **P2–P3 future**, after credential migration | Report 04 §9.3 |
| Phase 04 options A / B / C | `phase-04-webhooks-converge.md` |

### Recommended decision

| Checklist item | Recommendation |
|----------------|----------------|
| Platform model | **One durable dispatcher is the platform webhook model** (quality bar for anything sold as “webhooks”) |
| LHDN customer webhooks | **Preferred end-state: A — route LHDN lifecycle through One dispatcher** |
| Reject for MVP maintenance | **B — give Lhdn a second full outbox/signing stack** (duplicates platform tax) |
| Interim until Phase 04 completes A | **Treat LHDN fire-and-forget as freeze special-case (C discipline)** — document debt; no parallel “improvements” that build a second full product stack |
| Phase 04 write-in | **A (end-state) + C freeze rules until A lands** |

### Production-safe interpretation

For **Pure CaaS MVP** shipping checkout/dunning:

1. **Lock quality bar:** multi-endpoint, retries, signing, outbox = **One only**.  
2. **Lock end-state for LHDN customer outbound:** **A** (converge to One).  
3. **Until Phase 04 implements A:** operate under **C freeze rules**:
   - ADR/module README: “LHDN outbound is fire-and-forget by design until Phase 04 A.”  
   - Add/keep observability so silent failure is visible (Phase 04.2-C items).  
   - **Do not** invent Lhdn-local outbox (option B).  
   - **Do not** half-upgrade fire-and-forget into a second durable stack.  
4. When Phase 04 runs: implement **A** (map LHDN lifecycle → One outbox; preserve or version customer payloads).

If product later discovers LHDN payloads/signing cannot share One without breaking integrators, **re-open 00.2** and choose B with an ADR — do not silently fork.

### Rationale

- Matches roadmap T0.5 and definition of maintenance-healthy (“one outbound webhook delivery stack quality bar”).  
- A is preferred over B: one durable primitive, One already owns developer webhooks.  
- C alone forever would leave reliability claims dishonest for LHDN lifecycle — acceptable only as **dated freeze**, not permanent product design without reopening.  
- Aligns with “One durable as platform; LHDN converge to One or freeze with docs.”

### LOCKED draft (copy into decisions.md)

```markdown
## 00.2 Outbound webhooks

- **Platform model:** One `WebhookDeliveryOutbox` + dispatcher job + signing is the only platform-grade webhook system.
- **LHDN end-state (Phase 04):** **A** — route LHDN lifecycle customer webhooks through One dispatcher.
- **Rejected for this track:** **B** — second full Lhdn outbox/signing stack.
- **Interim (until A ships):** **C freeze** — fire-and-forget remains; document debt; observability only; no second stack “improvements.”
- **Written choice:** A (end-state) + C (interim freeze discipline).
- **Module extract:** Webhooks stay in One for this maintenance track (no `Modules/Webhooks` unless Phase 16 trigger).
```

---

## 00.3 Deferred revenue / RevenueRecognitionJob

### Evidence (analysis)

| Fact | Source |
|------|--------|
| `RevenueRecognitionJob` exists; **not registered** in DI | Report 04 §5.2B, §7.3; roadmap 10 §1.1 |
| `DeferredRevenueSchedule` entity/table kept | Same |
| Product not near-term finance/Xero schedule creation for MVP | ADR 023 Pure CaaS; roadmap T3.5 |
| Options: delete / implement / park | Phase 00.3; Phase 17.1 |

### Recommended decision

| Checklist item | Recommendation |
|----------------|----------------|
| Choice | **C — Park** with ADR (or Billing README) note |
| Do not | Delete table in this track without finance product OK |
| Do not | Implement schedule creation under “maintenance” |
| Phase 17 | Follow park path: document unregistered-by-design; no UI/metrics claiming it runs |

### Rationale

- **Production-safe:** avoids migration risk of table drop and avoids fake “feature complete” finance path.  
- Liminal delete-or-build is worse than an explicit park note (roadmap §1.1).  
- Revisit only when finance / deferred-revenue / Xero work is a scheduled product epic (not Phase 00–18 hygiene).

### LOCKED draft (copy into decisions.md)

```markdown
## 00.3 RevenueRecognitionJob / deferred revenue

- **Choice:** **C — Park**.
- **Note (required):** “`RevenueRecognitionJob` is unregistered by design until a product epic owns deferred revenue schedule creation (finance / Xero track). Entity/table may remain; no shipping claim that recognition runs.”
- **Not in this maintenance track:** implement schedule writers; drop table without product OK.
- **Phase 17:** park path only (README/ADR honesty; no metrics lie).
```

---

## 00.4 Messaging / WhatsApp / multi-channel

### Evidence (analysis)

| Fact | Source |
|------|--------|
| Messaging is thin transport; Communications orchestrates content | Report 04 §5.2G, §10.1 |
| `ConsoleMessagingService` is production-default WhatsApp path | Roadmap 10 §1.1 |
| Merge Messaging → Communications recommended **when** real WhatsApp is implemented | Report 04 §10.1, §12 P2 |
| Roadmap 10 §4.2 also values separation until multi-channel product | Slight tension with 04 — resolve via **time horizon** |
| Phase 16 is **trigger-only** | checklists README |

### Recommended decision

| Checklist item | Recommendation |
|----------------|----------------|
| WhatsApp / multi-channel in next **6 months**? | **No** (MVP default: email-real; WhatsApp not committed) |
| If no | **Freeze Messaging as thin transport**; no expansion without product plan |
| Merge Messaging → Communications | **Not now** — deferred to Phase 16 only when Meta Cloud (or equivalent) is a funded product decision |
| Console provider | Keep + **document honesty** (email-only / console WA); no marketing claim of automated WhatsApp dunning as production |

### Rationale

- Production-safe MVP: do not spend maintenance budget on module merge or Meta Cloud.  
- Report 04 merge ROI appears when channel adapters become real; until then merge cost > benefit.  
- Roadmap “do not merge yet” and 04 “merge when WhatsApp” agree if **merge is gated on product**, not done prophylactically.  
- Phase 17.4 aligns: freeze → document; ship → product Phase D, not silent maintenance.

### LOCKED draft (copy into decisions.md)

```markdown
## 00.4 Messaging / WhatsApp

- **WhatsApp / multi-channel in next 6 months:** **No**.
- **Choice:** Freeze Messaging as thin transport; Communications remains content/policy owner.
- **No merge now:** Messaging → Communications is **out of scope** until product commits a real multi-channel provider (then Phase 16).
- **Honesty:** Console WhatsApp is not a production channel; docs/defaults must not claim automated WhatsApp dunning as live.
- **BuildingBlocks ports:** `IMessagingService` / email ports stay technical; channel product work is not “just another adapter PR” without 00.4 reopen.
```

---

## 00.5 Credits vs Billing ledger

### Evidence (analysis)

| Fact | Source |
|------|--------|
| Credits wallet + double-entry ledger share Billing module | Report 04 §5.2B, §9.2 |
| Credits extract is **strongest future extract**, trigger = monetization + PR conflict | Report 04 §9.2, §12 P3 |
| Roadmap: do not extract Ledger service / Credits for maintenance | Roadmap 10 §4.1–4.2 |
| Phase 16 optional extract only with product trigger | `phase-16-optional-extract-merge.md` |

### Recommended decision

| Checklist item | Recommendation |
|----------------|----------------|
| Credits remain **inside Billing** for next **6–12 months**? | **Yes** |
| Extract trigger (if later) | Credit packages/promos become primary SaaS monetization **and** ledger PRs regularly conflict with wallet PRs |
| Until then | Optional **folder** partition only (`Billing/Wallet/*`) — **no** new module |

### Rationale

- MVP utility credits (LHDN/comms deduct) do not justify module tax (4 projects + schema + outbox + arch tests).  
- Production-safe: zero migration of balances during maintenance.  
- Aligns with “no new modules” and report 04 extraction framework.

### LOCKED draft (copy into decisions.md)

```markdown
## 00.5 Credits vs Billing

- **Credits stay in Billing for 6–12 months:** **Yes** (through at least **2027-02-09** unless product reopens).
- **No Credits/Wallet module** in this maintenance track.
- **Phase 16 extract trigger:** credit monetization is product-critical **and** change-rate diverges from merchant ledger (see report 04 §9.2).
- **Allowed without reopen:** internal folders/namespaces inside Billing only.
```

---

## 00.6 Scope freeze for this maintenance track

### Evidence (analysis)

| Fact | Source |
|------|--------|
| Nine modules appropriate; further modularization mostly premature | Report 04 §1, §11–12, §16; roadmap 10 §4 |
| Community/Vault modules already gone from backend; stay deleted (ADR 022) | Report 04 §8; roadmap 10 §1.1 |
| Frontend out of scope except forced client regen | plans/004 README; checklists README |
| Commerce `Endpoints/` pattern is house style | Report 02/10 chunk guidance; Phase 07 |

### Recommended decision

| Checklist item | Recommendation |
|----------------|----------------|
| No new modules unless Phase 16 trigger | **Confirm** |
| Frontend out of scope except forced TypeSpec client regen | **Confirm** |
| Community/Vault stay deleted (no resurrection) | **Confirm** |
| House style = Commerce `Endpoints/` pattern | **Confirm** |

### Explicit non-goals (maintenance track)

Do **not** use this track to:

- Create modules for Dunning, Catalog, Webhooks/Developer, Credits, Identity-vs-Tenancy, Tax, Analytics, Marketplace, Community rebuild  
- Microservice-split the modular monolith  
- Implement Meta WhatsApp, Xero, multi-country tax as “cleanup”  
- Revive Community/Vault modules or schemas as product  

### LOCKED draft (copy into decisions.md)

```markdown
## 00.6 Scope freeze

- **No new modules** unless a Phase 16 product trigger is explicitly reopened and agreed.
- **Frontend:** out of scope except forced client regen from TypeSpec (`task gen` / committed clients policy).
- **Community / Vault:** remain **deleted**; no module resurrection; docs honesty is Phase 02 (not rebuild).
- **House style:** Commerce `Endpoints/` + thin map façade for endpoint chunking (Phase 07+).
- **Module count target:** stay at current nine product modules for this track.
```

---

## 00.7 Exit criteria for Phase 00

Phase 00 is done when:

| Criterion | Met by |
|-----------|--------|
| 00.1–00.6 answered in writing and committed | `plans/004-maintenance/decisions.md` **or** checked `phase-00-align-decisions.md` with filled blanks |
| Team agrees Phase 01 can start without waiting on frontend | Explicit note in decisions.md |
| Dual-path phases (03, 04) have a non-guessed end-state | This analysis → decisions.md |

### Suggested `decisions.md` header (implementer)

```markdown
# 004 Maintenance — Locked decisions (Phase 00)

**Status:** LOCKED  
**Locked date:** YYYY-MM-DD  
**Based on:** plans/004-maintenance/checklists/phase-00-analysis.md  
**Product posture:** Lazuar Pure CaaS MVP (ADR 019/023)

Phase 01 (secrets / dead residue) may start immediately after this file is committed.
Frontend is not a gate.
```

---

## Decision matrix (quick reference)

| ID | Topic | Recommended lock | Phase that consumes it |
|----|--------|------------------|------------------------|
| 00.1 | API keys | One SSoT; **dual-read until 2026-11-30**; One-only target by 2026-12-15; LHDN scopes on One; One revoke only after cutover | 03 |
| 00.2 | Webhooks | One durable = platform; **LHDN → A converge**; **C freeze interim**; reject B | 04 |
| 00.3 | Revenue recognition | **Park (C)** with ADR/README note | 17 |
| 00.4 | Messaging / WA | **No WA in 6 months**; freeze thin Messaging; **no merge** | 12, 16, 17 |
| 00.5 | Credits | **Stay in Billing 6–12 months** | 16 (later only) |
| 00.6 | Scope | **No new modules**; FE out; Community/Vault dead; Commerce Endpoints house style | All |

---

## Conflicts resolved (04 vs 10)

| Tension | Resolution for MVP |
|---------|-------------------|
| Report 04 soft-yes Messaging→Communications merge vs roadmap “do not merge yet” | **No merge now**; merge only on funded multi-channel product (Phase 16) |
| Webhooks extract candidate vs platform in One | **Stay in One**; extract not maintenance |
| Credits strongest extract vs no new modules | **Stay in Billing 6–12 months**; extract is Phase 16 trigger only |
| Deferred revenue delete vs park | **Park** (safer than drop without finance owner) |
| Dual keys cutover A (immediate migrate/delete) vs B (dated dual-read) | **B dated**, then achieve A’s end-state via Phase 03 |

---

## What the implementer must **not** do after reading this

- Do not implement code for Phase 03/04 in the Phase 00 commit (optional: commit `decisions.md` only).  
- Do not treat this analysis file as LOCKED until humans promote it.  
- Do not open Phase 16 extracts/merges without reopening 00.4 / 00.5.  
- Do not improve LHDN fire-and-forget into a second durable stack while 00.2 says A+C.  
- Do not mint new LHDN developer keys as a long-term path once 00.1 is locked.

---

## Checklist mapping back to `phase-00-align-decisions.md`

Use this when filling the parent checklist after lock:

| Parent checkbox | Fill with |
|-----------------|-----------|
| 00.1 One ApiCredentials SSoT | Yes |
| 00.1 LHDN dual-read until cutover | Yes |
| 00.1 Cutover A vs B | **B** with dates **2026-11-30** / **2026-12-15** |
| 00.1 LHDN scopes on One | Yes |
| 00.1 Revoke One-only after cutover | Yes |
| 00.2 One durable platform | Yes |
| 00.2 LHDN A/B/C | **A end-state + C interim freeze** (write: “A (+ C until Phase 04)”) |
| 00.3 A/B/C | **C park** |
| 00.4 WA in 6 months | **No** → freeze Messaging thin |
| 00.5 Credits in Billing 6–12m | **Yes** |
| 00.6 All four confirms | Yes |

---

*End of Phase 00 analysis. No application code was modified. Promote via `plans/004-maintenance/decisions.md` before dual-path implementation.*
