# W0 — LP-077 analysis: recovered-revenue metrics

**Program:** `plans/007-feats`  
**ID:** LP-077 — *Recovered-revenue metrics*  
**Wave:** 0 (`00-implement-ids.md`; tracker row LP-077 = **P** today)  
**Date:** 2026-08-16  
**Status:** Analysis only — **do not implement from this file**  
**Related:** DN-003 / DN-021 / DN-028 in `plans/007-feats/12-dunning-and-recovery.md`; MD-011 / MD-012 in `plans/007-feats/17-merchant-dashboard-analytics.md`; Phase A residual in `plans/001-backend/001-backend-solidification-checklist.md` A.10.

**Feature in one sentence:** When a `PAST_DUE` (or `SUSPENDED`) subscription pays via **magic update-payment** or **off-session AUTO_CHARGE**, the campaign recovered-revenue counters increment, Commerce stats can roll them up, and the dashboard can show the number we already persist.

**Wave 0 bar** (`00-evaluation.md`): *Successful recovery (magic link or off-session) always exits dunning, advances period, attributes metrics.*

This ticket is **attribution + visibility**, not Paddle Retain.

---

## 1. Verdict

| Question | Answer |
|----------|--------|
| Does `RecoverFromPayment` recover the sub? | **Yes.** PAST_DUE → ACTIVE, advances dates, `ClearDunning`. Tests exist. |
| Does gateway success attribute `$` today? | **Yes, if** the sub was in arrears **and** a campaign id is available (metadata or `CurrentDunningCampaignId`) **and** the campaign row still exists. |
| Magic link (Stripe/CHIP)? | Metadata keeps `dunning_campaign_id`. Handler increments. **Unproven by tests.** |
| Magic link (Billplz / FPX)? | Billplz **drops** `dunning_campaign_id`. Handler **falls back** to `CurrentDunningCampaignId`. Works only if a campaign is already assigned. **Unproven by tests.** |
| Off-session AUTO_CHARGE? | Stripe/CHIP/Razorpay stamp `dunning_campaign_id` on the PI/purchase. Handler increments. Billing attempt 1 is `DunningCampaignId: null` on purpose (ACTIVE renewal, not recovery). **Unproven by tests.** |
| Ops Log Payment? | Recovers the sub. **Does not** call `RecordRecovery`. Adjacent hole (DN-021). |
| Commerce stats? | **No recovered field.** `GET /admin/commerce/stats` has MRR, past-due count, GMV, not recovered `$`. |
| Dashboard? | **No.** Home shows Net Cash / Active / Past Due / Cancellation Rate. Recovered `$` is **only** on the dunning campaign list, hardcoded `RM`. |
| Process metric? | `LazuarMetrics` has `dunning_cancels`, **not** recovered `$`. Health `dunning_cancels_since_start` is unrelated. |
| New table / event log? | **No for LP-077.** Lifetime counters already exist. Monthly / rate is MD-011 / DN-028. |
| Migration? | **No** if we only test + increment Log Payment + SUM existing columns. **Yes** only if we invent an event table (reject). |

**Honest remaining work:** prove the two named write paths, close the one missed writer (Log Payment), roll the existing lifetime counters into stats + one dashboard KPI. Do not build Retain.

After implement, tracker stays **P** (no monthly recovered, no recovery rate, RM hardcode leftover). Do **not** flip to **Y**.

---

## 2. What exists (read, not assumed)

### 2.1 Domain counters — increment-only, no identity

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs`

| Field | Type | Persistence |
|-------|------|-------------|
| `RecoveredRevenue` | `decimal` | `commerce.DunningCampaigns.RecoveredRevenue` precision **(18, 4)** |
| `SavedSubscriptions` | `int` | same table |
| `ChurnedSubscriptions` | `int` | same table |

```89:94:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs
    public void RecordRecovery(decimal amount)
    {
        RecoveredRevenue += amount;
        SavedSubscriptions++;
        UpdatedAt = DateTime.UtcNow;
    }
```

- No guard on `amount <= 0`. Caller must not call for comps.  
- No subscription id, no timestamp, no channel, no step.  
- `SavedSubscriptions++` on every call (saves, not unique subs).  
- `RecordChurn()` only on grace **CANCEL** (`DunningEngineJob.PastDue.cs`). **SUSPEND** does not increment churned (DN-008 / DN-021). Not LP-077.  
- Counters are **campaign-lifetime**, never reset, never time-bounded.  
- Query DTO casts to `float64` (`CommerceQueryService.Dunning.cs` line 93). UI-grade, not accounting-grade.

### 2.2 `RecoverFromPayment` — state, not metrics

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs`

| Method | Status | Dates | Dunning |
|--------|--------|-------|---------|
| `Activate` | → ACTIVE | **Does not** advance if already PAST_DUE / SUSPENDED | Does **not** clear |
| `RecoverFromPayment` | → ACTIVE | Always sets `CurrentPeriodEnd` + `NextBillingDate` | `ClearDunning()` |
| `Resume` | → ACTIVE | Sets `NextBillingDate` only (not period end) | `ClearDunning()` |

`RecoverFromPayment` is the PAST_DUE money path. `Resume` is the SUSPENDED money path. Neither touches `DunningCampaign`. Campaign id must be **captured before** either call.

`ClearDunning` nulls `CurrentDunningCampaignId`, step index, last offset, pause. The handler already captures id first.

Anniversary reset (next bill = now + 1 mo/yr) is **intentional today**. DN-026 (keep anniversary) is out of scope.

Covered by `SubscriptionRecoveryTests` (`RecoverFromPayment_FromPastDue_*`, `RecoverFromPayment_ClearsDunningAndRecoversMetricsPathReady`). The second test name is honest: it only proves **clear**. Comment says `RecordRecovery` is tested separately (domain-only).

### 2.3 The one writer — `GatewayPaymentCompleted` subscription path

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs`

Fork:

1. Resolve correlation id: `metadata.subscription_id` else `metadata.receipt` (`.Helpers.cs`).  
2. If that id is an **OPEN** Commerce `CheckoutSession` → `HandleOpenCheckoutSessionAsync` (**new** sub / order). **No** `RecordRecovery`.  
3. Else → `HandleSubscriptionPaymentAsync` (renewal / recovery).

Update-payment does **not** create a Commerce `CheckoutSession`. Correlation id is the real `Subscription.Id`. Recovery goes through the subscription path. **Do not** later route magic-link through `MergeClientIntoGateway` (that overwrites `subscription_id` with the checkout session id) or through `HandleOpenCheckoutSession` (that would **create a second subscription**).

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs`

```37:90:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs
        var wasInArrears = existingSub.Status is "PAST_DUE" or "SUSPENDED";
        // ...
        // Capture campaign id before ClearDunning (Resume / RecoverFromPayment).
        Guid? recoveryCampaignId = null;
        if (wasInArrears)
        {
            if (@event.Metadata.TryGetValue("dunning_campaign_id", out var dunningCampaignIdStr)
                && Guid.TryParse(dunningCampaignIdStr, out var fromMetadata))
            {
                recoveryCampaignId = fromMetadata;
            }
            else
            {
                recoveryCampaignId = existingSub.CurrentDunningCampaignId;
            }
        }
        // PAST_DUE → RecoverFromPayment; SUSPENDED → Resume; else Activate
        if (wasInArrears && recoveryCampaignId.HasValue)
        {
            var campaign = await _dbContext.DunningCampaigns
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == recoveryCampaignId.Value && c.OrganizationId == @event.OrganizationId);
            if (campaign != null)
            {
                campaign.RecordRecovery(@event.AmountPaid);
            }
        }
```

Amount = `@event.AmountPaid` (gross paid), **not** catalog price, **not** net of fees, **not** MRR. Same currency bucket regardless of `product.Currency`.

Also: mark `ChargeAttemptLog` succeeded, vault tokens if present, log `CommerceTransactionLog` as `SYSTEM`, publish `SubscriptionActivated` / `SubscriptionResumed`. Metrics and state share one `SaveChanges`.

Replay safety: second `PAYMENT_COMPLETED` after save sees `ACTIVE` → `wasInArrears` false → **no second increment**. Concurrent double-delivery before commit is an LP-090 race, not LP-077.

### 2.4 Magic link (update-payment)

| Layer | Path | What it stamps |
|-------|------|----------------|
| Email | `FulfillmentRequestedIntegrationEventHandler` | `{{update_payment_link}}` → `/{slug}/update-payment/{subId}` |
| Portal | `apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` | POST update-payment if status PAST_DUE / SUSPENDED |
| API | `PublicArrearsEndpoints.cs` | Gateway checkout **only**. No Commerce session. |

Metadata posted to the gateway:

```76:86:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs
            var metadata = new Dictionary<string, string>
            {
                { "type", "commerce_subscription" },
                { "subscription_id", subId.ToString() },
                { "tenant_id", sub.OrganizationId.ToString() }
            };
            if (sub.CurrentDunningCampaignId != null)
            {
                metadata["dunning_campaign_id"] = sub.CurrentDunningCampaignId.ToString()!;
            }
```

If the engine has not assigned a campaign yet (BillingEngine just flipped PAST_DUE, buyer pays from a non-dunning link), **there is no campaign id**. Sub still recovers. `$` is not attributed. That is the only “always attributes” miss on the named paths.

### 2.5 Off-session

| Site | `DunningCampaignId` | Meaning |
|------|---------------------|---------|
| `BillingEngineJob` attempt 1 | `null` | Silent **renewal** while still ACTIVE. Success must **not** count as recovered. |
| `DunningEngineJob.PastDue` AUTO_CHARGE | `campaign.Id` | Retry while PAST_DUE. Success **should** count. |

Payments `ExecuteOffSessionChargeIntegrationEventHandler` passes the id into `ChargeOffSessionAsync`. Adapters that can silent-debit stamp it:

| Adapter | Success metadata |
|---------|------------------|
| Stripe | PI metadata: `type`, `subscription_id=receipt`, `tenant_id`, `dunning_campaign_id` |
| CHIP | purchase.metadata same keys; webhook `purchase.paid` echoes metadata |
| Razorpay | order `notes` same keys (off-session still stubby contact) |
| Billplz | **throws / not supported** — no silent recovery |

Failed off-session publishes `GatewayPaymentFailed` **with** `dunning_campaign_id` (tests exist). Success does **not** publish `GatewayPaymentCompleted` from the adapter — Commerce waits for the gateway webhook. If the webhook never arrives, the sub stays PAST_DUE and counters stay put. That is LP-090, not LP-077.

### 2.6 Metadata by rail (why Billplz needs the fallback)

| Rail | Checkout / PI keeps `dunning_campaign_id`? | Webhook reconstructs it? |
|------|--------------------------------------------|--------------------------|
| Stripe session + PI | Yes (`Session.Metadata` + `PaymentIntentData.Metadata`) | Yes |
| CHIP purchase.metadata | Yes | Yes (`purchase.metadata`) |
| Billplz | **No.** Only `reference_1` = `subscription_id`, `reference_2` = `type` | Reconstructs `type` + `subscription_id` only |

`ProcessGatewayWebhookCommandHandler.Metadata` merges **Payments** `IntegrationCheckoutSession` keys. Update-payment does **not** create that row (`GenerateCheckoutSessionQuery` → cashier → adapter only). Billplz cannot recover `dunning_campaign_id` from merge.

The subscription-handler fallback to `CurrentDunningCampaignId` **is** the Billplz / stripped-metadata path. It is the LP-077 correctness hinge for Malaysian FPX.

Stale gap text (`docs/001-gaps/01-dunning-engine.md` § `GatewayPaymentCompleted`): “metrics only if metadata has `dunning_campaign_id`” and “does not handle receipt-only PI” — **both outdated**. Code has the fallback and `receipt` correlation.

Also stale: `docs/001-gaps/19-frontend-backend-integration.md` line 236 (“Engine job drives recovery metrics”). The engine drives **churn**. Recovery `$` is the **completed-payment handler**.

### 2.7 Ops Log Payment — recovers, does not attribute

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs`

Already computes `wasInArrears`, calls `RecoverFromPayment` / `Resume` / `Activate`+`ClearDunning`, writes a `CONFIRMED` `CommerceTransactionLog`. **Never** loads a campaign. `ICommerceRepository.GetDunningCampaignByIdAsync(org, id)` already exists.

`CommerceProductCompletenessTests.RecordSubscriberPayment_FromPastDue_RecoversAndLogsManualTx` asserts ACTIVE + campaign id cleared + log. **Does not** assert counters.

UI: Subscribers panel **Log Payment** (`SubscribersPage.tsx`). This is the offline / bank-transfer / FPX-manual path. DN-021 called it Wave 2 honesty; it is ~6 lines next to the capture-before-clear the gateway handler already does. Include it so the two recovery implementations do not diverge.

Skip `RecordRecovery` when `amount <= 0` or method `COMPED`.

### 2.8 Commerce stats — no recovered field

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/models/stats.tsp`  
`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Stats.cs`  
`GET /admin/commerce/stats` (`StatsEndpoints.cs`)

`CommerceStatsDto` today:

- `mrr`, `active_subscribers`, `past_due_subscribers`, `cancelled_subscribers`
- `net_new_last_30_days`, `churn_rate_percentage`, `average_revenue_per_user`
- `total_revenue_collected`, `cash_flow_trend[]`, `payment_methods[]`

Computed from **all** non-pending subscriptions + **all** `TransactionLogs`. No join to `DunningCampaigns`. Confirmed GMV includes recovery payments (they are `CONFIRMED` logs) but they are **not** labeled recovered.

Integration test `CommerceQueryServiceTests.DapperQueries_ShouldMatchEntityFrameworkSchema` only asserts `GetStatsAsync` does not throw.

### 2.9 Dashboard / ops UI

| Surface | Recovered `$` |
|---------|----------------|
| `apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx` | **None.** KPIs: Net Cash in Bank (billing), Active, Past Due, Cancellation Rate. |
| `DunningCampaignsPage.tsx` | Column `RM {campaign.recovered_revenue.toFixed(2)}` + saved/churned. **Lifetime.** Currency **hardcoded RM**. |
| Campaign builder / subscriber panel | No recovered `$`. |
| `lazuar-admin` / portal | None. |

MD-011 (monthly recovered) is Wave 2 in the dashboard report. LP-077 Wave 0 is “the number we already store is honest and visible,” not a Retain time series.

### 2.10 Tests today vs the Phase A residual

| File | What it proves | LP-077 hole |
|------|----------------|-------------|
| `DunningCampaignDomainTests.RecordRecovery_*` | `+=` amount, `++` saved | Isolated domain |
| `SubscriptionRecoveryTests` | State machine + clear dunning | Explicitly **not** metrics |
| `ExecuteOffSessionChargeIntegrationEventHandlerTests` | Adapter args + **failed** metadata keys | No Commerce increment |
| `CommerceProductCompletenessTests` coupon / Log Payment | Open-checkout path; PAST_DUE log-pay recovers | No `RecordRecovery` |
| `TenantIsolationHardeningTests.GatewayPaymentCompleted_CrossTenant_*` | Wrong org session is no-op | No recovery case |
| **Handler + campaign row** | **Missing** | Phase A A.10 residual: “full host e2e (webhook → Commerce handler → metrics row) is operator/manual” |

There is **no** `GatewayPaymentCompleted` test that seeds a PAST_DUE sub + campaign and asserts `RecoveredRevenue`.

---

## 3. Path matrix

| Recovery path | Exits dunning / advances? | Increments `RecoveredRevenue` today? |
|---------------|---------------------------|--------------------------------------|
| Magic link Stripe/CHIP, campaign assigned | Yes (`RecoverFromPayment` / `Resume`) | **Yes** (metadata) |
| Magic link Billplz, campaign assigned | Yes | **Yes** (fallback to `CurrentDunningCampaignId`) |
| Magic link, **no** campaign assigned yet | Yes | **No** |
| Off-session AUTO_CHARGE (dunning), webhook arrives | Yes | **Yes** (metadata + fallback) |
| Off-session Billing attempt 1 (still ACTIVE) | N/A (renewal) | **No** (correct) |
| Off-session success, webhook never arrives | **No** | **No** (LP-090) |
| Ops Log Payment / `RecordSubscriberPayment` | Yes | **No** |
| Comped / `amount = 0` | Yes | **No** (and must stay no) |
| Campaign hard-deleted / other org id | Yes | **No** (lookup fails, no throw) |
| Second webhook after already ACTIVE | No-op on dates | **No** (correct) |
| New subscribe (`HandleOpenCheckoutSession`) | N/A | **No** (correct) |

---

## 4. Gaps (severity for Wave 0)

| # | Gap | Severity | In LP-077? |
|---|-----|----------|------------|
| G1 | No handler test that magic-link / off-session metadata increments the campaign | **P0** — Phase A residual; cannot call the loop closed | **Yes** |
| G2 | No handler test that Billplz-shaped metadata (no `dunning_campaign_id`) still increments via fallback | **P0** — Malaysian path | **Yes** |
| G3 | Log Payment recovers without `RecordRecovery` | **P1** — 6 lines; same helper | **Yes** (adjacent, do it) |
| G4 | Stats + dashboard hide the only recovered number we persist | **P1** — Wave 0 honesty of numbers we already show | **Yes** (lifetime SUM only) |
| G5 | PAST_DUE with **no** assigned campaign pays → `$` unattributed | **P2** — rare if the link came from dunning email | Document; do **not** invent an unassigned bucket |
| G6 | `RM` hardcode on campaign list | P2 | **No** (DN-021 leftover) |
| G7 | `float64` DTO / mixed-currency one bucket | P2 | **No** |
| G8 | No monthly / rate / at-risk / by-step | — | **No** (MD-011, DN-028) |
| G9 | `RecordChurn` skips SUSPEND | — | **No** (DN-008) |
| G10 | `LazuarMetrics` has no recovery counter | — | **No** (optional, not merchant) |
| G11 | Docs 01 / 19 stale on who increments | Hygiene | Fix only if already editing those files |

**Do not** treat G5 as “always” by auto-assigning a campaign at pay time. Assign stays on fail-handler / engine (LP-079 snapshot owns that moment).

---

## 5. Options

### A — Prove + close writers + lifetime roll-up (choose this)

No schema change.

1. Add handler tests (G1, G2, replay, ACTIVE renewal, missing campaign).  
2. In `RecordSubscriberPaymentCommandHandler`, capture `CurrentDunningCampaignId` **before** recover; if `wasInArrears && amount > 0` load campaign via existing repository method and `RecordRecovery(amount)`.  
3. Add `recovered_revenue` (+ optional `saved_subscriptions`) to `CommerceStatsDto` as `SUM` of `commerce.DunningCampaigns` for the org.  
4. Dashboard: one KPI **Recovered (lifetime)** using that field. Label must say lifetime. Link or footnote to Dunning Campaigns. Do **not** replace Net Cash. Do **not** imply “this month.”

Optional 5-line extract: a private/static helper used by the gateway handler and Log Payment so capture-before-clear cannot drift. Not a new domain service.

### B — Recovery event table (reject for LP-077)

New `DunningRecovery` row per save (sub, campaign, amount, currency, channel, at). Enables monthly, rate, which-sub. That is MD-011 / DN-028 / option B of LP-079. Wave 0 does not need it.

### C — Dashboard-only / stats-only without write-path tests (reject)

Would paint a number we have not proven on the two sold paths. Opposite of Wave 0.

### D — Flip tracker to Y because DN-003 says shipped (reject)

DN-003 is the handler fallback. Tracker LP-077 is **P** because stats/dashboard/Log Payment/tests are incomplete. Leave **P**.

---

## 6. Minimal change (option A)

### 6.1 Log Payment writer

`RecordSubscriberPaymentCommandHandler` — after computing `wasInArrears`, **before** `Resume` / `RecoverFromPayment`:

```csharp
var recoveryCampaignId = wasInArrears ? subscription.CurrentDunningCampaignId : null;
// ... recover ...
if (recoveryCampaignId is Guid campaignId && amount > 0)
{
    var campaign = await _repository.GetDunningCampaignByIdAsync(request.OrganizationId, campaignId, ct);
    campaign?.RecordRecovery(amount);
}
```

Do not call `RecordRecovery(0)` for COMPED. Do not load campaign on ACTIVE renewal (even if a stale id existed — `Activate`+`ClearDunning` is not a dunning save).

Gateway handler stays as-is except optional helper extract. **Do not** change `RecoverFromPayment` semantics.

### 6.2 TypeSpec / stats

`packages/api-spec/modules/commerce/models/stats.tsp` — add:

```tsp
recovered_revenue: float64;        // SUM(DunningCampaigns.RecoveredRevenue), lifetime
saved_subscriptions: int32;        // SUM(SavedSubscriptions), lifetime
```

Keep `float64` to match `DunningCampaignDto.recovered_revenue`. Do not invent `decimal` only here.

`CommerceQueryService.Stats.cs` — extra Dapper, **do not** load every campaign into memory:

```sql
SELECT COALESCE(SUM("RecoveredRevenue"), 0), COALESCE(SUM("SavedSubscriptions"), 0)
FROM commerce."DunningCampaigns"
WHERE "OrganizationId" = @OrgId
```

Then `task gen:spec` + `task gen:types-ts` + `task gen:types-dotnet` (existing Taskfile). No honesty-allowlist change (same `GET /admin/commerce/stats`).

### 6.3 Dashboard

`DashboardPage.tsx`:

- Add a KPI: label **Recovered (lifetime)**, value `formatMYR(stats?.recovered_revenue || 0)`.  
- Grid can wrap to 5 cards; do not drop Past Due.  
- Do **not** compute last-30 from `UpdatedAt` on campaigns (that is last mutation, not recovery time).  
- Campaign list `RM` hardcode: leave (DN-021). If one-line, use the same `formatMYR` helper — optional, not DoD.

### 6.4 What not to build

- `DunningRecovery` / run table / monthly series / recovery rate / at-risk MRR.  
- `LazuarMetrics.RecordDunningRecovery` (optional; skip).  
- Changing `Activate` vs `RecoverFromPayment`.  
- Routing update-payment through Commerce checkout sessions.  
- Auto-assign campaign at pay time.  
- Currency i18n / mixed-currency split.  
- Per-sub audit, by-step attribution, ChargeAttemptLog as a third campaign-id fallback (capture-before-clear is enough).  
- TypeSpec change to `DunningCampaignDto` (already has the fields).

---

## 7. Tests (required to call LP-077 done)

New fixture: `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GatewayPaymentCompletedRecoveryMetricsTests.cs`

Copy `CreateDb` / in-memory `CommerceDbContext` / substitute repository `SaveChanges` → `db.SaveChangesAsync` from `CommerceProductCompletenessTests`. Seed: org, monthly product, campaign (`RecoveredRevenue = 0`), PAST_DUE sub assigned to that campaign.

### 7.1 Handler — magic link + off-session (acceptance)

| # | Event shape | Assert |
|---|-------------|--------|
| H1 | PAST_DUE + metadata `{ type, subscription_id, tenant_id, dunning_campaign_id }` + `AmountPaid = 49.90` | Status ACTIVE; dunning cleared; campaign `RecoveredRevenue == 49.90`; `SavedSubscriptions == 1`; `SubscriptionActivated` published |
| H2 | Same as H1 **without** `dunning_campaign_id` (Billplz reconstruct) | Same increment (fallback) |
| H3 | Off-session shape: `subscription_id` + `receipt` + `dunning_campaign_id` (no checkout session) | Same increment |
| H4 | Receipt-only correlation (`receipt` = sub id, no `subscription_id`) | Still recovers + increments |
| H5 | Replay H1 after save (now ACTIVE, same metadata) | Counters **unchanged** |
| H6 | ACTIVE vaulted renewal (`wasInArrears` false), metadata may even include a campaign id | Dates advance via `Activate`; counters **0** |
| H7 | SUSPENDED + campaign | `Resume`; increment; `SubscriptionResumed` |
| H8 | PAST_DUE, campaign id missing **and** `CurrentDunningCampaignId` null | ACTIVE; counters **0**; no throw |
| H9 | Metadata campaign id for **other org** | ACTIVE; **this** org campaign unchanged |
| H10 | Metadata campaign id, row deleted | ACTIVE; no throw |
| H11 | OPEN checkout session id in `subscription_id` (new subscribe) | New sub; campaign counters **0** |

H1 is magic-link Stripe/CHIP. H2 is magic-link Billplz. H3 is dunning AUTO_CHARGE. That is the ticket sentence.

### 7.2 Log Payment — extend `CommerceProductCompletenessTests`

| Test | Assert |
|------|--------|
| PAST_DUE + assigned campaign + amount 100 | Campaign `RecoveredRevenue == 100`, saved 1; id cleared |
| COMPED / amount 0 | Recovered; counters **0** |
| ACTIVE record-payment (not arrears) | Counters **0** |

Need the campaign **tracked** on the same repository/context the handler loads. Today’s test uses a substitute repository: stub `GetDunningCampaignByIdAsync` to return a real `DunningCampaign` instance and assert on it.

### 7.3 Domain — already sufficient

Keep `DunningCampaignDomainTests.RecordRecovery_IncrementsRevenueAndSavedCount`. No domain change.

### 7.4 Stats

- Extend `CommerceQueryServiceTests` (Postgres): insert two campaigns (100 + 20.5 recovered, 1 + 2 saved) → `GetStatsAsync` returns `recovered_revenue == 120.5`, `saved_subscriptions == 3`. Empty org → 0.  
- Schema smoke already calls `GetStatsAsync`; new columns must not break Dapper.

Do **not** add a live Stripe e2e. Phase A already left that as operator residual.

---

## 8. Touch list (when a later program implements)

| Area | Path | Change |
|------|------|--------|
| Writer | `RecordSubscriberPaymentCommandHandler.cs` | Capture id; `RecordRecovery` if arrears and amount > 0 |
| Optional | small shared helper next to the completed handler | One capture/record implementation |
| TypeSpec | `packages/api-spec/modules/commerce/models/stats.tsp` | Two fields |
| Generated | `packages/api-types-ts`, `packages/api-types-dotnet` | `task gen:*` |
| Query | `CommerceQueryService.Stats.cs` | SUM query |
| Ops | `DashboardPage.tsx` | Lifetime KPI |
| Tests | new handler fixture + Log Payment + stats SQL | §7 |
| Leave alone | `Subscription.RecoverFromPayment`, `DunningCampaign.RecordRecovery` body, campaign list DTO, engine job, adapters (except if a helper only), LP-079 snapshot, `LazuarMetrics`, TypeSpec dunning model | |

---

## 9. Done when

- H1–H5 and H8 green: magic-link (full metadata **and** Billplz-stripped) and off-session PAST_DUE pay increment **once**, then replay does not.  
- ACTIVE renewal does not increment (H6).  
- Log Payment on PAST_DUE increments; COMPED does not.  
- `GET /admin/commerce/stats` returns org-lifetime `recovered_revenue` / `saved_subscriptions` matching `SUM` of campaign rows.  
- Commerce dashboard shows **Recovered (lifetime)** from that field.  
- No new table. No monthly series.

**Still P after this ticket (honest):** no monthly recovered MRR, no recovery rate, no at-risk MRR, no per-sub audit, campaign list still says `RM`, `RecordChurn` still ignores SUSPEND, unassigned PAST_DUE pays still unattributed.

**Do not flip LP-077 to Y.** MD-011 / DN-028 remain the later dashboard job.

---

## 10. Sequencing vs neighbors

| ID | Relationship |
|----|----------------|
| LP-071 / LP-072 / LP-073 / LP-075 | Entry + retry + email + magic link. LP-077 **reads** their success webhook. Do not block on them. |
| LP-078 | Terminal cancel/suspend. Churn counter, not recovered `$`. |
| LP-079 | Snapshot at assign. Recovery still increments the **live** campaign row (same as today). Do not put recovered `$` on snapshot JSON. |
| LP-090 | Webhook idempotency / missing success webhook. Replay test H5 is the overlap; do not rebuild inbox here. |
| LP-161 | Ledger MRR. Recovered `$` is **cash collected while in arrears**, not recovered MRR. |

Implement LP-077 **after or in parallel** with LP-071/072 (need PAST_DUE + off-session to exist). It does not depend on LP-079.
