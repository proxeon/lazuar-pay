# 04 — PAST_DUE reminder path and NSubstitute `Arg<Guid>()`

**HEAD:** `fix/180-unify-outbox-inbox` (`4531f210` — `fix(api): register every module outbox and inbox through one helper`)

**Scope of this file:** two ModuleTests clusters that fail on current HEAD. They share a CRM surface (`ICrmQueryService.GetClientProfileAsync`) but they fail for **different reasons**. Cluster A is a test-only NSubstitute `CallInfo.Arg<Guid>()` leftover from issue **165**. Cluster B is a test fixture that still expects the pre-**168** fail-open reminder path (`MarkAsPastDue` with no checkout URL).

**This document does not implement a product fix.** Product commits `42b7ad37` (165) and `f75842db` (168) stay. The recommended work is test-only.

**Live verification on this HEAD** (same two fixtures, `--filter` covering both classes):

```
Failed!  - Failed: 17, Passed: 30, Skipped: 0, Total: 47
```

- `GatewayPaymentFailedIntegrationEventHandlerTests`: **11 failed / 5 passed / 16 total**
- `BillingEngineJobTests`: **6 failed / 25 passed** (the remaining tests in that class)

Exact exception / assertion text is quoted per test below.

---

# Cluster A — `GatewayPaymentFailedIntegrationEventHandlerTests`

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs`

**Production handler:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs`

**Shared dunning CRM read:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs`

**Contract after 165:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/Contracts/ICrmQueryService.cs`

**Issue:** [165 — B10-X09](../../issues/165-p1-b10-x09-crmqueryservice-getclientprofileasync-is-a-global-pii-read-by-gu.md) (`CrmQueryService.GetClientProfileAsync` is a global PII read by GUID). Resolved on `fix/165-crm-profile-org-scope`, landed as `42b7ad37` (`fix(crm): require organization id on profile-by-guid reads`).

**Cluster error (every assigned test):**

```
NSubstitute.Exceptions.AmbiguousArgumentsException : There is more than one argument of type System.Guid to this call.
The call signature is (Guid, Guid)
  and was called with (Guid, Guid)
```

Throw site in the fixture (always):

```
at Lazuar.ModuleTests.Commerce.GatewayPaymentFailedIntegrationEventHandlerTests.<>c.<SetUp>b__5_0(CallInfo ci)
    ...GatewayPaymentFailedIntegrationEventHandlerTests.cs:line 49
```

Production call sites that trigger the stub (split, see per-test table):

- `GatewayPaymentFailedIntegrationEventHandler.PublishPastDueAsync` line 138
- `DunningStepDispatcher.DispatchCommunicationStepAsync` line 72

Assertions never run. `HandleAsync` throws out of the NSubstitute return callback before `SaveChangesAsync`.

---

## A.1 Assigned tests

These 11 tests fail. They are the tests that actually invoke `GetClientProfileAsync(Guid, Guid)` after a first-time transition to `PAST_DUE` (or, for the comms tests, during day-0 / catch-up dispatch that happens in the same `HandleAsync` as that transition).

| # | Test | First production CRM call | Test `HandleAsync` line |
|---|------|---------------------------|-------------------------|
| 1 | `HandleAsync_ActiveSubscription_MarksPastDue_AndAssignsMatchingCampaign` | `PublishPastDueAsync` :138 | :100 |
| 2 | `HandleAsync_AlreadyAssigned_LiveCampaignEditDoesNotRewriteSnapshot` | `DunningStepDispatcher` :72 | :522 (first fail) |
| 3 | `HandleAsync_DoesNotPublishDispatchMessage` | `PublishPastDueAsync` :138 | :347 |
| 4 | `HandleAsync_FirstFail_DispatchesDay0Email_DoesNotOffSession` | `DunningStepDispatcher` :72 | :369 |
| 5 | `HandleAsync_MarksPendingChargeAttemptFailed_ByChargeAttemptId` | `PublishPastDueAsync` :138 | :187 |
| 6 | `HandleAsync_NoMatchingCampaign_MarksPastDueWithoutComms` | `PublishPastDueAsync` :138 | :493 |
| 7 | `HandleAsync_Paused_AssignsButDoesNotDispatch` | `PublishPastDueAsync` :138 | :465 |
| 8 | `HandleAsync_PrefersHigherPriorityCampaignForOrg` | `PublishPastDueAsync` :138 | :307 |
| 9 | `HandleAsync_ResolvesSubscriptionIdFromReceiptFallback` | `PublishPastDueAsync` :138 | :328 |
| 10 | `HandleAsync_SecondFail_DoesNotDoubleDispatchDay0` | `DunningStepDispatcher` :72 | :408 (first fail) |
| 11 | `HandleAsync_ThreeDaysOverdue_CatchUpDispatchesOffset0And3` | `DunningStepDispatcher` :72 | :566 |

These 5 tests in the same fixture **pass** because they never call `GetClientProfileAsync`:

| Passing test | Why CRM is not called |
|--------------|------------------------|
| `HandleAsync_UpdatePaymentDecline_KeepsActive_DoesNotAssignCampaign` | `IsUpdatePayment` is true; handler returns at lines 84–91 after marking nothing PAST_DUE. |
| `HandleAsync_CanceledSubscription_SkipsPastDueButCanFailAttempt` | Status is `CANCELED`; handler returns at lines 94–101. Charge attempt is marked failed without a CRM read. |
| `HandleAsync_MissingSubscriptionId_IsNoOp` | `TryResolveSubscriptionId` fails; handler returns at lines 51–56. |
| `HandleAsync_AlreadyPastDue_DoesNotReassignWhenCampaignPresent` | Already `PAST_DUE`, so `becamePastDue` is false and `PublishPastDueAsync` is skipped. Assigned campaign id is a bare GUID with **no row** in `DunningCampaigns`; `ResolveSnapshotAsync` returns null at `PastDueDunningProcessor.cs` :337–342; `ProcessAsync` returns without dispatch. |
| `HandleAsync_AlreadyPastDueWithDay0Logged_DoesNotRedispatch` | Already `PAST_DUE` + day-0 already in `ReminderLogs`; `dueSteps` is empty; no dispatcher CRM read; no `PublishPastDueAsync`. |

The 11 assigned failures are therefore **not** a dunning-logic regression and **not** a PAST_DUE domain regression. They are a single SetUp callback that cannot resolve `ci.Arg<Guid>()` against a two-`Guid` method.

---

## A.2 Exact SetUp / mock code with line numbers

Current fixture SetUp, including the broken CRM stub:

```35:61:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs
[TestFixture]
public class GatewayPaymentFailedIntegrationEventHandlerTests
{
    private CommerceDbContext _db = null!;
    private GatewayPaymentFailedIntegrationEventHandler _handler = null!;
    private IEventBus _eventBus = null!;
    private Guid _orgId;
    private Guid _productId;

    [SetUp]
    public void SetUp()
    {
        _orgId = Guid.CreateVersion7();
        _productId = Guid.CreateVersion7();

        _db = new CommerceDbContext(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        _eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(ci => new ClientProfileDto
        {
            Id = ci.Arg<Guid>().ToString(),
            Full_name = "Buyer",
            Email = "buyer@example.com"
        });
        _handler = new GatewayPaymentFailedIntegrationEventHandler(
            _db,
            _eventBus,
            crm,
            Substitute.For<ILogger<GatewayPaymentFailedIntegrationEventHandler>>(),
            new ConfigurationBuilder().AddInMemoryCollection().Build());
    }
```

What this stub is trying to do:

- Match **any** org + **any** profile id (the two-argument form required by 165).
- Build a `ClientProfileDto` whose `Id` is “the Guid that was passed in.”
- Always return a buyer email so `DunningStepDispatcher` treats the comms step as sendable (`profile != null` and email not blank).

The `Email = "buyer@example.com"` part is fine. The `Id = ci.Arg<Guid>().ToString()` part is not.

`CallInfo.Arg<T>()` (NSubstitute) means “the single argument of type `T`.” After 165 the call is:

```
GetClientProfileAsync(Guid organizationId, Guid profileId)
```

Two arguments, both `System.Guid`. `Arg<Guid>()` therefore throws `AmbiguousArgumentsException` **inside the return callback**, every time production actually invokes the method.

The DTO `Id` is never asserted by any test in this fixture. The only field the production path cares about here is `Email`:

- `PublishPastDueAsync` passes `profile?.Email` into `CommerceWebhookPayload.From` (`GatewayPaymentFailedIntegrationEventHandler.cs` :138–142). None of the assigned tests assert `customer_email` on the webhook payload.
- `DunningStepDispatcher.DispatchCommunicationStepAsync` uses the profile only as a presence/email gate (`DunningStepDispatcher.cs` :70–76). On success it publishes `reminder.dunning`; it does not put `profile.Id` on that payload (`client_profile_id` comes from `sub.ClientProfileId`).

So the callback’s `Id = ci.Arg<Guid>()` mapping is leftover convenience from the one-argument era. It is not required for these tests to pass.

Handler construction (same SetUp) injects the stub as the required `ICrmQueryService`. `IBillingQueryService` is left at the optional constructor default `null`. That is unrelated to this failure.

---

## A.3 Production change (165 — `ICrmQueryService`)

### A.3.1 What 165 changed

Issue 165 (`B10-X09`) is a tenancy / PII leak. Before the fix, CRM profile-by-id reads were global:

```csharp
// pre-42b7ad37
Task<ClientProfileDto?> GetClientProfileAsync(Guid profileId);
Task<IEnumerable<ClientProfileDto>> GetClientProfilesAsync(IEnumerable<Guid> profileIds);
```

Implementation (quoted from the issue file, which still shows the pre-fix snippet):

```54:62:apps/lazuar-api/Modules/CRM/Infrastructure/CrmQueryService.cs
    public async Task<ClientProfileDto?> GetClientProfileAsync(Guid profileId)
    {
        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == profileId);
```

Any in-process caller with a guessed profile GUID could read another tenant’s name, email, phone, TIN, company, id numbers, and address. `GetClientProfileByEmailAsync` already took `organizationId`. The id-based overloads did not.

Commit `42b7ad37` (`fix(crm): require organization id on profile-by-guid reads`) changed the contract to:

```8:13:apps/lazuar-api/Modules/CRM/Contracts/ICrmQueryService.cs
public interface ICrmQueryService
{
    Task<ClientProfileDto?> GetClientProfileAsync(Guid organizationId, Guid profileId);
    Task<IEnumerable<ClientProfileDto>> GetClientProfilesAsync(Guid organizationId, IEnumerable<Guid> profileIds);
    Task<ClientProfileDto?> GetClientProfileByEmailAsync(Guid organizationId, string email);
}
```

Implementation now filters both columns:

```54:62:apps/lazuar-api/Modules/CRM/Infrastructure/CrmQueryService.cs
    public async Task<ClientProfileDto?> GetClientProfileAsync(Guid organizationId, Guid profileId)
    {
        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Id == profileId);

        return profile == null ? null : MapToDto(profile);
    }
```

The same commit updated every production call site, including the two that this fixture exercises:

`GatewayPaymentFailedIntegrationEventHandler.PublishPastDueAsync` — `42b7ad37` changed:

```csharp
- var profile = await _crmQueryService.GetClientProfileAsync(sub.ClientProfileId);
+ var profile = await _crmQueryService.GetClientProfileAsync(sub.OrganizationId, sub.ClientProfileId);
```

Current code:

```133:146:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs
    private async Task PublishPastDueAsync(Domain.Aggregates.Subscription sub)
    {
        var product = await _dbContext.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == sub.ProductId);
        var profile = await _crmQueryService.GetClientProfileAsync(sub.OrganizationId, sub.ClientProfileId);
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
            _billingQueryService, sub.OrganizationId);
        var payload = CommerceWebhookPayload.From(
            sub, product, profile?.Email, "PAST_DUE", merchantHasSst: merchantHasSst);

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            sub.OrganizationId, TargetUrl: null, "subscription.past_due", payload));
    }
```

`DunningStepDispatcher.DispatchCommunicationStepAsync` — same commit:

```csharp
- var profile = await crm.GetClientProfileAsync(sub.ClientProfileId);
+ var profile = await crm.GetClientProfileAsync(sub.OrganizationId, sub.ClientProfileId);
```

Current code:

```59:77:apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs
    public static async Task<bool> DispatchCommunicationStepAsync(
        CommerceDbContext db,
        Subscription sub,
        IDunningStepCopy step,
        int daysOverdue,
        string effectiveActionType,
        IEventBus eventBus,
        CancellationToken ct,
        IBillingQueryService? billing = null,
        ICrmQueryService? crm = null)
    {
        if (crm != null)
        {
            var profile = await crm.GetClientProfileAsync(sub.OrganizationId, sub.ClientProfileId);
            if (profile == null || string.IsNullOrWhiteSpace(profile.Email))
            {
                return false;
            }
        }
```

The handler always passes `_crmQueryService` into `PastDueDunningProcessor.ProcessAsync`:

```113:121:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs
        var campaigns = await PastDueDunningProcessor.LoadActiveCampaignsAsync(_dbContext, CancellationToken.None);
        var whatsAppEnabled = _configuration.GetValue("Messaging:WhatsAppEnabled", false);
        var processor = new PastDueDunningProcessor(_logger);
        await processor.ProcessAsync(
            _dbContext, _eventBus, sub, campaigns, whatsAppEnabled, CancellationToken.None, _billingQueryService, _crmQueryService);

        if (becamePastDue)
        {
            await PublishPastDueAsync(sub);
        }
```

So any first-time PAST_DUE that also has a due EMAIL/WHATSAPP/ALL step hits CRM **twice** (dispatcher, then `PublishPastDueAsync`). The first hit is enough to throw.

`ProcessAsync` only reaches the dispatcher when:

1. A campaign is assigned or already present (`PastDueDunningProcessor.cs` :62–81).
2. Dunning is not paused (`:83–86`).
3. A snapshot can be resolved (`:88–92`).
4. There is a due communication step whose `DayOffset` is in `[0, daysOverdue]` and not already logged (`:95–100`, `:198–234`).

That is why some assigned tests throw from the dispatcher (they have a day-0 EMAIL step due today / overdue) and others throw from `PublishPastDueAsync` (no comms step to run, or dunning paused after assign).

### A.3.2 Incomplete test update in the same 165 commit

`42b7ad37` **did** touch this fixture. The test-file hunk is exactly:

```diff
-        crm.GetClientProfileAsync(Arg.Any<Guid>()).Returns(ci => new ClientProfileDto
+        crm.GetClientProfileAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(ci => new ClientProfileDto
         {
             Id = ci.Arg<Guid>().ToString(),
             Full_name = "Buyer",
             Email = "buyer@example.com"
         });
```

The matcher arity was updated. The `CallInfo.Arg<Guid>()` inside the callback was not. That is the entire Cluster A bug. Production 165 is correct; the test stub is half-migrated.

The same commit updated `BillingEngineJobTests` stubs to two-arg form **without** a `ci.Arg<Guid>()` callback (`Arg.Any<Guid>(), clientId` and `Arg.Any<Guid>(), Arg.Any<Guid>()` returning a concrete DTO). Those stubs do not throw. Cluster B fails for a different reason (168), not 165.

Other ModuleTests that were updated in 165 use `GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto { ... })` — no `CallInfo.Arg<T>()`. This fixture is the only assigned caller that still does.

Repo examples of the correct two-Guid `CallInfo` style (already used elsewhere):

- `ProvisionAuraWorkspaceTests.cs` uses `ci.ArgAt<Guid>(0)` / `ci.ArgAt<Guid>(1)`.
- `ExecuteOffSessionChargeIntegrationEventHandlerTests.cs` uses `ci.ArgAt<Guid>(7)`.

### A.3.3 Handler path the tests actually walk

`HandleAsync` (lines 49–131), in order, for a normal commerce decline:

1. `TryResolveSubscriptionId` — `subscription_id` or `receipt` Guid (`:214–235`). Missing id is a no-op (passing test).
2. Optional relational transaction. In-memory tests skip this (`Database.IsRelational()` is false).
3. `CommerceSubscriptionLock.AcquireAsync`.
4. Load subscription by id **and** `OrganizationId` (`:69–72`).
5. `MarkChargeAttemptFailedAsync` if a pending / named attempt exists (`:82`, `:148–177`).
6. Skip PAST_DUE for `update_payment` (`:84–91`) and for `CANCELED` / `SUSPENDED` (`:94–101`).
7. If status is not already `PAST_DUE`, `sub.MarkAsPastDue()` (`:104–111`). This is **in-memory only** until `SaveChangesAsync` at `:124`.
8. `PastDueDunningProcessor.ProcessAsync` with the CRM stub (`:116–117`).
9. If this call is the first transition, `PublishPastDueAsync` (`:119–121`) — **this is where most assigned tests throw**.
10. `SaveChangesAsync` + commit (`:124–125`). Never reached on the 11 failures.

Because the exception fires before `:124`, the database still has `ACTIVE` after a failed run. That is invisible: NUnit reports the `AmbiguousArgumentsException`, not an assertion on status.

`MarkAsPastDue` itself is a one-liner (`Subscription.cs` :315–319) and is not the defect.

---

## A.4 Per-test expected vs actual

Shared actual for all 11:

- **Actual:** `HandleAsync` throws `NSubstitute.Exceptions.AmbiguousArgumentsException` from SetUp callback line 49 (`ci.Arg<Guid>()`).
- **Exact message:** `There is more than one argument of type System.Guid to this call. The call signature is (Guid, Guid) and was called with (Guid, Guid)`.
- **Assertions:** not reached.
- **DB:** no `SaveChangesAsync`; subscription row stays whatever it was before the call (`ACTIVE` for first-fail tests).

### A.4.1 `HandleAsync_ActiveSubscription_MarksPastDue_AndAssignsMatchingCampaign` (lines 69–117)

**Arrange.** Active vaulted sub + one org campaign (`finalAction: SUSPEND`, `gracePeriodDays: 7`, `priorityOrder: 10`) with **no steps**. Event metadata has `subscription_id`.

**ProcessAsync.** `FindBest` matches the campaign (empty product/method lists match all; `DunningCampaign.Matches` :123–140). Assigns snapshot. `dueSteps` is empty (no steps). Returns.

**CRM call.** `PublishPastDueAsync` :138.

**Expected.** Status `PAST_DUE`; `CurrentDunningCampaignId == campaign.Id`; snapshot `CampaignId` / `GracePeriodDays == 7` / `FinalAction == SUSPEND`; exactly one `OutboundWebhookRequestedIntegrationEvent` with `event_type == subscription.past_due`, `TargetUrl == null`, matching org and `status == PAST_DUE`.

**Actual.** Exception at first CRM read. Campaign is assigned only on the tracked entity; never saved.

### A.4.2 `HandleAsync_AlreadyAssigned_LiveCampaignEditDoesNotRewriteSnapshot` (lines 506–548)

**Arrange.** Vaulted sub due today + `Day0EmailCampaign` plus a day-3 EMAIL step. First `HandleAsync` should freeze the snapshot. Then the live campaign is edited (grace 1, steps replaced with offsets 0 and 1). Second `HandleAsync` must keep the frozen JSON and must **not** catch-up dispatch the new day-1 step.

**ProcessAsync (first call).** Assigns campaign, day 0 is due, dispatcher runs.

**CRM call.** `DunningStepDispatcher` :72 on the first `HandleAsync` (`FailedEvent` at line 522).

**Expected.** After both calls: same `DunningCampaignSnapshotJson` as after the first call; snapshot still has offsets `0, 3` and original grace/final action; reminder logs only offset 0; no offset 1.

**Actual.** Exception on the first call, before `SaveChangesAsync`. The edit / second-call half never runs. This is why a SetUp-only fix unblocks **both** halves — the second call does not need a different stub.

### A.4.3 `HandleAsync_DoesNotPublishDispatchMessage` (lines 338–350)

**Arrange.** Vaulted active sub, **no** campaign.

**ProcessAsync.** `FindBest` returns null; warning; return at `:79`. No comms.

**CRM call.** `PublishPastDueAsync` :138 (first PAST_DUE still publishes the outbound webhook).

**Expected.** `DidNotReceive` `DispatchMessageIntegrationEvent`. The test does **not** assert PAST_DUE status; it only forbids the old messaging event.

**Actual.** Exception in `PublishPastDueAsync` before the `DidNotReceive` assertion. After the SetUp fix this test should pass: `PublishPastDueAsync` publishes `OutboundWebhookRequestedIntegrationEvent`, not `DispatchMessageIntegrationEvent`.

### A.4.4 `HandleAsync_FirstFail_DispatchesDay0Email_DoesNotOffSession` (lines 352–392)

**Arrange.** Product + vaulted sub due **today** + campaign with EMAIL day 0 (`"Please pay"`) and EMAIL day 3.

**ProcessAsync.** Assigns campaign. `daysOverdue == 0`, so only day 0 is due. Dispatcher runs.

**CRM call.** `DunningStepDispatcher` :72 (`HandleAsync` at line 369).

**Expected.** `PAST_DUE`; campaign assigned; `LastCompletedDayOffset == 0`; one reminder log (offset 0, target = today); snapshot has two steps (0, 3) and `Steps[0].EmailBody == "Please pay"`; one `FulfillmentRequestedIntegrationEvent` (`COMMUNICATIONS` / `reminder.dunning`); one `subscription.past_due` webhook; **no** `ExecuteOffSessionChargeIntegrationEvent`.

**Actual.** Exception in the dispatcher, so day 0 is never recorded and `PublishPastDueAsync` never runs.

### A.4.5 `HandleAsync_MarksPendingChargeAttemptFailed_ByChargeAttemptId` (lines 157–217)

**Arrange.** Two pending `ChargeAttemptLog`s (attempt 1 billing, attempt 2 dunning). Event 1 fails attempt 2 by `charge_attempt_id` (soft `card_declined`). Event 2 fails attempt 1 with `decline_code = stolen_card` (hard). Campaign has **no** steps.

**ProcessAsync (first event).** Assigns the empty-step campaign. No dispatcher.

**CRM call.** First event’s `PublishPastDueAsync` :138 (`HandleAsync` at line 187).

**Expected.** Attempt 2 `FAILED` / `failure_reason == charge_declined` / `GatewayName == STRIPE` / `GatewayResponseCode == card_declined` / `DeclineClass == soft` / `CompletedAt` set; attempt 1 still `PENDING` after the first event; after the second event attempt 1 `DeclineClass == hard`; subscription `PAST_DUE`.

**Actual.** Exception on the first event, **before** `SaveChangesAsync`. The attempt-failed mutations are on the tracked entities but never persisted, and the second event never runs. After the SetUp fix, both events complete: the first event’s `becamePastDue` path publishes once; the second event is already PAST_DUE so it only updates the remaining attempt.

### A.4.6 `HandleAsync_NoMatchingCampaign_MarksPastDueWithoutComms` (lines 478–504)

**Arrange.** Vaulted sub + a campaign that belongs to **another** org (with a day-0 EMAIL step). `DunningCampaign.Matches` requires `OrganizationId` (`:125–128`), so `FindBest` returns null.

**ProcessAsync.** No campaign; return at `:79`.

**CRM call.** `PublishPastDueAsync` :138 (line 493).

**Expected.** `PAST_DUE`; `CurrentDunningCampaignId` null; no reminder logs; no `FulfillmentRequestedIntegrationEvent`; one `subscription.past_due` webhook.

**Actual.** Exception in `PublishPastDueAsync`. The “without comms” half is already satisfied by `ProcessAsync` returning early; the test never gets to assert it.

### A.4.7 `HandleAsync_Paused_AssignsButDoesNotDispatch` (lines 450–476)

**Arrange.** Vaulted sub, `PauseDunning(UtcNow.AddDays(2))`, day-0 EMAIL campaign.

**ProcessAsync.** Assigns campaign, then hits `:83–86` (`DunningPausedUntil > now`) and returns **before** dispatcher.

**CRM call.** `PublishPastDueAsync` :138 (line 465). Assign happened; dispatch did not.

**Expected.** `PAST_DUE`; campaign assigned; `ReminderLogs` empty; no fulfillment; one `subscription.past_due` webhook.

**Actual.** Exception in `PublishPastDueAsync`. Pause behaviour is already correct; it is not observed.

### A.4.8 `HandleAsync_PrefersHigherPriorityCampaignForOrg` (lines 292–315)

**Arrange.** Vaulted sub + three campaigns: org low (`priorityOrder: 1`), org high (`50`), other-org (`100`). None have steps. `LoadActiveCampaignsAsync` orders `PriorityOrder` desc, then `CreatedAt` desc (`PastDueDunningProcessor.cs` :43–45). `FindBest` is `FirstOrDefault` over that order (`DunningCampaignMatcher.cs` :20–25). Other-org is skipped by `Matches`. High wins.

**CRM call.** `PublishPastDueAsync` :138 (line 307).

**Expected.** `CurrentDunningCampaignId == high.Id`; snapshot `CampaignId == high.Id`.

**Actual.** Exception after assign, before save/assert.

### A.4.9 `HandleAsync_ResolvesSubscriptionIdFromReceiptFallback` (lines 317–336)

**Arrange.** Vaulted sub + campaign. Event metadata has `receipt = sub.Id` and **no** `subscription_id`. `TryResolveSubscriptionId` accepts `receipt` (`:228–231`).

**CRM call.** `PublishPastDueAsync` :138 (line 328).

**Expected.** `PAST_DUE`; campaign assigned.

**Actual.** Resolution **succeeds**; the test then dies on the CRM stub. This is not a receipt-fallback bug.

### A.4.10 `HandleAsync_SecondFail_DoesNotDoubleDispatchDay0` (lines 394–420)

**Arrange.** Same as first-fail but only the day-0 step. Two `HandleAsync` calls.

**CRM call.** Dispatcher :72 on the **first** call (line 408).

**Expected.** One reminder log at offset 0; `Received(1)` fulfillment; `Received(1)` `subscription.past_due` (second call is already PAST_DUE, so no second webhook).

**Actual.** Exception on the first call. The idempotency assertion never runs.

### A.4.11 `HandleAsync_ThreeDaysOverdue_CatchUpDispatchesOffset0And3` (lines 550–577)

**Arrange.** Vaulted sub with `NextBillingDate = UtcNow.Date.AddDays(-3)` + campaign offsets 0 and 3.

**ProcessAsync.** `daysOverdue == 3`; both steps due; dispatcher called for offset 0 first.

**CRM call.** Dispatcher :72 (line 566).

**Expected.** Reminder log offsets `{0, 3}`; both logs share the original due date; `LastCompletedDayOffset == 3`; `Received(2)` `reminder.dunning`.

**Actual.** Exception on the first dispatcher call. Offset 3 never runs.

---

## A.5 Recommended test-only fix (NSubstitute)

Do **not** revert 165. Do **not** change `ICrmQueryService`, `CrmQueryService`, the failed handler, or `DunningStepDispatcher`.

Fix the SetUp callback in one of these equivalent ways.

### Preferred: `ArgAt<Guid>(1)` (keeps the original “Id is the profile id” intent)

`organizationId` is argument 0, `profileId` is argument 1. Pre-165, `ci.Arg<Guid>()` **was** the profile id. The faithful migration is:

```csharp
crm.GetClientProfileAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(ci => new ClientProfileDto
{
    Id = ci.ArgAt<Guid>(1).ToString(),
    Full_name = "Buyer",
    Email = "buyer@example.com"
});
```

### Also valid: drop the callback entirely

None of these tests read `ClientProfileDto.Id`. A constant DTO is enough and cannot throw:

```csharp
crm.GetClientProfileAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(new ClientProfileDto
{
    Id = Guid.CreateVersion7().ToString(),
    Full_name = "Buyer",
    Email = "buyer@example.com"
});
```

This matches the style already used in `BillingEngineJobTests.ArrangeMint` (lines 984–991).

### Do not use

- `ci.Arg<Guid>()` — this is the current bug.
- `ci.Args<Guid>()` then taking `[0]` if you meant profile id (that would be the org id).
- Re-introducing a one-argument `GetClientProfileAsync(Guid)` overload “for tests.”

After either preferred fix, all 11 assigned tests should pass without changing assertions. The stub already returns a non-empty email, so dispatcher `sent == true` and `PublishPastDueAsync` can build a payload.

Optional hardening (not required to unblock): assert the stub was called with the subscription’s org and client ids:

```csharp
await crm.Received().GetClientProfileAsync(sub.OrganizationId, sub.ClientProfileId);
```

That would need `crm` stored as a field. Skip it unless a later change wants to lock the 165 arity in this fixture.

---

## A.6 Concrete patch sketches

### Sketch A — `ArgAt<Guid>(1)` (recommended)

File: `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs`

```diff
         _eventBus = Substitute.For<IEventBus>();
         var crm = Substitute.For<ICrmQueryService>();
         crm.GetClientProfileAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(ci => new ClientProfileDto
         {
-            Id = ci.Arg<Guid>().ToString(),
+            Id = ci.ArgAt<Guid>(1).ToString(),
             Full_name = "Buyer",
             Email = "buyer@example.com"
         });
```

### Sketch B — constant DTO (also correct)

```diff
         _eventBus = Substitute.For<IEventBus>();
         var crm = Substitute.For<ICrmQueryService>();
-        crm.GetClientProfileAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(ci => new ClientProfileDto
-        {
-            Id = ci.Arg<Guid>().ToString(),
-            Full_name = "Buyer",
-            Email = "buyer@example.com"
-        });
+        crm.GetClientProfileAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(new ClientProfileDto
+        {
+            Id = "buyer",
+            Full_name = "Buyer",
+            Email = "buyer@example.com"
+        });
```

No other hunks. Do not touch `FailedEvent`, `Day0EmailCampaign`, or any `[Test]` body.

### Verify

```bash
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~GatewayPaymentFailedIntegrationEventHandlerTests"
```

Expect 16 passed / 0 failed.

---

## A.7 Files to change later

| File | Change |
|------|--------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs` | SetUp stub only (`ci.Arg<Guid>()` → `ci.ArgAt<Guid>(1)` or constant DTO). |
| **Do not change** `Modules/CRM/Contracts/ICrmQueryService.cs` | 165 is the desired contract. |
| **Do not change** `Modules/CRM/Infrastructure/CrmQueryService.cs` | Org filter stays. |
| **Do not change** `Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | Two-arg CRM read + PAST_DUE publish stay. |
| **Do not change** `Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs` | Two-arg CRM email gate stays. |
| **Do not change** `issues/165-p1-b10-x09-crmqueryservice-getclientprofileasync-is-a-global-pii-read-by-gu.md` | Already `status: resolved`. |

Optional later (out of scope for unblocking): grep remaining `ci.Arg<Guid>()` in ModuleTests. Current hits that are **not** this cluster: `CommerceProductCompletenessTests.cs` (`ci.Arg<Guid>()` against a **one**-Guid method — fine). Two-Guid `ArgAt` usage already exists in One/Payments tests.

---

# Cluster B — `BillingEngineJobTests`

**File:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs`

**Production job:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs`

**Mint helper used by the reminder path:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/RenewalCheckoutIssuer.cs`

**Issue:** [168 — B10-X12](../../issues/168-p1-b10-x12-getservice-crm-one-tokens-config-fail-open-on-money-comms.md) (`GetService` CRM / One / tokens / config fail-open on money comms). Resolved on `fix/168-comms-fail-closed`, landed as `f75842db` (`fix(commerce): do not dispatch or mark PAST_DUE without a recoverable URL`).

**Cluster error (5 of 6 assigned tests):**

```
Expected ...Status to be "PAST_DUE" with a length of 8, but "ACTIVE" has a length of 6, differs near "ACT" (index 0).
```

**Sixth assigned test (`RunOnce_TrialDueAfterAttempt1_MarksPastDue`):**

```
Expected reloaded.Status to be "PAST_DUE", but "TRIALING" differs near "TRI" (index 0).
```

No exception escapes `RunOnceAsync`. The job swallows `InvalidOperationException` from the reminder path, records the id in a local `failedIds` set, and continues the batch. Tests only see the unchanged status.

**Do not revert 168.** The product is fail-closed: no CRM email ⇒ no mint ⇒ no `MarkAsPastDue`.

---

## B.1 Assigned tests

These 6 tests fail because they drive a due subscription onto the **reminder / cannot-auto-debit** path **without** stubbing a CRM email (and therefore without a mintable checkout URL).

| # | Test | Assertion line | Expected | Actual |
|---|------|----------------|----------|--------|
| 1 | `RunOnce_BillplzOrReminderOnlyOrNoVault_MarksPastDue_DoesNotPublishOffSession` | :190 (`billplzSub`; `reminderSub` and `noVaultSub` not reached) | all three `PAST_DUE`; no off-session event | `billplzSub` still `ACTIVE` |
| 2 | `RunOnce_MarksEachDueSubscriptionPastDue_Independently` | :117 (`subA`; `subB` not reached) | both `PAST_DUE` | `subA` still `ACTIVE` |
| 3 | `RunOnce_MissingProduct_DoesNotThrowBatch_SiblingStillProcessed` | :543 (`sibling`) | orphan `ACTIVE`, sibling `PAST_DUE` | sibling still `ACTIVE` (orphan `ACTIVE` is correct) |
| 4 | `RunOnce_OneTimeProduct_DoesNotPastDueOrCharge` | :517 (`dueRecurring`) | one-time `ACTIVE`, recurring `PAST_DUE` | recurring still `ACTIVE` (one-time `ACTIVE` is correct) |
| 5 | `RunOnce_SkipsPastDueSuspendedCanceledAndFutureNotDue` | :164 (`activeDue`) | skip-set unchanged; `activeDue` `PAST_DUE` | `activeDue` still `ACTIVE` (skip-set assertions already passed) |
| 6 | `RunOnce_TrialDueAfterAttempt1_MarksPastDue` | :690 | `PAST_DUE` | still `TRIALING` |

The same class has **passing** tests that already call `ArrangeMint` (or otherwise never reach the reminder mint gate):

| Passing reminder-path tests | Why they pass after 168 |
|-----------------------------|-------------------------|
| `RunOnce_NonVaultedDue_MintsCheckoutBoundToExistingSubscription_ThenPastDue` | Calls `ArrangeMint` at :352. |
| `RunOnce_NoToken_AssignsCampaignAndDispatchesDay0` | `ArrangeMint` at :437. |
| `RunOnce_TwoNoToken_BothGetDay0` | `ArrangeMint` at :476. |
| `RunOnce_NonVaultedGenerateThrows_DoesNotMarkPastDue_RetriesNextTick` | Stubs CRM email + workspace, then makes `mediator.Send` throw. Expects `ACTIVE` — this is already the 168 fail-closed shape. |
| `RunOnce_FlaggedDueReminderOnly_CancelsWithoutMintOrPastDue` | `ArrangeMint` present but unused: `CancelAtPeriodEnd` finalizes cancel before the mint gate. |

Vaulted STRIPE/CHIP tests (`RunOnce_StripeVaulted_PublishesOffSessionAttempt1_DoesNotAdvanceDates`, Chip, two-dues, attempt-1 wait, collection pause, pending plan, SST, etc.) take `canCharge == true` and **return before** the CRM/email gate. They are unaffected by 168.

That contrast is the smoking gun: **the fixture already knows how to satisfy 168** (`ArrangeMint`). The six failures simply never call it.

---

## B.2 Exact SetUp / mock code with line numbers

### B.2.1 Fixture SetUp — CRM is registered, but profile is not stubbed

```49:91:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs
    [SetUp]
    public void SetUp()
    {
        _orgId = Guid.CreateVersion7();
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var ctx = Substitute.For<IExecutionContextAccessor>();
        ctx.TenantId.Returns(Guid.Empty);
        _db = new CommerceDbContext(options, ctx, Substitute.For<IMediator>(), new DatabaseJobTrigger());

        _eventBus = Substitute.For<IEventBus>();
        _mediator = Substitute.For<IMediator>();
        _crm = Substitute.For<ICrmQueryService>();
        _one = Substitute.For<IOneQueryService>();
        _tokens = Substitute.For<IMagicLinkTokenService>();
        _tokens.GenerateToken(Arg.Any<Guid>()).Returns("mint-token");
        _billing = Substitute.For<IBillingQueryService>();
        _billing.GetBillingProfileAsync(Arg.Any<Guid>()).Returns((TenantBillingProfileDto?)null);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://portal.test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddKeyedSingleton<IEventBus>("CommerceEventBus", _eventBus);
        services.AddSingleton(_mediator);
        services.AddSingleton(_crm);
        services.AddSingleton(_one);
        services.AddSingleton(_tokens);
        services.AddSingleton(_billing);
        services.AddSingleton<IConfiguration>(config);
        _sp = services.BuildServiceProvider();

        _job = new BillingEngineJob(
            _sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BillingEngineJob>.Instance,
            Options.Create(new BackgroundWorkerOptions()));
    }
```

Important details:

- `_crm` **is** in the container. Production does `GetService<ICrmQueryService>()` (`BillingEngineJob.cs` :78). After SetUp, `crm == null` is **false**. The 168 `crm == null` throw at :343–347 is **not** what these tests hit.
- `_crm.GetClientProfileAsync` is **not** configured in SetUp. NSubstitute’s default for `Task<ClientProfileDto?>` is a completed task with `null`. So `profile` is null, `email` is null, and 168 throws at :351–355.
- `_one.GetWorkspaceByIdAsync` is also unconfigured (default `null`). That only matters **after** a non-empty email exists; `RenewalCheckoutIssuer.MintAsync` then throws `Workspace {id} not found for renewal checkout.` (`RenewalCheckoutIssuer.cs` :38–40).
- `_mediator.Send(...)` is unconfigured. After a workspace exists, `GenerateCheckoutSessionQuery` would return `default(string)` (null), and mint would throw `GenerateCheckoutSessionQuery returned an empty renewal checkout URL.` (`RenewalCheckoutIssuer.cs` :69–71).
- `_tokens.GenerateToken` **is** stubbed (`"mint-token"`). Config `App:ClientUrl` **is** set. Those two pieces are already mint-ready.

So: registering CRM is not enough. 168 requires a **real email plus a successful mint** before `MarkAsPastDue`.

### B.2.2 Helper that already does the right thing — `ArrangeMint`

```984:996:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs
    private void ArrangeMint(string email, string checkoutUrl)
    {
        _crm.GetClientProfileAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(new ClientProfileDto
        {
            Id = Guid.CreateVersion7().ToString(),
            Full_name = "Buyer",
            Email = email
        });
        _one.GetWorkspaceByIdAsync(_orgId).Returns(
            new WorkspaceSnapshotDto(_orgId, "Acme", "acme", true, DateTime.UtcNow));
        _mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns(checkoutUrl);
    }
```

This is the 165-correct two-arg stub **and** the 168-correct mint stack (email + workspace slug + non-empty checkout URL). Tests that call it still pass on this HEAD.

### B.2.3 The one test that stubs CRM inline (not in the assigned set)

`RunOnce_NonVaultedGenerateThrows_DoesNotMarkPastDue_RetriesNextTick` (lines 384–421) stubs:

```396:405:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs
        _crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Buyer",
            Email = "buyer@example.com"
        });
        _one.GetWorkspaceByIdAsync(_orgId).Returns(
            new WorkspaceSnapshotDto(_orgId, "Acme", "acme", true, DateTime.UtcNow));
        _mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gateway down"));
```

That test **expects** `ACTIVE` and a null checkout URL across two ticks. It is the existing specification of 168’s fail-closed mint: if checkout generation throws, do not mark PAST_DUE, retry next tick. The assigned failures are the mirror image: they never give the job an email, so they now take the same fail-closed exit, but they still assert `PAST_DUE`.

### B.2.4 Product / claim helpers the assigned tests rely on

```1005:1018:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs
    private static Product CreateProduct(Guid orgId, string gatewayName = "STRIPE", string interval = "mo") =>
        new(
            orgId,
            "Plan",
            $"plan-{Guid.CreateVersion7():N}"[..20],
            50m,
            "FIXED",
            0m,
            "MYR",
            interval,
            gatewayName,
            new CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
```

Default gateway is `STRIPE`. `PaymentGatewayCapabilities.SupportsOffSession` is true only for `STRIPE` and `CHIP` (`PaymentGatewayCapabilities.cs` :10–14). `BILLPLZ` is reminder-only.

In-memory claim (`BillingEngineJob.cs` :160–178) picks rows with `NextBillingDate <= now`, status not in `PENDING/PAST_DUE/SUSPENDED/CANCELED`, collection pause expired or null, no open dispute, not in `failedIds ∪ processedIds`. That is why skip-set tests can still reach `activeDue`, and why a mint throw on one row does not starve the sibling (the thrown id goes into `failedIds` at :117).

---

## B.3 Production change (168 — BillingEngineJob reminder path)

### B.3.1 What 168 changed (and what it did **not** change)

Issue 168 (`B10-X12`) called out an asymmetric fail policy on the reminder-only path:

- **Before:** `crm == null` or missing email → log a warning and still `MarkAsPastDue()` **without** a renewal checkout URL.
- **Before:** `mediator` / `one` / `tokens` null → throw (fail-closed for mint).
- Invoice reminders (separate job) could send a pay URL **without** a workspace slug (404), then persist a dispatch log that blocked retry.

Commit `f75842db` made billing reminder-only fail-closed: **no CRM email ⇒ throw ⇒ no PAST_DUE**. Invoice reminders (not this cluster) skip dispatch when the workspace slug is missing; that job gained `MissingWorkspaceSlug_DoesNotDispatchOrLog` in the same commit. **`BillingEngineJobTests` was not updated in `f75842db`.** That is why this cluster still expects the old warning-and-mark behaviour.

### B.3.2 Diff of the reminder gate

Before `f75842db` (from `git show f75842db`):

```csharp
        string? email = null;
        if (crm != null)
        {
            var profile = await crm.GetClientProfileAsync(sub.OrganizationId, sub.ClientProfileId);
            email = profile?.Email;
        }

        string? checkoutUrl = null;
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning(
                "Subscription {Id} has no CRM email; marking PAST_DUE without a renewal checkout URL.",
                sub.Id);
        }
        else
        {
            if (mediator == null || one == null || tokens == null)
            {
                throw new InvalidOperationException(
                    "Cannot mint a renewal checkout: IMediator, IOneQueryService, and IMagicLinkTokenService are required.");
            }

            checkoutUrl = await RenewalCheckoutIssuer.MintAsync(
                mediator, one, config, tokens, sub, product, email, ct, billing);
            sub.SetCurrentRenewalCheckout(checkoutUrl, sub.NextBillingDate!.Value);
        }

        sub.MarkAsPastDue();
        await StartPastDueDunningRunAsync(...);
```

After `f75842db` (current HEAD, lines 343–368):

```343:368:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        if (crm == null)
        {
            throw new InvalidOperationException(
                "Cannot mark PAST_DUE without ICrmQueryService to mint a recoverable checkout.");
        }

        var profile = await crm.GetClientProfileAsync(sub.OrganizationId, sub.ClientProfileId);
        var email = profile?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                $"Cannot mark PAST_DUE for subscription {sub.Id} without a CRM email to mint a renewal checkout.");
        }

        if (mediator == null || one == null || tokens == null)
        {
            throw new InvalidOperationException(
                "Cannot mint a renewal checkout: IMediator, IOneQueryService, and IMagicLinkTokenService are required.");
        }

        var checkoutUrl = await RenewalCheckoutIssuer.MintAsync(
            mediator, one, config, tokens, sub, product, email, ct, billing);
        sub.SetCurrentRenewalCheckout(checkoutUrl, sub.NextBillingDate!.Value);

        sub.MarkAsPastDue();
        await StartPastDueDunningRunAsync(db, eventBus, config, billing, crm, sub, ct);
```

The assigned tests hit the **email** throw (`:351–355`), not the `crm == null` throw.

`MarkAsPastDue` now runs **only after** a non-empty `checkoutUrl` is minted and stored. There is no longer a “PAST_DUE with null checkout” success path.

### B.3.3 How the throw becomes “stayed ACTIVE” instead of a test exception

```108:120:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
                try
                {
                    await ProcessOneSubscriptionAsync(db, eventBus, crm, mediator, one, config, tokens, billing, sub, failedIds, ct);
                    await db.SaveChangesAsync(ct);
                    if (tx != null) await tx.CommitAsync(ct);
                    processedIds.Add(sub.Id);
                }
                catch (Exception ex)
                {
                    failedIds.Add(sub.Id);
                    _logger.LogError(ex, "Billing failed for subscription {Id}; continuing batch.", sub.Id);
                    if (tx != null) await tx.RollbackAsync(ct);
                }
```

In-memory tests have `tx == null` (`:102–106`). Sequence for a due reminder-path row with no CRM email:

1. Claim the row (`ClaimDueSubscriptionInMemoryAsync`).
2. Product exists, not `one_time`, not collection-paused, not `CancelAtPeriodEnd`.
3. `canCharge` is false (see B.4 per test) **or** TRIALING-after-attempt-1 falls through (`:328–341`).
4. `crm` is the SetUp substitute (not null).
5. `GetClientProfileAsync` returns `null`.
6. Throw `Cannot mark PAST_DUE for subscription {id} without a CRM email to mint a renewal checkout.`
7. Catch: `failedIds.Add(sub.Id)`. No `SaveChangesAsync`. Status on the tracked entity was never changed (`MarkAsPastDue` is after the throw).
8. Next loop iteration excludes that id. Sibling can still be claimed.

`failedIds` is a local `HashSet<Guid>` inside `ProcessBillingAsync` (`:70`). Tests cannot read it. They can only observe:

- status unchanged (`ACTIVE` or `TRIALING`)
- no `subscription.past_due` webhook
- no `ExecuteOffSessionChargeIntegrationEvent` (for reminder-path rows)
- `RunOnceAsync` does not throw (batch isolation)

### B.3.4 How a due row reaches the mint gate

```286:341:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs
        var canCharge = PaymentGatewayCapabilities.SupportsOffSession(product.GatewayName)
                        && !sub.IsReminderOnly
                        && !sub.HasOpenDispute
                        && !string.IsNullOrEmpty(sub.VaultedTokenId)
                        && !string.IsNullOrEmpty(sub.VaultedCustomerId);

        if (canCharge)
        {
            var targetDate = sub.NextBillingDate!.Value.Date;
            var attemptCount = await db.ChargeAttemptLogs
                .CountAsync(l => l.SubscriptionId == sub.Id && l.TargetBillingDate == targetDate, ct);

            if (attemptCount == 0)
            {
                // ... ChargeAttemptLog + ExecuteOffSessionChargeIntegrationEvent ...
                return;
            }

            if (!string.Equals(sub.Status, "TRIALING", StringComparison.Ordinal))
            {
                // ACTIVE attempt-1 waits for the payment webhook
                return;
            }

            _logger.LogWarning(
                "Subscription {Id} still TRIALING after attempt 1 with no webhook; marking PAST_DUE.",
                sub.Id);
        }

        // ← mint gate (168) starts here
```

So the mint gate runs when:

- **Cannot auto-debit:** Billplz / reminder-only / missing vault / non-off-session gateway. This is assigned tests 1–5 (and the recurring sibling in test 4).
- **Can auto-debit but TRIALING with attempt 1 already logged:** fall-through to mint + PAST_DUE. This is assigned test 6.

`StoreVaultedToken` (`Subscription.cs` :307–313) sets `IsReminderOnly = false`. A Billplz row can have vault ids and still fail `canCharge` because `SupportsOffSession("BILLPLZ")` is false.

### B.3.5 Mint requirements after the email check

```20:74:apps/lazuar-api/Modules/Commerce/Application/RenewalCheckoutIssuer.cs
    public static async Task<string> MintAsync(
        IMediator mediator,
        IOneQueryService one,
        IConfiguration? config,
        IMagicLinkTokenService tokenService,
        Subscription sub,
        Product product,
        string customerEmail,
        CancellationToken ct,
        IBillingQueryService? billing = null)
    {
        // ...
        var workspace = await one.GetWorkspaceByIdAsync(sub.OrganizationId)
            ?? throw new InvalidOperationException(
                $"Workspace {sub.OrganizationId} not found for renewal checkout.");
        // ...
        var url = await mediator.Send(new GenerateCheckoutSessionQuery(...), ct);

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("GenerateCheckoutSessionQuery returned an empty renewal checkout URL.");
        }

        return url;
    }
```

**Stubbing CRM email alone is not enough** for the “so mint can run” option. You must also stub workspace + a non-empty checkout URL. That is exactly `ArrangeMint`. If you stub only email, the job throws at the workspace line, catch still leaves the row `ACTIVE`, and the assigned assertions still fail.

---

## B.4 Per-test expected vs actual

### B.4.1 `RunOnce_BillplzOrReminderOnlyOrNoVault_MarksPastDue_DoesNotPublishOffSession` (lines 168–198)

**Arrange.** Three due ACTIVE rows, no `ArrangeMint`:

| Row | Product gateway | Vault | `IsReminderOnly` | `canCharge` |
|-----|-----------------|-------|------------------|-------------|
| `billplzSub` | `BILLPLZ` | `cus_junk` / `tok_junk` | forced false by `StoreVaultedToken` | **false** (`SupportsOffSession("BILLPLZ")` is false) |
| `reminderSub` | `STRIPE` | none | `true` (`Activate(..., isReminderOnly: true)`) | **false** |
| `noVaultSub` | `STRIPE` | none | false | **false** (empty vault) |

All three walk into the mint gate. Claim order is `NextBillingDate` ascending; all three are “yesterday,” so claim order follows insert/tie-break. First claimed row (`billplzSub` in the observed failure) throws on missing email, is added to `failedIds`, stays `ACTIVE`. The test asserts `billplzSub` first (`:190`) and stops.

**Expected.**

```
billplzSub.Status == PAST_DUE
reminderSub.Status == PAST_DUE
noVaultSub.Status == PAST_DUE
DidNotReceive ExecuteOffSessionChargeIntegrationEvent
```

**Actual (live).**

```
Expected (... s.Id == billplzSub.Id).Status to be "PAST_DUE" ..., but "ACTIVE" ...
at BillingEngineJobTests.cs:line 190
```

Off-session publish is indeed absent (the path never reaches `canCharge` publish). That half of the test is still true and untested because the first status assert fails.

If this test is updated with `ArrangeMint`, all three should become `PAST_DUE` with checkout URLs, and still must not publish off-session. That preserves the original intent.

If this test is updated to document fail-closed, all three stay `ACTIVE`, still no off-session, and the test name would be lying (`MarksPastDue`). Prefer `ArrangeMint` here.

### B.4.2 `RunOnce_MarksEachDueSubscriptionPastDue_Independently` (lines 100–119)

**Arrange.** One STRIPE product, two due ACTIVE subs (`subA` due −1 day, `subB` due −2 hours). No vault. No `ArrangeMint`. Both `canCharge == false`.

**Expected.** Both `PAST_DUE` after one `RunOnceAsync` (batch independence).

**Actual (live).**

```
Expected a.Status to be "PAST_DUE" ..., but "ACTIVE" ...
at BillingEngineJobTests.cs:line 117
```

Independence **does** still hold: `subA` throws, goes into `failedIds`, `subB` is claimed next, throws the same way, also stays `ACTIVE`. The batch does not abort. The test never sees that because it asserts PAST_DUE on `subA` first.

`ArrangeMint` restores both PAST_DUE (same as `RunOnce_TwoNoToken_BothGetDay0`, which already passes). Fail-closed rewrite would assert both still `ACTIVE` and `RunOnceAsync` not throwing — weaker, and it would no longer prove the reminder success path is independent.

### B.4.3 `RunOnce_MissingProduct_DoesNotThrowBatch_SiblingStillProcessed` (lines 525–545)

**Arrange.** `orphan` points at a random missing `ProductId`. `sibling` points at a real STRIPE product, due, no vault. No `ArrangeMint`.

**Orphan path.** `ProcessOneSubscriptionAsync` :194–201: product null → `failedIds.Add` + **return** (no throw). Status stays `ACTIVE`. This part of the test **passes** (`:541–542`).

**Sibling path.** Reminder mint gate, no email, throw, catch, `failedIds.Add`, stays `ACTIVE`.

**Expected.** Orphan `ACTIVE`; sibling `PAST_DUE`. Batch must not throw (already true).

**Actual (live).**

```
Expected (... s.Id == sibling.Id).Status to be "PAST_DUE" ..., but "ACTIVE" ...
at BillingEngineJobTests.cs:line 543
```

The test name (`DoesNotThrowBatch_SiblingStillProcessed`) is still true in the fail-closed sense: the sibling **is** processed (claimed, mint attempted, failure isolated). It is **not** marked PAST_DUE.

**Recommended:** `ArrangeMint` so the sibling’s happy path is still PAST_DUE. The orphan still has no product and still stays ACTIVE. That keeps the original isolation + success-path proof.

Fail-closed alternative: assert sibling `ACTIVE` **and** no `subscription.past_due` event **and** `RunOnceAsync` does not throw. Then add a comment that 168 fail-closes missing email. Do not drop the orphan `ACTIVE` assert.

### B.4.4 `RunOnce_OneTimeProduct_DoesNotPastDueOrCharge` (lines 497–523)

**Arrange.** `one_time` STRIPE product + vaulted due sub. Recurring STRIPE product + due sub **without** vault. No `ArrangeMint`.

**One-time path.** `:204–209`: interval `one_time` → `failedIds.Add` + return. Stays `ACTIVE`. No charge log. This assert **passes** (`:515–516`).

**Recurring path.** No vault → mint gate → missing email → throw → stays `ACTIVE`.

**Expected.** One-time `ACTIVE`; recurring `PAST_DUE`; no off-session event; no charge logs on the one-time id.

**Actual (live).**

```
Expected (... s.Id == dueRecurring.Id).Status to be "PAST_DUE" ..., but "ACTIVE" ...
at BillingEngineJobTests.cs:line 517
```

The one-time skip is not the bug. The recurring “control” row used to become PAST_DUE via the pre-168 warning path.

**Recommended:** `ArrangeMint`. One-time still skipped before mint (`:204` returns). Recurring mints and becomes PAST_DUE. Off-session still not published (no vault / one-time skip).

### B.4.5 `RunOnce_SkipsPastDueSuspendedCanceledAndFutureNotDue` (lines 121–166)

**Arrange.** Six rows, one product, no `ArrangeMint`:

| Row | Setup | Claimed? |
|-----|-------|----------|
| `pastDue` | already `MarkAsPastDue()` | no (status `PAST_DUE`) |
| `canceled` | `Cancel()` | no |
| `suspended` | `Suspend()` | no |
| `future` | next bill +10 days | no (`NextBillingDate > now`) |
| `pending` | never `Activate`; next bill forced to −1 day via EF | no (status `PENDING`) |
| `activeDue` | ACTIVE, due yesterday, no vault | **yes** |

**Expected.** First five statuses unchanged; `activeDue == PAST_DUE`.

**Actual (live).** The five skip asserts at `:154–163` **pass**. Then:

```
Expected (... s.Id == activeDue.Id).Status to be "PAST_DUE" ..., but "ACTIVE" ...
at BillingEngineJobTests.cs:line 164
```

`activeDue` is the only row that enters the mint gate.

**Recommended:** `ArrangeMint` so `activeDue` can complete PAST_DUE. Skip-set behaviour does not use CRM.

### B.4.6 `RunOnce_TrialDueAfterAttempt1_MarksPastDue` (lines 671–692)

**Arrange.** STRIPE product. `ActivateTrial(endsAt: +7 days, reminderOnly: false, qty 1, unit = product.Price)` → status `TRIALING`, `NextBillingDate = endsAt`. Then `StoreVaultedToken("cus_trial", "pm_trial")`. Then EF overwrites `NextBillingDate` to yesterday. Then a `ChargeAttemptLog` attempt 1 for that due date, source billing. **No `ArrangeMint`.**

**`canCharge`.** True (STRIPE + vault + not reminder-only).

**Attempt count.** 1, so the job does **not** publish another off-session (`:299–326` skipped).

**Status.** `TRIALING`, so it does **not** take the “wait for webhook” return at `:330–336`. It logs the warning at `:338–340` and **falls through to the mint gate**.

**Mint gate.** No CRM email → throw → catch → `failedIds` → status remains `TRIALING`.

**Expected.**

```
reloaded.Status == PAST_DUE
DidNotReceive ExecuteOffSessionChargeIntegrationEvent
```

**Actual (live).**

```
Expected reloaded.Status to be "PAST_DUE", but "TRIALING" differs near "TRI" (index 0).
at BillingEngineJobTests.cs:line 690
```

This is the one assigned test that does **not** stay `ACTIVE`. The cluster summary “expected PAST_DUE but stayed ACTIVE” is the reminder-path majority; the trial convert stall is the same 168 throw on a `TRIALING` row.

**Recommended:** `ArrangeMint`. Fall-through then mints, `MarkAsPastDue()`, status becomes `PAST_DUE`, still no second off-session (attempt 1 already exists; mint path does not publish `ExecuteOffSessionChargeIntegrationEvent`).

Fail-closed alternative: assert still `TRIALING` and no off-session. That would re-open the original product bug 168 did **not** intend to reintroduce: a stalled trial convert with no recoverable URL and no PAST_DUE. Prefer `ArrangeMint` so the documented “mark PAST_DUE after attempt 1 while TRIALING” behaviour remains tested **when** a checkout can be minted.

---

## B.5 Recommended test-only fix (do **not** revert 168)

Two valid directions. They are not interchangeable per test name.

### Option 1 (preferred for these six names): stub so mint can run

Call the existing helper at the start of each assigned test (or once in SetUp):

```csharp
ArrangeMint("buyer@example.com", "https://pay.test/bills/renew-1");
```

Why this is safe for the assigned set:

- All six tests assert PAST_DUE on a row that **should** become PAST_DUE when a recoverable URL exists.
- None of the six assert “no checkout URL.”
- Vaulted STRIPE/CHIP tests in the same class return before the mint gate even if `ArrangeMint` is in SetUp.
- `RunOnce_NonVaultedGenerateThrows_*` **overrides** `mediator.Send` to throw; if `ArrangeMint` is in SetUp, that test must keep its own `.ThrowsAsync` **after** SetUp (NSubstitute last-config-wins). Safer: do **not** put `ArrangeMint` in SetUp; call it only in tests that want a successful mint.
- One-time / missing-product / cancel-at-period-end paths return before mint, so a per-test `ArrangeMint` on a sibling does not activate those rows.

**Do not stub only `_crm` email.** Without workspace + URL, mint still throws and status still does not change.

### Option 2: assert fail-closed (status stays `ACTIVE` / `TRIALING`)

Rewrite assertions to match 168:

- expected `PAST_DUE` → expected current status (`ACTIVE`, or `TRIALING` for the trial test)
- `DidNotReceive` `OutboundWebhookRequestedIntegrationEvent` with `subscription.past_due`
- `DidNotReceive` `ExecuteOffSessionChargeIntegrationEvent` where that was already asserted
- `RunOnceAsync` does not throw (already implied)

`failedIds` is not observable. Do not add a production getter just to assert it.

This option **renames or comments** tests whose names say `MarksPastDue`. Otherwise CI would be green while the names lie.

Use Option 2 only if the product decision is “these rows are not a PAST_DUE success path when email is missing, and we want the tests to lock that.” Even then, **keep at least one** happy-path test that calls `ArrangeMint` and asserts PAST_DUE (those already exist: `RunOnce_NoToken_*`, `RunOnce_NonVaultedDue_*`). The assigned six, by name, are the happy path.

### Option 3 (best of both, slightly more work)

- Option 1 on the six assigned tests (preserve names / original intent).
- Add **one new** test, e.g. `RunOnce_NoCrmEmail_DoesNotMarkPastDue_RetriesNextTick`, cloned from `RunOnce_NonVaultedGenerateThrows_*` but leaving CRM unstubbed (or returning a profile with blank email). Assert `ACTIVE`, null checkout URL, no `subscription.past_due`, two ticks still `ACTIVE`. That locks 168 explicitly.

### Explicitly forbidden

- Reverting `f75842db` / restoring “mark PAST_DUE without a renewal checkout URL.”
- Changing `BillingEngineJob` to fail-open again so these tests pass.
- Catching the email throw inside `ProcessOneSubscriptionAsync` and then `MarkAsPastDue()` anyway.

---

## B.6 Concrete patch sketches

### Sketch B1 — per-test `ArrangeMint` (preferred, smallest intent-preserving change)

File: `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs`

`RunOnce_MarksEachDueSubscriptionPastDue_Independently`:

```diff
         _db.Products.Add(product);
         _db.Subscriptions.AddRange(subA, subB);
         await _db.SaveChangesAsync();

+        ArrangeMint("buyer@example.com", "https://pay.test/bills/renew-1");
+
         await _job.RunOnceAsync(CancellationToken.None);
```

`RunOnce_SkipsPastDueSuspendedCanceledAndFutureNotDue`:

```diff
         _db.Entry(pending).Property(s => s.NextBillingDate).CurrentValue = DateTime.UtcNow.AddDays(-1);
         await _db.SaveChangesAsync();

+        ArrangeMint("buyer@example.com", "https://pay.test/bills/renew-1");
+
         await _job.RunOnceAsync(CancellationToken.None);
```

`RunOnce_BillplzOrReminderOnlyOrNoVault_MarksPastDue_DoesNotPublishOffSession`:

```diff
         _db.Products.AddRange(billplz, reminder, noVault);
         _db.Subscriptions.AddRange(billplzSub, reminderSub, noVaultSub);
         await _db.SaveChangesAsync();

+        ArrangeMint("buyer@example.com", "https://pay.test/bills/renew-1");
+
         await _job.RunOnceAsync(CancellationToken.None);
```

`RunOnce_OneTimeProduct_DoesNotPastDueOrCharge`:

```diff
         _db.Products.AddRange(oneTime, recurring);
         _db.Subscriptions.AddRange(oneTimeSub, dueRecurring);
         await _db.SaveChangesAsync();

+        ArrangeMint("buyer@example.com", "https://pay.test/bills/renew-1");
+
         await _job.RunOnceAsync(CancellationToken.None);
```

`RunOnce_MissingProduct_DoesNotThrowBatch_SiblingStillProcessed`:

```diff
         _db.Products.Add(product);
         _db.Subscriptions.AddRange(orphan, sibling);
         await _db.SaveChangesAsync();

+        ArrangeMint("buyer@example.com", "https://pay.test/bills/renew-1");
+
         await _job.RunOnceAsync(CancellationToken.None);
```

`RunOnce_TrialDueAfterAttempt1_MarksPastDue`:

```diff
         _db.ChargeAttemptLogs.Add(new ChargeAttemptLog(sub.Id, due.Date, 1, ChargeAttemptLog.SourceBilling));
         await _db.SaveChangesAsync();

+        ArrangeMint("buyer@example.com", "https://pay.test/bills/renew-1");
+
         await _job.RunOnceAsync(CancellationToken.None);
```

No assertion changes. No production changes.

### Sketch B2 — fail-closed assertion rewrite (only if product owners want these tests to lock missing-email)

Example for `RunOnce_MarksEachDueSubscriptionPastDue_Independently` (would also need a rename):

```diff
-    public async Task RunOnce_MarksEachDueSubscriptionPastDue_Independently()
+    public async Task RunOnce_DueWithoutCrmEmail_LeavesActive_Independently()
     {
         // ... same arrange, no ArrangeMint ...
         await _job.RunOnceAsync(CancellationToken.None);

         var a = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == subA.Id);
         var b = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == subB.Id);
-        a.Status.Should().Be("PAST_DUE");
-        b.Status.Should().Be("PAST_DUE");
+        a.Status.Should().Be("ACTIVE");
+        b.Status.Should().Be("ACTIVE");
+        a.CurrentRenewalCheckoutUrl.Should().BeNull();
+        b.CurrentRenewalCheckoutUrl.Should().BeNull();
+        await _eventBus.DidNotReceive().PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(
+            e => e.EventType == "subscription.past_due"));
     }
```

Trial test would assert `"TRIALING"`, not `"ACTIVE"`.

**Do not ship Sketch B2 for all six without renaming.** Prefer Sketch B1 + an optional new fail-closed test (Option 3).

### Sketch B3 — new explicit 168 test (optional companion to B1)

```csharp
[Test]
public async Task RunOnce_NoCrmEmail_DoesNotMarkPastDue_RetriesNextTick()
{
    var product = CreateProduct(_orgId, "BILLPLZ");
    var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
    sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1), isReminderOnly: true);
    _db.Products.Add(product);
    _db.Subscriptions.Add(sub);
    await _db.SaveChangesAsync();
    // Intentionally no ArrangeMint: GetClientProfileAsync returns null.

    await _job.RunOnceAsync(CancellationToken.None);
    await _job.RunOnceAsync(CancellationToken.None);

    var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
    reloaded.Status.Should().Be("ACTIVE");
    reloaded.CurrentRenewalCheckoutUrl.Should().BeNull();
    await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
    await _mediator.DidNotReceive().Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>());
}
```

This mirrors `RunOnce_NonVaultedGenerateThrows_DoesNotMarkPastDue_RetriesNextTick` but for the **email-missing** throw instead of the gateway-down throw.

### Verify

```bash
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj \
  --filter "FullyQualifiedName~BillingEngineJobTests"
```

After Sketch B1: the six assigned tests should pass; existing `ArrangeMint` / vaulted / generate-throws tests should stay green.

---

## B.7 Files to change later

| File | Change |
|------|--------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs` | Add `ArrangeMint(...)` to the six assigned tests (Sketch B1). Optionally add `RunOnce_NoCrmEmail_DoesNotMarkPastDue_RetriesNextTick` (Sketch B3). |
| **Do not change** `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | 168 fail-closed reminder path stays. |
| **Do not change** `Modules/Commerce/Application/RenewalCheckoutIssuer.cs` | Mint still requires email + workspace + non-empty URL. |
| **Do not change** `issues/168-p1-b10-x12-getservice-crm-one-tokens-config-fail-open-on-money-comms.md` | Already `status: resolved`. |
| Optional later | If other jobs still mark money-comms state without a recoverable URL, they are out of this cluster (168 already added `InvoiceReminderJobTests.MissingWorkspaceSlug_DoesNotDispatchOrLog`). |

---

# Cross-cluster notes (do not merge the fixes)

These two clusters look related because both talk to `GetClientProfileAsync(Guid, Guid)`. They are not the same bug.

| | Cluster A — Gateway failed handler | Cluster B — Billing engine |
|--|------------------------------------|----------------------------|
| Symptom | `AmbiguousArgumentsException` | FluentAssertions status mismatch |
| When | NSubstitute return callback runs | After `RunOnceAsync` returns successfully |
| Root | `ci.Arg<Guid>()` on a two-Guid method | Tests expect pre-168 PAST_DUE-without-URL |
| Product commit | 165 / `42b7ad37` (arity) | 168 / `f75842db` (fail-closed mint) |
| CRM in test | Stubbed **with email**, callback broken | Substitute registered, **email not stubbed** |
| Status in DB | Unchanged (exception before save) but NUnit never asserts it | Unchanged (`ACTIVE` / `TRIALING`), asserted |
| Fix | One line in SetUp | `ArrangeMint` on six tests (or rewrite asserts) |
| Revert product? | No | No |

A developer who “fixes” Cluster B by making `BillingEngineJob` mark PAST_DUE without an email would re-open 168 (buyer in PAST_DUE with no hosted pay URL). A developer who “fixes” Cluster A by adding a one-arg `GetClientProfileAsync` overload would re-open 165 (global PII read). Both clusters are test-only on this HEAD.

**Suggested apply order:** Cluster A first (one-line, unblocks 11 tests immediately), then Cluster B Sketch B1 (six `ArrangeMint` calls). Re-run both filters before touching anything else.

**HEAD reminder:** `4531f210` (`fix/180-unify-outbox-inbox`) is unrelated to either failure. The failing stubs and the 168 reminder gate were already on the tree these tests were compiled against.
