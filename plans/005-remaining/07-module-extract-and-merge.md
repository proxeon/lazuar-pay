# 07 — FW-5: Module extract and merge (Credits / Webhooks / Messaging→Communications)

**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Workstream:** FW-5 (Phase 16 optional extract/merge)  
**Status:** **STAY DEFERRED** — no product trigger; gate not met  
**Constraint:** Analysis only — no application code was modified  

---

## 0. Purpose of this document

This is the **uncondensed HOW (if ever)** for three product-gated modularization moves that Phase 16 closed without executing:

| Workstream | Action | From → To |
|------------|--------|-----------|
| **16.A** | **Extract** | Billing wallet/credits → new `Modules/Credits` (or `Wallet`) |
| **16.B** | **Extract** | One outbound webhooks → new `Modules/Webhooks` (or `Developer`) |
| **16.C** | **Merge** | Messaging thin transport → Communications |

It answers:

1. **When** each move is allowed (triggers + gate).  
2. **Why** all three are premature **today**.  
3. **Exactly how** to execute each **if** triggered (steps, ownership maps, cutover, acceptance).  
4. **What to do instead** (folders/namespaces only — allowed without reopen).  
5. **Recommendation:** keep deferred until product reopens the relevant 00.x lock.

This document **does not** authorize extract/merge. It is a playbook for a future epic after product sign-off.

---

## 1. Authority and prior decisions

### 1.1 Locked decisions (`plans/004-maintenance/decisions.md`)

| ID | Lock | Implication for FW-5 |
|----|------|----------------------|
| **00.2** Outbound webhooks | Platform model = One `WebhookDeliveryOutbox` + dispatcher + signing. **Module extract stays in One** unless Phase 16 product trigger. | **16.B not allowed** as maintenance work. |
| **00.4** Messaging / WhatsApp | **No** WhatsApp / multi-channel in next 6 months. Freeze thin Messaging; Communications owns content/policy. **No merge** until product funds a real multi-channel provider. | **16.C not allowed** until 00.4 reopened. |
| **00.5** Credits vs Billing | Credits **stay in Billing** 6–12 months (through ≥ **2027-02-09** unless product reopens earlier). **No** Credits/Wallet module in maintenance track. | **16.A not allowed** before calendar floor **or** explicit product reopen. |
| **00.6** Scope freeze | **No new modules** unless Phase 16 product trigger reopened. Module count target = **nine** product modules. | Creating `Modules/Credits` or `Modules/Webhooks` violates freeze without reopen. |

Conflicts matrix (already resolved for MVP):

| Tension (report 04 vs roadmap) | Resolution |
|--------------------------------|------------|
| Credits = strongest future extract vs no new modules | **Stay in Billing**; extract is Phase 16 trigger only |
| Webhooks extract candidate vs platform in One | **Stay in One**; extract not maintenance |
| Messaging→Communications soft-yes vs “do not merge yet” | **No merge now**; merge only on funded multi-channel |

### 1.2 Phase 16 outcome (`phase-16-done.md`, `phase-16-analysis.md`)

| Item | Result |
|------|--------|
| Gate 16.0 | **NOT MET** |
| 16.A Credits extract | **Skipped** |
| 16.B Webhooks extract | **Skipped** |
| 16.C Messaging merge | **Skipped** |
| New projects / schema renames / host DI | **None** |
| TypeSpec route moves | **None** |
| Module count after Phase 16 | Still **nine** product modules |

Phase 16 analysis intentionally **did not** produce move inventories, dual-write plans, or solution graphs. This file **does** produce those as a **conditional** playbook (only if reopen criteria fire later).

### 1.3 Report 04 (`04-module-boundaries-modularization.md`)

Authoritative technical candidate analysis:

| Section | Content |
|---------|---------|
| §5.2B Billing dual models | Ledger vs wallet — strongest extract candidate **later** |
| §5.2C One kitchen sink | Webhooks co-located with identity — medium extract **later** |
| §9.2 Credits/Wallet | Extraction plan outline |
| §9.3 Webhooks/Developer | Extraction plan outline |
| §10.1 Messaging → Communications | Primary **merge** candidate |
| §11 Decision framework | Four conditions that must all hold before extract |
| §12 P2/P3 | Folders first; extract/merge only when product forces it |
| §13 | Detailed plans (expanded below with current inventory) |

### 1.4 Future-work index (`FUTURE-WORK.md` FW-5)

- Urgency: **Only with product trigger**  
- Pre-work: design note, product sign-off, update `decisions.md`, prefer internal folders first  
- Explicit rejects: Catalog / Identity / Dunning-as-module / microservices / Community-Vault resurrection  
- Checklist: `checklists/phase-16-optional-extract-merge.md`, gate template `checklists-future/phase-f15-module-extract-gate.md`

### 1.5 New-module rules (ADR 001)

Any extract that creates a new top-level module **must** obey:

1. Four projects: Contracts / Domain / Application / Infrastructure (CRM-style 3-layer is not the default).  
2. Private PostgreSQL schema + migrations history table in that schema.  
3. No cross-schema SQL joins.  
4. Cross-module writes via integration events + outbox/inbox.  
5. Host references **Infrastructure only**.  
6. Register namespace in `ModuleBoundaryTests.ModuleNamespaces`.  
7. Host: `Add*Module`, migrate DbContext, `Use*Subscriptions`, map endpoints.

Cost of one new module ≈ **4 csproj + schema + dual outbox/inbox workers + arch-test anchors + Taskfile migrate line + Program composition + TypeSpec package surface**.

---

## 2. Current module inventory (as of 2026-08-09)

Nine product modules (unchanged; freeze holds):

| Module | Schema | Role relevant to FW-5 |
|--------|--------|------------------------|
| **One** | `one` | Identity + API credentials + **outbound webhook registry/delivery** |
| **Billing** | `billing` | Double-entry ledger + **prepaid credits wallet** + docs + B2C |
| **Messaging** | `messaging` | Thin dispatch + tenant replica + delivery logs |
| **Communications** | `communications` | Templates, broadcasts, suppressions, BYOK email, lifecycle orchestration |
| Commerce, Payments, Lhdn, CRM, Ops | (their schemas) | Consumers / publishers of credit deduct, webhooks, dispatch |

Architecture anchor list (`ModuleBoundaryTests.ModuleNamespaces`):

```
Modules.One, Modules.Messaging, Modules.CRM, Modules.Payments,
Modules.Ops, Modules.Billing, Modules.Lhdn, Modules.Commerce, Modules.Communications
```

---

## 3. Shared gate (must all be true before any of 16.A / 16.B / 16.C)

Do **not** open extract/merge PRs until **every** box is true:

| # | Gate | Evidence |
|---|------|----------|
| G1 | **Product trigger** for the specific candidate (see §4) is true in writing | Product ticket / epic, not eng preference |
| G2 | Relevant **00.x decision reopened** and `decisions.md` updated | Diff to 00.2 / 00.4 / 00.5 / 00.6 as needed |
| G3 | **Written design note** for this extract/merge: why, failure domain, event migration, dual-write if any, rollback | New doc under `plans/` or ADR |
| G4 | **Product owner sign-off** on blast radius and freeze window | Explicit ACK |
| G5 | Report 04 §11.3 framework all hold (see below) | Design note cites each |
| G6 | Horizon dual-path work not blocked (or extract does not depend on unfinished dual-path) | e.g. do not extract Webhooks mid–LHDN webhook converge chaos without plan |
| G7 | Prefer **folders-only** alternative considered and rejected with reasons | Design note §alternatives |

### Report 04 §11.3 — extract only if **all** hold

1. Two concerns change for **different reasons** on a regular basis.  
2. Independent **test/deploy** desire **or** different compliance boundary.  
3. Existing contracts already form a **clean cut** (few dual-write transactions).  
4. Team can afford **2–4 weeks** migration + dual-run without blocking revenue features.

Otherwise: **namespaces + folders inside the module** + fix leaks — not new `Modules/*`.

### Reopen criteria (from `phase-16-done.md`)

1. Product owner reopens the relevant 00.x decision in writing.  
2. Concrete trigger matches the candidate (§4).  
3. Written design note (why, failure domain, event migration).  
4. Gate 16.0 / F15.0 / G1–G7 all true.

---

## 4. Triggers (per candidate)

### 4.1 Credits / Wallet extract (16.A) — triggers

**Locked floor:** stay in Billing through ≥ **2027-02-09** (00.5) unless product reopens earlier.

**Product trigger (must both be true):**

1. **Credit monetization is product-critical** — packages, promos, multi-currency credits, dedicated FinOps reporting, or utility billing becomes a primary revenue surface (not just “nice prepaid balance for LHDN/email”).  
2. **Change-rate diverges from merchant ledger** — wallet PRs constantly conflict with ledger/tax/document/consolidation PRs; different owners or release cadences.

**Supporting signals (not sufficient alone):**

- Multiple new credit products (holds complexity, reservations, multi-wallet).  
- External audit wants wallet schema isolated from ledger.  
- Onboarding cost of dual models in Billing Domain is measurably high for new engineers **and** product invests in wallet as a product.

**Not a trigger:**

- Folder cleanliness / “Billing is fat.”  
- Desire to rename namespaces.  
- One painful PR that mixed ledger + credit by accident (fix that PR; don’t extract).

### 4.2 Webhooks / Developer extract (16.B) — triggers

**Locked default:** platform webhooks stay in One (00.2).

**Product trigger:**

- **Multi-endpoint delivery product dominates One’s change log** — delivery observability UI, third-party OAuth apps, rate limits per integrator, event catalog product, multi-endpoint fan-out SLAs become first-class developer-platform work that blocks identity/workspace shipping.

**Supporting signals:**

- Dedicated developer-platform team/roadmap.  
- Webhook delivery volume/SLA needs independent scaling story (still modular monolith, but schema/job isolation).  
- Endpoint registration + signing + delivery become larger surface than CIAM/tenancy.

**Not a trigger:**

- Desire to “finish” architecture taxonomy after LHDN→One webhook converge (FW-2).  
- One growing file (`OutboundWebhookDispatcherJob`).  
- API credentials cleanup (finish dual-key cutover **first**; credentials may stay in One even after webhook extract).

**Sequencing note:** Prefer completing **FW-2** (LHDN lifecycle → One dispatcher) and **FW-1** (API key One-only) **before** any Webhooks module extract so extract does not freeze mid dual-stack.

### 4.3 Messaging → Communications merge (16.C) — triggers

**Locked default:** no merge; freeze thin Messaging (00.4). Console WhatsApp is not production.

**Product trigger:**

- Real multi-channel provider (e.g. **Meta Cloud WhatsApp**, SMS provider, push) is **funded** and implementation **starts**.  
- 00.4 reopened to allow channel product work.

**Supporting signals:**

- Channel adapters need shared retry/rate-limit policy with Communications BYOK.  
- Product wants one “notification domain” owner for ops and billing of channels.  
- Messaging module tax (schema + 2 workers + arch tests) exceeds isolation benefit once multi-channel is real.

**Not a trigger:**

- Messaging is thin / over-split (true, but intentional).  
- Docs confusion about Community/Vault (fixed in Phase 02 honesty).  
- BuildingBlocks email port ownership cleanup (FW-3 / F12) — can move email ownership without full module merge.

### 4.4 Explicit non-triggers (still rejected — 16.D)

Even if someone opens a “modularization” PR:

| Candidate | Stance |
|-----------|--------|
| Catalog module from Commerce | **Rejected** |
| Identity vs Tenancy split in One | **Rejected** |
| Dunning as separate module | **Rejected** for tidiness; only if campaign engine explodes (report 04 §9.1 — still not FW-5 default) |
| Tax / Analytics / Marketplace modules | **Rejected** (00.6 non-goals) |
| Microservices split of modular monolith | **Rejected** |
| Community / Vault resurrection | **Rejected** |

---

## 5. Why premature **today**

### 5.1 Product and calendar

| Fact | Effect |
|------|--------|
| Pure CaaS MVP (ADR 019/023) prioritizes checkout + dunning + payments, not taxonomy | Extract is not revenue path |
| 00.5 calendar floor ≥ **2027-02-09** for Credits in Billing | 16.A blocked by date **and** product |
| 00.4 no multi-channel / WhatsApp for ~6 months | 16.C blocked |
| 00.2 platform webhooks = One; extract not maintenance | 16.B blocked |
| 00.6 no new modules | Net-new `Modules/*` forbidden without reopen |
| Phase 16 gate already evaluated **2026-08-09** and closed docs-only | Precedent: do not reopen without product |

### 5.2 Cost vs benefit today

Each new module costs ~4 projects + schema + outbox/inbox jobs + arch tests + Taskfile + composition. Messaging **already** pays that tax for a thin pipe — merge would *reduce* cost, but only when multi-channel work makes co-location worth the migration pain.

Credits and Webhooks **already work** with Contracts seams:

- Credits: `ICreditCostService`, `DeductTenantCreditCommand`, hold commands, admin credit endpoints live under Billing; consumers (Lhdn, Messaging, Communications) use Contracts.  
- Webhooks: Commerce publishes `OutboundWebhookRequestedIntegrationEvent`; One owns delivery. Clean event cut already exists **without** a new module.

Extracting now multiplies migration risk (data, dual-write, consumer ProjectReferences) **without** a product owner for the new boundary.

### 5.3 Higher-ROI work still ahead of extract

Report 04 §12 and FUTURE-WORK priority:

| Priority | Work | Why before extract |
|----------|------|--------------------|
| P0 / FW-1 | API key dual-read cutover | Dual systems hurt more than “fat One” |
| P0 / FW-2 | LHDN → One webhook dispatcher | Completes platform model **inside** One |
| P0 / FW-4 | Cross-schema SQL leaks | Extract multiplies leaks if SQL boundaries are dirty |
| P1 / FW-3 | BuildingBlocks product ports (email → Messaging ownership) | Ownership honesty without new modules |
| P2 | Folders: `Billing/Wallet`, `One/Webhooks`, `Commerce/Dunning` | Cognitive modularization without project tax |

**Bottom line (report 04 §11 / §16):** Further modularization is **mostly premature** relative to MVP. Pain is leaky SQL and dual systems, not missing top-level folders.

### 5.4 Seam quality without extract

| Candidate | Seam today | Why “good enough” |
|-----------|------------|-------------------|
| Credits | Domain aggregates + Contracts commands already wallet-shaped inside Billing | Clean cut exists **for later**; no need to pay schema migrate now |
| Webhooks | Event `OutboundWebhookRequested` + One delivery | Platform model intentionally centralized |
| Messaging vs Communications | ADR-style split: render at source, dispatch at edge | Isolation valuable until multi-channel is real |

---

## 6. Alternatives — folders / namespaces only (allowed **without** reopen)

These are **preferred** until G1–G7 fire. They do **not** change module count, schemas, or arch-test module list.

### 6.1 Billing — wallet partition

**Allowed now (00.5):**

```
Modules/Billing/
  Domain/
    Wallet/          # or Aggregates/Wallet/
      TenantCreditBalance.cs
      CreditHold.cs
      ...
    Ledger/
      LedgerEntry.cs
      ...
  Infrastructure/
    Commands/Wallet/
      DeductTenantCreditCommandHandler.cs
      CreditHoldCommandHandlers.cs
      ClawbackCreditsCommandHandler.cs
    EventHandlers/Wallet/
      PlatformTopUpEventHandler.cs
      StarterCreditSeederHandler.cs
      ApiCreditPurchasedHandler.cs
      ChargebackClawbackHandler.cs
    Endpoints/
      AdminCreditsEndpoints.cs   # already separate
    Services/
      CreditCostService.cs
  Contracts/
    # keep ICreditCostService + credit commands here until extract
```

**Do not:** new schema `credits`, new csproj, or rename public Contracts namespaces without a deprecation plan.

**Value:** onboarding clarity (“wallet vs ledger”) without dual-write.

### 6.2 One — webhooks partition

**Allowed now (00.2):**

```
Modules/One/
  Domain/
    Webhooks/
      TenantWebhookEndpoint.cs
      WebhookDeliveryOutbox.cs
      WebhookUrlValidator.cs
  Infrastructure/
    Workers/
      OutboundWebhookDispatcherJob.cs
      OutboundWebhookSignature.cs
    EventHandlers/
      OutboundWebhookEventHandlers.cs
    Endpoints/
      WebhookEndpoints.cs   # already split under Endpoints/
  Application/
    Commands/   # SaveWebhook* stay near other workspace commands or under Commands/Webhooks/
```

**Do not:** new `Modules/Webhooks` or schema `webhooks` without 16.B gate.

**Sequencing:** folder move is fine **during** FW-2 converge; extract is not.

### 6.3 Messaging / Communications — do **not** fake-merge with folders across modules

Cross-module folder renames do not merge projects. Until 16.C:

- Keep Messaging as thin transport (README freeze text already documents 00.4).  
- Keep Communications as content/policy owner.  
- Optional **inside** Communications: `Infrastructure/Dispatch/` for publishers of `DispatchMessageIntegrationEvent` only (still publishes event; does not swallow Messaging).  
- Optional **inside** Messaging: `Infrastructure/Channels/` when multi-channel adapters land (still Messaging module until merge).

### 6.4 Commerce — dunning folders (related but not FW-5)

`Commerce/Dunning/*` is the same “folder first” pattern (report 04 §9.1 / §12 P2). Not Credits/Webhooks/Messaging, but same rule: **no** `Modules/Dunning` without product trigger.

### 6.5 What folders do **not** replace

| Need | Folders enough? |
|------|-----------------|
| Cognitive ownership | Yes |
| Independent schema / migration lock isolation | **No** — needs extract |
| Separate deploy unit | **No** — and microservices still rejected |
| Stop PR conflict between two teams on same Domain project | Partial — extract only if conflict is chronic |

---

## 7. IF triggered: Credits / Wallet extract (16.A) — full steps

### 7.1 Target end-state

```
Modules/Credits/   # or Wallet — pick one name in design note; recommended: Credits
  Contracts/   # ICreditCostService, Deduct/Hold/Clawback commands, optional events
  Domain/      # TenantCreditBalance, CreditLedger, CreditHold, CreditDeductionIdempotencyLog
  Application/ # thin; most handlers may stay Infrastructure (match Billing house style)
  Infrastructure/
    CreditsDbContext   # schema "credits"
    handlers, workers, AdminCreditsEndpoints, CreditCostService
    Migrations/
```

**Billing retains:** double-entry ledger, tax, B2C consolidation, document generation, deferred revenue, billing profiles/sequences, non-credit admin/public endpoints.

**Optional Billing subscription after extract:** credit pack sold → ledger accounting entry via `CreditsToppedUp` (or similar) event — **not** dual ownership of balance.

### 7.2 Ownership map (move out of Billing)

| Kind | Items (current paths under Billing) |
|------|-------------------------------------|
| Domain aggregates/entities | `Domain/Aggregates/TenantCreditBalance.cs`, `CreditHold.cs`; `Domain/Entities/CreditLedger.cs`, `CreditDeductionIdempotencyLog.cs` |
| Contracts | `Contracts/ICreditCostService.cs` (+ `CreditAction`, `CreditPackage`); `Contracts/Commands/DeductTenantCreditCommand.cs`; `Contracts/Commands/CreditHoldCommands.cs` |
| Handlers | `Infrastructure/Commands/DeductTenantCreditCommandHandler.cs`, `CreditHoldCommandHandlers.cs`, `ClawbackCreditsCommandHandler.cs` |
| Event handlers | `PlatformTopUpEventHandler`, `StarterCreditSeederHandler`, `ApiCreditPurchasedHandler`, `ChargebackClawbackHandler` (credit paths only) |
| Services / endpoints | `CreditCostService.cs`; `Endpoints/AdminCreditsEndpoints.cs` |
| Query surface | Credit balance/history methods currently on `IBillingQueryService` / `BillingQueryService` — **split** into `ICreditQueryService` on Credits.Contracts; leave pure ledger queries on Billing |

**Stay in Billing (do not move):**

- `LedgerEntry` / `LedgerLine` / account types  
- Document generation, sequences, B2C job  
- `InvoiceIssued` / `DocumentPublished` / commission / manual payment events  
- `RevenueRecognitionJob` (still parked per 00.3)  
- Gateway payment handlers that post **money** ledger (vs credit top-up branch)

### 7.3 Current external consumers (must retarget ProjectReferences)

| Consumer | Usage |
|----------|--------|
| **Messaging** Infrastructure | `ICreditCostService` + `DeductTenantCreditCommand` in `DispatchMessageIntegrationEventHandler` |
| **Communications** Infrastructure | `ICreditCostService` on broadcasts; hold id on fan-out; Billing.Contracts.Events for document published (ledger event — **stays** Billing) |
| **Lhdn** Application | `ICreditCostService` + `DeductTenantCreditCommand` on `SubmitTaxDocumentCommand` |

After extract: these reference **Credits.Contracts** for wallet types; keep Billing.Contracts only for ledger/document events they still need.

### 7.4 Event design (design note must lock names)

Recommended Contracts events (new module owns them):

| Event | Publisher | Consumers |
|-------|-----------|-----------|
| `CreditsToppedUpIntegrationEvent` | Credits (after top-up handlers) | Optional Billing ledger; analytics |
| `CreditsDeductedIntegrationEvent` | Credits (optional) | Audit / Ops |
| `CreditsInsufficientIntegrationEvent` | Credits (optional) | Product UX |

Inbound to Credits (subscriptions):

| Event | Source | Handler purpose |
|-------|--------|-----------------|
| Platform payment completed with credit pack metadata | Payments / existing top-up path | Top-up balance |
| `ApiCreditPurchased` | Payments | Top-up |
| Chargeback / refund clawback signals | Payments / Billing | Clawback |
| Starter grant | One `TenantProvisioned` or entitlement | Seed balance |

**Do not** move merchant money ledger events into Credits.

### 7.5 Migration sequence (recommended)

1. **Design note + reopen 00.5 / 00.6** — freeze window, schema name (`credits`), dual-write yes/no.  
2. **Introduce** empty `Modules/Credits` projects per ADR 001; register in solution + host **without** cutting over traffic.  
3. **Contracts first** — move or copy credit Contracts types to Credits.Contracts; temporary type-forward or dual package for one release if needed.  
4. **DbContext + schema**  
   - Preferred long-term: `credits.*` tables.  
   - Migration options:  
     - **A.** EF migrate create new tables + one-shot data copy job from `billing.TenantCreditBalances` etc.  
     - **B.** Schema rename with careful downtime (`ALTER TABLE ... SET SCHEMA`) — faster but ops-heavy.  
   - Isolation: `MigrationsHistoryTable("__EFMigrationsHistory", "credits")`.  
5. **Implement handlers** against CreditsDbContext; feature-flag dual-write if zero-downtime required:  
   - Phase dual-write: write Billing + Credits, read Credits.  
   - Phase dual-read fallback: read Credits then Billing.  
   - Prefer **short** dual window; wallet balances are correctness-critical.  
6. **Switch writers** — Deduct/Hold/Clawback/Top-up only go to Credits.  
7. **Retarget consumers** — Lhdn, Messaging, Communications ProjectReferences + usings.  
8. **Move admin endpoints** TypeSpec under credits/billing honesty (admin credits routes).  
9. **Delete** credit entities/handlers from Billing Domain/Infrastructure; remove credit DbSets from `BillingDbContext`.  
10. **Optional** Billing subscribes to top-up for accounting ledger lines.  
11. **Arch tests** — add `Modules.Credits` to `ModuleNamespaces`; ensure Domain isolation and Contracts-only outer refs.  
12. **Taskfile / CI** — `api:db:migrate` add `CreditsDbContext`; Program composition `AddCreditsModule`, migrate, subscriptions, endpoints.  
13. **Tests** — move/adapt credit module tests; preserve deduct idempotency tests; integration top-up via gateway metadata.  
14. **Docs** — Billing README, Credits README, cross-module communication doc, gap docs, TypeSpec README.

### 7.6 Acceptance criteria

- [ ] No ProjectReference Credits → Commerce Domain (or any non-Contracts foreign module layer).  
- [ ] Deduct **idempotency** preserved (`CreditDeductionIdempotencyLog` behavior).  
- [ ] Platform top-up still via payment metadata type (existing path).  
- [ ] Lhdn submit and Messaging dispatch still deduct correctly.  
- [ ] Broadcast hold → consume/release still correct with `CreditHoldId` on `DispatchMessageIntegrationEvent`.  
- [ ] Billing ledger unchanged for non-credit money paths.  
- [ ] Host still single deployable modular monolith.  
- [ ] No new cross-schema SQL.

### 7.7 Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Balance drift during dual-write | Single writer as soon as possible; reconciliation query job |
| MediatR command type move breaks handlers | Keep command names; move assembly carefully; full test pass |
| `IBillingQueryService` credit methods forgotten | Explicit split checklist in design note |
| Top-up handler still posts only to Billing wallet | Inventory all TopUp/clawback entry points before cutover |

### 7.8 Effort band

**Medium** (report 04): ~2–4 weeks with dual-run if production has real balances; less if greenfield empty wallets.

---

## 8. IF triggered: Webhooks / Developer extract (16.B) — full steps

### 8.1 Target end-state

```
Modules/Webhooks/   # or Developer — design note picks name
  Contracts/   # registration ports, delivery query DTOs; optional delivery requested command
  Domain/      # TenantWebhookEndpoint, WebhookDeliveryOutbox, validators
  Application/
  Infrastructure/
    WebhooksDbContext  # schema "webhooks" or "developer"
    OutboundWebhookDispatcherJob, OutboundWebhookSignature
    WebhookEndpoints (HTTP)
    OutboundWebhookEventHandlers
    outbox/inbox workers if module emits events
```

**One retains:** GlobalUser, Organization, memberships, invitations, entitlements, API credentials (default), provision/auth endpoints, genesis bootstrapper.

**Do not move in first extract:** API credentials (second phase only if Developer platform product owns keys). Host API-key middleware continues to use One unless a later epic says otherwise.

### 8.2 Ownership map (move out of One)

| Kind | Items (current under One) |
|------|---------------------------|
| Domain | `TenantWebhookEndpoint.cs`, `WebhookDeliveryOutbox.cs`, `WebhookUrlValidator.cs` |
| Application commands | `SaveWebhookCommand` (+ create/update handlers if split); provision path pieces that create webhooks (`ProvisionAuraWorkspaceCommandHandler.Webhook.cs`) — **re-home carefully**: provision may call Webhooks via Contracts command or event |
| Infrastructure | `Workers/OutboundWebhookDispatcherJob.cs`, `OutboundWebhookSignature.cs`; `EventHandlers/OutboundWebhookEventHandlers.cs`; `Endpoints/WebhookEndpoints.cs` |
| Repository surface | webhook methods on `OneRepository` / `IOneRepository` → Webhooks repository |
| DI | Hosted dispatcher registration; `Subscribe<OutboundWebhookRequestedIntegrationEvent, ...>` |

**Publisher of request event stays on Commerce (and others):**  
`OutboundWebhookRequestedIntegrationEvent` should remain owned by the **publisher** (Commerce.Contracts today). Webhooks module **subscribes**. Do not force Commerce to reference Webhooks.Contracts for an event it publishes — keep publisher-owned event (report 04 §13.2).

### 8.3 Migration sequence

1. **Reopen 00.2 / 00.6** only if product confirms developer-platform extract; design note covers schema + provision coupling.  
2. **Complete or stabilize FW-2** (LHDN → One dispatcher) so only **one** delivery stack moves.  
3. **Contracts** — `IWebhookRegistrationService`, delivery admin queries as needed.  
4. **New module projects** + `WebhooksDbContext` schema.  
5. **Table migration** from `one.TenantWebhookEndpoints` / `one.WebhookDeliveryOutboxes` (names per snapshot) → `webhooks.*`.  
6. **Move dispatcher job + signing + handler**; One unregisters dispatcher.  
7. **HTTP routes** — TypeSpec: `packages/api-spec/modules/one/models/webhook.tsp` (+ routes) move to `modules/webhooks/` (or developer package); regenerate clients.  
8. **Provision / workspace APIs** — either:  
   - One provision calls Webhooks Contracts to register endpoint, or  
   - Provision emits event handled by Webhooks.  
9. **Repository cleanup** — remove webhook DbSets from `OneDbContext`.  
10. **Arch tests + Taskfile + Program** registration.  
11. **Tests** — move `OutboundWebhookTests`, `OutboundWebhookClaimTests`, provision webhook cases; Commerce lifecycle webhook publish tests unchanged (still publish event).  
12. **Docs** — One README, webhook ADRs (e.g. 009 stateless webhook metadata), runbooks.

### 8.4 Acceptance criteria

- [ ] One no longer hosts delivery outbox or dispatcher.  
- [ ] Commerce (and others) still publish `OutboundWebhookRequestedIntegrationEvent` successfully.  
- [ ] Signature verify compatibility unchanged for integrators (header format stable).  
- [ ] Provision still can create/reuse webhook endpoint idempotently.  
- [ ] API credentials still work (left in One unless phase 2).  
- [ ] No second Lhdn durable stack invented (00.2 still rejects option B).  
- [ ] Deploy path unchanged (single host).

### 8.5 Risks

| Risk | Mitigation |
|------|------------|
| Provision tightly coupled to webhook entity | Contracts API before cutover; integration tests on provision |
| In-flight outbox rows during migrate | Drain dispatcher; short maintenance window or dual-read outbox |
| Extract mid dual API-key chaos | Finish FW-1 first |
| TypeSpec / admin UI path breaks | Client regen + ops/admin smoke |

### 8.6 Effort band

**Medium.** Higher if provision + admin UI + TypeSpec surface are large. Prefer after webhook product metrics show dominance of One PR surface.

---

## 9. IF triggered: Messaging → Communications merge (16.C) — full steps

### 9.1 Target end-state

- **No** `Modules/Messaging` projects.  
- Communications owns: templates/broadcasts/suppressions **and** dispatch handlers, delivery logs, channel adapters.  
- BuildingBlocks keeps technical ports: `IEmailService`, `IMessagingService` (adapters may live host/BB or Communications Infrastructure).  
- Single schema preference: `communications` (migrate `messaging.*` tables in or drop replica).  
- One less migrate context, one less outbox/inbox pair, one less arch-test module entry.

### 9.2 Why merge only with multi-channel

Report 04 §10.1: Messaging is over-split relative to Communications (Communications decides content, emits `DispatchMessage`; Messaging only sends). Isolation is valuable for a high-volume multi-provider future — **until** that future is funded, merge cost > feature work. When WhatsApp (etc.) is implemented, one module for channel adapters is clearer.

### 9.3 Current Messaging surface (move inventory)

| Layer | Items |
|-------|--------|
| Contracts | `DispatchMessageIntegrationEvent.cs` |
| Domain | `MessageDeliveryLog.cs`, `TenantReplica.cs` |
| Application | `SendTenantNotificationCommandHandler.cs`; tenant replica event handlers; `ITenantReplicaRepository` |
| Infrastructure | `MessagingDbContext`; `DispatchMessageIntegrationEventHandler`; tenant provision/update/workspace handlers; `TenantReplicaRepository`; `Endpoints.cs` (minimal notify); inbox/outbox jobs |
| Schema | `messaging.TenantReplicas`, `messaging.MessageDeliveryLogs`, outbox/inbox |

### 9.4 Current publishers of `DispatchMessageIntegrationEvent` (must keep compiling)

| Publisher area | Role |
|----------------|------|
| Communications | Lifecycle, document published, order completed, fulfillment, broadcast fan-out, template test send |
| One | `NotificationDispatchDomainEventHandlers` (auth/email verification style) |
| Others (if any) | Grep again at execution time — inventory is part of design note |

After merge: event type may move to **Communications.Contracts** (or stay as shared Contracts type under Communications). All publishers update ProjectReferences (drop Messaging.Contracts; use Communications.Contracts for dispatch event).

### 9.5 Messaging also consumes Billing credits today

`DispatchMessageIntegrationEventHandler` uses `ICreditCostService` + `DeductTenantCreditCommand`. After merge, that handler lives in Communications Infrastructure (already references Billing.Contracts for other reasons). **No need** to extract Credits first, but if both 16.A and 16.C fire, do Credits first or coordinate command package ownership.

### 9.6 Merge sequence

1. **Reopen 00.4** — funded multi-channel provider named; product owner ACK merge.  
2. **Design note** — event type ownership, schema strategy, tenant replica keep/drop, channel adapter folder layout.  
3. **Inventory** all `DispatchMessageIntegrationEvent` publishers and Messaging ProjectReferences (One Application, Communications App+Infra, tests, TypeSpec `modules/messaging/`).  
4. **Move domain + handlers** into Communications (suggested folders):  
   ```
   Communications/Domain/Dispatch/
   Communications/Infrastructure/Dispatch/
   Communications/Infrastructure/Channels/   # Email, WhatsApp adapters product code
   ```  
5. **TenantReplica decision:**  
   - **Keep** under Communications if dispatch still needs local slug/status without One sync call, **or**  
   - **Delete** and resolve via `IOneQueryService` if volume allows (simpler; more runtime coupling to One).  
6. **Schema migration:**  
   - Move `MessageDeliveryLogs` (+ optional replica) into `communications` schema, **or**  
   - Keep reading old `messaging` schema briefly (discouraged — temporary dual schema).  
7. **DI:** Communications registers former Messaging subscriptions + workers; remove `AddMessagingModule`.  
8. **Host:** remove Messaging from MediatR assembly list, migrate list, endpoint map, solution.  
9. **TypeSpec:** `packages/api-spec/modules/messaging/` — empty ownership note today; fold into communications or delete import from `main.tsp` per honesty rules.  
10. **Arch tests:** remove `Modules.Messaging` from `ModuleNamespaces`.  
11. **Taskfile:** remove MessagingDbContext migrate line.  
12. **Delete** Messaging projects after green CI.  
13. **Docs:** Messaging README → Communications; 001-gaps messaging sections; BB ownership 009 if email moves with merge.  
14. **Keep** BuildingBlocks ports technical; do not put Resend product rules only in BB (FW-3 alignment).

### 9.7 Acceptance criteria

- [ ] Zero `Modules.Messaging.*` projects in solution.  
- [ ] All former dispatch paths send (email path production; WhatsApp only if product live).  
- [ ] Delivery logs retained or explicitly archived.  
- [ ] Credit deduct on send still correct (incl. broadcast holds).  
- [ ] Architecture tests green with 8 modules (or 8 + Credits if 16.A also done — count carefully).  
- [ ] No regression on One auth notification emails.  
- [ ] Single deployable host.

### 9.8 Risks

| Risk | Mitigation |
|------|------------|
| Communications becomes fat god module | Channel adapters in clear folder; still better than two modules for one product |
| Lost isolation for high-volume send scaling | Accept until real scale; re-extract **channel worker** only if needed later (unlikely soon) |
| Event type rename breaks outbox payloads | Keep event CLR name / type discriminator stable during migrate |
| TenantReplica stale logic bugs | Integration tests on provision → dispatch |

### 9.9 Effort band

**Medium–high** (touch many publishers + schema + host). Best done as the **first PR of multi-channel implementation**, not a pure refactor sprint.

---

## 10. Cross-cutting execution checklist (any of 16.A / B / C)

When a gate opens, every extract/merge PR train should cover:

### 10.1 Solution / host

- [ ] `.slnx` / project entries  
- [ ] `Lazuar.Api.csproj` Infrastructure ProjectReference only  
- [ ] `Composition/ModuleRegistrationExtensions.cs` (or Program composition)  
- [ ] DbContext `MigrateAsync` at startup  
- [ ] `Use*Subscriptions`  
- [ ] `Map*Endpoints`  
- [ ] MediatR assembly registration  

### 10.2 Data

- [ ] Schema + `__EFMigrationsHistory` isolation  
- [ ] Data copy / rename plan + rollback  
- [ ] Outbox/inbox drain if moving tables  
- [ ] No cross-schema FKs  

### 10.3 Contracts / events

- [ ] Publisher-owned integration events  
- [ ] Consumer ProjectReferences Contracts-only  
- [ ] Architecture tests updated  

### 10.4 TypeSpec / clients

- [ ] `packages/api-spec` module folder moves  
- [ ] `task gen` / committed clients policy  
- [ ] Admin/ops/portal paths that hardcode OpenAPI tags  

### 10.5 Tooling

- [ ] `Taskfile.yml` `api:db:migrate` / migrations:add  
- [ ] Docker/dev docs if schema list published  
- [ ] CI architecture test project references  

### 10.6 Tests

- [ ] Unit/module tests moved with code  
- [ ] Integration tests for cutover  
- [ ] Idempotency / signature / provision smoke  

### 10.7 Decisions hygiene

- [ ] Update `decisions.md` with reopen outcome  
- [ ] Update `FUTURE-WORK.md` FW-5 section to Done + PR link  
- [ ] Mark F15 checklist items  

---

## 11. Decision framework recap (when **not** to execute even if someone is eager)

Refuse extract/merge if any of:

1. Product has not reopened 00.x.  
2. Trigger is “clean architecture” without change-rate divergence.  
3. Dual-path work (API keys, LHDN webhooks) is mid-flight and extract multiplies risk.  
4. Team cannot freeze revenue features for migration window.  
5. Folders-only alternative has not been tried for cognitive pain.  
6. Cross-schema SQL leaks still uncleared in the area (FW-4) — fix leaks first so extract does not copy bad patterns.

---

## 12. Recommendation

### Stay deferred

| Candidate | Recommendation | Earliest realistic reopen signal |
|-----------|----------------|----------------------------------|
| **Credits extract** | **Do not extract.** Keep in Billing. Use `Billing/.../Wallet` folders if cognitive pain. | Credit monetization product-critical **and** ledger PR conflict **and** ≥ **2027-02-09** or earlier product reopen of 00.5 |
| **Webhooks extract** | **Do not extract.** Keep platform delivery in One. Finish FW-2 converge. Optional `One/.../Webhooks` folders. | Developer-platform delivery product dominates One changelog **and** 00.2/00.6 reopened |
| **Messaging → Communications** | **Do not merge.** Freeze thin Messaging per 00.4. | Funded multi-channel provider implementation starts **and** 00.4 reopened |

### Default engineering policy

1. **No** new `Modules/*` without G1–G7.  
2. **Yes** internal folders/namespaces anytime.  
3. Invest in **FW-1, FW-2, FW-4** before any FW-5 code.  
4. When a trigger fires: new design note + this playbook §7/§8/§9 — do not treat Phase 16 “N/A extract” files as incomplete work.

### Success definition for “doing nothing”

- Nine product modules remain.  
- Credits still in `billing` schema.  
- Webhooks still in `one` schema.  
- Messaging and Communications remain separate.  
- `decisions.md` freezes respected.  
- No “cleanup” PR that creates Credits/Webhooks modules “for later.”

---

## 13. Evidence index

| Artifact | Path |
|----------|------|
| Locked decisions | `plans/004-maintenance/decisions.md` (§00.2, §00.4, §00.5, §00.6) |
| Phase 16 done | `plans/004-maintenance/phase-16-done.md` |
| Phase 16 analysis (N/A) | `plans/004-maintenance/phase-16-analysis.md` |
| Phase 16 checklist | `plans/004-maintenance/checklists/phase-16-optional-extract-merge.md` |
| F15 future gate | `plans/004-maintenance/checklists-future/phase-f15-module-extract-gate.md` |
| Module boundaries report | `plans/004-maintenance/04-module-boundaries-modularization.md` (§9–§13, §11) |
| FUTURE-WORK FW-5 | `plans/004-maintenance/FUTURE-WORK.md` |
| New module ADR | `docs/architecture-decision-log/001-implementing-new-module.md` |
| Messaging freeze README | `apps/lazuar-api/Modules/Messaging/README.md` |
| Arch test module list | `apps/lazuar-api/tests/Lazuar.ArchitectureTests/ModuleBoundaryTests.cs` |
| Host module registration | `apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs` |
| Taskfile migrate contexts | `Taskfile.yml` (`api:db:migrate`) |
| TypeSpec one webhooks | `packages/api-spec/modules/one/models/webhook.tsp` |
| TypeSpec messaging | `packages/api-spec/modules/messaging/models.tsp` |

### Live code anchors (inventory snapshot)

| Concern | Example paths |
|---------|----------------|
| Credit balance / holds | `Modules/Billing/Domain/Aggregates/TenantCreditBalance.cs`, `CreditHold.cs` |
| Credit contracts | `Modules/Billing/Contracts/ICreditCostService.cs`, `Commands/DeductTenantCreditCommand.cs` |
| Credit consumers | `Modules/Lhdn/.../SubmitTaxDocumentCommand.cs`, `Modules/Messaging/.../DispatchMessageIntegrationEventHandler.cs`, `Modules/Communications/.../BroadcastEndpoints.cs` |
| Webhook domain | `Modules/One/Domain/TenantWebhookEndpoint.cs`, `WebhookDeliveryOutbox.cs` |
| Webhook delivery | `Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` |
| Webhook request event publisher | Commerce lifecycle / integration checkout tests referencing `OutboundWebhookRequestedIntegrationEvent` |
| Messaging dispatch | `Modules/Messaging/Contracts/DispatchMessageIntegrationEvent.cs` |
| Messaging module tree | `Modules/Messaging/**` |

---

## 14. Document maintenance

- When product reopens a candidate: add design note link under the relevant §7/§8/§9 and mark that section **In progress**.  
- When extract/merge ships: mark section **Done** with date + PR; update `FUTURE-WORK.md` FW-5; update `decisions.md`.  
- Do **not** interpret this file as a backlog commitment to execute 16.A–C.

---

*End of uncondensed FW-5 analysis. Recommendation: **stay deferred**. No application code was modified.*
