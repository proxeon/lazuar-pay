# W1-LP-005 — Prepaid utility credits (meter only when LHDN / later WhatsApp actually costs us)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 row `LP-005` (“Prepaid utility credits (only once LHDN/WhatsApp meter something real)”). Tracker label in [00-checklist-tracker.md](../00-checklist-tracker.md) is “Prepaid utility credits (LHDN / WhatsApp)” (`Ours = P`). Evidence: [10-lhdn-einvoice-competitors.md](../10-lhdn-einvoice-competitors.md) (“Charge path exists; do not sell credits for live e-invoice while UI is hidden”), [16-communications-whatsapp-email.md](../16-communications-whatsapp-email.md) G23 / COM-033, [19-refuse-list-and-adjacents.md](../19-refuse-list-and-adjacents.md) (plane U wallet is allowed).  
**Not this ID:**

| File | Their `LP-005` | This ticket |
|------|----------------|-------------|
| [18-pricing-onboarding-trust.md](../18-pricing-onboarding-trust.md) | Per-checkout credit tax — **Never** | **Ignore.** That is a refuse row, not this feature. Do not charge 1 credit per pay. |
| [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) | Tenant isolation fail-closed | **Ignore.** Different taxonomy. |
| Same 18-pricing `LP-004` / `LP-003` | “Credits consume on LHDN/WA as marketed” / “credits in sidebar” | Related honesty, not a second wallet. Sidebar / public pricing are **not** this ticket (`LP-006` is signup+pricing; official `LP-003` is acquirer refuse). |

**Invariant:** `TenantCreditBalance` meters **our** prepaid software usage (plane U). Deduct only when a **real** LHDN live-key submit is accepted, or — later, not now — when a **paid** WhatsApp provider actually sends. Do not invent meters. Do not charge for `ConsoleMessagingService` log lines. Do not tax GMV / checkout.

---

## 0. Scope lock

In scope:

- Configured credit costs (`Credits:Costs`) and `ICreditCostService.GetCost` default
- LHDN deduct on `SubmitTaxDocumentCommand` (single path, idempotent `lhdn:{key}`)
- WhatsApp deduct on `DispatchMessageIntegrationEventHandler`
- Proof that email, broadcast, checkout, TIN, cancel, poll, and the submitted-event handler do **not** deduct
- Honest ops copy on the existing credits page (if touched)
- Tests for the meter invariant

Out of scope (do not expand this ticket):

- Meta Cloud / real WhatsApp (`LP-074`, `LP-155`, decisions.md **00.4** freeze)
- Un-hiding LHDN / invoicing UI (Wave 2 `LP-110`+)
- Public pricing page / SST on our fee (`LP-006` official / 18-pricing `LP-001` / `LP-010`)
- Putting credits in the ops sidebar (18-pricing `LP-003` — different ID)
- Per-checkout credit (`18` `LP-005` — **Never**)
- Tenant usage billing for *their* customers (`LP-061`)
- Extracting a Credits module (00.5: stay in Billing)
- Moving LHDN deduct onto `LhdnDocumentSubmittedIntegrationEvent` (already closed; do not reopen)
- Charging TIN validate, cancel, status poll, or VALID
- Wiring `CreditAction.EmailSend` / `BroadcastEmailPerRecipient`
- Building / using `ReserveCredits` for broadcasts
- Starter grant, top-up checkout, chargeback clawback (already shipped; do not rewrite)
- `SendTenantNotificationCommandHandler` (system console alert, not a tenant meter)

**Dependency (do not implement here):** a real WhatsApp provider. Until one exists, WhatsApp cost **must be 0** and the dispatch path must refuse to deduct even if someone flips `Messaging:WhatsAppEnabled`.

---

## 1. What “actually costs us” means here

Two planes, do not mix:

| Plane | Who | Money | Wallet? |
|-------|-----|-------|---------|
| **B** — buyer → merchant | Their Billplz / Stripe / CHIP | Guest GMV | **Never** `TenantCreditBalance` |
| **U** — tenant → Lazuar | Prepaid credits | Our software meters | **Yes — this ticket** |
| **A** — SaaS seat | Future Hub Pro | Flat fee | Not this ticket (`LP-004` official) |

IRBM MyInvois does **not** invoice Lazuar per submit. “Costs us” here is **prepaid software usage of a real utility**, not a third-party IRBM invoice (ADR 019 §3). Email is tenant **BYOK Resend** — we do not pay Resend for their mail, so we do not meter `EmailSend`. Console WhatsApp is a log line — we pay nothing — so we do not meter it.

| Action | Real utility today? | Meter? |
|--------|---------------------|--------|
| Live-key `POST /lhdn/documents` (and the same command from B2C consolidation) | Yes — document enters the MyInvois pipeline | **Yes** — `CreditAction.LhdnSubmit` |
| Test-key LHDN submit (`IsTestMode`) | Sandbox | **No** |
| `LhdnDocumentSubmitted` / `Validated` / `Cancelled` events | Observability / merchant ledger | **No** (submitted-event deduct is **intended closed**) |
| TIN validate, cancel, poll | IRBM calls, not the sold SKU | **No** |
| Refund handler persist of stub credit-note XML | Bypass of `SubmitTaxDocumentCommand`; stub buyer | **No** this ticket (Wave 2 LHDN honesty) |
| WhatsApp via `ConsoleMessagingService` | Stub | **No** |
| WhatsApp via Meta Cloud | Not built (00.4) | **Later** — do not invent the rate as a live SKU |
| Email (tenant Resend) | Their key, their bill | **No** |
| Broadcast fan-out | Vitamin; TypeSpec already `credits_* = 0` | **No** |
| Checkout / renewal / refund of *their* sale | Plane B | **Never** |

**Design lock — keep LHDN deduct on `SubmitTaxDocumentCommand`.**

Plan 10 already froze this: one deduct on accept, idempotency key `lhdn:{key}`, test keys skip, **do not** reintroduce deduct on the submitted event. Moving the charge to `LhdnSubmissionJob` after MyInvois HTTP would be more “physical,” but it is a second write path, it would start charging the refund-handler’s stub credit notes, and it is how the old double-charge comes back. **Out of scope.** Early charge if the job later `FAILED` (missing credentials) is an accepted limitation of this ticket.

---

## 2. Wallet model (current)

`TenantCreditBalance` is one row per org (`billing.TenantCreditBalances`, unique `OrganizationId`). Integer `AvailableCredits`. Optimistic concurrency via PostgreSQL `xmin`. Child `CreditLedger` lines are signed amounts + free-text `Reference`.

| Method | Behavior |
|--------|----------|
| `TopUp(n, ref)` | `n > 0`; add n |
| `Deduct(n, ref)` | `n > 0`; throw `BusinessRuleValidationException` (“402: Insufficient credits…”) if `AvailableCredits < n` |
| `Clawback(n, ref)` | Clamp at zero; no throw (spent credits after a top-up dispute) |

`DeductTenantCreditCommandHandler`:

- Optional `IdempotencyKey` → `CreditDeductionIdempotencyLogs` unique `(OrganizationId, IdempotencyKey)`
- Same key → return (no second deduct)
- Unique-violation race → treat as success
- `xmin` conflict → retry up to 3
- Missing wallet → `InvalidOperationException` (“Tenant credit wallet not found.”)
- `Deduct(0)` is illegal at the domain (`ArgumentException`)

`ICreditCostService` / `CreditCostService` binds `appsettings` `Credits`:

```json
"Credits": {
  "Costs": { "WhatsAppSend": 2, "LhdnSubmit": 3 },
  "Packages": [
    { "AmountMyr": 50, "Credits": 500 },
    { "AmountMyr": 100, "Credits": 1100 },
    { "AmountMyr": 200, "Credits": 2500 }
  ],
  "StarterGrant": 50
}
```

`GetCost` today: missing key → **1**. That is a fake meter waiting for any new `CreditAction` or a typo.

`CreditAction` enum: `EmailSend`, `WhatsAppSend`, `LhdnSubmit`, `BroadcastEmailPerRecipient`. Only the middle two are read in production. Email/broadcast must stay unused.

Holds (`ReserveCredits` / `ConsumeCreditHold` / `ReleaseCreditHold`) exist for a future broadcast reservation. **Nothing in production calls `ReserveCreditsCommand`.** Broadcast fan-out instead sets `DispatchMessageIntegrationEvent.CreditHoldId = broadcast.Id` (not a Billing hold id) so the dispatcher **skips** wallet deduct. That is how v1 broadcasts stay free. Do not “fix” holds in this ticket.

---

## 3. Current files

### 3.1 Wallet + cost

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Domain/Aggregates/TenantCreditBalance.cs` | Plane U aggregate |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Domain/Entities/CreditLedger.cs` | Signed wallet line |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Domain/Entities/CreditDeductionIdempotencyLog.cs` | Deduct idempotency |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Domain/Aggregates/CreditHold.cs` | Unused reservation |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Contracts/ICreditCostService.cs` | `CreditAction` + packages |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Services/CreditCostService.cs` | Config bind; **default cost 1** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Contracts/Commands/DeductTenantCreditCommand.cs` | Deduct + clawback commands |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Commands/DeductTenantCreditCommandHandler.cs` | Idempotent deduct |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Commands/CreditHoldCommandHandlers.cs` | Hold/release (no callers) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs` | `GetAvailableCreditsAsync` / `HasSufficientCreditsAsync` (`amount <= 0` → true) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/appsettings.json` | `Credits` + `Messaging:WhatsAppEnabled=false` |

### 3.2 Top-up / grant / clawback (keep; do not rewrite)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/StarterCreditSeederHandler.cs` | On `AppEntitlementGranted` `BILLING` — one wallet + `StarterGrant` (50) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminCreditsEndpoints.cs` | `GET /admin/billing/credits`, packages, `POST …/top-up` (min RM 50) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Queries/GenerateSystemCheckoutSessionQueryHandler.cs` | Platform tenant `…0001` BYOK; metadata `type=utility_credit_topup` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformTopUpEventHandler.cs` | Grants package credits; ledger `SYSTEM_CREDIT_TOPUP`; skips if no gateway tx id |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` | Merchant GMV ledger; **skips** `utility_credit_topup` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs` | Clawback **only** utility top-up disputes |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ApiCreditPurchasedHandler.cs` | **Dead** — not subscribed; do not revive |

TypeSpec: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/billing/routes.tsp` + `models.tsp` (`CreditBalanceDto`, packages, top-up).

### 3.3 LHDN meter

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` | **The** deduct owner. Pre-check credits unless test. Persist XML. Deduct `LhdnSubmit` with `lhdn:{IdempotencyKey\|doc.Id}`. Deduct failure is logged; document stays. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs` | `POST /lhdn/documents` requires `Idempotency-Key`; maps 402 from the pre-check |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | Sets `IsTestMode` claim from test keys |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/ExecutionContextAccessor.cs` | `IsTestMode` from that claim; **false** in background jobs (no HTTP user) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentSubmittedIntegrationEventHandler.cs` | Log only — **must stay** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnSubmissionJob.cs` | MyInvois HTTP; publishes submitted event; **must not deduct** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/ConsolidatedInvoiceIssuedIntegrationEventHandler.cs` | B2C month job → same `SubmitTaxDocumentCommand` (same meter) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/InvoiceIssuedIntegrationEventHandler.cs` | Stub-buyer auto-submit. **`InvoiceIssuedIntegrationEvent` is never published.** Do not revive. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` | Cancel &lt;72h; &gt;72h persists a stub credit note **without** the command (no deduct). Leave. |

`LhdnDocumentValidatedIntegrationEventHandler` writes merchant ledger LHDN status + PDF. No wallet.

### 3.4 WhatsApp / email dispatch

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/Messaging/ConsoleMessagingService.cs` | **Only** `IMessagingService`. Logs `[Local Dispatch] [MESSAGING/SMS]`. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/DependencyInjection.cs` | Registers console as singleton |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` | Email via tenant Resend. WA gated by `Messaging:WhatsAppEnabled` (default false). If flag on: check `WhatsAppSend` credits, call console, **add cost, deduct**. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs` | Flag off → demote `WHATSAPP`/`ALL` to email or skip |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Workers/BroadcastFanoutJob.cs` | Email only; `CreditHoldId: broadcast.Id` skips deduct |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/BroadcastEndpoints.cs` | Preview always `credits_* = 0`, `sufficient_credits = true` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Application/SendTenantNotificationCommandHandler.cs` | Console to tenant slug. Not a wallet meter. Leave. |

Default deploy: flag false → WA never reaches `IMessagingService` → no deduct. **Safe only while the flag stays false.**

### 3.5 Ops UI (honesty only)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/BillingSettingsPage.tsx` | Routed `/workspace/billing`. **Not in sidebar.** Copy claims credits are for “automated LHDN tax submissions and WhatsApp dunning.” |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/UtilityLedgerPage.tsx` | Routed `/workspace/ledger`. History only. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/Sidebar.tsx` | Workspace: General, Payment Gateways, Email Provider. **No credits link.** |

README L65 still says WhatsApp dunning deducts micro-credits. Out of scope unless a one-line honesty pass is cheap; do not turn this ticket into a README program.

---

## 4. End-to-end

### 4.1 LHDN (live key)

```
POST /lhdn/documents + Idempotency-Key
  → SubmitTaxDocumentCommand
      IsTestMode? skip credit check
      else HasSufficientCreditsAsync(LhdnSubmit) — 402 if not
      generate + validate XML
      persist TaxDocument PENDING (+ LHDN idempotency log)
      DeductTenantCreditCommand(amount=LhdnSubmit, key=lhdn:{header})
  → LhdnSubmissionJob later
      MyInvois SubmitDocumentAsync
      MarkAsSubmitted → LhdnDocumentSubmitted (Billing: log only)
```

Retry of the same HTTP key returns the existing doc id and does not deduct again (command short-circuit **and** wallet key).

B2C 28th job: `ConsolidatedInvoiceIssued` → same command with a new GUID key (background `IsTestMode=false`). That **is** the same real meter.

### 4.2 WhatsApp (default deploy)

```
Dunning WHATSAPP step
  → DunningStepDispatcher (flag false) → EMAIL or skip
  → DispatchMessage: wantsWhatsApp but flag false
      → MessageDeliveryLog SKIPPED "WhatsApp channel disabled"
      → IMessagingService not called
      → actualCost stays 0 → no Deduct
```

### 4.3 WhatsApp if someone flips the flag (the incident)

```
DispatchMessage WHATSAPP
  → GetCost(WhatsAppSend) = 2
  → HasSufficientCredits / maybe 402-style skip
  → ConsoleMessagingService.SendMessageAsync → log line, Task.Completed
  → actualCost += 2
  → DeductTenantCreditCommand(2, event.Id)
```

That is billing a console log. **This ticket exists to make that impossible.**

---

## 5. What is already correct

1. **One wallet, one org.** Unique index + `xmin` + deduct retries. Domain refuses overdraft.
2. **LHDN is a single deduct path.** Command owns it. Submitted-event handler is log-only (comment + test).
3. **Test keys skip LHDN credits.** `IsTestMode` from the API key.
4. **LHDN deduct is idempotent** on `lhdn:{Idempotency-Key}` (or `lhdn:{doc.Id}` if the header were empty — HTTP requires the header).
5. **Email is not metered.** Dispatch never calls `GetCost(EmailSend)`.
6. **Broadcasts are declared free.** TypeSpec + preview zeros. Fan-out does not call `Deduct` (hold-id skip).
7. **Default `WhatsAppEnabled=false`** plus dunning demotion means production does not currently deduct WA.
8. **GMV ledger skips credit top-ups.** `GatewayPaymentCompletedHandler` ignores `utility_credit_topup`.
9. **Chargeback clawback is plane U only.** Does not touch subscriptions / merchant ledger.
10. **Starter grant + packages + top-up + history API** already exist. Do not rebuild them.
11. **`Deduct(n<=0)` is illegal.** Callers must not send 0.

Tracker `Ours = P` is right: the wallet is real; the meters are only half-honest.

---

## 6. Exact gaps

### G1 — WhatsApp config cost is 2 while transport is a console stub (P0)

`Credits:Costs:WhatsAppSend = 2`. Dispatch adds that cost after `IMessagingService` “success.” The only implementation is `ConsoleMessagingService`. Flip `Messaging:WhatsAppEnabled` (or a future test) and we sell 2 credits per log line. Report 16 already named this G23 / COM-033.

### G2 — Dispatch will deduct for console if the flag is on (P0)

Even after setting config to 0, a later operator can set `WhatsAppSend` back to 2. There is **no** `is ConsoleMessagingService` / `!IsBillable` guard. The meter must be tied to a **billable transport**, not to the flag.

### G3 — `GetCost` defaults to 1 (invents fake meters)

Any unconfigured `CreditAction` (`EmailSend`, `BroadcastEmailPerRecipient`, a future typo) costs **1**. That contradicts “do not invent fake meters.” Default must be **0**.

### G4 — LHDN deducts even when `LhdnSubmit` is 0

`HasSufficientCreditsAsync(0)` is true. Then `Deduct(0)` throws. The document is already saved; the throw is logged as “deduction failed.” Harmless for current config (3), but if product sets LHDN to 0 (honest “not selling yet”), submit logs an error on every accept. Skip deduct when `lhdnCost <= 0`.

### G5 — No automated test that console WhatsApp never bills

`DispatchMessageIntegrationEventHandlerTests` stub `GetCost(WhatsAppSend) → 0` and only cover flag **false**. There is no test for flag **true** + console + cost 2 → **zero** `DeductTenantCreditCommand`.

### G6 — Ops / marketing copy sells WA (and hidden LHDN) as live SKUs

`BillingSettingsPage` description names “automated LHDN tax submissions and WhatsApp dunning.” LHDN merchant UI is `[MVP-HIDE]`. WhatsApp is stub. Do not add a sidebar. If this page is edited, make the copy true: credits pay for **live-key LHDN submits**; WhatsApp is not billed until a real provider exists.

### G7 — `CreditHoldId` on broadcasts is a landmine (not a gap to close here)

Fan-out passes `broadcast.Id`, which is not a `CreditHold`. Dispatcher treats any `HasValue` as “already billed.” Fine while WA is unbilled. Do **not** invent a real hold in this ticket. When Meta ships, holds must be real Billing ids or this skip must go.

**Not gaps for this ticket**

| Observation | Why not LP-005 |
|-------------|----------------|
| Charge on persist, not MyInvois HTTP | Accepted; moving it reopens double-charge and would bill stub refund XML |
| B2C job uses the command (charges) | Same real LHDN meter |
| Refund &gt;72h credit note not charged | Wave 2 document honesty; stub buyer |
| `InvoiceIssuedIntegrationEventHandler` stub TIN | Dead publisher; do not wire |
| Credits page not in sidebar | Different job; do not sell packs this ticket |
| `ApiCreditPurchasedHandler` unsubscribed | Dead; leave |
| `SendTenantNotification` console | System alert, not tenant usage |
| Domain tests still say `"email dispatch"` | String only; email is not metered |
| README L65 / L77 WhatsApp claims | Honesty program; optional one-liner only |

---

## 7. Minimal code changes

Prefer config + two guards. No new module, no TypeSpec field, no Meta adapter, no sidebar.

### 7.1 Must change

| File | Function | Change |
|------|----------|--------|
| `apps/lazuar-api/src/Lazuar.Api/appsettings.json` | `Credits:Costs` | Set `"WhatsAppSend": 0`. Keep `"LhdnSubmit": 3`. Do not add `EmailSend` / `BroadcastEmailPerRecipient`. |
| `apps/lazuar-api/Modules/Billing/Infrastructure/Services/CreditCostService.cs` | `GetCost` | Missing / unparsed action → **0**, not 1. |
| `apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` | WA cost + deduct | After resolving `whatsappCost`, if the transport is not billable, force `whatsappCost = 0` (and therefore skip sufficiency check, skip `actualCost`, skip deduct). Billable = **not** `ConsoleMessagingService`. Do not deduct on `IMessagingService` success unless cost &gt; 0 **and** transport is billable. Keep flag-off skip as today. |
| `apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` | deduct block | If `isTestMode` **or** `lhdnCost <= 0`, do not send `DeductTenantCreditCommand`. Keep 402 pre-check only when `!isTestMode && lhdnCost > 0`. |

Smallest transport guard (no new interface):

```csharp
if (_messagingService is ConsoleMessagingService)
    whatsappCost = 0;
```

If you prefer not to take a concrete type dependency from the handler, add `bool IsBillable { get; }` on `IMessagingService` (`ConsoleMessagingService` → `false`). That is still in scope. Do **not** add a Meta class.

### 7.2 Should change (same ticket, small)

| File | Function | Change |
|------|----------|--------|
| `apps/lazuar-ops/src/modules/workspace/pages/BillingSettingsPage.tsx` | title / description / helper | Say credits are for **live LHDN e-invoice submits**. WhatsApp recovery is **not billed** (not connected). Do not promise “automated WhatsApp dunning.” |
| `IMessagingService` | optional | `IsBillable` as above if you refuse the `is` check. |

### 7.3 Do not change

- `LhdnDocumentSubmittedIntegrationEventHandler` (must remain no-op on the wallet)
- `LhdnSubmissionJob` (must not deduct)
- `TenantCreditBalance` / deduct handler / xmin / idempotency log
- `PlatformTopUpEventHandler`, starter seeder, clawback
- Broadcast preview zeros / `CreditHoldId` skip
- Dunning demotion (already correct with flag false)
- TypeSpec credit DTOs
- Ops sidebar
- `CreditAction` enum members (leave unused; default 0)
- Sample cashier / commerce checkout

### 7.4 Optional later (not required to close LP-005)

- Refund unused LHDN credits if the job marks `FAILED` (new idempotent reverse; easy to get wrong)
- Move deduct to MyInvois success **and** remove command deduct (Wave 2; never add a second path)
- Real WhatsApp adapter: then set `WhatsAppSend` to a researched MY utility rate, implement `IsBillable = true`, deduct only after provider message id
- Credits in sidebar + public pack table (other IDs)

---

## 8. Tests to add

Wallet/idempotency/concurrency already exist. Add **meter honesty**. Do not spin a new host unless one already covers public LHDN.

### 8.1 New: `CreditCostServiceTests`

File: `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Services/CreditCostServiceTests.cs`

| Case | Expect |
|------|--------|
| Options with `LhdnSubmit=3`, `WhatsAppSend=0` | `GetCost(LhdnSubmit)==3`, `GetCost(WhatsAppSend)==0` |
| Empty `Costs` | `GetCost` every `CreditAction` == **0** (not 1) |
| `EmailSend` / `BroadcastEmailPerRecipient` omitted | **0** |
| Unknown JSON key | ignored; no throw |
| `StarterGrant` / packages | unchanged (optional assert) |

### 8.2 Extend: `LhdnSingleCreditPathTests`

Already asserts live deduct of configured amount and test-mode skip.

| Case | Expect |
|------|--------|
| `GetCost(LhdnSubmit)==0`, not test | **No** `DeductTenantCreditCommand`; **no** `HasSufficientCreditsAsync` (or call with 0 is ok if you still call it — prefer no deduct send) |
| `GetCost==3`, live | unchanged: one deduct, key `lhdn:{header}`, amount 3 |
| Test mode | still no deduct even if cost is 3 |

Do **not** add a test that the submitted-event handler deducts. Keep `LhdnDocumentSubmittedIntegrationEventHandlerTests` as the no-wallet constructor proof.

### 8.3 Extend: `DispatchMessageIntegrationEventHandlerTests`

Need a handler built with **real** `ConsoleMessagingService` (or a substitute that is still “not billable”) and configurable flag/cost.

| Case | Expect |
|------|--------|
| Flag false, cost 2, channel WHATSAPP | No `IMessagingService` send; no `Deduct`; log `SKIPPED` / disabled (already exists — keep) |
| Flag **true**, cost 2, `ConsoleMessagingService` (or `IsBillable=false`) | May call send (log); **`DeductTenantCreditCommand` not sent**; `actualCost` path not charged |
| Flag true, cost 0, substitute messaging | Send may run; **no deduct** (`actualCost > 0` guard) |
| Channel EMAIL only | never `GetCost` used to deduct; no `Deduct` (already implied) |

If the guard is `is ConsoleMessagingService`, construct the SUT with the real console type for the P0 case.

### 8.4 Do not add

- Meta Cloud contract tests
- Broadcast hold reservation tests
- Checkout-consumes-credit tests (that would be building the refuse row)
- New WebApplicationFactory only for this

### 8.5 Manual (optional)

1. Live LHDN key + wallet 50: one submit → balance 47; same `Idempotency-Key` → still 47.
2. `sk_test_` submit → balance unchanged.
3. Leave `WhatsAppEnabled=false`: dunning WA step → email or skip; wallet unchanged.
4. Temporarily `WhatsAppEnabled=true` on local: WA step must **not** reduce credits while console is the transport.

---

## 9. Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Changing `GetCost` default 1→0 breaks a hidden caller that relied on 1 | Low | Grep shows only LHDN + WA `GetCost`. Tests pin 0. |
| Setting `WhatsAppSend=0` while leaving dispatch unguarded | Med | Do both: config 0 **and** console-not-billable |
| `is ConsoleMessagingService` couples handler to infra type | Low | Acceptable; or `IsBillable` on the port |
| Operator sets `WhatsAppSend=2` in prod “to prepare” | High if unguarded | Guard is the feature |
| Skipping LHDN deduct when cost 0 while 402 pre-check still uses 3 in an old binary | None if one deploy |
| Moving deduct to the job “to be more correct” | High | **Forbidden** in this ticket |
| Selling credit packs for WhatsApp in the same PR | High (honesty) | Do not add sidebar / change packages copy to “WA messages” |
| `CreditHoldId` skip + future Meta | Med later | Document only |
| LHDN persist-then-job-fail still charged | Low (accepted) | Do not refund in this ticket |

---

## 10. Acceptance criteria

Close LP-005 when all of the following are true:

1. **No fake meters.** `GetCost(EmailSend)`, `GetCost(BroadcastEmailPerRecipient)`, and any omitted action return **0**. No new deduct call sites. Checkout / renewals / refunds of guest money never call `DeductTenantCreditCommand`.
2. **WhatsApp stub never bills.** With `ConsoleMessagingService` as `IMessagingService`, `DeductTenantCreditCommand` is not sent for WhatsApp, including `Messaging:WhatsAppEnabled=true` and `WhatsAppSend>=1` in config.
3. **Default config does not advertise a WA price.** `Credits:Costs:WhatsAppSend` is **0** until a billable provider exists.
4. **LHDN live submit still meters once.** Not test mode, `LhdnSubmit > 0`: exactly one `DeductTenantCreditCommand` per new idempotency key, amount from config, key `lhdn:{Idempotency-Key}`. Retry same key → no second deduct.
5. **LHDN test mode still free.**
6. **LHDN cost 0 does not deduct** and does not throw from `Deduct(0)`.
7. **`LhdnDocumentSubmittedIntegrationEventHandler` still does not touch the wallet.**
8. **Email and broadcasts remain free** (`credits_*` preview 0; no `EmailSend` deduct).
9. Tests in §8.1–8.3 exist and pass. Existing deduct idempotency + LHDN single-path + submitted-handler tests still pass.
10. Ops credits page, if edited, does not claim WhatsApp is a live paid channel.

Do **not** flip tracker `Ours` to `Y` until (2)–(4) and (7) are true. Selling “credits for live e-invoice” in the sidebar while LHDN UI is hidden can stay `P` — that is Wave 2 / commercial packaging, not a meter bug.

---

## 11. Suggested implement order

1. `GetCost` default 0 + `WhatsAppSend: 0` + tests §8.1  
2. Dispatch console / not-billable guard + tests §8.3  
3. LHDN skip deduct when cost 0 + tests §8.2  
4. Ops copy (G6)  
5. Manual smoke §8.5  

That is the whole ticket.

---

## 12. Implementer grep (do not add callers)

```text
DeductTenantCreditCommand
GetCost(
CreditAction.
WhatsAppSend
LhdnSubmit
EmailSend
BroadcastEmailPerRecipient
utility_credit_topup
ConsoleMessagingService
```

Production deduct senders today: `SubmitTaxDocumentCommandHandler`, `DispatchMessageIntegrationEventHandler`. After this ticket they must remain the only two, and the second must be a no-op for console.
