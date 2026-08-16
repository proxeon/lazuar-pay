# W1-LP-005 — done

Prepaid utility credits meter **only** a real live-key LHDN submit. WhatsApp console stub is never billed, even if `Messaging:WhatsAppEnabled=true` and `WhatsAppSend >= 1`. `GetCost` no longer invents a cost of 1 for unused / omitted actions. `Deduct(0)` is not sent. Ops credits copy does not sell WhatsApp as a live SKU.

No Meta Cloud. No checkout tax. Production deduct senders remain `SubmitTaxDocumentCommandHandler` and `DispatchMessageIntegrationEventHandler` (the second is a no-op for console). `LhdnDocumentSubmittedIntegrationEventHandler` is still log-only.

Tracker LP-005 Lazuar stays **P** — wallet is real; LHDN merchant UI is still hidden (Wave 2 packaging, not a meter bug).

## Files changed

### Config + cost

- `apps/lazuar-api/src/Lazuar.Api/appsettings.json` — `Credits:Costs:WhatsAppSend` **0**. `LhdnSubmit` stays 3. No `EmailSend` / `BroadcastEmailPerRecipient` keys.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Services/CreditCostService.cs` — missing / unparsed action → **0**, not 1.

### WhatsApp dispatch (not billable)

- `apps/lazuar-api/Modules/Messaging/Application/IMessagingService.cs` — `IsBillable`.
- `apps/lazuar-api/Modules/Messaging/Infrastructure/Messaging/ConsoleMessagingService.cs` — `IsBillable => false`.
- `apps/lazuar-api/Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` — after `GetCost(WhatsAppSend)`, force cost **0** if transport is `ConsoleMessagingService` or `!IsBillable`. Flag-off skip unchanged. Deduct still only when `actualCost > 0`.

### LHDN meter

- `apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` — 402 pre-check and `DeductTenantCreditCommand` only when `!isTestMode && lhdnCost > 0`. Key still `lhdn:{Idempotency-Key}`.

### Ops copy

- `apps/lazuar-ops/src/modules/workspace/pages/BillingSettingsPage.tsx` — credits are for live-key LHDN submits; WhatsApp is not connected and not billed. No sidebar.

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Services/CreditCostServiceTests.cs` — **new.** Configured 3/0; empty costs → every `CreditAction` is 0; omitted Email/Broadcast → 0; unknown JSON key ignored. Gap-fill: unknown `CreditAction` enum → **0**; omitted `WhatsAppSend` → **0**; `appsettings.json` `WhatsAppSend` is **0** (no Email/Broadcast keys).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnSingleCreditPathTests.cs` — live cost 0 sends no deduct (including `Deduct(0)`) and no sufficiency check. Live cost **> 0** still deducts once (`lhdn:{key}`, amount 3). Test-mode skip unchanged.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs` — flag on + **real** `ConsoleMessagingService` + cost 2 → no `DeductTenantCreditCommand`; cost 0 substitute → send, no deduct; flag off + cost 2 → skip, no deduct. Email never `GetCost(EmailSend)`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/ConsoleMessagingServiceTests.cs` — `IsBillable` is false.

## Tests run

- `Lazuar.ModuleTests` filter `CreditCostServiceTests|LhdnSingleCreditPathTests|DispatchMessageIntegrationEventHandlerTests|ConsoleMessagingServiceTests|LhdnDocumentSubmittedIntegrationEventHandlerTests|DeductTenantCreditIdempotencyTests` — **24 passed**
- `Lazuar.ModuleTests` filter `Billing|Messaging|LhdnSingleCreditPathTests|LhdnRateLimitingTests` — **66 passed**
- `Lazuar.ArchitectureTests` — **14 passed**
- `Modules.Billing.Tests` — **20 passed**

### Gap-fill re-run (2026-08-16)

Added/tightened: `GetCost_UnknownAction_ReturnsZero`, `GetCost_WhatsAppSendOmitted_DefaultsToZero`, `GetCost_AppsettingsJson_WhatsAppSendDefaultsToZero`, `Handle_LiveMode_CostGreaterThanZero_StillDeducts`; Console flag-on never deducts; LHDN cost 0 never sends `Deduct(0)`.

- `Lazuar.ModuleTests` filter `CreditCostServiceTests|LhdnSingleCreditPathTests|DispatchMessageIntegrationEventHandlerTests|ConsoleMessagingServiceTests|LhdnDocumentSubmittedIntegrationEventHandlerTests|DeductTenantCreditIdempotencyTests` — **28 passed**, 0 failed, 0 skipped. Duration 456 ms.
- `Lazuar.ModuleTests` filter `Billing|Messaging|LhdnSingleCreditPathTests|LhdnRateLimitingTests` — **87 passed**, 0 failed, 0 skipped. Duration 4 s.
- `Lazuar.ArchitectureTests` — **14 passed**, 0 failed, 0 skipped. Duration 459 ms.
- `Modules.Billing.Tests` — **20 passed**, 0 failed, 0 skipped. Duration 56 ms.

Manual §8.5 (live key wallet 50→47, test key, flag flip) **not run** here.

Not committed. Not pushed.

Meta Cloud / `WhatsAppSend` as a live rate remain Wave 4 (`LP-074` / `LP-155`). Sidebar packs and public pricing are not this ticket (`LP-006`).
