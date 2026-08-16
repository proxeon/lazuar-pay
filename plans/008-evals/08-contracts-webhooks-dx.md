# 08 — API contracts, webhooks, and integrator DX after Waves 1–4

**Date:** 16 August 2026  
**Branch:** `feat/007-waves-1-4-implement` (parent: [008 README](./README.md))  
**Slice:** TypeSpec / OpenAPI / Minimal API honesty, outbound event catalog, One dispatcher, VitePress vs Scalar vs Ops picker vs `/lhdn/webhooks`, Idempotency-Key, M2M subscription admin, `examples/hub-cashier-next`, envelope vs generated DTOs.

This report re-reads the tree. It does **not** treat `plans/007-feats` tracker cells as truth. Wave 1–4 “done” notes are used only as a map of intended tickets (`LP-135`, `LP-137`, `LP-142`, `LP-144`, `LP-054`) and then checked against code.

Honesty numbers below come from a live compile of TypeSpec (`pnpm --filter @repo/api-spec build`) followed by `node scripts/check-openapi-minimal-honesty.mjs --verbose` on this workstation. Combined OpenAPI after that compile: **149** operations. Minimal scrape: **160**. Allowlist `impl_only`: **7**. `openapi_only_exceptions`: **0**. The gate **failed**.

---

## 1. What this slice is after Waves 1–4

Integrator-facing surface after the wave set is **three product lines plus one dispatcher**, not one Stripe-shaped API:

| Product | Integrator write path | Outbound events | Auth |
|---------|----------------------|-----------------|------|
| Payments cashier | `POST /api/v1/integrations/payments/checkouts` | `payment.completed`, `payment.failed` | `sk_` + `payments.checkouts:*` |
| Commerce CaaS | Public buy links `/public/commerce/*`; optional M2M list/get/cancel | `subscription.*`, `order.completed`, `payment_link.paid` | Public + magic token; M2M needs `commerce.subscriptions:*` |
| LHDN | `POST /lhdn/documents` + poll | `invoice.valid`, `invoice.invalid` | `sk_` + `lhdn.documents:*` |
| Delivery | One `WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob` | Same envelope for every family | `whsec_` HMAC `t=,v1=` |

The architectural lock is still **one dispatcher in One** (maintenance 00.2). Commerce, Payments, and Lhdn publish `OutboundWebhookRequestedIntegrationEvent` with `TargetUrl: null`. They do not own a second signing stack. Fire-and-forget `WebhookSenderService` is gone from the tree (grep for `IWebhookSenderService` returns nothing).

That lock is real. The honesty holes that remain are **documentation and contract wiring**, not a second dispatcher.

---

## 2. TypeSpec vs Minimal API honesty

### 2.1 Pipeline as designed

TypeSpec is still the declared SSoT (`docs/architecture-decision-log/005-typespec-api-contract-generation.md`). The live pipeline:

1. Author in `packages/api-spec/**/*.tsp`.
2. `task gen` → `gen:spec` (`pnpm --filter @repo/api-spec build`) compiles `main.tsp` plus six product entrypoints (`docs-one`, `docs-ops`, `docs-billing`, `docs-lhdn`, `docs-commerce`, `docs-payments`) into gitignored `packages/api-spec/dist/**/openapi.yaml` (`packages/api-spec/package.json` `build` script, lines 5–6).
3. `gen:types-ts` writes `packages/api-types-ts/src/index.ts`.
4. `gen:types-dotnet` writes `packages/api-types-dotnet/Lazuar.ApiContracts.cs`.
5. `gen:sdk-lhdn` Kiota-generates LHDN SDKs from **product-scoped** `dist/lhdn/openapi.yaml`, not from the combined spec.

CI (`/.github/workflows/ci.yml` job `contracts`, lines 11–52) runs `task gen --force`, then `git diff --exit-code` on generated clients/SDKs, then `node scripts/check-openapi-minimal-honesty.mjs`.

The honesty script (`scripts/check-openapi-minimal-honesty.mjs`) asserts:

- OpenAPI ⊆ Minimal ∪ `openapi_only_exceptions`
- Minimal ⊆ OpenAPI ∪ `impl_only`

Paths are compared relative to `/api/v1`. Host `/health*` is out of scope. The scrape starts at `MapAllModuleEndpoints` (`apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs` lines 65–88) and walks `MapGroup` / `MapGet|Post|Put|Delete|Patch`.

`packages/api-spec/honesty-allowlist.yaml` still has exactly seven `impl_only` rows and an empty `openapi_only_exceptions`. The seven are intentional host-only routes: HMAC billing PDF redirect, inbound payments webhook, messaging notify/logs, communications unsubscribe GET + Resend webhook, templates legacy-cleanup DELETE.

Human companion: `docs/contracts/openapi-vs-minimal-api.md`.

### 2.2 Combined spec vs product-scoped spec (the live split)

`main.tsp` imports commerce **admin** and **public** routes only:

```15:17:packages/api-spec/main.tsp
import "./modules/commerce/models.tsp";
import "./modules/commerce/admin-routes.tsp";
import "./modules/commerce/public-routes.tsp";
```

It does **not** import `modules/commerce/integration-routes.tsp`.

`docs-commerce.tsp` **does** import that file (line 9) and advertises the M2M surface in its service `@doc` (lines 24–27).

Consequence after a fresh `tsp compile`:

- `packages/api-spec/dist/commerce/openapi.yaml` contains `/integrations/commerce/subscriptions`, `/{id}`, `/{id}/cancel` (paths at generated YAML lines 2679, 2763, 2811).
- Combined `packages/api-spec/dist/openapi.yaml` (the file honesty and `task gen` clients consume) has **no** `/integrations/commerce` paths.

W1-LP-137 analysis (`plans/007-feats/impl/W1-LP-137-analysis.md` §4.1) explicitly said to import the new file from `docs-commerce.tsp`. The implementer followed that sentence and never added the same import to `main.tsp`. That is why Scalar Commerce can show a machine API that the honesty gate and the committed TS/C# clients do not know.

### 2.3 Live honesty result (after regenerating TypeSpec)

`node scripts/check-openapi-minimal-honesty.mjs --verbose` after `pnpm --filter @repo/api-spec build`:

```
OpenAPI operations:  149
Minimal operations:  160
impl_only allowlist: 7
openapi_only_ex:     0

UNDOCUMENTED (in Minimal, not in OpenAPI, not allowlisted):
  GET  /integrations/commerce/subscriptions
  GET  /integrations/commerce/subscriptions/{id}
  POST /integrations/commerce/subscriptions/{id}/cancel
  POST /public/communications/unsubscribe
```

The three commerce integration routes are real Minimal maps (`IntegrationSubscriptionEndpoints.cs` 22–94) registered from `MapAllModuleEndpoints` line 77 (`apiGroup.MapCommerceIntegrationEndpoints()`). They are missing from combined OpenAPI because of §2.2.

The fourth miss is `POST /public/communications/unsubscribe` (`PublicComplianceEndpoints.cs` 62–88), RFC 8058 one-click List-Unsubscribe. The allowlist only covers **GET** `/public/communications/unsubscribe` (`honesty-allowlist.yaml` 49–52). GET is the HTML browser link. POST is the mail-client one-click twin. Both exist on the host. Only GET is allowlisted. Neither belongs in product OpenAPI, but honesty does not know that until the POST row exists.

Scraper noise (does not fail the gate): unresolved `SubscriberEndpoints.MapPreview` and `ResendWebhookParser.MapReason`. Those are `Map*` name collisions, not missing HTTP routes.

Stale `dist/openapi.yaml` **before** this compile reported a longer undocumented list (admin change-plan / quantity / collection, portal plans, disputes, workspace audit). Those paths **are** in TypeSpec now (`admin-routes.tsp` 151–176, `public-routes.tsp` 74–88, `one/routes.tsp` 154–159, `admin-routes.tsp` 258–262). They disappear from the undocumented set once spec is rebuilt. The committed generated clients, however, can still be behind TypeSpec — see §2.5.

### 2.4 What is honest

These integrator paths are present in both TypeSpec and Minimal, and the honesty script does not flag them:

- Payments M2M: `POST/GET /integrations/payments/checkouts`, `GET /integrations/payments/me` (`packages/api-spec/modules/payments/routes.tsp` 16–55; `IntegrationEndpoints.cs` 20–130).
- One workspace webhooks: list/create/update/rotate/disable/logs/redeliver (`packages/api-spec/modules/one/routes.tsp` 186–242; `WebhookEndpoints.cs`).
- One provision: `POST /one/integrations/workspaces/provision` (`one/routes.tsp` 281–285).
- LHDN documents + the still-mapped `/lhdn/webhooks` CRUD (`lhdn/routes.tsp` 19–54; `DocumentEndpoints.cs`, `AdminWebhookEndpoints.cs`).
- Public commerce checkout + portal + custom checkout (`public-routes.tsp`; `Endpoints.cs` `publicGroup.MapPublicCommerceEndpoints()`).
- Admin commerce product/subscriber/coupon/stats trees (`admin-routes.tsp`; `MapCommerceEndpoints`).

Allowlisted host-only routes match the documented table in `docs/contracts/openapi-vs-minimal-api.md` 56–67, except the new POST unsubscribe.

`openapi_only_exceptions` is empty. There are no phantom TypeSpec paths in the combined spec after compile.

### 2.5 Generated clients vs TypeSpec source (committed drift)

`packages/api-spec/modules/commerce/models/webhooks.tsp` line 35 status union is:

```text
"ACTIVE" | "PAST_DUE" | "CANCELED" | "SUSPENDED" | "TRIALING"
```

Committed generated TypeScript still omits `TRIALING`:

```2516:2519:packages/api-types-ts/src/index.ts
        "Commerce.SubscriptionWebhookData": {
            subscription_id: string;
            /** @enum {string} */
            status: "ACTIVE" | "PAST_DUE" | "CANCELED" | "SUSPENDED";
```

Committed generated C# enum `SubscriptionWebhookDataStatus` (`Lazuar.ApiContracts.cs` 8057–8071) is `ACTIVE | PAST_DUE | CANCELED | SUSPENDED` only. No `TRIALING` member.

Committed generated TS paths (`packages/api-types-ts/src/index.ts`) include `/integrations/payments/*`, `/lhdn/webhooks`, `/one/workspaces/{id}/webhooks*`, and **do not** include `/integrations/commerce/*`.

CI `contracts` job would:

1. Recompile TypeSpec (Wave 3 `TRIALING` lands in OpenAPI schemas).
2. Regenerate TS/C# (status enum grows; still no M2M commerce paths, because `main.tsp` does not import them).
3. `git diff --exit-code` **fails** on the TRIALING enum delta unless someone already committed a fresh `task gen`.
4. Honesty **fails** on the three M2M commerce maps plus POST unsubscribe.

That is the contracts job as of this evaluation: the Wave 3 status union and the Wave 1 M2M admin API were not closed through the combined-spec / committed-client loop.

### 2.6 Auth scheme honesty (TypeSpec vs runtime)

Commerce admin TypeSpec is `@useAuth(BearerAuth)` on `/admin/commerce` (`admin-routes.tsp` 9–10). Runtime is cookie/JWT `OrgRead` / `OrgMember` / `OrgAdmin` (`Endpoints.cs` 23, 56, 65). A machine `sk_` hitting `/admin/commerce/subscribers` is not the product path; M2M is the new `/integrations/commerce` group. Scalar “Try it” on admin routes will lie about Bearer-as-machine-key.

Payments and LHDN product docs declare `BearerAuth | ApiKeyAuth` so Scalar Try-it accepts a raw `Authorization` header (`docs-payments.tsp` 36–38, `docs-lhdn.tsp` 18–20). Combined `main.tsp` does not restate that at the service level.

`CreateManualSubscriberDto` uses `product_id` (`subscriber.tsp` 104). The old `plan_id` / `source` landmine called out in `docs/contracts/openapi-vs-minimal-api.md` line 89 is still closed.

### 2.7 Models-only modules

`packages/api-spec/README.md` 78–83: CRM and Messaging stay models-only. CRM DTOs exist for generated C# consumption by handlers/tests. Messaging is an empty namespace so `main.tsp` can import the module without phantom routes. That is still true. Superadmin `/platform/*` remains thin (`platform/routes.tsp`: login/logout/me + payment-config only).

---

## 3. Frozen event catalog vs TRIALING

### 3.1 What was frozen (P09 / LP-135)

The frozen **event type** list is five Commerce lifecycle names plus two commerce money names:

```8:15:packages/api-spec/modules/commerce/models/webhooks.tsp
union CommerceSubscriptionEventType {
  "subscription.activated",
  "subscription.resumed",
  "subscription.past_due",
  "subscription.canceled",
  "subscription.suspended",
}
```

`docs-commerce.tsp` 26–27 restates the same freeze in the product OpenAPI description: “Frozen SaaS names (no subscription.updated): subscription.activated|resumed|past_due|canceled|suspended, plus order.completed and payment_link.paid.”

VitePress `apps/lazuar-docs/docs/reference/events.md` is declared the **human SSoT** (`how-to-maintain.md` line 16; W1-LP-135-done). Catalog v1 lists those five plus `order.completed` and `payment_link.paid`. Payments lists `payment.completed` / `payment.failed`. LHDN lists `invoice.valid` / `invoice.invalid`.

There is **no** `subscription.trialing` event type anywhere in TypeSpec, runtime publishers, Ops picker, or VitePress.

### 3.2 What Wave 3 actually added

`W3-LP-054-done.md` is accurate about runtime:

- `Product.TrialDays` / `SetTrialDays` (`Product.cs` 30, 117–129).
- `Subscription.ActivateTrial` sets `Status = "TRIALING"`, `TrialEndsAt`, and both clocks to the trial end (`Subscription.cs` 118–134).
- `SubscriptionActivation.Start` routes recurring products with `TrialDays > 0` through `ActivateTrial` (`SubscriptionActivation.cs` 8–26).
- Zero-amount / trial checkout still publishes `SubscriptionActivatedIntegrationEvent` (`ProcessZeroAmountCheckoutCommand.cs` 61, 91–102). Manual enroll of a trial product does the same when `send_welcome_email` is true (`CreateManualSubscriberCommandHandler.cs` 91–94, 134–142).
- TypeSpec **data** union gained `TRIALING` (`webhooks.tsp` 35).
- Lifecycle handler uses **live** `sub.Status` on `subscription.activated` (`SubscriptionLifecycleIntegrationEventHandlers.cs` 105–106):

```103:106:apps/lazuar-api/Modules/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs
        if (sub != null)
        {
            var payloadStatus = eventType == "subscription.activated" ? sub.Status : status;
            return CommerceWebhookPayload.From(sub, product, email, payloadStatus, isFirstPayment);
```

`HandleAsync(SubscriptionActivatedIntegrationEvent)` still hard-codes the **event type** as `"subscription.activated"` and the fallback status argument as `"ACTIVE"` (lines 31–38). The override on line 105 is what lets a trial start emit:

```text
event_type = subscription.activated
data.status = TRIALING
data.amount = 0   // CommerceWebhookPayload.From line 38
```

`CommerceWebhookPayload.From` zeros amount when `status == "TRIALING"` (`CommerceWebhookPayload.cs` 38).

Billing engine **does not** exclude `TRIALING` (`BillingEngineJob.cs` 133: `NOT IN ('PENDING', 'PAST_DUE', 'SUSPENDED', 'CANCELED')`). A due trial is claimed and converted on the existing vault/mint path. W3-LP-054-done says this explicitly. That means trial end is **not** a new webhook type. Integrators see another `subscription.activated` (recovery/conversion) or `subscription.past_due` if collection fails.

### 3.3 Catalog honesty after TRIALING

VitePress `events.md` **never mentions** `TRIALING`. Grep of `apps/lazuar-docs/**/*.md` for `TRIALING` / `trial_ends_at` is empty.

The activated row still says “First paid period or recovery that lands `ACTIVE`” (`events.md` 46). After Wave 3 that sentence is false for a trial start: first access can land `TRIALING` with amount 0 and no charge.

`how-to-maintain.md` line 16: “update [events.md] in the **same PR** as a new event type.” Wave 3 did not add an event type, so the maintenance rule did not fire. The **payload status union** still changed. The human SSoT was not updated. That is the catalog drift.

Ops subscriber UI knows `TRIALING` (`SubscribersPage.tsx` 344, 397, 574). Integrator docs do not.

Committed generated clients (TS/C#) do not know `TRIALING` either (§2.5). A typed receiver generated from the last committed `task gen` will reject a legal Wave 3 body if it validates the status enum strictly.

`CommerceSubscriptionDto` exposes `trial_ends_at` (`subscriber.tsp` 48; query service maps `TrialEndsAt`). The webhook `data` builder does **not** emit `trial_ends_at`. Integrators who need the trial clock must read M2M GET or wait for conversion.

### 3.4 Tests still describe the five-type world

`SubscriptionLifecycleWebhookTests.cs` 107–111 parametrizes only `ACTIVE | PAST_DUE | CANCELED | SUSPENDED`. There is no case that builds a trial subscription and asserts `data.status == TRIALING` on `subscription.activated`. Domain tests cover `ActivateTrial` (`SubscriptionTrialTests.cs`) and billing non-claim before trial end (`BillingEngineJobTests`). The outbound contract test suite was not extended when the status union grew.

---

## 4. `subscription.updated` is still forbidden

Grep of `*.cs`, `*.tsp`, `*.ts`, `*.tsx` for `subscription.updated` hits only:

- the freeze comment in `webhooks.tsp` line 4
- the product-doc sentence in `docs-commerce.tsp` line 26

No publisher emits it. No Ops checkbox offers it. No VitePress shipped-events row lists it. The not-in-v1 table still names it as explicitly forbidden (`events.md` 72).

Wave 1 cancel-at-period-end (`W1-LP-056-done.md`) and Wave 3 plan/quantity change (`W3-LP-058-analysis.md`) were specified to **not** invent `subscription.updated`. Runtime matches that:

- Period-end cancel flips a flag (`cancel_at_period_end` on the DTO). The outbound type on finalize is still `subscription.canceled`.
- Plan/qty change is scheduled (`pending_product_id`, `pending_quantity`). The next `subscription.activated` (renewal success) or `subscription.past_due` carries the new `product_id`. No extra type.

Aura-shaped receivers that unlock on the five named types are still valid. They now must tolerate `data.status = TRIALING` on `subscription.activated` without inventing a sixth type.

Offline log-payment no longer re-fires `subscription.activated` on every cash payment (`W1-LP-065-done.md`). That is the other half of “do not overload activated as updated.”

---

## 5. Four catalog surfaces (they do not agree)

### 5.1 VitePress `reference/events.md` — declared SSoT

`apps/lazuar-docs/docs/reference/events.md` is the page `how-to-maintain.md` says to update in the same PR as a new type. W1-LP-135-done made it the v1 catalog: envelope, shipped types only, not-in-v1 table.

What it gets right:

- Envelope `{ id, event_type, created_at, data }` (lines 11–19).
- Headers `X-Lazuar-Signature` / `X-Lazuar-Event` / `X-Lazuar-Delivery-Id` / `X-Lazuar-Webhook-Id` (24–29).
- Explicit “do not treat Scalar as the catalog” (line 7).
- Product-family fence: Payments `payment.*` ≠ Commerce `subscription.*` / `order.completed` / `payment_link.paid` ≠ LHDN `invoice.*` (line 33).
- `data` keys for payments named as **runtime builders**, not `PaymentWebhookPayloadDto` (line 31).
- Not-in-v1: `payment.refunded`, `invoice.submitted`, `invoice.cancelled`, `subscription.updated` (67–72).

What it gets wrong or leaves blank after Waves 1–4:

- No `TRIALING` on `subscription.activated` (see §3.3).
- `subscription.activated` copy still says “First paid period … lands `ACTIVE`.”
- `checkout_url` is listed on subscription rows (line 46) and is emitted by `CommerceWebhookPayload` when present (`CommerceWebhookPayload.cs` 117–120), but TypeSpec `SubscriptionWebhookData` has no `checkout_url` field. The human catalog is ahead of the generated DTO and behind runtime on trial.
- LHDN is documented as One-envelope + `invoice.valid` / `invoice.invalid`. That matches dispatcher reality. It does **not** mention the dead `/lhdn/webhooks` register path (see §5.4).

Sidebar (`apps/lazuar-docs/docs/.vitepress/config.ts` 43) puts “Event catalog” under Reference. Nav also links Webhooks (the how-to-verify page), not the catalog. The two pages are correctly split: `integrations/webhooks.md` line 115 says it is not a second catalog.

### 5.2 Scalar (lazuar-developers)

Scalar is product-scoped OpenAPI, not a catalog.

`apps/lazuar-developers/lib/openapi.ts` reads `packages/api-spec/dist/<module>/openapi.yaml` (or `OPENAPI_SPEC_ROOT` in Docker). Routes:

| Hub path | Spec dir | File |
|----------|----------|------|
| `/payments` | `payments` | `app/payments/route.ts` |
| `/commerce` | `commerce` | `app/commerce/route.ts` |
| `/lhdn` | `lhdn` | `app/lhdn/route.ts` |
| `/one` | `one` | `app/one/route.ts` |
| `/billing` | `billing` | `app/billing/route.ts` |
| `/ops` | `ops` | `app/ops/route.ts` |

There is no Scalar page that renders `dist/openapi.yaml` (the combined spec). There is no webhook-event schema explorer. `PaymentWebhookPayloadDto` and `CommerceWebhookEnvelope` exist as **unused models** in the generated OpenAPI components; they are not request/response of any operation. A Scalar user who searches “webhook” on Payments sees a flat DTO that is not the wire body (see §10).

Homepage (`app/page.tsx`) now leads with VitePress guides and labels OpenAPI as “schema, not onboarding” (lines 5–10, 49–51, 77–82). That is the LP-144 correction. Residual Scalar-as-onboarding risks:

- HubShell nav (`app/components/HubShell.tsx` 4–10) is still LHDN-shaped: Hub / Quickstart / Authentication / Event catalog / **LHDN API**. No Payments or Commerce nav item. “Quickstart” is the e-invoice page.
- `/ops` is linked from the homepage under “Reference (advanced)” with a dashed card (`page.tsx` 66–70) — labeled internal, still one click from the hub.
- Commerce Scalar, after `gen:spec`, **does** include `/integrations/commerce/subscriptions` because `docs-commerce.tsp` imports those routes. Combined clients used by Ops (`@repo/api-types-ts`) do **not**. A merchant reading Scalar Commerce can Try-it a path the typed ops client cannot name.

`app/webhooks/page.tsx` is **not** Scalar. It is a hand-written cheat-sheet that banners to VitePress (lines 81–87). That banner is the LP-135 honesty fix. The same page then contradicts the catalog on LHDN (see §5.4).

### 5.3 Ops webhook picker

`DeveloperSettingsPage.tsx` 16–26 is a hardcoded `WEBHOOK_EVENT_OPTIONS` array:

```16:26:apps/lazuar-ops/src/modules/workspace/pages/DeveloperSettingsPage.tsx
const WEBHOOK_EVENT_OPTIONS = [
  { value: "subscription.activated", label: "Subscription activated", hint: "New paid subscription" },
  { value: "subscription.resumed", label: "Subscription resumed", hint: "Recovered from past due / suspend" },
  { value: "subscription.suspended", label: "Subscription suspended", hint: "Dunning final action" },
  { value: "subscription.canceled", label: "Subscription canceled", hint: "Cancel or dunning cancel" },
  { value: "subscription.past_due", label: "Subscription past due", hint: "Renewal failed" },
  { value: "order.completed", label: "Order completed", hint: "One-time purchase" },
  { value: "payment_link.paid", label: "Payment link paid", hint: "Custom payment link settled" },
  { value: "payment.completed", label: "Payment completed", hint: "M2M / integrator checkout paid" },
  { value: "payment.failed", label: "Payment failed", hint: "M2M / integrator checkout failed at gateway" },
] as const;
```

Missing: `invoice.valid`, `invoice.invalid`. There is no `subscription.updated`. There is no `subscription.trialing`. Hint on activated still says “New **paid** subscription.”

Empty selection means all events (`TenantWebhookEndpoint.AcceptsEvent`, lines 74–86: empty list → `true`). The form copy says so (lines 349–351). “Select all” only selects the nine checkboxes (`selectAllEvents` line 182). A merchant who clicks “Select all” thinking they subscribed to everything will **not** receive LHDN invoice events. A merchant who leaves the filter empty **will** receive invoice events if the poller fires. Those two buttons are not equivalent for LHDN.

API create accepts any string array (`CreateWebhookEndpointRequestDto.enabled_events` is `string[]` with no enum). The picker is UI sugar, not a server allowlist. A client can PUT `["invoice.valid"]` even though Ops never shows the box.

Provision default for a new Connect webhook is `payment.completed` + `payment.failed` only (`ProvisionAuraWorkspaceCommandHandler.cs` 33–38). An Aura cashier provisioned with `webhook_url` will not see Commerce lifecycle events unless they empty the filter or add types later. That is correct for the cashier product fence and easy to miss if someone expected “empty = all” from provision.

### 5.4 `/lhdn/webhooks` is a live dead path

This is the worst remaining DX honesty hole.

**Register still writes the Lhdn table.**

```25:32:apps/lazuar-api/Modules/Lhdn/Application/Commands/WebhookCommands.cs
    public async Task<Guid> Handle(RegisterWebhookCommand request, CancellationToken ct)
    {
        var webhook = new WebhookSubscription(request.OrganizationId, request.Payload.Url, request.Payload.Secret);
        
        _repository.AddWebhookSubscription(webhook);
        await _repository.SaveChangesAsync(ct);

        return webhook.Id;
    }
```

Domain `WebhookSubscription` (`Modules/Lhdn/Domain/Aggregates/WebhookSubscription.cs` 19–27) stores `Url`, `Secret`, `IsActive`. It has **no events column**. TypeSpec still requires `events: string[]` on register (`lhdn/models.tsp` 92–96). The array is accepted and discarded.

**List invents the events.**

```129:136:apps/lazuar-api/Modules/Lhdn/Application/Queries/LhdnQueries.cs
        return webhooks.Select(w => new WebhookSubscriptionDto
        {
            Id = w.Id.ToString(),
            Url = w.Url,
            Events = new List<string> { "invoice.valid", "invoice.invalid" },
            Is_active = w.IsActive,
            Created_at = new DateTimeOffset(w.CreatedAt)
        });
```

HTTP maps are live (`AdminWebhookEndpoints.cs` 21–47): `POST/GET /lhdn/webhooks`, `DELETE /lhdn/webhooks/{id}`, OrgAdmin only.

**Dispatch does not read that table.**

`DispatchExternalWebhookCommandHandler` (`DispatchExternalWebhookCommand.cs` 46–72) builds a data-only snake_case object and publishes `OutboundWebhookRequestedIntegrationEvent` with `TargetUrl: null` and `EventType = invoice.{status.ToLower()}`. One fans out to `one.TenantWebhookEndpoints`. Lhdn README §5 (lines 59–71) states this as the locked end-state. Fire-and-forget sender is retired. `lhdn.WebhookSubscriptions` is “not POSTed; table retained.”

So: an integrator who follows TypeSpec, Scalar LHDN, Kiota SDK (`packages/lhdn-sdk-dotnet/src/Generated/Lhdn/Webhooks/WebhooksRequestBuilder.cs`), or the Developers hub LHDN section can persist a row that **never receives a delivery**.

**Developers hub still teaches the dead path.**

`apps/lazuar-developers/app/webhooks/page.tsx` 224–259:

- “register via `POST /lhdn/webhooks` or console when available”
- “emit JSON with `event` derived from document status”
- Callout: “LHDN path currently signs with HMAC-SHA256 hex of the raw body in `X-Lazuar-Signature` (body-only).”

All three sentences are false after R42/R43. Runtime LHDN deliveries are the **same** One envelope and the **same** `t=,v1=` header as Commerce/Payments. The top-level key is `event_type`, not `event`. There is no body-only HMAC left in the tree.

`app/quickstart/page.tsx` line 57 still offers `POST /lhdn/webhooks` as an optional step. Line 129–135 then correctly describes workspace `t=,v1=` signing and links the event catalog. The two sections on one page disagree.

VitePress `events.md` and `integrations/webhooks.md` do **not** recommend `/lhdn/webhooks`. They point at `POST /one/workspaces/{id}/webhooks`. The human SSoT is ahead of the Developers hub and the LHDN SDK.

R41 migrator (`LegacyWebhookSubscriptionMigrator.cs`) copies active Lhdn rows into One with `EnabledEvents = ["invoice.valid","invoice.invalid"]`. New `POST /lhdn/webhooks` after that migrator has run is **not** dual-written. R43 notes explicitly skipped the façade (`plans/005-remaining/r43-notes.md`: “Dual-write of `/lhdn/webhooks` register API: skipped”). The façade is still skipped.

---

## 6. Integration guides vs Scalar dump

### 6.1 What LP-144 shipped

W1-LP-144-done: “How-to starts in VitePress, not Scalar.” That is true of information architecture:

- VitePress home (`apps/lazuar-docs/docs/index.md`) is a guide index: hosted Commerce, Payments cashier, event catalog, who-does-what, sample app. Footer: “Scalar OpenAPI is **schema reference**, not onboarding.”
- Sidebar (`docs/.vitepress/config.ts` 10–48) is Integrations + Reference. Event catalog and “OpenAPI & Scalar” sit under Reference.
- Developers homepage (`app/page.tsx`) leads with external VitePress links, then a “API reference (not onboarding)” grid, then dashed “advanced” cards for One / Billing / Ops.
- Root `docs/payments-integration-quickstart.md` still exists as a long engineer stub (provision, product fences, Aura consume table). VitePress cashier pages are the public twin.

Product-line fence is consistent across VitePress `guide/product-lines.md`, `docs/payments-integration-quickstart.md` §0, Developers payments-cashier page, and `events.md`. Payments / Commerce / LHDN / Paddle-outside-Hub are not collapsed. That is the most important DX win of the wave set.

### 6.2 Guide completeness vs runtime

| Guide | Honest about | Lag / lie |
|-------|----------------|-----------|
| `integrations/hosted-checkout.md` | Six-step CaaS; fulfill on `order.completed` / `subscription.activated`; success URL is not money | No trial copy; activated implied paid |
| `integrations/payments-cashier.md` + `create-checkout.md` + `payment-flow.md` | M2M amount + metadata; Idempotency-Key; BYOK; hop 1 vs hop 2 | Cashier-only; correct |
| `integrations/webhooks.md` | Envelope, verify, 2xx/4xx/5xx, redeliver, registration at provision or One API | Product line is Payments; Commerce/LHDN deferred to catalog |
| `integrations/api-keys.md` | `sk_test_`/`sk_live_`, closed scopes, Payments preset, commerce.subscriptions:* mentioned (line 52) | Introspect is Payments `/me` only; no commerce `/me` |
| `integrations/run-sample-app.md` | Sample on :3020; envelope + data; never unlock on success_url | Status still “draft” |
| `reference/events.md` | Shipped types; not-in-v1 | Silent on TRIALING status |
| Developers `/webhooks` | Banners to VitePress; commerce + payments tables match catalog types | LHDN section is pre-R43 |
| Developers `/quickstart` | LHDN submit + Idempotency-Key + poll | Offers `/lhdn/webhooks`; verify snippet uses `subscription.activated` body on an e-invoice page |
| Developers `/payments-cashier` | Provision + checkout + `t=,v1=` | Points at repo `docs/payments-integration-quickstart.md` as “full algorithm” (line 114) instead of VitePress webhooks |
| Developers `/auth` | Scope table includes commerce.subscriptions:* (lines 31–36) | Prose still says “Closed scope catalog (LHDN, payments, webhooks)” (line 64) — omits commerce |
| Developers README | Create-next-app boilerplate | Not an integrator document |

Scalar `/ops` is still reachable. LP-144 labeled it internal; it did not unmount it. That is acceptable if the homepage copy holds.

Nav on VitePress points “Developers (Scalar)” at `http://localhost:3000` (`config.ts` 77–79). Production publish still needs a real hub URL. Local-only link is honest for this branch, not for a public docs host.

### 6.3 What Scalar dumps that guides do not teach

A new integrator who ignores the homepage and opens `/commerce` Scalar sees the **entire** admin tree: products CRUD, dunning campaigns, coupons, stats, custom checkouts, disputes, subscriber cancel/keep/record-payment/anonymize/change-plan/quantity/collection pause. `docs-commerce.tsp` 29–31 says those are console-only. The OpenAPI document itself does not mark them `x-internal`. Try-it will 401/403 a machine key.

The integrator surface that **is** machine-key is `/integrations/commerce/subscriptions` (list/get/cancel only). It appears in product-scoped Scalar and in `docs-commerce.tsp` prose. It does not appear in combined OpenAPI, committed TS clients, or a dedicated VitePress page. `api-keys.md` mentions the scopes in one sentence. There is no “list a Hub subscription from curl” guide.

Payments Scalar is the cleanest of the six: three operations, matching the cashier guide.

LHDN Scalar includes `/lhdn/webhooks` as first-class operations and Kiota generates a client for them. Guides that still mention that path are Scalar-shaped leftovers, not VitePress SSoT.

---

## 7. Idempotency-Key

Three product families, three policies.

### 7.1 Payments M2M (optional header wins)

TypeSpec: optional `@header("Idempotency-Key")` plus optional body `idempotency_key` (`payments/routes.tsp` 29; `payments/models.tsp` 24–28).

Runtime (`IntegrationEndpoints.cs` 39, 135–144):

1. Non-empty `Idempotency-Key` header.
2. Else body `idempotency_key`.
3. Else null — every call is a new session.

Same key + same fingerprint → replay. Same key + different body → `IDEMPOTENCY_CONFLICT` (409). Docs (`create-checkout.md` 24–32, 120; `docs/payments-integration-quickstart.md`; sample `hub.ts` 88) all teach header-preferred. Sample uses `Idempotency-Key: sample-order-{order.id}` (`app/api/checkout/route.ts` 125).

This is the table-stakes cashier contract. It is shipped and documented.

### 7.2 Public Commerce checkout (optional header, 409 on mismatch)

W1-LP-142-done. TypeSpec documents the header on `POST /public/commerce/checkout` and `POST .../update-payment` (`public-routes.tsp` 46–48, 155–157).

Runtime (`PublicCheckoutEndpoints.cs` 39–41) reads the header only (no body twin). `CommerceCheckoutIdempotency` (`CommerceCheckoutIdempotency.cs`) caps length at 200 and fingerprints tenant + product slug + email + coupon + quantity + session + interval + price. Handler throws `IDEMPOTENCY_CONFLICT` → HTTP 409 (`PublicCheckoutEndpoints.cs` 80–82). Missing header keeps legacy new-session behavior.

This is **not** advertised as loudly as Payments. Hosted-checkout.md does not mention the header. Portal stores a UUID in `sessionStorage` per product (done note). Integrators building their own hop-1 form must discover the header from TypeSpec or the command handler.

### 7.3 LHDN submit (header required)

TypeSpec: `@header("Idempotency-Key") idempotencyKey: string` — required (`lhdn/routes.tsp` 22).

Runtime (`DocumentEndpoints.cs` 29–32): empty header → 400 “Idempotency-Key header is required for SDK submissions.”

.NET SDK `LhdnClientFactory.IdempotencyHandler` (`packages/lhdn-sdk-dotnet/src/LhdnClientFactory.cs` 14–24) auto-mints `Guid.NewGuid()` on every POST that lacks the header. That is good for naive retries of a **new** client instance and bad if the caller wanted a semantic key (`internal_id`) and forgot to set one: each retry without an explicit header is a **new** MyInvois document. The TS SDK does not have the same auto-header (W1 inventory called this out; still true of the factory file we read).

Quickstart curl (`app/quickstart/page.tsx` 117–121) and `docs-lhdn.tsp` line 15 both say to send the header. That is the correct teaching. The .NET auto-GUID is a footgun next to that teaching.

### 7.4 What is still not idempotent at the HTTP layer

There is no shared `IIdempotencyStore` middleware. Each family owns its table/fingerprint. M2M subscription **cancel** is not idempotent in the Payments sense: already-canceled returns 400 `ALREADY_CANCELED` (`IntegrationSubscriptionEndpoints.cs` 80–83), not a replay 200. Webhook **create** is URL-idempotent (same normalized URL returns the existing row without reminting; `one/routes.tsp` 196). Webhook **redeliver** clones a new outbox row. Those are documented in `integrations/webhooks.md` 145–151 and 239–251.

`POST /integrations/commerce/subscriptions` does not exist (no enroll). There is nothing to idempotency-key there.

---

## 8. M2M subscription admin API

### 8.1 What shipped (LP-137)

Runtime group `/api/v1/integrations/commerce` (`IntegrationSubscriptionEndpoints.cs` 16–97):

| Method | Path | Policy | Behavior |
|--------|------|--------|----------|
| GET | `/subscriptions?page&limit&status` | `IntegrationCommerceSubscriptionsRead` | Reuses `GetSubscribersAsync`; optional in-memory status filter |
| GET | `/subscriptions/{id}` | Read | `GetSubscriberByIdAsync`; 404 if missing |
| POST | `/subscriptions/{id}/cancel` | `IntegrationCommerceSubscriptionsWrite` | Immediate `CancelAdminSubscriptionCommand(..., AtPeriodEnd: false)`; 400 if already canceled |

Scopes (`PlatformApiScopes.cs` 24–26, 50–51, 140–142):

- `commerce.subscriptions:read`
- `commerce.subscriptions:write` (write implies read in the read policy, `AuthAndCorsExtensions.cs` 173–181)

Human `ADMIN` / `SUPER_ADMIN` also pass both policies (same file 167–169, 177–178). Payments-only keys 403 (`ApiKeyAuthenticationTests.cs` 484).

Ops API Keys UI has a “Commerce subscriptions” group (`ApiKeysPage.tsx` 41–46). Developers auth table lists both scopes (`app/auth/page.tsx` 31–36). VitePress `api-keys.md` line 52 mentions them. Default Payments integrator preset does **not** include them (`ApiKeysPage.tsx` 58–62; `DefaultAuraIntegratorScopes` is payments + webhooks only).

DTO is the full admin `CommerceSubscriptionDto` (email, phone, vault ids, dunning, trial_ends_at, pending plan). LP-137 analysis accepted CRM-heavy fields for a server key. Vault token ids ride along. That is more than a “subscription status” API.

### 8.2 Contract honesty hole (repeats §2)

TypeSpec file exists (`integration-routes.tsp` 9–35) and is imported only by `docs-commerce.tsp`. Combined OpenAPI, honesty, and committed `@repo/api-types-ts` do not contain the three paths. Scalar Commerce (after spec gen) does.

CI `contracts` job will fail honesty until `main.tsp` imports `integration-routes.tsp` (or the routes are allowlisted — they must not be; they are product routes).

### 8.3 Behavioral caveats

**Status filter is not a SQL predicate.** `GetSubscribersAsync` loads every non-`PENDING` subscription for the org (`CommerceQueryService.Subscribers.cs` 52–74, `Status != 'PENDING'`), paginates in the service, then the endpoint **re-filters the current page** (`IntegrationSubscriptionEndpoints.cs` 37–42) and **replaces `total_count` with the filtered page size**. `GET ?status=TRIALING&page=1` can return a short page and a lying total. `?status=ACTIVE` on a tenant whose first page is mixed will under-count.

**Cancel is immediate only.** TypeSpec doc says “Immediate cancel (same as admin cancel without at_period_end)” (`integration-routes.tsp` 31). Period-end cancel (`LP-056`) is not exposed on M2M. Already canceled is 400, not 200.

**No get-by-customer-email, no list-by-product, no enroll, no refund, no plan change.** Out of v1 lock. Integrators who need those stay on Ops JWT or wait for webhooks.

**List excludes `PENDING`.** A checkout that has not activated will not appear. `TRIALING` will appear (it is not `PENDING`).

---

## 9. `examples/hub-cashier-next`

This is the only first-party integrator sample. It is **Payments-only**. It does not call Commerce or LHDN.

### 9.1 What it proves (honest)

README (`examples/hub-cashier-next/README.md` 11–21) and VitePress `run-sample-app.md` 9–15 agree:

- Server-side `POST /integrations/payments/checkouts` with Bearer `sk_` and `Idempotency-Key`.
- Redirect to hosted `checkout_url`.
- Unlock only after verified `payment.completed`.
- Runtime envelope `{ id, event_type, created_at, data }`.
- No gateway SDKs; no `@repo/api-types-ts`.
- Port **3020**. Hub base includes `/api/v1`.

`lib/hub.ts` 83–89 sends `Authorization`, `Content-Type`, `Idempotency-Key` and a snake_case body. It does **not** send body `idempotency_key` (header-only, which is the preferred path).

`lib/webhook-verify.ts` 1–9 names `OutboundWebhookSignature.cs` as SSoT. Parse / compute / verify mirror `t=,v1=` over `{t}.{rawBody}`, full `whsec_` as UTF-8 key, 300s skew, constant-time hex compare.

`lib/types.ts` 30–51 defines `HubWebhookEnvelope` + `PaymentWebhookData` and comments “not flat TypeSpec gap.” That is the most honest client type in the monorepo.

`app/webhooks/hub/payments/route.ts`:

- `request.text()` first (line 33) — never `json()` before HMAC.
- 401 on bad signature (36–38).
- Dedupes `X-Lazuar-Delivery-Id` (53–59).
- `payment.failed` marks failed, never unlocks (62–65, 139–151).
- Unknown events ACK 200 (68–71) so Hub stops retrying.
- Resolves order via `data.metadata.order_id` then `data.checkout_id` (74–78).
- Already-paid and same `gateway_transaction_id` return 200 (89–113).
- Missing order → 422 (80–86), which Hub treats as permanent (`IsPermanentHttpFailure` is any 4xx, `OutboundWebhookDispatcherJob.cs` 232–233).

`app/api/checkout/route.ts` 125–132 stamps `metadata.order_id` and `type: sample_order`. Success page is documented as poll-only.

Scripts: `scripts/test-webhook-verify.mjs` (unit vectors), `scripts/send-fake-webhook.mjs` (dev-only).

### 9.2 What it does not prove

- Commerce `subscription.*` / trial / M2M list.
- LHDN invoice events.
- Typed OpenAPI client.
- Multi-instance store (file `.data/` + in-process delivery Set).
- Hop 1 (gateway → Hub) without a tunnel.

That scope lock is correct. The sample is the golden path for the **cashier** product. It is not a substitute for a Commerce SaaS receiver.

### 9.3 DX friction around the sample

- `apps/lazuar-developers/README.md` is still the create-next-app stub. It does not mention the sample or VitePress.
- Developers payments-cashier page points at `docs/payments-integration-quickstart.md` and “script/second-app-proof.md was removed” (`payments-cashier/page.tsx` 127–134). VitePress `run-sample-app.md` and `second-app-checklist.md` are the living pages; the hub page does not link them as URLs.
- Sample package is excluded from product turbo (`examples/README.md` referenced from cashier README). That is intentional so `pnpm build` of the product apps does not typecheck the toy.

---

## 10. Envelope `{id,event_type,created_at,data}` vs generated DTOs

### 10.1 Runtime envelope (one builder)

Every family lands in the same wrap:

```55:61:apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs
        var jsonPayload = JsonSerializer.Serialize(new
        {
            id = Guid.CreateVersion7().ToString(),
            event_type = @event.EventType,
            created_at = DateTime.UtcNow,
            data = @event.Payload
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
```

`@event.Payload` is already a snake_case `JsonElement` from the publisher. The wrap is **not** a TypeSpec model the dispatcher instantiates. `id` is a new uuid v7 per fan-out (shared across endpoints in one handle call — one serialize, many outbox rows). Redeliver clones the stored JSON; envelope `id` is unchanged; `X-Lazuar-Delivery-Id` is new (`integrations/webhooks.md` 249; One routes doc on redeliver, `one/routes.tsp` 237–238).

Publishers:

| Family | Builder | EventType | `data` |
|--------|---------|-----------|--------|
| Commerce lifecycle | `CommerceWebhookPayload.From/Build` | `subscription.{activated,resumed,past_due,canceled,suspended}` | subscription_id, status (live on activated), current_period_end, customer_id, client_profile_id, email, product_id, amount (0 if TRIALING), currency, interval, is_first_payment?, metadata?, checkout_url? |
| Commerce order | `OrderCompletedIntegrationEventHandler` | `order.completed` | order_id, client_profile_id, product_id, status, quantity |
| Commerce custom link | `GatewayPaymentCompleted…OpenCheckout.cs` 37–54 | `payment_link.paid` | amount, currency, gateway_transaction_id, status, checkout_session_id, client_profile_id |
| Payments cashier | `IntegrationCheckoutGatewayEventsHandler.BuildPayload` 195–219 | `payment.completed` / `payment.failed` | event_id, checkout_id, gateway, gateway_transaction_id, provider_session_id, amount, currency, status, metadata, description, customer_email |
| LHDN | `DispatchExternalWebhookCommandHandler` 54–65 | `invoice.valid` / `invoice.invalid` | internal_id, lhdn_uuid, status, qr_link, error_message |

`OutboundWebhookRequestedIntegrationEvent` (`Modules/Commerce/Contracts/Events/OutboundWebhookRequestedIntegrationEvent.cs` 13–17) is the only cross-module request shape: `OrganizationId`, `TargetUrl?`, `EventType`, `Payload`. TargetUrl is unused as a gate (handler comment lines 29–30: “No product-URL equality gate”).

### 10.2 TypeSpec models that are not the wire

**`CommerceWebhookEnvelope`** (`webhooks.tsp` 21–26) is the right **shape**: `id`, `event_type`, `created_at`, `data`. Comments say “doc model — not a POST body” and “Runtime dispatcher wraps `data`.” It is not referenced by any HTTP operation. It is therefore a schema island. After `task gen` it appears in `components.schemas` for anyone who looks. Scalar will not attach it to a path.

Gaps vs runtime even as a doc model:

- `event_type` union is the five subscription types only. It cannot describe `order.completed` or `payment_link.paid`.
- `data` has no `checkout_url`, no `trial_ends_at`.
- `data.status` includes `TRIALING` in TypeSpec source; committed generated clients do not.

**`PaymentWebhookPayloadDto`** (`payments/models.tsp` 54–66) is a **flat** object: `event_id`, `event_type`, `checkout_id`, `workspace_id`, `amount`, … at the top level. Runtime is nested under `data` and does **not** emit `workspace_id` or `occurred_at`. It **does** emit `provider_session_id`, `description`, `customer_email`, `gateway`. VitePress `events.md` line 31 and the sample `types.ts` 38 explicitly warn that this DTO is not the wire. The TypeSpec comment on the model (lines 50–52) still claims it is the “Outbound payment.* webhook envelope.” That comment is false.

A generated C# `PaymentWebhookPayloadDto.FromJson(rawBody)` against a live delivery will not bind `data.*`. `event_type` might bind if someone flattens; `checkout_id` will be default. This is the highest-severity **typed-client** lie on the Payments product.

There is no `InvoiceWebhookEnvelope` in TypeSpec. LHDN `data` fields live only in VitePress and in the C# anonymous object.

`WebhookDeliveryLogDto` (`one/models/webhook.tsp` 54–61) is honest about shallowness: id, event_type, status, attempt_count, last_error, created_at. No payload, no URL, no HTTP status. Matches `IOneQueryService.GetWorkspaceWebhookLogsAsync` last-50 behavior.

### 10.3 Headers vs body

Dispatcher sets (`OutboundWebhookDispatcherJob.cs` 101–105):

- `X-Lazuar-Signature: t={unix},v1={hex}` (`OutboundWebhookSignature.ComputeHeaderValue`, signed material `{unix}.{rawBody}`, key = full decrypted `whsec_…`)
- `X-Lazuar-Event` = outbox `EventType`
- `X-Lazuar-Delivery-Id` = outbox row id
- `X-Lazuar-Webhook-Id` = endpoint id

Verify (`OutboundWebhookSignature.TryVerify`, lines 37–69): parse `t`/`v1`, optional 300s skew, recompute, fixed-time hex compare. Prefix `whsec_` is **not** stripped (`ResolveSigningSecret` 221–228; leftover plaintext `whsec_` rows are lazy-encrypted).

Developers LHDN callout that still describes body-only hex is describing a deleted algorithm.

### 10.4 Fan-out, retries, permanence

`AcceptsEvent`: empty filter = all types, including `invoice.*` and future additive names (`TenantWebhookEndpoint.cs` 74–86). Versioning policy (`docs/api-versioning.md` 15) says adding event types is non-breaking for empty filters and opt-in for named filters.

Dispatcher claims with `FOR UPDATE SKIP LOCKED` (relational) or in-memory take-50 (`OutboundWebhookDispatcherJob.cs` 155–214). 2xx → success. 4xx → `RecordPermanentFailure` (no retry). 5xx/transport → `RecordFailure` with backoff, max 5, then FAILED. Redeliver clones PENDING. This matches VitePress `integrations/webhooks.md` 73–80.

Sample ACK of unknown events as 200 is the right receiver policy for empty-filter endpoints that will start seeing additive types.

---

## 11. One dispatcher (end-to-end)

Path today:

```
Commerce / Payments / Lhdn publisher
  → OutboundWebhookRequestedIntegrationEvent (TargetUrl null)
  → module outbox (CommerceEventBus / PaymentsEventBus / LhdnEventBus)
  → One OutboundWebhookEventHandlers
  → one.WebhookDeliveryOutboxes (one JSON body per matching endpoint)
  → OutboundWebhookDispatcherJob (10s loop, lease, HMAC, POST)
  → integrator URL
```

Second-hop persist for Commerce lifecycle is committed (`SubscriptionLifecycleIntegrationEventHandlers` `SaveChangesAsync` after publish, line 85; `W0-LP-132-done.md`). Payments cashier handler saves the session after publish (`IntegrationCheckoutGatewayEventsHandler.cs` 86, 126). Lhdn publish rides the poller’s `LhdnDbContext` save.

Silent URL-match drop is gone. The remaining silent skip is “no active endpoint” or “no endpoint accepts this event,” both logged (`OutboundWebhookEventHandlers.cs` 35–52).

There is still no `Modules/Webhooks` extract. That is the 00.2 lock, not a gap.

LHDN registry dual-write / façade is the open product follow-on, not a dispatcher follow-on.

---

## 12. Residual honesty map (this slice only)

Severity is “would a careful integrator build the wrong thing from a first-party surface.”

| ID | Surface | Lie / lag | Integrator impact |
|----|---------|-----------|-------------------|
| H1 | `POST /lhdn/webhooks` + LHDN Scalar + Kiota + Developers LHDN section | Register writes a table dispatch does not read; signing docs still say body-only hex; payload key `event` vs `event_type` | ERP follows SDK, never gets `invoice.valid` |
| H2 | `main.tsp` omits `integration-routes.tsp` | M2M list/get/cancel is live but undocumented in combined OpenAPI / committed clients / honesty | CI contracts red; typed TS client cannot name the path; Scalar Commerce (product spec) can |
| H3 | `PaymentWebhookPayloadDto` | Flat DTO labeled as the outbound envelope | Generated `FromJson` misses `data.*` |
| H4 | VitePress catalog + committed generated status enum vs Wave 3 | Runtime `data.status=TRIALING` on `subscription.activated`; catalog and committed clients still four statuses | Strict enum clients 422; unlock logic that requires ACTIVE will skip trials |
| H5 | Ops picker vs empty filter | “Select all” ≠ “all events”; no `invoice.*` checkboxes | LHDN-only merchants either get everything or nothing, depending which button they click |
| H6 | Developers `/webhooks` + `/quickstart` | Pre-R43 LHDN copy next to a VitePress banner | Two truths on one hub |
| H7 | `POST /public/communications/unsubscribe` | Host exists; not in OpenAPI; not in allowlist | Honesty fail only; not an integrator path |
| H8 | TypeSpec Commerce admin `@useAuth(BearerAuth)` | Runtime is OrgAdmin cookie | Scalar Try-it on admin routes |
| H9 | M2M `?status=` | Filter after paginate; total_count rewritten | Wrong pages / counts |
| H10 | .NET LHDN SDK auto `Idempotency-Key` GUID | Semantic retry key easy to drop | Duplicate MyInvois submits |
| H11 | `CommerceWebhookEnvelope` island | Right shape, unused by any operation; missing order/payment_link types and checkout_url | No generated receiver type that matches all Commerce deliveries |
| H12 | VitePress Scalar nav `localhost:3000` | Fine locally; wrong if docs are published | Broken “API” menu in prod |

`subscription.updated` is **not** on this list. It is still correctly absent.

`payment.refunded` is correctly in the not-in-v1 table. Refunds exist in Ops/adapters; no outbound event. Docs that say “refunds maturing” (`integrations/webhooks.md` 106) match runtime.

---

## 13. What “done” meant on the DX tickets (re-checked)

| Ticket | Done note claim | Live |
|--------|-----------------|------|
| LP-135 | VitePress catalog v1; Developers banners to it; no submitted/cancelled as live | Catalog exists and is the best SSoT. Developers still lists only valid/invalid as LHDN types (good) but teaches the wrong register/signing (bad). TRIALING never landed on the catalog. |
| LP-137 | M2M list/get/cancel + scopes + Ops catalog | Runtime and Ops UI yes. Combined TypeSpec / clients / honesty no. |
| LP-142 | Public checkout Idempotency-Key + 409 | Yes on public commerce. Payments already had it. LHDN required-header already had it. |
| LP-144 | Guides first, Scalar labeled reference | VitePress IA and Developers homepage yes. HubShell nav and LHDN Scalar dump still pull toward the old hub. |
| LP-054 | Trial + status union includes TRIALING | Domain and TypeSpec source yes. Catalog, Ops webhook hint, committed generated enums no. Event type correctly unchanged. |

Wave 4 tickets in this slice (`LP-155` WhatsApp, etc.) do not change contracts or the dispatcher.

---

## 14. Evidence anchors (absolute)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/main.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/docs-commerce.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/integration-routes.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/models/webhooks.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/payments/models.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/honesty-allowlist.yaml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/check-openapi-minimal-honesty.mjs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ci.yml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/TenantWebhookEndpoint.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/DispatchExternalWebhookCommand.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/WebhookCommands.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/AdminWebhookEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/IntegrationSubscriptionEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/CommerceWebhookPayload.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/reference/events.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/integrations/webhooks.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/app/webhooks/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/DeveloperSettingsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next/lib/types.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next/app/webhooks/hub/payments/route.ts`

---

## 15. Close

After Waves 1–4 the **runtime** integrator contract is one signed envelope, one dispatcher, a frozen **event-type** catalog that still forbids `subscription.updated`, and a real Payments cashier sample that verifies the envelope correctly.

The **paper** contract is split across too many SSoTs that were not all updated when Wave 1 added M2M commerce routes, Wave 3 added `TRIALING` as a **status**, and R43 moved LHDN onto that same envelope. Combined TypeSpec / honesty / committed generated clients are behind Minimal API. Developers-hub LHDN copy is behind the dispatcher. VitePress catalog is behind trial. Ops picker is behind invoice events.

`subscription.updated` is not the problem. The problem is three live surfaces (`POST /lhdn/webhooks`, flat `PaymentWebhookPayloadDto`, `main.tsp` without `integration-routes.tsp`) that still describe a product the host no longer is.

This file is analysis only. No code was changed except a local TypeSpec compile used to measure honesty (gitignored `packages/api-spec/dist/`).
