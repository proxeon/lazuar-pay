# W1-LP-123 — done

PDPA buyer wipe is a product loop, not only a domain method. Ops **Subscribers → Anonymize** (confirm modal, shows email) POSTs ` /admin/commerce/subscribers/{id}/anonymize`. Commerce resolves the subscription, scrubs `TransactionLogs` name/email for that org + pre-wipe address, then sends `AnonymizeClientProfileCommand`. CRM no-ops if the profile is already `deleted_*@localhost`. Outbox still carries the **pre-wipe** email so Commerce cancels remaining subs and Communications suppresses as `ANONYMIZED`.

CRM stays HTTP-less. No buyer self-serve delete. LHDN / ledger amounts stay. Second click is 200.

Tracker `LP-123` Lazuar **P → Y**.

## Files changed

### CRM

- `Modules/CRM/Domain/ClientProfileEntity.cs` — `IsAnonymized` / `IsAnonymizedEmail`.
- `Modules/CRM/Infrastructure/AnonymizeClientProfileCommandHandler.cs` — `IgnoreQueryFilters` + org match; already-dummy returns without publish.
- `Modules/CRM/README.md` — merchant trigger is Commerce admin POST.

### Commerce

- `AnonymizeSubscriberCommand` + handler — tenant-bound subscription; scrub logs while CRM still has the real email; then CRM command.
- `CommerceTransactionLog.Anonymize(profileId)` — `Anonymized User` / `deleted_{id}@localhost`; amounts unchanged.
- `ICommerceRepository` / `CommerceRepository.GetTransactionLogsByCustomerEmailAsync`.
- `SubscriberEndpoints` — `POST .../subscribers/{id}/anonymize` → 200 `{ status: "anonymized" }`; 404 unknown / foreign sub.

### HTTP / spec / UI

- TypeSpec `admin-routes.tsp` + generated OpenAPI / `api-types-ts`.
- `apps/lazuar-ops/.../SubscribersPage.tsx` — destructive confirm, then POST; panel shows dummy name/email and `CANCELED`.
- Portal privacy §4 — “Creators can anonymize a buyer from Subscribers → Anonymize.”

### Tests

- `ClientProfileAnonymizedEventTests` — wipe + dummy detector; handler happy path persists outbox with pre-wipe email; already anonymized no second event; wrong org throws.
- `AnonymizeSubscriberCommandHandlerTests` — log scrub org-scoped; already dummy skips scrub; wrong org / missing sub; Commerce consumer cancels matching ACTIVE/PAST_DUE only and publishes `SubscriptionCanceled`.
- `ClientProfileAnonymizedSuppressionTests` — `ANONYMIZED` / `gdpr_client_profile_anonymized`; dummy and empty email skip.
- `CrossTenantIdorTests.AnonymizeSubscriber_ForeignOrg`; `CommerceEndpointsAuthorizationTests` OrgAdmin on the POST.

## Tests run

- `Lazuar.ModuleTests` filter `ClientProfileAnonymized|AnonymizeSubscriber|AnonymizeSubscriber_ForeignOrg|MapCommerceEndpoints_AnonymizeSubscriber` — **18 passed**.
- `Lazuar.ArchitectureTests` — **14 passed**.
- `Lazuar.ModuleTests` filter `CrossTenantIdorTests|CrmOutboxInboxRegistrationTests` — **9 passed**.
- `node scripts/check-openapi-minimal-honesty.mjs` — OK (135 OpenAPI, 142 Minimal, 7 impl_only).
- `npx tsc --noEmit -p apps/lazuar-ops/tsconfig.json` — clean.

Manual §5.3 (checkout → Ops Anonymize → receipt skip) **not run** here.

Not committed. Not pushed.
