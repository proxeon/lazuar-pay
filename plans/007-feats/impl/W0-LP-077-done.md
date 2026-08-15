# W0-LP-077 — done

PAST_DUE / SUSPENDED recovery now **proves** campaign `RecordRecovery` on the two sold write paths (magic-link metadata **and** Billplz-stripped fallback / off-session webhook), and Ops **Log Payment** increments the same counters when it recovers arrears with `amount > 0`. Commerce stats roll the existing lifetime `SUM(RecoveredRevenue)` / `SUM(SavedSubscriptions)` for the org. Dashboard shows **Recovered (lifetime)** from that field and links to Dunning Campaigns.

Campaign id is captured **before** `RecoverFromPayment` / `Resume` / `ClearDunning`. Replay after ACTIVE does not increment. ACTIVE renewal does not increment even if metadata carries a campaign id. COMPED / `amount = 0` does not increment. No new table. No monthly series. Tracker stays **P**.

## Files changed

### Writers

- `apps/lazuar-api/Modules/Commerce/Application/DunningRecoveryAttribution.cs` — shared capture-before-clear (metadata wins, else `CurrentDunningCampaignId`)
- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` — uses helper; increment path unchanged
- `apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs` — capture id; `RecordRecovery(amount)` when arrears and amount > 0

### Stats / contract / dashboard

- `packages/api-spec/modules/commerce/models/stats.tsp` — `recovered_revenue`, `saved_subscriptions`
- `packages/api-spec/dist/**` + `packages/api-types-ts` + `packages/api-types-dotnet` — `task gen:spec` / `gen:types-ts` / `gen:types-dotnet`
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Stats.cs` — org-scoped SUM, not an in-memory load
- `apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx` — **Recovered (lifetime)** KPI + footnote to `/commerce/dunning-campaigns`

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GatewayPaymentCompletedRecoveryMetricsTests.cs` — H1–H11
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs` — Log Payment PAST_DUE increments; COMPED / ACTIVE do not
- `apps/lazuar-api/tests/Lazuar.IntegrationTests/CommerceQueryServiceTests.cs` — SUM 120.5 / 3; empty org 0

## Tests run

- `Lazuar.ModuleTests` filter `GatewayPaymentCompletedRecoveryMetricsTests|CommerceProductCompletenessTests|DunningCampaignDomainTests|SubscriptionRecoveryTests` — **48 passed**
- `Lazuar.ModuleTests` filter `TenantIsolationHardeningTests|CommerceHonestyDtoTests|GatewayPaymentFailedIntegrationEventHandlerTests` — **24 passed**
- `Lazuar.IntegrationTests` filter `CommerceQueryServiceTests` — **3 passed**

Not committed. Not pushed.

Still **P**: no monthly recovered MRR, no recovery rate, campaign list still hardcodes `RM`, unassigned PAST_DUE pays still unattributed, `RecordChurn` still ignores SUSPEND. MD-011 / DN-028 remain the later dashboard job.
