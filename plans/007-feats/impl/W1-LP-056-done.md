# W1-LP-056 — done

Cancel at period end is a real product action. `ACTIVE` + future `NextBillingDate` + `at_period_end` sets `CancelAtPeriodEnd` and stays `ACTIVE`. No `subscription.canceled` (typed or outbound) until the paid-through instant. The hourly billing job claims the flagged row when due, skips charge / reminder mint / `PAST_DUE`, then `Cancel()` and publishes the same `SubscriptionCanceledIntegrationEvent` as immediate cancel. Keep clears the flag before that tick. Portal default is period-end; admin default stays immediate.

Immediate cancel (LP-055) is unchanged. Dunning terminal `CANCEL` and GDPR remain immediate. No `NON_RENEWING`, no `CancelAt`, no `subscription.updated`.

## Files changed

### Domain / persistence

- `Modules/Commerce/Domain/Aggregates/Subscription.cs` — `CancelAtPeriodEnd`; `ScheduleCancelAtPeriodEnd` / `ClearScheduledCancel`; `Cancel()` clears the flag; `RecoverFromPayment` / `Resume` clear it. `Activate` does not.
- `Modules/Commerce/Infrastructure/CommerceDbContext.cs` + `20260817120000_AddSubscriptionCancelAtPeriodEnd` + snapshot — `commerce.Subscriptions.CancelAtPeriodEnd` `boolean NOT NULL DEFAULT false`.

### Commands

- `CancelAdminSubscriptionCommand` — `AtPeriodEnd = false`, returns `CANCELED` | `scheduled`.
- `CancelPortalSubscriptionCommand` — `AtPeriodEnd = true`, returns `canceled` | `scheduled`.
- `KeepAdminSubscriptionCommand` / `KeepPortalSubscriptionCommand` + handlers — org / magic-token checks; 400 if already `CANCELED`.
- `SubscriptionCancelDecision` + `SubscriptionCancelApplier` — shared table: schedule when `ACTIVE` + future paid-through; otherwise immediate; already flagged is a no-op; already `CANCELED` is idempotent.

### Billing / dunning

- `BillingEngineJob` — after product / `one_time` guards: if flag, `Cancel()` + typed canceled event; no attempt log, no off-session, no mint, no `PAST_DUE`.
- `DunningEngineJob.Claim` — pre-dunning SQL + in-memory exclude `CancelAtPeriodEnd`.

### HTTP + spec

- Portal `POST .../portal/cancel` — `at_period_end ?? true`; status `scheduled` | `canceled`.
- Portal `POST .../portal/keep`.
- Admin `POST /subscribers/{id}/cancel` optional body (omit / `{}` = immediate).
- Admin `POST /subscribers/{id}/keep`.
- Portal query maps `NextBillingDate` → `current_period_end` and returns `cancel_at_period_end`.
- Ops subscriber DTO includes `cancel_at_period_end`.
- TypeSpec + generated `Lazuar.ApiContracts.cs` / `api-types-ts`.

### Frontends

- `apps/lazuar-portal/.../portal/page.tsx` — healthy `ACTIVE` Cancel Plan is period-end + paid-through copy; optional immediate; flagged shows Keep; `PAST_DUE` is immediate-only.
- `apps/lazuar-ops/.../SubscribersPage.tsx` — schedule vs now, badge, Keep; schedule does not flip status to `CANCELED`.
- Deleted unused `CommunityPortalView.tsx` (lied about period-end).
- Legal refund §4 mentions access until paid-through.

### Tests

- `SubscriptionCancelAtPeriodEndTests` — domain schedule/throw/clear/recover/resume; admin schedule / due fallback / already-flagged / keep; portal token + foreign client.
- `CrossTenantIdorTests` — admin period-end cancel + keep foreign org.
- `BillingEngineJobTests` — flagged due vaulted finalize + sibling still charges; flagged reminder-only no mint / no `past_due`; flagged future untouched.
- `DunningEngineJobTests` — pre-dunning skips flagged `ACTIVE` due in 3 days.
- `CommerceHonestyDtoTests` — ops bool + portal paid-through map.

## Tests run

- `Lazuar.ModuleTests` filter `SubscriptionCancelAtPeriodEndTests|CancelAdminSubscription_|BillingEngineJobTests|PreDunning_FlaggedActiveDueInThreeDays|PreDunning_Minus3Email|CommerceHonestyDtoTests|CancelAdminSubscription_ForeignOrg|KeepAdminSubscription_ForeignOrg` — **41 passed**.
- `Lazuar.ModuleTests` filter `DunningEngineJobTests|SubscriptionRecoveryTests|CommerceProductCompletenessTests.CancelAdmin` — **64 passed**.
- `npx tsc --noEmit -p apps/lazuar-portal/tsconfig.json` and `apps/lazuar-ops/tsconfig.json` — clean.

Not committed. Not pushed.

Tracker `LP-056` can be marked **Y** when a reviewer agrees G1–G5 landed (schedule silent, billing finalize, immediate kept, undo, honest portal paid-through).
