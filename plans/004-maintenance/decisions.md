# 004 Maintenance — Locked decisions (Phase 00)

**Status:** LOCKED  
**Locked date:** 2026-08-09  
**Based on:** plans/004-maintenance/checklists/phase-00-analysis.md  
**Product posture:** Lazuar Pure CaaS MVP (ADR 019/023)

Phase 01 (secrets / dead residue) may start immediately after this file is committed.  
Frontend is not a gate.

**Production-safe posture:** Prefer one long-term SSoT, dated dual-read / freeze windows, park liminal features, no new modules, no merge/extract without product trigger.

---

## 00.1 API credentials

- **SSoT (long-term):** One `ApiCredentials` is the only mint/list/revoke store.
- **Legacy:** `lhdn.DeveloperApiKeys` is dual-read only until cutover; no new permanent product surface.
- **Cutover posture:** **B** — dual-read until **2026-11-30**; target One-only middleware + One revoke event by **2026-12-15** (Phase 03).
- **Scopes:** LHDN scopes are modeled on One credentials (yes).
- **Revoke after cutover:** One `ApiKeyRevokedIntegrationEvent` only.
- **Read order during dual-read:** One first, then Lhdn legacy (document in Phase 03.2).

| Milestone | Date | Meaning |
|-----------|------|---------|
| Dual-read **allowed until** | **2026-11-30** | Auth may still hit `lhdn.DeveloperApiKeys` |
| Target dual-read **removed by** | **2026-12-15** | Middleware One-only; dual revoke subscription gone |
| Table drop / archive | **≥ 30 days after** One-only in prod | Phase 03.6 |

If prod row count is zero earlier, cutover may move **forward** (never leave dual-read open “because it still works”).

### Explicit non-decisions

- Hash/format migration algorithm details → Phase 03.2 design doc, not Phase 00.
- Whether LHDN HTTP routes remain as One-backed façades → Phase 03.5 + TypeSpec honesty (Phase 05).

---

## 00.2 Outbound webhooks

- **Platform model:** One `WebhookDeliveryOutbox` + dispatcher job + signing is the only platform-grade webhook system.
- **LHDN end-state (Phase 04):** **A** — route LHDN lifecycle customer webhooks through One dispatcher.
- **Rejected for this track:** **B** — second full Lhdn outbox/signing stack.
- **Interim (until A ships):** **C freeze** — fire-and-forget remains; document debt; observability only; no second stack “improvements.”
- **Written choice:** A (end-state) + C (interim freeze discipline).
- **Module extract:** Webhooks stay in One for this maintenance track (no `Modules/Webhooks` unless Phase 16 trigger).

Until Phase 04 implements A, operate under C freeze rules:

- ADR/module README: “LHDN outbound is fire-and-forget by design until Phase 04 A.”
- Observability so silent failure is visible (Phase 04.2-C).
- Do **not** invent Lhdn-local outbox (option B).
- Do **not** half-upgrade fire-and-forget into a second durable stack.

If product later discovers LHDN payloads/signing cannot share One without breaking integrators, **re-open 00.2** and choose B with an ADR — do not silently fork.

---

## 00.3 RevenueRecognitionJob / deferred revenue

- **Choice:** **C — Park**.
- **Note (required):** “`RevenueRecognitionJob` is unregistered by design until a product epic owns deferred revenue schedule creation (finance / Xero track). Entity/table may remain; no shipping claim that recognition runs.”
- **Not in this maintenance track:** implement schedule writers; drop table without product OK.
- **Phase 17:** park path only (README/ADR honesty; no metrics lie).

---

## 00.4 Messaging / WhatsApp

- **WhatsApp / multi-channel in next 6 months:** **No**.
- **Choice:** Freeze Messaging as thin transport; Communications remains content/policy owner.
- **No merge now:** Messaging → Communications is **out of scope** until product commits a real multi-channel provider (then Phase 16).
- **Honesty:** Console WhatsApp is not a production channel; docs/defaults must not claim automated WhatsApp dunning as live.
- **BuildingBlocks ports:** `IMessagingService` / email ports stay technical; channel product work is not “just another adapter PR” without 00.4 reopen.

---

## 00.5 Credits vs Billing

- **Credits stay in Billing for 6–12 months:** **Yes** (through at least **2027-02-09** unless product reopens).
- **No Credits/Wallet module** in this maintenance track.
- **Phase 16 extract trigger:** credit monetization is product-critical **and** change-rate diverges from merchant ledger (see report 04 §9.2).
- **Allowed without reopen:** internal folders/namespaces inside Billing only.

---

## 00.6 Scope freeze

- **No new modules** unless a Phase 16 product trigger is explicitly reopened and agreed.
- **Frontend:** out of scope except forced client regen from TypeSpec (`task gen` / committed clients policy).
- **Community / Vault:** remain **deleted**; no module resurrection; docs honesty is Phase 02 (not rebuild).
- **House style:** Commerce `Endpoints/` + thin map façade for endpoint chunking (Phase 07+).
- **Module count target:** stay at current nine product modules for this track.

### Explicit non-goals (maintenance track)

Do **not** use this track to:

- Create modules for Dunning, Catalog, Webhooks/Developer, Credits, Identity-vs-Tenancy, Tax, Analytics, Marketplace, Community rebuild
- Microservice-split the modular monolith
- Implement Meta WhatsApp, Xero, multi-country tax as “cleanup”
- Revive Community/Vault modules or schemas as product

---

## Decision matrix (quick reference)

| ID | Topic | Locked choice | Phase that consumes it |
|----|--------|---------------|------------------------|
| 00.1 | API keys | One SSoT; **dual-read until 2026-11-30**; One-only target by 2026-12-15; LHDN scopes on One; One revoke only after cutover | 03 |
| 00.2 | Webhooks | One durable = platform; **LHDN → A converge**; **C freeze interim**; reject B | 04 |
| 00.3 | Revenue recognition | **Park (C)** with ADR/README note | 17 |
| 00.4 | Messaging / WA | **No WA in 6 months**; freeze thin Messaging; **no merge** | 12, 16, 17 |
| 00.5 | Credits | **Stay in Billing 6–12 months** | 16 (later only) |
| 00.6 | Scope | **No new modules**; FE out; Community/Vault dead; Commerce Endpoints house style | All |

---

## Conflicts resolved (from analysis 04 vs 10)

| Tension | Resolution for MVP |
|---------|-------------------|
| Report 04 soft-yes Messaging→Communications merge vs roadmap “do not merge yet” | **No merge now**; merge only on funded multi-channel product (Phase 16) |
| Webhooks extract candidate vs platform in One | **Stay in One**; extract not maintenance |
| Credits strongest extract vs no new modules | **Stay in Billing 6–12 months**; extract is Phase 16 trigger only |
| Deferred revenue delete vs park | **Park** (safer than drop without finance owner) |
| Dual keys cutover A (immediate migrate/delete) vs B (dated dual-read) | **B dated**, then achieve A’s end-state via Phase 03 |

---

*Phase 00 complete. Dual-path phases (03, 04) must follow these end-states — do not guess.*
