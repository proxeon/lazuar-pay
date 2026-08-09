# Phase 00 — Align product / engineering decisions

**Goal:** Lock end-states so dual-path and freeze work is not guessed.  
**Output:** Written answers in this file or a short `plans/004-maintenance/decisions.md`.  
**No code required** (optional: commit decisions doc only).

**Status:** LOCKED — see [`../decisions.md`](../decisions.md) (2026-08-09).

---

## 00.1 API credentials end-state

- [x] Confirm **One `ApiCredentials`** is the only long-term mint/list/revoke store
- [x] Confirm LHDN `DeveloperApiKeys` is **legacy dual-read only** until cutover (Phase 03)
- [x] Choose cutover posture:
  - [ ] A: Migrate remaining keys → One, then delete LHDN store + dual middleware path
  - [x] B: Keep dual-read for N months with calendar date (write date: **2026-11-30** dual-read allowed until; **2026-12-15** One-only target)
- [x] Confirm scopes model: LHDN scopes live on One credentials (**yes**)
- [x] Confirm revoke event end-state: **One `ApiKeyRevokedIntegrationEvent` only** after cutover

## 00.2 Outbound webhooks end-state

- [x] Confirm One durable dispatcher (`WebhookDeliveryOutbox` + job + signing) is the **platform** model
- [x] Choose LHDN customer webhooks:
  - [x] A: Route LHDN lifecycle through One dispatcher
  - [ ] B: Give Lhdn the same outbox/signing primitives (shared BB or copy One pattern once)
  - [x] C: Explicitly freeze fire-and-forget as permanent special-case (document debt) — **interim until A ships** (not permanent)
- [x] Write chosen option: **A (end-state) + C (interim freeze until Phase 04)**

## 00.3 Deferred revenue / RevenueRecognitionJob

- [x] Choose:
  - [ ] A: **Delete** job + unused schedule surface if not near-term product
  - [ ] B: **Implement** schedule creation (only if finance work is scheduled)
  - [x] C: **Park** with ADR note “unregistered by design until …”
- [x] Write choice: **C — Park** (`RevenueRecognitionJob` unregistered by design until finance/Xero product epic)

## 00.4 Messaging / WhatsApp / multi-channel

- [x] Is WhatsApp (or multi-channel) planned in next 6 months? (**no**)
- [ ] If yes: favor **Messaging → Communications merge** later (Phase 16)
- [x] If no: freeze Messaging as thin transport; no expansion without plan
- [x] Write choice: **Freeze Messaging as thin transport; no merge until funded multi-channel product (Phase 16)**

## 00.5 Credits vs Billing ledger

- [x] Credits remain **inside Billing** for next 6–12 months? (**yes**, through at least **2027-02-09**)
- [ ] If no: note trigger for Credits module extract (Phase 16)
- [x] Write choice: **Yes — stay in Billing; no Credits module; Phase 16 extract only on monetization + PR-conflict trigger**

## 00.6 Scope freeze for this maintenance track

- [x] Confirm **no new modules** unless Phase 16 trigger
- [x] Confirm **frontend out of scope** except forced client regen from TypeSpec
- [x] Confirm Community/Vault modules stay **deleted** (no resurrection)
- [x] Confirm house style = **Commerce Endpoints/** pattern

## 00.7 Exit criteria

- [x] All 00.1–00.6 answered in writing and committed (or linked issue)
- [x] Team agrees Phase 01 can start without waiting on frontend
