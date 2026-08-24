# 10 — Honesty, ranked gaps, sequencing, refuse, next ten

**Date:** 24 August 2026  
**Slice:** Cross-cut of the new stack after Bar B code landed. What we may say today. What we must not say. What is actually next.  
**Kind:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) or [011/12](../011-new-lazuar-pay/12-first-slice-tracker.md) Status cells. **Not** a flip of [013/checklists/b99-bar-b-done.md](../013-prods/checklists/b99-bar-b-done.md). **Not** Hub parity. **Not** a project reference into `apps/lazuar-api`.

Parent index: [README.md](./README.md). Binding: [011](../011-new-lazuar-pay/README.md), [012](../012-one-to-pay/README.md), [013](../013-prods/README.md). 013 papers and 008 papers are **historical**. Live files on this SHA are authority when they disagree.

---

## Coordinates (this write)

| Field | Value |
|-------|--------|
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `main` (`.git/HEAD` → `ref: refs/heads/main`) |
| HEAD | `ee2db8e5758305089a38298456c456d6bf0e97ca` |
| `git log -1 --oneline` | `ee2db8e5 feat(pay): Bar B receipts, webhook secret, merchant money UI` |
| Subject (COMMIT_EDITMSG) | Verify Stripe webhooks with `Pay:StripeWebhookSecret` (not BYOK `sk_`), list receipts, auto-seed SST unknown as unregistered, catch a bad org key on public start. Merchant workspace can paste keys, mint a MYR pay link, and show payments. Webhook replay is a no-op. |
| 013 papers’ analysis SHA | `6f866ff0` — `feat(pay): scaffold merchant and checkout Vite apps` on `feat/012-connect-one` |
| Isolation | Holds. IsolationTests still ban `lazuar-api` / `Modules.` / `BuildingBlocks` / `MediatR`. Host csproj has **no** `ProjectReference` into Hub. |

`feat/013-bar-b` and `main` both point at the same SHA. The Bar B implementation was fast-forwarded onto `main` (`merge feat/013-bar-b: Fast-forward` in `.git/logs/refs/heads/main`).

### Recent history for the three new apps

Reconstructed from `.git/logs/refs/heads/feat/013-bar-b` and `.git/logs/refs/heads/feat/012-connect-one` (the commits that actually touched `apps/lazuar-pay`, `apps/lazuar-pay-merchant`, `apps/lazuar-pay-checkout`). Newest first:

```text
ee2db8e5 feat(pay): Bar B receipts, webhook secret, merchant money UI
f9f4779b feat(pay): D16 Initial PayDbContext EF migration
0f62e996 feat(pay): D–Q Bar B host, public pay, Stripe, fulfill, CI
f95916a5 feat(pay-merchant): M13–M27 OIDC PKCE shell on :5178
d7cf5262 feat(pay-merchant): M12/M21 pickApiBearerToken never id_token
d847e507 feat(pay-merchant): M11 public OIDC env example
4bfac874 feat(pay-merchant): M10 register SPA via One apps API
06c87015 docs(013): B00 freeze Bar B — Stripe rail, one PayDbContext
faf9fe47 docs(013): add Bar B implementation checklists
2ae8d5b6 docs(plans): add 013 production-ready replace analysis
6f866ff0 feat(pay): scaffold merchant and checkout Vite apps
1bd9f338 feat(pay): fixture POST/GET /v1/checkouts with One member gate
18e10d6f docs(012): C99 connected — whoami and org ready shipped
e466a2fe feat(pay): C20-C24 org ready via One authz/check member
811be438 docs(pay): C19 whoami runbook — One on 8080, Pay on 8081
a35a0334 feat(pay): C18 add whoami and org ready to pay-spec
e47ed381 test(pay): C17 widen isolation scans to src and test csproj
9b8a935b test(pay): C15-C16 hermetic whoami and health isolation
83a36dac feat(pay): C13-C14 GET /v1/whoami forwards Bearer to One /me
c30a11fa feat(pay): C12 map One /me JSON to Pay whoami DTO
47589733 feat(pay): C11 register typed HttpClient for One
e938e4a7 feat(pay): C10 bind One BaseUrl and timeout options
56f45080 docs(012): freeze C00 One-to-Pay connect checklists
6ca8f19f feat(pay): add TypeSpec package for the focused Pay host
b536993a feat(pay): scaffold focused Pay host on 8081
```

013 analyses froze at `6f866ff0`. Everything from `4bfac874` through `ee2db8e5` is **after** those papers. That is why 013 still says “no Postgres, no Stripe, health-probe Vite.” The papers were honest on their SHA. They are **wrong as a description of today**.

---

## 0. Standing law this paper will not reverse

Copied so a later 014 parent cannot “clarify” them into Hub Payments.

| Lock | Meaning on this SHA |
|------|---------------------|
| Do not sell Hub parity | 011 dogfood sentence on **8081 + 5178 + 5179** is the bar. Ops 25 routes / 152 TypeSpec ops / 784 module files are the museum. |
| Do not say we file MyInvois | Tax later = a **provider**. Receipt is Official Receipt `RCPT-…`. Never VALID. Never Tax Invoice. |
| Do not say five adapters on new Pay | Live adapter set is **one** class: `StripeHosted`. Capability string is `hosted_link`. CHIP / Billplz / Razorpay / Xendit are **not** in `apps/lazuar-pay`. |
| Stripe is hosted Checkout `mode=payment` | Not Stripe.js card element. Not Stripe Billing. Not off-session. Not Connect. |
| Isolation holds | No `ProjectReference` to `apps/lazuar-api`. No MediatR. No `Modules.*`. No `BuildingBlocks`. Vite apps do not depend on `@repo/api-types-ts`. |
| 011 Status cells are stale in both directions | Some `todo` while code exists (Bar B). The ten `done` cells from 012 C99 are still true as *existence*; several **Notes** still say “fixture.” 013 checklists overclaim tests that are not in the tree. **This paper does not flip cells.** |
| 013 papers at `6f866ff0` are stale | They said no DB, no Stripe, merchant/checkout are health probes, host csproj has zero PackageReference. All of that was true then. None of it is the live host. |
| Buyers are not Zitadel humans | Checkout `:5179` has no `oidc-client-ts`. |
| Listen **8081**, never 8080 | `launchSettings.json` still `http://localhost:8081`. Root `docker-compose.yml` still boots Hub `lazuar-api` on **8080**. |
| Refuse list stays refuse | All 24 `NP-XX-*` rows. Deleting them is how the museum comes back. |

---

## 1. Sales-script sentences the new stack will back TODAY

These are sentences a human can defend with **live files on `ee2db8e5`**, not with Hub, not with 013 papers, not with a hoped CHIP factory. Each sentence is scoped to the new three processes. If a sentence needs a live Stripe dashboard, a live One laptop, or a human clicking `:5175`, it is marked **runbook**, not **CI**.

### 1.1 Process and door

1. **“Focused Pay is a separate C# host on 8081.”** `Properties/launchSettings.json` binds `http://localhost:8081`. `Program.cs` maps `/health`, `/v1/health`, `/ready`, whoami, org-ready, checkouts, catalog, public pay, gateways, PSP webhooks, payment queries, One webhooks. It does not bind 8080.

2. **“Pay talks to One over HTTP. It does not contain `Modules/One`.”** `OneClient` posts to `{One:BaseUrl}/me` and `{One:BaseUrl}/tenants/{orgId}/authz/check`. `appsettings.json` default is `http://localhost:8080/api/v1`. IsolationTests fail the build if source contains `Modules.One`, `BuildingBlocks`, or `MediatR`.

3. **“Merchant staff sign in through One, not through a Pay password form.”** `apps/lazuar-pay-merchant` uses `react-oidc-context` + `oidc-client-ts`, `response_type: 'code'`, tokens in `sessionStorage`. `LoginPage.tsx` copy: “This page is not a password form.” `locks.test.ts` greps the SPA for `type="password"`, `/one/auth/login`, and `lazuar_auth`. `pickApiBearerToken` returns JWT `access_token` and **never** `id_token` (`bearerToken.test.ts`).

4. **“Merchant homepage is `:5178`. Login is One `:5175`. Not ops `:3003`, not admin `:5173`.”** Vite `--port=5178 --strictPort`. CORS allow-list is 5178 and 5179 only. `CorsTests.Health_does_not_allow_ops_origin` and `Health_does_not_allow_portal_origin` assert `:3003` and `:3004` get no `Access-Control-Allow-Origin`.

5. **“One tenant id is Pay `org_id`. There is no Pay `organizations` table.”** `CreateWorkspacePage` calls One `POST /tenants`. IsolationTests forbid `ToTable("organizations")`, `ToTable("users")`, `ToTable("members")`. Thin `org_settings` is keyed by that uuid.

6. **“Buyers have no One account.”** `lazuar-pay-checkout/package.json` has `react` and `react-dom` only. `locks.test.ts` forbids `oidc-client-ts`, `react-oidc-context`, `@repo/api-types-ts`. Public `GET /v1/pay/{token}` does not send Bearer. `PublicPayTests.Public_get_does_not_need_bearer` asserts a second GET does not call One.

7. **“The public door is Pay `/v1`, not Hub `/api/v1`.”** Prefix `/v1`. JSON snake_case. Spec lives in `packages/pay-spec`, not `packages/api-spec`.

### 1.2 Persistence (this is the sentence 013 could not say)

8. **“Checkouts survive process restart. They live in Postgres, database `lazuar_pay`, published 5435.”** `CheckoutStore` comment is now “Postgres-backed checkouts. Not a ledger.” `docker-compose.pay.yml` runs `postgres:16-alpine` as `pay-db`, `POSTGRES_DB: lazuar_pay`, `5435:5432`. `Program.cs` default connection: `Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres`. Tests use EF InMemory (`PayApiFactory`), not that Postgres.

9. **“One schema, one `PayDbContext`, one migrator.”** `HasDefaultSchema("public")`. Tables: `org_settings`, `checkouts`, `idempotency_keys`, `products`, `prices`, `gateway_credentials`, `psp_webhook_events`, `charges`, `subscriptions`, `journal_entries`, `journal_lines`, `documents`, `document_sequences`, `payers`, `audit_events`, `mail_outbox`, `one_webhook_events`. `task pay:db:migrate` runs `dotnet ef database update --context PayDbContext`.

10. **“`/health` never calls One. `/ready` is Postgres only.”** Health tests throw if One is contacted. `/ready` is `db.Database.CanConnectAsync` → `{ status: "ready" }` or 503 `{ status: "not_ready" }`.

### 1.3 One rail, hosted, labeled

11. **“Bar B’s first rail is Stripe. Not five adapters.”** `GatewayEndpoints.Put` rejects any provider other than `"stripe"` with `"Bar B first rail is stripe"`. `WebhookEndpoints` rejects any path provider other than `StripeHosted.Provider` (`"stripe"`) with `"unknown provider"`. Grep of `apps/lazuar-pay` for `CHIP`, `Billplz`, `Razorpay`, `Xendit`, `PaymentGatewayFactory` is empty.

12. **“The capability we actually ship is `hosted_link`.”** PUT/GET gateway JSON returns `capability = "hosted_link"`. `StripeHosted.CreateHostedUrlAsync` creates Stripe Checkout Session `Mode = "payment"` and returns `session.Url`. There is no Stripe Elements, no PaymentIntent confirm on 8081, no `ChargeOffSessionAsync`.

13. **“Merchant `owner` / `admin` can paste a Stripe secret; `member` cannot.”** `MemberGate.RequireWriterAsync` requires One `authz/check member` **and** whoami role `owner` or `admin`. Catalog create uses the writer gate. Gateway PUT uses the writer gate. Merchant `WorkspacePage` hides the paste UI unless `canWriteMoney(role)`. CatalogTests `Member_cannot_create_product` is 403.

14. **“BYOK `sk_` is encrypted at rest with AES-GCM.”** `SecretBox.Protect` / `Unprotect`. Column `gateway_credentials.ciphertext`. GET returns `last4`, never the secret.

15. **“A buyer can open a shareable link on `:5179/c/{token}` and be redirected to Stripe Checkout.”** Merchant mints the link from `public_token`. Checkout `App.tsx` `POST /v1/pay/{token}/start` with name/email, then `window.location.assign(redirect_url)`. Start is unauthenticated. Paused orgs get 403 `"Org charges are paused"`. Missing token is 404 without calling One.

### 1.4 Money path that exists in hermetic tests

16. **“A verified Stripe `checkout.session.completed` with `mode=payment` and amount > 0 writes charge + balanced journal + Official Receipt `RCPT-{MYT year}-#####` in `Fulfillment.FulfillPaidAsync`, and a replay returns `{ duplicate: true }` without a second document.”** `WebhookTests.Completed_session_writes_receipt_and_replay_is_noop` asserts document count 1, number starts with `RCPT-`, debit sum equals credit sum, second POST contains `"duplicate"`.

17. **“Empty PSP body is 400. Invalid Stripe signature is 400. Missing `Pay:StripeWebhookSecret` when the rail is configured is 503.”** `PublicPayTests.Empty_webhook_is_400`. `WebhookTests.Invalid_signature_is_400`. `WebhookTests.Missing_webhook_secret_is_503_when_rail_configured`.

18. **“Setup / zero-amount Stripe sessions are ignored by the webhook handler.”** Code path: `if (session.Mode == "setup" || (session.AmountTotal is null or 0)) return { ignored = "setup_or_zero" }`. `Fulfillment` also returns early if `checkout.Amount <= 0`. **There is no hermetic test that names this path.** Do not sell the test; sell the branch.

19. **“Receipt title is Official Receipt, not Tax Invoice. Missing number serializes as `PENDING`.”** `Fulfillment` sets `Title = "Official Receipt"`. `PaymentQueryEndpoints` returns `number = d.Number ?? "PENDING"`.

20. **“Isolation still holds after Stripe.net and EF landed.”** Host csproj PackageReference is `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, `Stripe.net` **only**. No `Lazuar.Api`, no MediatR package. IsolationTests still scan csproj + `src/**/*.cs` + Vite `package.json`.

### 1.5 What “today” does **not** include, even though the sentences above are true

- A human loop of 011/12 steps 1–12 marked `done`. [011/12](../011-new-lazuar-pay/12-first-slice-tracker.md) is still all `todo` on the Pay side and the One side.
- [B99](../013-prods/checklists/b99-bar-b-done.md) is still all unchecked. Bar B is **not closed**.
- CI does not talk to Stripe or Zitadel. `task pay:test` is hermetic `WebApplicationFactory` + `FakeOneHandler` + EF InMemory.
- Live dogfood still needs One on 8080 (Hub **off**), Pay 8081, merchant 5178, checkout 5179, Postgres 5435, a registered SPA `client_id`, `Pay:StripeWebhookSecret`, `Pay:WrapKey`, `Pay:OneWebhookSecret`, and a Stripe test key. That is a **runbook**, not a green cell.

---

## 2. Do-not-demo / do-not-say list

Read this before a screen share. If a sentence is on this list, the live files will not back it. Several are the exact lies 008 caught in Hub READMEs.

### 2.1 Product lies

| Do not say | Why live files refuse it |
|------------|--------------------------|
| “We replaced Hub.” | Root `docker-compose.yml` still builds `apps/lazuar-api/Dockerfile` as `lazuar-hub-api` on **8080**. Ops 3003 / portal 3004 / admin 3005 still exist. [parked-hub-cutover.md](../013-prods/checklists/parked-hub-cutover.md) is unchecked. |
| “Pay v1 is complete.” | Bar C is parked: renew, refund-once, SST × seats, magic-link portal, second rail. [parked-bar-c.md](../013-prods/checklists/parked-bar-c.md). |
| “We have five payment adapters.” | One class: `StripeHosted`. Hub still has five under `Modules/Payments/Infrastructure/Gateways/` (`StripeGatewayAdapter`, `ChipCollectGatewayAdapter`, `BillplzGatewayAdapter`, `RazorpayGatewayAdapter`, `XenditGatewayAdapter` + `PaymentGatewayFactory`). That factory is **museum**. New Pay has zero `IPaymentGatewayFactory`. |
| “CHIP is live on 8081.” | `decisions.md` first rail = **Stripe**. CHIP is the next Malaysian rail, parked. Grep in `apps/lazuar-pay` for CHIP is empty. |
| “We take cards on our page.” | Buyer is redirected to Stripe-hosted Checkout. Capability is `hosted_link`. There is no card element on `:5179`. |
| “Off-session / vaulted auto-charge works.” | `StripeHosted` always `Mode = "payment"`. No setup-intent vault. No `ChargeOffSessionAsync`. Hub’s `IPaymentGatewayAdapter` has that method; new Pay does not implement the interface. |
| “We file MyInvois / e-invoice at pay.” | No LHDN module, no UBL, no VALID. `NP-XX-001` refuse. Receipt title is Official Receipt. |
| “This is a Tax Invoice.” | `NP-XX-003` refuse. `Fulfillment` hard-codes `"Official Receipt"`. |
| “Receipt number is the checkout id.” | Checkout id is `Guid.NewGuid().ToString("N")`. Document number is `RCPT-{year}-{seq:00000}`. NP-DOC-002. |
| “SST is computed.” | Journal is cash debit + revenue credit for the **gross checkout amount**. No tax line. No fee line. SST registration is auto-seeded `false` (see §3). |
| “VIEWER is a One role.” | One membership is `owner` / `admin` / `member`. Pay treats `member` as read-only on money **in the UI and on writer routes**. One has no `viewer` on type `tenant`. |
| “`/v1/orgs/{id}/ready` means we can charge.” | `OrgReadyEndpoints` always returns `ready: true` after `check(member)`. Dummy. Host README already said this; it is still true. |
| “Merchants use `lazuar-ops` / `lazuar-admin`.” | CORS denies 3003. NP-XX-018. Merchant is `:5178`. |
| “Buyers log in.” | Checkout has no OIDC. Fail the demo if `:5175` appears on 5179. |
| “We mint `lzr_sk_` in Pay.” | No `lzr_sk_` string in `apps/lazuar-pay`. Mint is One. Pay only forwards `Authorization`. Untested as a machine-key path. |
| “Invite flow lives in Pay.” | No invite UI in merchant. Copy-link is One (`NP-ONE-011` still `todo` in 011/11). |
| “Subscriptions renew.” | Checkout create hard-codes `Interval = "one_off"`. Catalog may store `mo`/`yr` on a price; the pay-link path ignores it. `NP-FUL-004` is V1 / Bar C. |
| “Refunds work.” | No refund route. No `IssueRefundAsync`. Bar C. |
| “We email receipts.” | `mail_outbox` table exists. **Nothing writes to it.** Grep `MailOutbox` is DbSet + row type + migration only. |
| “Compose is Pay.” | `docker compose up` is still Hub. Pay DB is `docker compose -f apps/lazuar-pay/docker-compose.pay.yml`. There is **no** `apps/lazuar-pay/Dockerfile`. |
| “pay-spec matches the host.” | Spec is missing gateway, payments, receipts, unversioned `/ready`. Spec header still says checkout is a fixture. Host README still says “Checkout is an in-memory fixture (`status: open`). Not a real charge.” That README is a **Hub-class stale sentence** on this SHA. |

### 2.2 Demo footguns (the click will lie)

| Do not demo | What actually happens |
|-------------|------------------------|
| Curl `POST /v1/checkouts` with Ada’s token and call it “we took a payment.” | Session is `status: "open"`. Money moves on **verified webhook**, not on create. Create is a merchant call. |
| Open `:5178` without `VITE_ZITADEL_CLIENT_ID`. | Login page alerts “Missing VITE_ZITADEL_CLIENT_ID. Register the SPA with scripts/register-spa.sh.” |
| Boot Hub `task dev` and One on the same laptop. | Both want **8080**. C19 runbook: Hub off. |
| Point `lazuar-ops` `VITE_API_URL` at 8081 “to save time.” | CORS denies 3003. Types are Hub. Auth is Hub cookie. P60 refuse. |
| Paste a Stripe `whsec_` into the merchant “Stripe keys” box and expect webhooks to verify with it. | PUT stores the secret as **API key** (`sk_`). Webhook verify reads **process** `Pay:StripeWebhookSecret`. See P0. |
| Return from Stripe and trust `?status=verifying`. | `StripeHosted` default success URL appends `?status=verifying`. Checkout `App.tsx` **never reads** that query. It GETs `/v1/pay/{token}` once. If the webhook is late, the page still shows **Pay**. Success URL is not paid (that part is honest); the missing poll is not. |
| Show a `member` that they “cannot charge” and then curl `POST /v1/checkouts` with that member’s token. | UI hides the button. **API create checkout is `RequireMemberAsync`, not `RequireWriterAsync`.** Member can mint a pay link via HTTP. |
| Trigger `tenant.suspended` and assume an in-flight Stripe session cannot fulfill. | Create and start check `ChargesPaused`. `FulfillPaidAsync` does **not**. |
| Claim SST fail-closed after creating a checkout. | Create auto-inserts `OrgSettingsRow { SstRegistered = false }`. Fulfillment’s `null` throw never runs on that path. |

### 2.3 Refuse list (keep; do not un-refuse in 014)

All 24 `NP-XX-*` rows in 011/11 remain `refuse`. Production-ready **fails** if any of them ship, even if the dogfood sentence is otherwise green.

| ID | Refuse | Live check on `ee2db8e5` |
|----|--------|---------------------------|
| NP-XX-001 | Homemade LHDN / XML / UBL | No LHDN types in Pay host. |
| NP-XX-002 | TIN-at-checkout as legal feature | Checkout has name + email only. |
| NP-XX-003 | Title Tax Invoice / print VALID | Title is Official Receipt. |
| NP-XX-004 | WhatsApp dunning | Not present. |
| NP-XX-005 | Xero | Not present. |
| NP-XX-006 | Web3, escrow, CMS, 15-app | Not present. |
| NP-XX-007 | Zitadel/OpenFGA/SCIM/password **inside Pay** | No login route on 8081. Merchant OIDC against Zitadel **issuer**, Pay holds no PAT. |
| NP-XX-008 | Dual JWT vs membership | `/me` + `authz/check`. No cookie JWT. |
| NP-XX-009 | Per-module schemas / inbox as self-talk | One `PayDbContext`, public schema. Webhook calls `Fulfillment` in-process. |
| NP-XX-010 | Debit notes, self-billed 11–14 | Not present. |
| NP-XX-011 | Homemade FPX e-mandate | Not present. |
| NP-XX-012 | Stripe Billing `subscription.updated` as SoT | Webhook listens `checkout.session.completed` only. |
| NP-XX-013 | Zitadel human per cardholder | Checkout has no OIDC. Payers table is Pay. |
| NP-XX-014 | Second `organizations` table | IsolationTests ban it. |
| NP-XX-015 | FGA types `payment` / `document` | `authz/check` is `relation=member`, `type=tenant`. |
| NP-XX-016 | Pay calls One `authz/write` | `OneClient` has GET me + POST check only. |
| NP-XX-017 | Pay holds Zitadel PAT / FGA admin | Config is `One:BaseUrl`, timeout, wrap key, webhook secrets. Register-spa.sh forbids `ZITADEL_PAT`. |
| NP-XX-018 | Ship merchants to `lazuar-admin` `:5173` | Login copy names `:5178` / `:5175`. |
| NP-XX-019 | Notify or Audit as a **process** | `audit_events` in the same DB. No `lazuar-notify` binary. Mail table unused. |
| NP-XX-020 | Lazuar Media in v1 | Not present. |
| NP-XX-021 | Block Pay on npm `@lazuar/one-client` | Merchant talks HTTP. |
| NP-XX-022 | Hosted One SKU / Okta / SCIM as next **Pay** ticket | Not started. |
| NP-XX-023 | Pay calls `POST /platform/tenants` | Merchant calls One `POST /tenants`. |
| NP-XX-024 | Parse Zitadel project-role claims | Whoami copies One `role`. |

---

## 3. Ranked P0 / P1 / P2 (money safety first)

Priority is **blast radius on cash**, not story-point size. Hub parity items are not on this list.

### P0 — money can be wrong, forged, undercharged, or charged after suspend

#### P0-1. Webhook signing secret is process-wide, not BYOK

Live verify:

```csharp
var whsec = config["Pay:StripeWebhookSecret"];
if (string.IsNullOrWhiteSpace(whsec))
{
    return PayErrors.Status(503, "Service Unavailable", "Pay:StripeWebhookSecret missing");
}
EventUtility.ValidateSignature(json, sig.ToString(), whsec);
```

Merchant PUT stores **API** secret (`sk_test_…`) in `gateway_credentials.ciphertext`. GET never returns a webhook secret. Hub’s `StripeGatewayAdapter.ParseWebhookAsync` takes **per-call** `webhookSecret` (tenant config). New Pay dropped that parameter and used one env var.

COMMIT_EDITMSG is correct that you must **not** verify Stripe signatures with `sk_`. That is not the same as “one platform `whsec` is BYOK.” Stripe issues a **per-endpoint** signing secret. True BYOK is: merchant pastes `sk_` **and** `whsec_` (or Pay registers the endpoint on that account via API and stores the returned secret **per org**). As written:

- Anyone with `Pay:StripeWebhookSecret` can forge `checkout.session.completed` for **every** org that has a Stripe row.
- A merchant cannot rotate *their* webhook secret without an ops change to Pay’s process env.
- The merchant UI inviting “paste `sk_test_`” trains people to think that is the only secret that matters.

This is the hole 014 was told to name.

#### P0-2. Idempotency row is committed **before** fulfill (lost cash, or fulfilled cash for the wrong org)

```csharp
db.PspWebhookEvents.Add(new PspWebhookEventRow { OrgId = orgId, Provider = StripeHosted.Provider, EventId = stripeEvent.Id, ... });
await db.SaveChangesAsync(ct);

if (stripeEvent.Type is "checkout.session.completed")
{
    ...
    await fulfillment.FulfillPaidAsync(checkoutId, StripeHosted.Provider, session.Id, ct);
}
```

`Fulfillment` then `BeginTransactionAsync` on its own. 013 G/F law was: **one** HTTP request, **one** DB transaction: event + paid + journal + `RCPT-`. Live code is two commits.

If `FulfillPaidAsync` throws (SST unknown, DB glitch), the event id is already unique. Retry returns `{ duplicate: true }` and **never fulfills**. Buyer paid Stripe. Pay has no `RCPT-`. That is the opposite of “retry no-ops after success.”

`FulfillPaidAsync(checkoutId, …)` does not receive `orgId`. It loads checkout **by id only**. Path `{orgId}` is used to check “this org has a Stripe row” and to key the event. A forged (or mis-routed) event for org A whose `client_reference_id` is org B’s checkout will mark org A’s event id used and **pay org B’s session**. Bind `checkout.OrgId == path orgId` before fulfill.

#### P0-3. SST fail-closed is implemented, then bypassed on the only create path

`OrgSettingsRow` comment:

```csharp
/// <summary>null = unknown (fail closed for SST). true/false when merchant set it.</summary>
public bool? SstRegistered { get; set; }
```

`Fulfillment`:

```csharp
if (settings?.SstRegistered is null)
{
    throw new InvalidOperationException("SST registration unknown; fail closed");
}
```

`CheckoutEndpoints.Create`:

```csharp
if (settings is null && orgId is not null)
{
    settings = new OrgSettingsRow { OrgId = orgId, SstRegistered = false };
    db.OrgSettings.Add(settings);
    await db.SaveChangesAsync(cancellationToken);
}
```

COMMIT_EDITMSG: “auto-seed SST unknown as unregistered.” That is **fail open** relative to NP-MON-004 (“if you cannot know whether the merchant is SST-registered, fail closed, do not undercharge”). Known-false is allowed to book tax 0. **Unknown coerced to false** is the undercharge. There is no merchant field to set SST yes/no. Journal has no tax line either way:

```csharp
db.JournalLines.Add(... Account = "cash", Dc = "D", Amount = checkout.Amount);
db.JournalLines.Add(... Account = "revenue", Dc = "C", Amount = checkout.Amount);
```

NP-MON-001 asked for cash, revenue, **tax**, **fee**. Balanced two-line GMV is not that journal. `unknown ≠ 0` for fees is not implemented (no fee line at all — which is safer than booking 0, and not the same as SST).

013 [f18-sst-fail-closed.md](../013-prods/checklists/f18-sst-fail-closed.md) is fully checked, including “Unknown SST cannot commit a GMV journal as tax=0.” Live create path never leaves SST unknown.

#### P0-4. `tenant.suspended` does not pause fulfill; it is untested

`OneWebhookEndpoints` on `type == "tenant.suspended"` sets `ChargesPaused = true` (and on insert also `SstRegistered = false` — another silent SST coerce). Create checkout and public start return 403 `"Org charges are paused"`. `FulfillPaidAsync` never reads `ChargesPaused`.

O16.3 allows an **in-flight** capture to commit. That is a product choice. O16.2 also checks “PSP fulfill of **new** attempts fails closed while paused” and O16.5 “Pause + reactivate proven hermetically.” Grep of `apps/lazuar-pay/tests` for `ChargesPaused`, `tenant.suspended`, `OneWebhook` is **empty**. The HMAC door exists; the proof does not.

HMAC compare is `Convert.ToHexString` (uppercase) vs header bytes, length-checked `FixedTimeEquals`. If One sends lowercase hex or `sha256=`, verify 401s. No test, no fixture payload. Empty One body is hashed as empty then parsed as `{}`.

If the HMAC webhook is late, new charges continue until it arrives. 011 said “Money in Pay stays true if webhook is late” — already-captured cash stays (good) — **and** “new charges must fail closed.” Without a pull of One tenant status on create/start, late/missing HMAC is fail **open** for new charges. Start/create do not call One for `tenant.status`; they only trust `org_settings.charges_paused`.

#### P0-5. Member can charge through the Bezos door

`MemberGate.RequireWriterAsync` exists and is used for PUT gateway and POST product. `CheckoutEndpoints.Create` uses `RequireMemberAsync`. A `member` token that passes `authz/check member` can `POST /v1/checkouts` and mint a public token. Merchant UI hides the button. NP-ONE-021 is “VIEWER cannot charge, change keys, or refund” — Pay meaning of VIEWER is `member`. The API does not match the UI.

Refund is not implemented, so “cannot refund” is vacuously true.

#### P0-6. Setup-not-paid is a branch without a test (fail lock)

011/12 lock: “Setup session is not counted as paid” still `todo`. Code ignores `mode=setup` and `AmountTotal` 0/null. `G22.3` claims “Fixture payload for setup-intent **or** amount 0 … fulfill **not** called” and is checked. Grep of tests for `setup`, `setup_or_zero`, `Mode` is empty. `WebhookTests` only covers missing secret, bad signature, and a **payment** completed + replay.

Until that test exists, do not tell a customer the fail lock is proven. StripeHosted currently never creates `mode=setup`, so the branch is defensive against a crafted webhook (which P0-1 makes cheap if the process secret leaks).

### P1 — honesty, isolation of secrets, dogfood that still lies in copy

| ID | Gap | Evidence |
|----|-----|----------|
| P1-1 | Host README + pay-spec still describe a **fixture** | `apps/lazuar-pay/README.md`: “Checkout is an in-memory fixture (`status: open`). Not a real charge.” `packages/pay-spec/main.tsp` line 7: “Checkout is a fixture (open session), not a charge.” `pay-spec/README.md`: “Grow `main.tsp` when `POST /v1/checkouts` exists.” `Taskfile.yml` `pay:test` desc: “health + isolation” (42 tests). Same disease 00-why-leave named. |
| P1-2 | Spec does not list live money GETs | Live extra: `PUT/GET /v1/orgs/{orgId}/gateway`, `GET /v1/orgs/{orgId}/payments`, `GET …/receipts`, `GET …/receipts/{id}`, `GET /ready`. Spec has catalog + public pay + webhooks. Do not generate Hub-sized OpenAPI; do grow `pay-spec` when the door is real. |
| P1-3 | `SecretBox` default wrap key is a hardcoded string | `SHA256.HashData("lazuar-pay-dev-wrap-key")` when `Pay:WrapKey` missing. Comment says “Dev/test only.” Production that forgets the env encrypts every org’s `sk_` with a git-known key. |
| P1-4 | Checkout does not poll `verifying` | K16 listed open / paid / expired / missing / verifying. `App.tsx` handles missing, paid, expired, else Pay. No `verifying` UI, no interval poll. Buyer returning from Stripe may click Pay twice (Stripe will open a second session). |
| P1-5 | Pay-link ignores catalog interval / product id | Create checkout hard-codes `Interval = "one_off"`. Subscriptions insert only if interval is `mo` or `yr`. Merchant “create product + pay link” POSTs product then a **detached** checkout with the same amount. NP-FUL-002 “buyer access = Pay subscription row” does not happen on the dogfood button. |
| P1-6 | No `lzr_sk_` proof | Bearer is forwarded. If One `/me` accepts machine keys, Pay might work. There is no test, no scope check, no worker. NP-ONE-014 still `todo`. |
| P1-7 | No invite / second-engineer path in merchant | HomePage lists whoami tenants. No copy-link. NP-ONE-011/012/022 still `todo` in 011. Member-can-see is UI-only if someone else invited them in One. |
| P1-8 | Audit on key change missing | Fulfillment writes `checkout.paid` audit in the money transaction. `GatewayEndpoints.Put` does not write `audit_events`. NP-AUD-003. |
| P1-9 | No Pay Dockerfile, root compose still Hub | 013/03 designed an 8081 image. Still absent. Do not publish Pay as `lazuar-hub-api`. |
| P1-10 | 011/11 and 011/12 not flipped | Humans cannot read Status and know Bar B code exists. That is this paper’s job, not a silent flip. |
| P1-11 | G24 “comment listing parked rails” is missing | `StripeHosted.cs` has no Billplz/Razorpay/Xendit/CHIP comment. Factory absence is real; the comment is not. |

### P2 — after money is boring

| ID | Gap | Wave |
|----|-----|------|
| P2-1 | CHIP (or Billplz-as-reminder-only) as **second** rail | NP-GW-003 / NP-SOON-008. After Stripe hosted_link is boring. HTTP extract only. |
| P2-2 | SST exclusive on unit × seats | NP-MON-003. Steal `SstTaxMath` **judgment**, not the module. |
| P2-3 | Refund once / dispute no double-reverse | NP-MON-005/006. Bar C. |
| P2-4 | Expire open sessions | NP-CHK-004 `expired`. UI branch exists; no job. |
| P2-5 | Receipt email, failed-pay mail | NP-MAIL-001. Table exists. |
| P2-6 | Buyer magic-link portal on `:5179` | NP-BUY-003–005. Bar C. |
| P2-7 | Subscribers list | NP-FUL-003 half. Payments + receipts list exist; no `GET /subscriptions`. |
| P2-8 | Hub cutover | parked-hub-cutover. After B99 is actually lived. |
| P2-9 | Razorpay / Xendit | NP-LAT-002. Reminder-only, labelled. Not day one. |

---

## 4. Adapter port sequencing

The temptation is: Hub already has five adapters and a factory, so “port the module.” 014’s problem statement forbids that. Live new Pay already made the right **shape** (one class, two jobs) and the wrong **next impulse** (add CHIP while Stripe’s webhook secret is still a process env).

### 4.1 What Hub actually is (steal HTTP, not the cathedral)

`IPaymentGatewayAdapter` (`Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs`):

- `GenerateCheckoutAsync`
- `ParseWebhookAsync` (**includes** `webhookSecret` per call)
- `IssueRefundAsync`
- `GenerateCustomerPortalAsync`
- `ChargeOffSessionAsync`
- plus more on the rest of the interface (not quoted here on purpose — 014 is not a port of the whole port)

`PaymentGatewayFactory.GetAdapter(string gatewayType)` uppercases the name and resolves `IEnumerable<IPaymentGatewayAdapter>`. That is how five names become one switch. It is also how Billplz-class rails get treated like Stripe.

`StripeGatewayAdapter` creates Checkout sessions, parses `checkout.session.completed` **and** `payment_intent.succeeded`, expands PaymentIntent for **fee**, refuses missing currency, refuses setup without a PM (`Verified=false` so Stripe retries). That last judgment is the one new Pay compressed into `mode=setup \|\| amount 0 → ignored`.

`ChipCollectGatewayAdapter` talks `https://gate.chip-in.asia/api/v1/`, requires `merchantId` (Brand ID), builds a purchase JSON, hosted `checkout_url`. Hub also has `ChipWebhookRegistrar`. **Do not copy the registrar, the factory, or `Modules.Payments.Application.Ports`.** Steal: HMAC header names, empty-body 400, skip_capture ≠ captured, brand_id required.

### 4.2 What new Pay actually is

`Program.cs`:

```csharp
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<Fulfillment>();
...
app.MapGateways();
app.MapWebhooks();
```

`StripeHosted` has **one** method, `CreateHostedUrlAsync`. Parse lives in `WebhookEndpoints.Handle`. That is already “two functions,” not an interface of seven. G24 is **true as a live set** (one provider class) and **false as a copied Hub port**.

### 4.3 Sequence (do this order; do not parallelize 1 with 3)

**Step A — Harden the Stripe hosted rail that already exists.**  
Per-org `whsec` next to `sk_`. Same DB transaction for event + fulfill. `checkout.OrgId` must equal path `orgId`. Amount on the Stripe session must match checkout amount (Hub refused missing currency; new Pay never checks `amount_total` against the row). `payment_status=paid` (or equivalent) before fulfill. Hermetic tests for setup-not-paid, member-cannot-PUT-keys, member-cannot-POST-checkouts, SST unknown throw, suspend pause, replay after **successful** fulfill only. Wrap key mandatory outside Testing. This is still **one** adapter.

**Step B — Only then add CHIP as HTTP judgment, as the Malaysian rail.**  
011/01: Stripe **and** one Malaysian rail you will actually dogfood, not five on day one. Bar B froze Stripe XOR CHIP and picked Stripe. The Malaysian rail is **Bar C / second rail**, after Stripe hosted_link is boring in dogfood — not a factory ticket, not a week of Razorpay. Shape: `ChipHosted.CreateHostedUrlAsync` + `ChipWebhook.Handle` (or functions on the existing webhook route that switch on `{provider}`). Provider string `"chip"`. Capability `hosted_link` until a vault exists. **No** `IPaymentGatewayAdapter`. **No** `PaymentGatewayFactory`. **No** DNS fallback. **No** CHIP webhook registrar job from Hub.

**Step C — Explicitly refuse the factory-of-five.**  
Billplz / Xendit / Razorpay stay reminder-only later (`NP-LAT-002`, `NP-SOON-008`). If someone adds `GetAdapter("RAZORPAY")` to 8081, IsolationTests will not catch it (the ban is Hub module strings, not “second provider”). The refuse is product: G24, G10, 011/01. Write the parked-rail comment G24 already claimed.

**Do not** “implement both Stripe and CHIP while we are here.” That is the sentence G24.3 already checked, and the one a 014 implementer will try to reverse when the Malaysian demo is next week.

### 4.4 What “adapters in new Pay” is allowed to mean

See §8. Short form: two HTTP functions per rail, wrap-rails label, same-handler fulfill, BYOK secrets including webhook secret, IsolationTests still green (cathedral still banned). It is **not** `AddPaymentsModule`.

---

## 5. Tracker drift table (011/11 Status vs live)

011/11 counts on this SHA are **unchanged** from 012 C99:

| Wave | Rows | todo | doing | done | blocked | refuse | n/a |
|------|------|------|-------|------|---------|--------|-----|
| S0 | 22 | 17 | 0 | 5 | 0 | 0 | 0 |
| S1 | 42 | 37 | 0 | 5 | 0 | 0 | 0 |
| V1 | 12 | 12 | 0 | 0 | 0 | 0 | 0 |
| soon | 9 | 9 | 0 | 0 | 0 | 0 | 0 |
| later | 6 | 6 | 0 | 0 | 0 | 0 | 0 |
| refuse | 24 | 0 | 0 | 0 | 0 | 24 | 0 |
| **Total** | **115** | **81** | **0** | **10** | **0** | **24** | **0** |

011/12 steps 1–6 and 8–12 are still `todo`. Step 7 remains `refuse (keep)`. B99 is unchecked. **Do not treat this table as a flip.**

### 5.1 LOOK `todo` but code exists (Bar B under-claim)

Status is 011/11. “Live” is `ee2db8e5`. “Proven?” is: hermetic test and/or a human loop. Most of these are **code without a flipped cell and often without a test**.

| ID | 011 Status | Live reality | Proven? |
|----|------------|--------------|---------|
| NP-ONE-001 | todo | `scripts/register-spa.sh` POSTs One `/tenants/{id}/apps` `type: spa`. | Script exists. Not a CI test. Runbook. |
| NP-ONE-002 | todo | `oidcConfig.ts` authorization code + PKCE, public `client_id`. | SPA code. Needs live Zitadel. |
| NP-ONE-004 | todo | Redirect default `http://localhost:5178/callback`. M25 is One allowlist — **One’s** file, not in this host. | Partial. |
| NP-ONE-005 | todo | Login copy + OIDC redirect to `:5175` issuer `:8085`. Homepage routes are `/` workspaces. | SPA code. |
| NP-ONE-009 | todo | `CreateWorkspacePage` → One `POST /tenants`. | SPA code. |
| NP-ONE-017 | todo | `POST /v1/one/webhooks` HMAC + `one_webhook_events`. | **No test.** |
| NP-ONE-018 | todo | `ChargesPaused` on suspend; create/start 403. Fulfill ignores pause. | **No test.** Partial vs the ID. |
| NP-ONE-020 | todo | Holds `client_id` (public), wrap key, PSP/One webhook secrets. No PAT in tree. Default wrap key is a footgun (P1-3). | Partial. |
| NP-ONE-021 | todo | Writer gate on keys + catalog. Checkout create is **member**. UI hides charge for member. | Partial; API hole. |
| NP-ONE-022 | todo | Member can load workspace payments/receipts (member GET). No invite to *create* the second engineer. | Partial. |
| NP-CAT-001 | todo | `POST /v1/orgs/{orgId}/products` name required. | CatalogTests create 201. |
| NP-CAT-002 | todo | Price row with interval default `one_off`. Not a real monthly/yearly product linked to checkout. | Partial. |
| NP-CAT-003 | todo | Currency forced MYR. | Catalog create 400 otherwise. |
| NP-CAT-005 | todo | Merchant list/create on `:5178`. | UI + API. |
| NP-CHK-004 | todo | Fulfillment sets `paid`. No expire job. UI has expired branch. | Partial (`paid` only). |
| NP-CHK-005 | todo | `:5179` `/c/{token}` pay page. | UI. No Playwright. |
| NP-CHK-006 | todo | `http://localhost:5179/c/{public_token}`. | UI. |
| NP-CHK-007 | todo | Checkout package has no OIDC. | Vitest lock. Fail-if-login is not an e2e. |
| NP-GW-001 | todo | Encrypted BYOK `sk_` per org+provider. | Code. Wrap-key default weak. |
| NP-GW-002 | todo | Stripe hosted Checkout `mode=payment`. **Not** “card checkout” as Elements. | Code + webhook test. Notes must say `hosted_link`. |
| NP-GW-004 | todo | `EventUtility.ValidateSignature`. | Tests: bad sig 400, missing secret 503. |
| NP-GW-005 | todo | Empty body 400. | PublicPayTests. |
| NP-GW-006 | todo | Unique `(org_id, provider, event_id)`; replay `{ duplicate: true }`. Event saved **before** fulfill (P0-2). | Replay test after success. |
| NP-GW-007 | todo | JSON `capability: hosted_link`. Copy on checkout says processor ≠ success URL. | Label exists. Matrix of five rails does not. |
| NP-GW-008 | todo | Branch ignores setup/zero. | **No test.** Fail lock unproven. |
| NP-GW-009 | todo | PUT keys + writer gate. No dedicated member-cannot-PUT-keys test (member-cannot-create-product exists). | Partial. |
| NP-FUL-001 | todo | Webhook HTTP → `Fulfillment.FulfillPaidAsync` in-process. **Not** one DB transaction with the event row. | Partial. |
| NP-FUL-002 | todo | Subscriptions table; insert only if interval `mo`/`yr`. Dogfood path is `one_off`. | Partial. |
| NP-FUL-003 | todo | `GET /payments` + receipts. No subscribers list. | Partial. |
| NP-MON-001 | todo | Two-line cash/revenue, balanced. No tax/fee accounts. | Partial (balance yes, chart no). |
| NP-MON-004 | todo | Throw on `SstRegistered is null`; create seeds `false`. | **Fails the spirit of the ID.** |
| NP-DOC-001 | todo | `RCPT-{year}-{n:00000}`. | WebhookTests. |
| NP-DOC-002 | todo | Number is not the checkout Guid. Null → `PENDING` on GET. | Code. |
| NP-DOC-003 | todo | Title Official Receipt. | Code. |
| NP-DOC-004 | todo | No VALID string in host money path. | Absence. |
| NP-DOC-005 | todo | Merchant receipts list. | UI. |
| NP-BUY-001 | todo | Name/email on `POST /v1/pay/{token}/start`. | Code. Not required by UI. |
| NP-API-002 | todo | `POST /v1/webhooks/{provider}/{orgId}`. | Code + tests. |
| NP-API-004 | todo | Merchant `payFetch` to `/v1`. No Hub types. | SPA. |
| NP-API-005 | todo | Other-org checkout GET 403. Catalog/payments gated by path org. Public pay is token, not org. | Partial; webhook org bind hole. |
| NP-API-006 | todo | Idempotency on checkout create persists. Not all money POSTs. | Partial. |

**Still genuinely `todo` (code does not exist, or is schema-only):** NP-ONE-010 profile, 011 invite, 012 accept, 013 roster, 014 `lzr_sk_` mint, 016 batch-check, 019 provision on `tenant.created`; NP-CAT-004 seats; NP-GW-003 Malaysian rail; NP-FUL-004/005 renew / honest PAST_DUE; NP-MON-002 fee-from-PSP, 003 SST × seats, 005/006 refund/dispute; NP-BUY-002–005; NP-MAIL-*; NP-AUD-002/003 (003 missing on key put); NP-SOON-*; NP-LAT-*.

NP-GW-003 must **not** be flipped because Stripe exists. XOR. CHIP is absent.

### 5.2 Marked `done` — overclaimed or stale notes

The ten `done` IDs are the 012 C99 set. None of them should be reverted to `todo`. Overclaim is in **Notes** and in reading `/ready` as 021.

| ID | 011 Status | Live reality | Overclaim? |
|----|------------|--------------|------------|
| NP-ONE-003 | done | Whoami still forwards `Authorization` Bearer; never treats `id_token` specially on the host (SPA picker refuses it). | No. SPA must still send access_token. |
| NP-ONE-006 | done | `GET /v1/whoami` → One `/me` once. Health never calls One. | No. |
| NP-ONE-007 | done | Path `{orgId}` SoT on `/ready`, catalog, keys, payments. Header is hint. | Must keep on **every new** money route. Webhook path org is **not** bound to checkout.OrgId (P0-2) — that is a regression against the *spirit* of 007, not a reason to un-flip 007. |
| NP-ONE-008 | done | Projection copies One `role`. No Zitadel claim parse. | No. |
| NP-ONE-015 | done | Notes: dummy `/ready` + checkout gate `check(member)`. Feature text says member/**admin**/**owner** before merchant **admin** routes. Writer gate uses whoami role, not `authz/check admin`. `/ready` still `ready: true` for any member. | **Mild overclaim of the Feature sentence.** Notes already say dummy. Do not flip 021 because 015 is done. |
| NP-CHK-001 | done | Notes: “Fixture POST.” Live is Postgres `checkouts` row, still amount/currency/tenant. | Notes **under-claim**. Status done is fair. |
| NP-CHK-002 | done | success/cancel stored. Not fulfillment. | No. |
| NP-CHK-003 | done | Notes: in-memory idempotency. Live: `idempotency_keys` table. | Notes under-claim. |
| NP-API-001 | done | Notes: “Fixture session.” Live persists. Still not “paid” on POST. | Notes under-claim. Do not read this as NP-CHK-004. |
| NP-API-003 | done | `GET /v1/checkouts/{id}` other org 403. Payments list is extra. | No. |

**No 011 `done` cell is a lie that the route is missing.** The lie available to a reader is “15 done IDs mean Bar B.” There are still ten. Bar B IDs are the `todo` pile in §5.1.

013 **checklists** overclaim tests: G22.3 setup fixture, O16.5 pause hermetic, F18 unknown SST, G24 parked-rail comment. Those files are not 011 Status, but they will be quoted as if they were.

---

## 6. 013 paper drift table

Every 013 analysis paper pins **Pay `6f866ff0` / One `0f79fe4` / branch `feat/012-connect-one`**. Live authority is `ee2db8e5` on `main`. The papers remain useful as **bar and anti-goals**. They are dangerous as **inventory**.

| Paper | What it said at `6f866ff0` | Live `ee2db8e5` | Drift |
|-------|----------------------------|-----------------|-------|
| [README](../013-prods/README.md) | Host: whoami, org ready, in-memory checkout. Merchant: health probe, no OIDC. Checkout: health probe. | Host: Postgres, Stripe hosted, webhooks, journal, receipts. Merchant: OIDC + money UI. Checkout: `/c/{token}` pay page. | Index is stale. 014 index must not copy these role rows. |
| [01-production-ready-bar.md](../013-prods/01-production-ready-bar.md) | Bar A pass (C99). Bar B fail. 17 host `.cs` files. 31 tests. No webhook, no `RCPT-`, no Postgres, no Dockerfile, csproj zero PackageReference. Merchant/checkout health probes. | Bar A still pass. Bar B **code-shaped** but B99 unchecked and P0 holes. ~31 host `.cs` files excluding bin/obj. **42** `[Test]` methods. csproj has EF + Stripe.net. Still **no** Dockerfile. | Bar definition still binding. Inventory dead. “There is no Postgres” is false. |
| [02-replace-old-cutover.md](../013-prods/02-replace-old-cutover.md) | No compose service for Pay. Checkout fixture in-memory. | `docker-compose.pay.yml` exists for **DB only**. Host still not in root compose. Cutover still parked. | DB compose landed; kill criteria paper still right to wait for lived Bar B. |
| [03-host-production-seams.md](../013-prods/03-host-production-seams.md) | Fixture host, ConcurrentDictionary, no Dockerfile, no connection string. | `ConnectionStrings:Pay` default in Program, `/ready` Postgres, one migrator, 5435. Still no image. | Seams paper **succeeded** as a design; its “current honesty” section is obsolete. |
| [04-merchant-frontend.md](../013-prods/04-merchant-frontend.md) | “It is a health probe. It has no OIDC, no router, no whoami.” | Router, AuthProvider, whoami, workspace, keys, products, payments, receipts. | Paper’s **locks** (no ops clone, no Hub types, access_token) still true. Inventory false. |
| [05-checkout-frontend.md](../013-prods/05-checkout-frontend.md) | Vite is a health probe. Step 9–10 todo. | `/c/{token}`, public GET/start, name/email, Stripe redirect. No OIDC. | Locks still true. “Health probe” false. Verifying poll still missing. |
| [06-money-rails.md](../013-prods/06-money-rails.md) | **“There is no Pay database. There is no key column. There is no PSP client.”** Host csproj zero PackageReference. No webhook in pay-spec. | Database, `gateway_credentials`, `Stripe.net`, webhook in spec and host. | **Name this.** 06 is the paper 014/05–08 will be tempted to treat as current. It is not. Plane A vs B vs C split is still law. |
| [07-fulfillment-ledger-docs.md](../013-prods/07-fulfillment-ledger-docs.md) | “There is no webhook route, no Postgres, no journal, no `RCPT-`.” CheckoutStore ConcurrentDictionary. | Fulfillment writes charge, optional subscription, journal, document, audit. | Design (same handler, Official Receipt, SST judgment) still law. Inventory false. Same-TX-as-event not landed. |
| [08-one-identity-production.md](../013-prods/08-one-identity-production.md) | P10 SPA unwired. P30 HMAC parked. Checkout in-memory. | OIDC wired. HMAC route exists. `lzr_sk_` still absent. | Identity chrome mostly landed; machine key + proven suspend tests did not. |
| [09-data-migration.md](../013-prods/09-data-migration.md) | “Focused Pay on `6f866ff0` … still has **no Postgres**.” | Greenfield `lazuar_pay` on 5435. Still no Hub `lazuar_mvp` import — that part remains correct. | “No DB” false. “Do not pour nine schemas into Pay” still true. |
| [10-ci-observability-decommission.md](../013-prods/10-ci-observability-decommission.md) | No Pay Dockerfile, no bake target, no Playwright. pay:test 31 tests. | Still no Dockerfile / bake `pay`. Tests grew (webhooks, catalog, public pay, extra isolation). Still no Playwright. | Deploy gap remains. Test inventory stale. |
| [checklists/README.md](../013-prods/checklists/README.md) | Bar B program map. | Many phase files checked. B99 not. | Use as a **map**, not as proof. Recheck live files. |
| [checklists/decisions.md](../013-prods/checklists/decisions.md) | Stripe first rail; token on `/v1/pay/{token}`; one PayDbContext; 5435. | Host matches those locks. | **Still live law.** |
| [b99-bar-b-done.md](../013-prods/checklists/b99-bar-b-done.md) | All `[ ]`. | Still all `[ ]`. | Honest. Code ≠ lived sentence. |
| [parked-bar-c.md](../013-prods/checklists/parked-bar-c.md) | Do not start until B99. | Still parked. | Keep. |
| [parked-hub-cutover.md](../013-prods/checklists/parked-hub-cutover.md) | Do not start until B99. | Still parked. Root compose still Hub. | Keep. |

**Rule for 014 parent:** quote 013 for the dogfood sentence, fail locks, and refuse. Quote **this SHA’s C#** for what exists. Never paste 013 § “What is already on 8081” into a sales deck.

---

## 7. Next ten actions

Analysis only. One intent each. Ordered. Small. **Not** a checklist flip. **Not** “port Hub Payments.” Money safety before CHIP.

1. **Store and verify a per-org Stripe webhook signing secret (BYOK `whsec_`), not only process `Pay:StripeWebhookSecret`.** Merchant PUT already takes a secret; split API key vs webhook secret (two fields) or register the endpoint on that account and persist the returned secret on `gateway_credentials`. Process env may remain a **dev fallback** and must 503 in Production if neither org nor env secret exists. This closes P0-1.

2. **Put `psp_webhook_events` insert and `FulfillPaidAsync` in one transaction; require `checkout.OrgId == path orgId`; do not commit the event id if fulfill throws.** Also match `amount_total` (minor units) to the checkout row and refuse `payment_status` other than paid. This closes P0-2.

3. **Stop auto-seeding `SstRegistered = false` on checkout create.** Leave `null` unknown. Add a merchant writer field yes/no. Keep the fulfill throw on null. Do not book a two-line all-revenue journal that pretends SST was decided. This closes P0-3. Qty=1 can book tax 0 **only** when the merchant set false.

4. **Read `ChargesPaused` inside `FulfillPaidAsync` for sessions created after pause (or for all unpaid), and call One (or trust the flag plus a pull) so missing HMAC is not fail-open for new charges.** Add hermetic One-webhook tests: bad HMAC 401, suspend pauses start+create, replay duplicate, reactivate. This closes P0-4.

5. **Gate `POST /v1/checkouts` with `RequireWriterAsync`.** Member GET stays. UI already hides. Curl must 403. Add the test next to `Member_cannot_create_product`. This closes P0-5.

6. **Add the missing hermetic fail-lock tests before adding a rail:** Stripe `mode=setup` does not insert documents; amount 0 does not; member cannot PUT `/gateway`; SST null throws and does **not** consume the event id if action 2 landed. This closes P0-6 as *proof*, not as a comment.

7. **Make `Pay:WrapKey` mandatory outside `Testing`; delete the hardcoded `"lazuar-pay-dev-wrap-key"` hash from any non-dev path.** Production that boots without a wrap key must not encrypt `sk_` with a git string.

8. **Rewrite the three stale sentences** (host README fixture paragraph, `pay-spec/main.tsp` service comment, `pay-spec/README.md` “when POST checkouts exists”, Taskfile `pay:test` blurb) so 014 does not catch Hub-README disease on the new stack. Grow `pay-spec` **only** for doors that exist (gateway metadata, payments, receipts). Do not import LHDN.

9. **On `:5179`, poll `GET /v1/pay/{token}` after return from Stripe and render a `verifying` state.** Do not treat `?status=verifying` or `success_url` as paid. This is K19, still untrue in the SPA.

10. **Do not start CHIP, a factory, Hub cutover, refunds, or LHDN in the same slice as 1–9.** After 1–9 are boring in dogfood, CHIP HTTP extract (create hosted URL + parse webhook) is the Malaysian rail — two functions, provider `"chip"`, capability `hosted_link`, no `IPaymentGatewayAdapter`. That is action 11, deliberately not in this ten.

---

## 8. What success looks like for “adapters in new Pay” without becoming Hub Payments again

Success is **not** “five names resolve in a factory.” Success is not `AddPaymentsModule`. Success is not IsolationTests weakened because `Modules.Payments.Application.Ports` is “just an interface.” Success is not ops Payment Settings retargeted at 8081.

### 8.1 A rail is done on 8081 when

1. Merchant `owner`/`admin` pastes **that** rail’s secrets (API + webhook), encrypted, last4 only on GET. `member` 403.
2. Public `POST /v1/pay/{token}/start` creates a **hosted** PSP session (`mode=payment` or CHIP purchase) and returns `redirect_url`. Capability JSON/copy says `hosted_link` (or `vaulted` only after a real PM/token exists — not today).
3. `POST /v1/webhooks/{provider}/{orgId}` verifies **that org’s** signing secret, empty body 400, unique `(org_id, provider, event_id)`.
4. The **same HTTP request**, **same DB transaction**, calls `Fulfillment` in-process: CAS `open` → `paid`, charge row, optional subscription, balanced journal, `RCPT-`, audit. No MediatR. No outbox to self. No “wait for One to hear it.”
5. Setup / skip_capture / amount≤0 does not fulfill. Hermetic test says so.
6. Wrap-rails: reminder-only rails never silent-debit. Stripe hosted_link never claims off-session.
7. IsolationTests still fail a `ProjectReference` to `apps/lazuar-api` and still grep-ban `MediatR`, `Modules.`, `BuildingBlocks`.
8. `pay-spec` lists the webhook and start ops. It does not list Hub’s 152.

### 8.2 Two rails, not five

Stripe hosted_link is rail 0 (landed, not hardened). CHIP hosted_link is rail 1 (HTTP extract from `ChipCollectGatewayAdapter.GenerateCheckoutAsync` + webhook parse — not the registrar, not the factory, not `GatewayCommon` as a package). Billplz/Xendit/Razorpay are **later**, labelled reminder-only, after 0 and 1 are boring in production (`NP-SOON-008`, `NP-LAT-002`).

A `switch (provider)` in `WebhookEndpoints` is allowed. A DI factory that can resolve unused names “for later” is how Hub grew five adapters before one dogfood loop was honest.

### 8.3 Failure mode (what 014 must catch)

The PR that copies `IPaymentGatewayAdapter` into `apps/lazuar-pay`, registers five classes, and ticks NP-GW-002 **and** NP-GW-003 **and** NP-LAT-002 in one merge. The PR that adds `customer.subscription.updated` because Stripe Billing is “free.” The PR that titles the receipt Tax Invoice to match ops. The PR that adds CORS for `:3003` “so we can demo.” The PR that treats B99 as done because `WebhookTests` is green.

On this SHA we are **past** “no Stripe, no DB” and **not at** “Bar B lived.” The honest sentence:

> New Pay can redirect a buyer to Stripe Checkout, verify a process-wide webhook secret, write an Official Receipt and a two-line journal, and show it on `:5178` to an `owner`. It cannot claim BYOK webhook secrets, SST fail-closed, proven setup-not-paid, proven suspend, or a Malaysian rail. It is not Hub. It is not five adapters. Isolation still holds.

That is the only sales script that survives a file-open on `ee2db8e5`.
