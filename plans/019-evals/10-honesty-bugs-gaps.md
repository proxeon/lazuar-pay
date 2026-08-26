# 10 — Honesty, ranked bugs/gaps, how to solve (018 SHA)

**Date:** 26 August 2026  
**Slice:** Cross-cut of the **newest** Pay host + merchant shell + hosted checkout after 016’s harden list, 017’s folder-by-job move, and 018’s Aura shell / independent vault / Test rail / pay-link occupancy. What we may say on this SHA. What we must not say. Whether each 016 P0/P1 is still true after the folders moved. Ranked live bugs. Ranked gaps versus the kernel thesis in [018-evals/001-evals.md](../018-evals/001-evals.md) and versus an SME cashier. How to solve, sequenced: money first, then the test that would have caught it, then SPA. Refuse list.  
**Kind:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a Hub cutover. **Not** a project reference into `apps/lazuar-api`.

Live files on `9f04ad58` are authority. Sibling 019 reports (01–09) may still be writing; this paper re-read the host, both Vite apps, `packages/pay-spec`, and the test project itself. [016/10-honesty-frontend-risks.md](../016-adapters-check/10-honesty-frontend-risks.md) is historical: it quoted `Gateways/` and `One/` on `c621ceba`. Those folders are gone. If a 016 sentence is still true, the **new path** is cited. If it is fixed, this paper says **FIXED** with evidence.

---

## Coordinates

| Field | Value |
|-------|--------|
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `feat/018-merchant-shell` (`.git/HEAD` → `ref: refs/heads/feat/018-merchant-shell`) |
| HEAD | `9f04ad58c578ab8df0a4e9a302a116940243d548` (`9f04ad58`) — `fix(pay-ui): match receipts table to pay-link chrome` |
| Today | 26 August 2026 |
| 016 honesty paper | [10-honesty-frontend-risks.md](../016-adapters-check/10-honesty-frontend-risks.md) on `c621ceba` / `feat/015-four-adapters` |
| 018 product paper | [001-evals.md](../018-evals/001-evals.md) — kernel vs escrow vs WhatsApp SME. Beliefs, not a host spec. |
| 011 product | [01-product.md](../011-new-lazuar-pay/01-product.md) — v1 must/should/later. Tax fail-closed was later amended out of this program. |
| Host README | [apps/lazuar-pay/README.md](../../apps/lazuar-pay/README.md) |
| 019 index | [README.md](./README.md) — map only. Not a substitute for this file. |

### `git log` on this branch (newest first, from `.git/logs/HEAD`)

```text
9f04ad58 fix(pay-ui): match receipts table to pay-link chrome
f4b5a63e fix(pay-ui): match payments table to pay-link chrome
401e7e3c feat(pay): set how many people can pay a pay link
59863420 feat(pay-ui): restyle buyer checkout with aura-ui chrome
53c807ae fix(pay-ui): tighten pay-link table layout
b3464de4 feat(pay-ui): list pay links in a table, mint from a dialog
42a1761f fix(pay): apply four-adapter columns on Development start
84a3ee24 fix(pay): keep local Postgres password when loading .env
77ef9502 fix(pay-ui): always offer Test when minting pay links
5fffd481 fix(pay-ui): show Test processor and drop square tiles
22469d61 feat(pay): add local Test processor with no secrets
fadbd147 feat(pay-ui): open processor keys in an Edit dialog
82e387b7 feat(pay): vault processors independently; bind rail at mint
6da8c68b fix(pay-ui): show staff email in sidebar, not Zitadel sub
b5c0599d feat(pay-ui): restyle create workspace inside the dashboard shell
c97e6d05 feat(pay-ui): open last workspace after login
5253dc98 feat(pay-ui): Aura-style merchant shell for org workspace
```

Everything from `5253dc98` through `9f04ad58` is the 018 merchant-shell program on this branch. 016 evaluated `Gateways/` + a single active rail + no Test + no payment-links table. Those sentences are **stale as a map**. Money sentences in 016 are **not** automatically stale: HMAC, start idempotency, pause-on-fulfill, Razorpay join, wrap-key, Stripe `whsec_` fallback were on the 016 fix-first list and must be re-read at the **new** paths.

### Folder move (016 → this SHA)

016 quoted `apps/lazuar-pay/src/Lazuar.Pay/Gateways/` and `…/One/`. Isolation now **fails the build** if those namespaces return:

```13:15:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
        "IEnumerable<IHostedRail>",
        "namespace Lazuar.Pay.Gateways",
        "namespace Lazuar.Pay.One;"
```

Live jobs:

| 016 path | 018 SHA path |
|----------|----------------|
| `Gateways/GatewayEndpoints.cs` | `Credentials/GatewayEndpoints.cs` |
| `Gateways/WebhookEndpoints.cs` | `Webhooks/WebhookEndpoints.cs` |
| `Gateways/{Stripe,Chip,Billplz,Xendit,Razorpay}{Hosted,Webhook}.cs` | `Rails/{Stripe,Chip,Billplz,Xendit,Razorpay,Test}/` |
| `Gateways/PayProviders.cs` | `Rails/PayProviders.cs` |
| `PublicPay/PublicPayEndpoints.cs` | same folder; plus `CheckoutUrls.cs` |
| `One/OneWebhookEndpoints.cs` | `Identity/OneWebhooks/OneWebhookEndpoints.cs` + `OneWebhookSignature.cs` |
| `One/MemberGate.cs` | `Identity/Client/MemberGate.cs` |
| *(none)* | `PaymentLinks/`, `Rails/Test/` |

Tests mirror `src/` except `IsolationTests.cs` at the test root.

---

## Files opened

Host (live):

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj`
- `apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/{HealthEndpoints,PayErrors}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/{MemberGate,OneClient,Bearer}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/{OneWebhookEndpoints,OneWebhookSignature}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/{CheckoutEndpoints,CheckoutStore,CreateCheckoutRequest}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/{PaymentLinkEndpoints,PaymentLinkOccupancy,CreatePaymentLinkRequest}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/{PublicPayEndpoints,CheckoutUrls,BuyerEmail}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/{WebhookEndpoints,PspParseResult}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/IHostedRail.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/{Stripe,Chip,Billplz,Xendit,Razorpay,Test}/*`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/{Fulfillment,MoneyMath}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/{PayDbContext,Rows}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260825120000_PaymentLinkPayers.cs`
- `apps/lazuar-pay/README.md`
- `apps/lazuar-pay/docker-compose.pay.yml`

Tests (live):

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/{PayApiFactory,FulfillmentProbe}.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/{WebhookTests,FillTests}.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/{Stripe,Chip,Billplz,Xendit,Razorpay,Test}/*`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/{Checkouts,Credentials,Catalog,Secrets,Hosting,Money}/*`

Frontends (live):

- `apps/lazuar-pay-merchant/src/pages/org/{GatewayPage,CheckoutsPage,OverviewPage,PaymentsPage,ReceiptsPage}.tsx`
- `apps/lazuar-pay-merchant/src/{App.tsx,locks.test.ts}`
- `apps/lazuar-pay-merchant/src/lib/{payApi,processors,roles,http}.ts`
- `apps/lazuar-pay-merchant/src/auth/{oidcConfig,bearerToken}.ts`
- `apps/lazuar-pay-merchant/src/layout/nav.ts`
- `apps/lazuar-pay-merchant/src/pages/LoginPage.tsx`
- `apps/lazuar-pay-merchant/package.json`
- `apps/lazuar-pay-checkout/src/{App.tsx,locks.test.ts}`
- `apps/lazuar-pay-checkout/{package.json,vite.config.ts}`

Spec / compose / product papers:

- `packages/pay-spec/main.tsp`
- `docker-compose.yml` (root — still Hub)
- `plans/018-evals/001-evals.md`
- `plans/011-new-lazuar-pay/{README.md,01-product.md,02-one-integration.md}`
- `plans/016-adapters-check/10-honesty-frontend-risks.md`
- `plans/019-evals/README.md` (index only)

Hub was **not** treated as the product. Grep into `apps/lazuar-api` was only to confirm kernel doors (`lzr_sk_`, outbound `payment.completed`) still live **there** and **not** in `apps/lazuar-pay`.

---

## What Pay actually is on 9f04ad58

Say these on a whiteboard. Each sentence is scoped to the three new processes. **CI** means hermetic `task pay:test` / NUnit + vitest greps. **Runbook** means a human, One on 8080 with Hub **off**, Postgres 5435, and (for live rails) a tunneled public https callback.

### Process and doors

1. **Focused Pay is a separate `net10.0` host on 8081.** `launchSettings.json` binds `http://localhost:8081`. PackageReference is EF Design + Npgsql + Stripe.net only. CHIP/Billplz/Xendit/Razorpay/Test are `HttpClient` or in-process. IsolationTests fail if a csproj contains `apps/lazuar-api` or `Razorpay.Api`. **CI.**

2. **Pay talks to One over HTTP. It does not contain `Modules/One`.** `OneClient` calls `GET me` and `POST tenants/{id}/authz/check`. Isolation bans `Modules.One`, MediatR, BuildingBlocks, `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `ChipWebhookRegistrar`, `PublicDnsFallback`, `IEnumerable<IHostedRail>`, `namespace Lazuar.Pay.Gateways`, `namespace Lazuar.Pay.One;`. **CI.**

3. **Merchant staff sign in through One `:5175`, not a Pay password form.** `LoginPage.tsx`: “This page is not a password form.” `locks.test.ts` greps `type="password"`, `/one/auth/login`, `lazuar_auth`. `pickApiBearerToken` sends JWT `access_token`, never `id_token`. **CI** (grep + unit). Live OIDC is **runbook**.

4. **Merchant homepage is `:5178`. Buyer page is `:5179`. Not ops `:3003`, not portal `:3004`, not admin `:5173`.** Vite `strictPort`. CORS allow-list is 5178/5179 and preview 4178/4179 plus 127.0.0.1 twins. `CorsTests` deny 3003/3004. **CI.**

5. **One tenant id is Pay `org_id`. There is no Pay `organizations` table.** Isolation bans `ToTable("organizations"|"users"|"members")`. **CI.**

6. **Buyers have no One account.** Checkout `package.json` is React + Radix slot + lucide. `locks.test.ts` forbids `oidc-client-ts`. Public GET does not send Bearer; second GET does not call One (`PublicPayTests.Public_get_does_not_need_bearer`). **CI.**

7. **Checkouts and pay links persist in Postgres `lazuar_pay` on 5435.** `CheckoutStore` comment: “Postgres-backed checkouts. Not a ledger.” `docker-compose.pay.yml` is DB only. Tests use EF InMemory. Restart persistence is **runbook**.

8. **`/health` never calls One. `/ready` is Postgres `CanConnect`.** Health tests. `/v1/orgs/{id}/ready` is still dummy `ready: true` after a member check — do not fold that into this sentence.

9. **Dispatch is a switch of known names, not a factory.** `PublicPayEndpoints.Start` and `WebhookEndpoints.Handle` `switch` on `PayProviders.*`. `Program.cs` `AddScoped` each hosted class. **CI.**

### Rails, vault, money path

10. **The wrap set is five lowercase PSP names plus a local Test rail: `stripe`, `chip`, `billplz`, `xendit`, `razorpay`, `test`.** Capability is `hosted_link` for all of them. `PayProviders.All` is the five PSPs. `Listed` appends Test when `!IsProduction()`. PUT `test` is 400 `"test processor does not take secrets"`. Unknown webhook provider is 400. **CI.**

11. **Vault is per-rail. Saving a secret does not pick the pay-link rail.** PUT no longer writes `org_settings.active_provider`. `GatewayTests.List_returns_all_five_and_put_does_not_default_pay_links` asserts `ActiveProvider` is null after two PUTs and `processors` length 6 (five + Test). Staff picks a rail **at mint**. **CI.**

12. **Merchant `owner` / `admin` paste keys and mint; `member` cannot.** `MemberGate.RequireWriterAsync`. Tests: `Member_cannot_put_gateway`, `Member_cannot_create_checkout`, `Member_cannot_create_product`. UI `canWriteMoney`. Member GET metadata remains. **CI.**

13. **BYOK API secret and webhook secret are AES-GCM at rest.** `SecretBox`. GET never echoes `sk_` / `whsec_` / PEM. `Pay:WrapKey` is required outside **Testing** (not merely outside Production). Git-known `"lazuar-pay-dev-wrap-key"` is Testing-only. `SecretBoxTests.Production_missing_wrap_key_throws`. **CI.**

14. **Stripe process `whsec_` is a Testing-only fallback.** `StripeWebhook.ResolveSecret` unwraps org ciphertext first; only `env.IsEnvironment("Testing")` reads `Pay:StripeWebhookSecret`. Empty ciphertext in Development/Production is 503 `"webhook secret missing"`. **CI** for Testing 503-when-nulled; Production fallback-refused is a reading of the `if`.

15. **A verified PSP paid event writes charge + balanced two-line journal + Official Receipt `RCPT-{MYT year}-#####`. Replay is `{ duplicate: true }` without a second document.** Stripe/CHIP/Billplz/Xendit/Razorpay hermetic tests. Title is `"Official Receipt"`. Razorpay payload `tax`/`fee` stay unbooked (`JournalLines.Count() == 2`). **CI.** Lived Ada-on-Stripe is **runbook**.

16. **Setup / zero / preauthorized / unpaid / SETTLED / `payment.failed` are not paid.** Tests exist for Stripe setup/zero, CHIP preauth, Billplz unpaid, Xendit SETTLED, Razorpay failed. **CI.**

17. **Start of an already-hosted open checkout returns the stored URL and does not mint a second PSP session.** `PublicPayTests.Start_twice_returns_same_url_without_second_psp_http` (CHIP FakePspHandler send count stays 1). Stripe create also sends `IdempotencyKey = "lazuar-checkout:" + checkout.Id`. **CI.** Lived double-click is **runbook**.

18. **One HMAC speaks Standard Webhooks.** Header `t={unix},v1={lowercase hex}` over `{unix}.{body}`, 300s skew. Body-only uppercase hex is 401. `tenant.suspended` sets `ChargesPaused`; `tenant.reactivated` clears it; `org_id` **or** `tenant_id`. **CI** (`OneWebhookTests`).

19. **In-flight Plane B does not fulfill a paused org.** Handler 409 **before** insert; `Fulfillment` throws `ChargesPausedException` as a second belt; catch rolls back; event id is absent so PSP retry after unsuspend can pay. `WebhookTests.Paused_org_does_not_mint_receipt`. Start on paused is 403 even with a stored URL. **CI.**

20. **Razorpay paid join is notes **or** payment-link id.** Parser reads `notes.checkout_id` and `payload.payment_link.entity.id` as `HostedSessionId`. Handler looks up `ProviderSessionId` when checkout id is blank. `Razorpay_captured_without_notes_joins_plink`. `payment_link.paid` / `order.paid` are ignored. **CI.** Lived Razorpay notes-copy is still **runbook**.

21. **Billplz localhost callback is 400.** `BillplzHosted.TryPublicBase` refuses non-https, loopback, `lazuar-local-dev.com`. `Billplz_localhost_callback_start_is_400_without_psp_http` actually sets `PublicBaseUrl=http://localhost:8081` and asserts 400. (The older test name `Billplz_paid_form_and_localhost_blocked` still does **not** block localhost — see P2.) **CI for the 400.**

22. **Currency omit on the five PSP parsers throws `missing currency`.** Stripe/CHIP/Billplz/Xendit/Razorpay. Handler still skips the currency compare when `parsed.Currency is null` — that arm is live for **Test** (see bugs). Amount mismatch 400 does not insert the event row (`FillTests.Amount_mismatch_does_not_mint_receipt`). **CI.**

23. **Pay does not compute SST. Pay does not file MyInvois. Receipt is not a Tax Invoice.** Fulfillment title hard-coded. `SstRegistered` column unused, asserted null after a paid Stripe. Merchant copy: “No SST, no e-invoice.” Checkout paid copy: “Official Receipt, not an e-invoice.” **CI.**

24. **Test processor: no secrets, no PSP HTTP. `POST /v1/pay/{token}/start` marks the checkout paid and writes an Official Receipt.** Allowed when `!IsProduction()`. `TestRailTests.Mint_and_start_pays_without_keys`. Buyer copy: “Test processor: Pay marks this paid. No card, no secret.” **CI.** This is **dogfood money**, not acquiring. Do not show a Test `RCPT-` to a customer as proof of a card capture.

25. **A pay link is a shared URL with occupancy.** `POST /v1/payment-links` writer-gated. Default `max_payers = 1`. `unlimited: true` → `MaxPayers` null. Each payer is a child checkout keyed by `slot_key` (8–128 chars). Same slot start-twice does not take two seats. Two slots on a link of two succeed; a third is 409 `"This pay link is full"`. Checkout SPA stores `lazuar-pay-slot:{token}` in `localStorage`. **CI** for sequential occupancy. Concurrent overfill is **not** locked (see P0-1).

26. **Success URL is not paid.** Hosted defaults go through `CheckoutUrls.Success` → `{Pay:CheckoutBaseUrl}/c/{token}?status=verifying`. Required outside Testing. Merchant mint uses `VITE_CHECKOUT_ORIGIN` (default `http://localhost:5179`). SPA polls 2s × 15, then “Not paid yet” + Refresh status. **CI** as source/grep. Lived race is **runbook**.

### Frontends

27. **`:5178` is an org dashboard, not a single WorkspacePage.** Routes: overview, processor, pay links, payments, receipts. Processor is cards + Edit dialog. Pay links is a table + Create dialog (label, MYR amount, rail, 1 / limited / unlimited). Payments table shows processor. Receipts table does **not**. **CI** as source + locks.

28. **`:5179` continues an already-started checkout instead of minting a second session.** If `started && redirect_url`, `location.assign`. 400/503 show host `detail`. Placeholder `customer@example.com` is not “usable.” Full / expired / paid have distinct chrome. **CI** as grep. No Playwright of the poll.

That is the honest product: **a hosted cashier with a staff shell, five wrap-rails, a local Test rail, and occupancy on shared links.** It is **not** yet the kernel 018 named. It is **not** HitPay. It is **not** escrow.

---

## What we must not say

If a sentence is in this table, a screen share will lie. 016 rows that 016–018 **did** retire are marked **retired** so a later editor does not keep repeating a closed smear.

| Do not say | Why live files refuse it | 016? |
|------------|--------------------------|------|
| “We replaced Hub.” | Root `docker-compose.yml` still builds `apps/lazuar-api` on 8080 into `lazuar_mvp`. No `apps/lazuar-pay/Dockerfile`. `docker-compose.pay.yml` is Postgres 5435 only. | still |
| “Pay is a kernel other apps can swallow in an afternoon.” | No `lzr_sk_` anywhere under `apps/lazuar-pay`. No outbound `payment.completed` producer. `packages/pay-spec` has no machine-key or outbound-webhook route. 018 said this; live files still say it. | **new as a headline** (018); still true |
| “A second Lazuar app is paying through this host.” | Merchant Vite and curl-with-human-Bearer are the only first-party callers. Hub’s `examples/hub-cashier-next` talks to **Hub**. | still (kernel gap) |
| “Escrow is on the Processor card.” | Processor cards are Test/Stripe/CHIP/Billplz/Xendit/Razorpay. No funded/inspect/release. 018: keep it off this card. | still (refuse) |
| “We are HitPay / WhatsApp pay links for aunties.” | English copy, no `navigator.share`, no `wa.me`, no Malay, no QR, no DuitNow tile. Occupancy exists; share-as-a-product does not. | still (SME gap) |
| “We have a payment-gateway factory of six.” | Switch of known names. Isolation bans `PaymentGatewayFactory` and `IEnumerable<IHostedRail>`. | keep as factory lie |
| “CHIP/Billplz/Xendit/Razorpay are not on 8081.” | They are, as `hosted_link` HTTP. | **retired** |
| “One active rail per org / PUT picks the default.” | Vault is independent. `ActiveProvider` stays null. Bind at mint. | **retired** (016 said the opposite) |
| “Five logos / wallets on `:5179`.” | No picker. Wallets, if any, are on the **processor** page. Test is a copy line, not a wallet tile (`locks.test.ts`). | still |
| “We take cards on our page.” | Redirect. No PAN. Test marks paid without a card — that is not taking cards. | still |
| “Off-session / vault / e-mandate / auto-debit.” | Capability `hosted_link`. CHIP start asserts `force_recurring` absent. Rail copy: “Not e-mandate.” | still |
| “Pay registers CHIP webhooks for you.” | No `ChipWebhookRegistrar`. Copy: paste PEM. Isolation now **does** ban the class name. | still |
| “We rewritten Billplz DNS / `lazuar-local-dev.com`.” | Predicate **rejects** that host. Isolation bans `PublicDnsFallback`. | still |
| “We file MyInvois / this is a Tax Invoice / SST is computed.” | Tax out. Official Receipt. | still, stronger |
| “Webhook secret is a platform env var for every rail.” | PUT requires per-org `webhook_secret`. Stripe process env is **Testing-only**. | **retired as the PUT/dev-fallback story**; do not revive |
| “`:5179` ignores `?status=verifying`.” | It polls; timeout now has Refresh. | **retired** |
| “Member can mint a pay link via curl.” | `RequireWriterAsync` on checkouts **and** payment-links. | **retired** |
| “`tenant.suspended` never sets pause / in-flight still books.” | HMAC verifies; fulfill 409s; event not consumed. Tests. | **retired** |
| “Start always mints a second PSP session.” | Stored `PspRedirectUrl` is returned; CHIP send count stays 1. | **retired** |
| “Razorpay cannot join without notes.” | `HostedSessionId` fallback + test. | **retired** as code; lived payload still runbook |
| “VIEWER is a One role.” | owner/admin/member. | still |
| “`/v1/orgs/{id}/ready` means we can charge.” | Dummy `ready: true` after member. | still |
| “We email receipts.” | `mail_outbox` table, no producer (`MailOutbox` never `Add`’d in `src/`). | still |
| “Compose is Pay.” | Root compose is Hub. Pay compose is DB. | still |
| “Subscriptions renew / interval follows the product.” | Checkout create and pay-link mint hard-code `Interval = "one_off"`. Catalog may store `mo`/`yr`; fulfill’s subscription branch is dead until create stops hard-coding. Product row is a **label** on the link (`product_id` is sent; amount is typed again). | still (reduced: product_id is no longer dropped on payment-links) |
| “Refunds work.” | No refund route under `apps/lazuar-pay`. | still |
| “pay-spec is the whole host.” | Spec still omits payment-links, occupancy, `slot_key`, Test, `GET /gateways`, payments, receipts, unversioned `/ready`. `CreateCheckoutRequest` in spec has **no** `provider` (host 400s without one). Start body has no `slot_key`. | still, worse vs 018 surface |
| “InMemory tests prove one DB transaction.” | Factory still ignores `TransactionIgnoredWarning`. The **two-SaveChanges** hole is gone (see 016 table). Postgres proof is not CI. | still as CI claim |
| “Test receipts are processor captures.” | Test writes the same `Official Receipt` / `RCPT-` title. Payments list shows `provider=test`. Receipts list does **not** return provider. | **new** |
| “A 1-person pay link closes after one successful payment.” | Copy says that. Occupancy counts **`open` or `paid`**. A Stripe start without a webhook fills the seat. | **new** (P1-1) |
| “Limited occupancy is race-safe.” | Count-then-insert, no lock on the parent row. Unique index is `(PaymentLinkId, SlotKey)`, not remaining seats. Sequential tests pass. Concurrent two-slot last-seat is untested. | **new** (P0-1) |
| “Bar B / 011 v1 is done.” | No lived B99. No refunds, no mail, no SST (refused), no kernel door, occupancy race open. | still |

### Demo footguns (the click will lie)

| Do not demo | What actually happens |
|-------------|------------------------|
| Curl `POST /v1/checkouts` and call it paid. | `status: "open"` unless provider is Test **and** you `POST .../start`. Money on live rails moves on verified Plane B. |
| Open `:5178` without `VITE_ZITADEL_CLIENT_ID`. | Alert; Sign in disabled. |
| Boot Hub `task dev` and One together. | Both want 8080. |
| Point `lazuar-ops` at 8081. | CORS denies 3003. |
| Paste only `sk_test_` and expect webhooks. | PUT 400s without `webhook_secret`. |
| Trust Stripe dashboard “success” without a tunnel. | SPA verifies for 30s, then “Not paid yet” + Refresh. Not paid. |
| Trigger One `tenant.suspended` without `Pay:OneWebhookSecret` matching One’s signer. | 503 missing secret, or 401 if dialect/secret mismatch. Pause tests are hermetic. |
| Tell a member “you cannot see last4.” | Last4 is on Processor cards for members. Intended. |
| Create “Dogfood” and claim the pay link loads catalog price/interval. | Label + independently typed amount. Interval stays `one_off`. |
| Billplz dogfood without public https `Pay:PublicBaseUrl`. | Start 400 `"callback base not public"`. SPA now shows that **detail**. |
| CHIP start with `customer@example.com`. | Host 400. SPA blocks it client-side now. |
| Production host + merchant “Test · Ready” card. | SPA **always** offers Test (`withTest`, `rails` includes `'test'`). Host in Production 400s `"test processor is not enabled"`. |
| Two phones, one “1 person” Stripe link, both tap Pay at once. | Occupancy race: both can mint, both can pay (P0-1). Sequential tests will not catch it. |
| POST `/v1/webhooks/test/{orgId}` in Development with `{"id":"x","checkout_id":"..."}`. | No signature. Amount/currency optional. Fulfills an open Test checkout. Same `RCPT-` as Stripe. |

---

## 016 P0/P1 re-verification table

016/10 ranked six inherited 014 P0s, five new P0s (A–E), and a P1 list. Folders moved. Re-read on `9f04ad58`.

### 016 / 014 P0s

| ID | 016 claim | New path | Verdict on 9f04ad58 |
|----|-----------|----------|---------------------|
| **P0-1** process-wide Stripe `whsec_` | `ResolveSecret` fell back for every **non-Production** empty ciphertext | `Rails/Stripe/StripeWebhook.cs` 78–91 | **FIXED.** Fallback is `env.IsEnvironment("Testing")` only. Empty ciphertext otherwise → `webhook secret missing` → 503. PUT still requires `webhook_secret` (`Credentials/GatewayEndpoints.cs` 60–63). Residual: leftover empty-ciphertext **Testing** rows still verify with the process secret — that is the test factory, not a forge-all-orgs vector in Development. |
| **P0-2** event committed before fulfill; org unbound | insert + `SaveChanges`, then fulfill’s own TX; `{orgId}` unbound | `Webhooks/WebhookEndpoints.cs` 114–170 | **FIXED as the two-SaveChanges hole. OPEN as Postgres proof.** Handler adds the event, calls `FulfillPaidAsync` (which `SaveChanges`s), then `Commit`. **No** `SaveChanges` between Add and fulfill. Org bind still 400 (`checkout.OrgId != orgId`). Unique `DbUpdateException` → `{ duplicate: true }`. `FillTests.Fulfill_throw_returns_5xx_event_not_committed_retry_pays` uses `FulfillmentProbe` throwing **before** inner `SaveChanges`, so even InMemory leaves the event absent. Factory still `Ignore(InMemoryEventId.TransactionIgnoredWarning)` (`PayApiFactory.cs` 27–54). Do not sell “one transaction” as a **Postgres** CI property. |
| **P0-3** SST fail-closed defeated by seed `false` | tax throw vs auto-seed | `Money/Fulfillment.cs`; `Data/Rows.cs` 8–9 | **FIXED** (tax deleted, not implemented). Column unused. Do not reopen as “we should compute SST.” Refuse. |
| **P0-4** One HMAC dialect wrong; fulfill ignores pause | body-only uppercase hex vs `t=,v1=`; fulfill did not read `ChargesPaused` | `Identity/OneWebhooks/OneWebhookSignature.cs`; `OneWebhookEndpoints.cs`; `Money/Fulfillment.cs` 32–35; `Webhooks/WebhookEndpoints.cs` 126–165 | **FIXED.** Standard Webhooks verify + skew. `org_id` or `tenant_id`. Suspend/reactivate tests. Handler 409 without consuming paid id; `ChargesPausedException` catch. `Start_paused_is_403_even_with_stored_url`. |
| **P0-5** `POST /v1/checkouts` was member | writer gate | `Checkouts/CheckoutEndpoints.cs` 29; `PaymentLinks/PaymentLinkEndpoints.cs` 27 | **FIXED.** Still `RequireWriterAsync`. `Member_cannot_create_checkout`. Payment-links also writer-gated. |
| **P0-6** setup-not-paid untested | | `WebhookTests` setup/zero; `ChipRailTests` preauth; `BillplzRailTests` unpaid; `RazorpayRailTests` failed; `XenditRailTests` SETTLED | **FIXED as proof** for those events. Residual tests (not 014’s P0): CHIP `purchase.payment_failure` then paid; Stripe omit-currency as its own method (parser now throws; no dedicated test name). |

### 016 new P0s (A–E)

| ID | 016 claim | New path | Verdict |
|----|-----------|----------|---------|
| **P0-A** public start not idempotent | every click `CreateHostedUrlAsync` | `PublicPayEndpoints.cs` 151–155 | **FIXED.** If `PspRedirectUrl` is set, persist payer fields and return it. `Start_twice_returns_same_url_without_second_psp_http`. SPA continues (`App.tsx` 123–126). Residual: SaveChanges **after** PSP HTTP for CHIP/Billplz/Xendit/Razorpay can still orphan a processor session if persist throws (P1-3). Stripe has an idempotency key. |
| **P0-B** HMAC + pause | see P0-4 | see P0-4 | **FIXED.** |
| **P0-C** Razorpay notes-only join | `notes.checkout_id` only | `Rails/Razorpay/RazorpayWebhook.cs` 85–107; `WebhookEndpoints.cs` 101–107 | **FIXED in code + hermetic test.** Lived `payment.captured` without `payment_link.entity` is still **runbook**. |
| **P0-D** parser mismatch 400, no event — lost cash if **we** are wrong | CHIP total units, Xendit major, Billplz sen, Stripe skip currency | parsers now have unit comments; Billplz/Stripe/CHIP/Xendit/Razorpay throw `missing currency`; `FillTests.Amount_mismatch_does_not_mint_receipt` | **FIXED as the skip-currency/default-MYR holes on the five PSPs. OPEN as lived-payload proof.** Mismatch 400 **still does not consume** the event id — correct fail-closed if the payload is hostile; still lost-cash if a live CHIP `total` is major units. Hermetic fixtures pin CHIP `total: 1000` for RM10, Xendit `paid_amount: 10` major, Billplz `paid_amount=1000` sen, Razorpay `amount: 1000` minor, Stripe `amount_total: 1000`. Do not call units “production-proven.” Test rail still **omits** currency/amount without throwing (P1-2). |
| **P0-E** residual Stripe platform `whsec_` on empty ciphertext, non-Production | fallback every non-prod | `StripeWebhook.cs` 85–90 | **FIXED.** Testing-only. |

### 016 P1s (short)

| 016 ID | Verdict | Notes |
|--------|---------|-------|
| P1-1 one-TX unproven | **OPEN as CI**, not as the original two-SaveChanges bug | Probe seam exists. Still InMemory. |
| P1-2 Billplz hardcodes MYR | **FIXED** | `BillplzWebhook.cs` 77–80 `TryNormalizeCurrency` or throw. |
| P1-3 Stripe omit currency skips check | **FIXED** on Stripe parser | `StripeWebhook.cs` 63–66 throw. Handler skip remains for null currency (Test). |
| P1-4 CHIP create sends no purchase currency | **OPEN** | `ChipHosted.cs` 43–54 products price only. See P1-4 below. |
| P1-5 localhost 5179 success URLs | **FIXED** as config | `CheckoutUrls.Base` requires `Pay:CheckoutBaseUrl` outside Testing. Merchant `VITE_CHECKOUT_ORIGIN`. |
| P1-6 verifying 30s trap | **MOSTLY FIXED** | Timeout sets `verifyTimedOut` and a Refresh button. Does not keep polling. No return-to-Pay (good vs double-start). |
| P1-7 SPA maps all 503/400 to one string | **FIXED** | `readDetail` / `problemDetail`. Checkout locks assert the collapsed Billplz sentence is **gone**. |
| P1-8 git wrap key outside Production | **FIXED** | `SecretBox.LoadKey` throws unless Testing. |
| P1-9 catalog decorative; interval always `one_off` | **PARTIAL** | Payment-links now send `product_id` (label). Amount still typed. Interval still `"one_off"` on mint (`PublicPayEndpoints.cs` 257, `CheckoutEndpoints.cs` 92). |
| P1-10 SaveChanges after PSP | **OPEN** for four HTTP rails | Comment still in `PublicPayEndpoints.cs` 170–171. Stripe idempotency key mitigates Stripe. |
| P1-11 Razorpay other-event ids | **MOSTLY FIXED** | Non-captured uses `headerEventId` or `eventType:paymentId` or `eventType:none`, not bare `"razorpay"`. |
| P1-12 email_required one-shot / placeholder | **PARTIAL** | GET now returns `provider`. SPA still GETs once on mount; placeholder blocked client-side. |
| P1-13 spec residuals | **OPEN**, larger | Payment-links / Test / slot_key / processors list missing. Spec `CreateCheckoutRequest` lacks `provider`. |
| P1-14 no Pay image; compose Hub; CORS literals | **OPEN** | Preview origins 4178/4179 added. Still no Dockerfile. Root compose Hub. |
| P1-15 `Billplz_paid_form_and_localhost_blocked` name lies | **PARTIAL** | Real 400 test exists **next** to it. Old name remains. |
| P1-16 member last4 | **INTENDED** | Processor cards show last4 to members. Copy should keep saying public metadata. |

**016’s P0 list is retired as a cash list** except the **shape** of P0-D (lived units) and P0-2 (Postgres proof). The **new** P0 is occupancy, which 016 did not have because payment-links did not exist.

---

## Ranked bugs (P0/P1/P2) with evidence and how to solve

Priority is blast radius on **cash and Official Receipts**, then product-false, then polish. “How to solve” is analysis, not a PR stack.

### P0 — money can be wrong, doubled, or a receipt can lie about who paid

#### P0-1. Pay-link occupancy is count-then-insert. Two slots can take the last seat.

**Evidence.** Capacity is a read of child rows, then an insert, with **no** transaction and **no** lock on `payment_links`:

```236:264:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var taken = await db.Checkouts.CountAsync(
            x => x.PaymentLinkId == link.Id && (x.Status == "open" || x.Status == "paid"),
            ct);
        if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
        {
            return (null, PayErrors.Status(409, "Conflict", "This pay link is full"));
        }
        // ...
        db.Checkouts.Add(row);
        await db.SaveChangesAsync(ct);
        return (row, null);
```

The only unique index involving occupancy is **per slot**, and only when the provider is Npgsql:

```43:48:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
            if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                e.HasIndex(x => new { x.PaymentLinkId, x.SlotKey })
                    .IsUnique()
                    .HasFilter("\"SlotKey\" IS NOT NULL");
            }
```

Migration `20260825120000_PaymentLinkPayers` creates that filtered unique index. It does **not** constrain `COUNT(*) WHERE status IN ('open','paid') <= max_payers`.

Hermetic tests are **sequential**: `Two_people_can_pay_a_link_of_two` starts A, then B, then C. `Same_slot_start_twice_does_not_take_two_seats` proves the slot unique **logically** (InMemory has no filtered unique index either; the code path returns the existing row before insert).

**Race:**

1. Merchant mints a 1-person (or N-person, remaining = 1) Stripe/CHIP/… link.
2. Two browsers (two `slot_key`s) `POST /start` at once.
3. Both read `taken = 0` (or `N-1`).
4. Both insert open children, both call `CreateHostedUrlAsync`, both pay.
5. Two Plane B fulfills, two `RCPT-`, two journal pairs. Link list shows `taken_count = 2` on a max of 1.

Pay’s per-checkout ledger is not double-booked **per checkout**. The **product** “1 person only” is. Ada’s PSP is charged twice. This is 016 P0-A’s cousin after occupancy shipped.

**How to solve.** Serialize mint on the parent row (`SELECT … FOR UPDATE` on `payment_links` in the same TX as the child insert), **or** a constraint that cannot be expressed as a simple unique index (advisory lock / `INSERT … WHERE remaining`). On unique/capacity violation, 409 `"full"` and **do not** call the PSP. Write a **concurrent** test (two starts, `max_payers=1`, FakePspHandler send count ≤ 1, documents ≤ 1). InMemory cannot prove the SQL lock; this is a Postgres test or a host-level mutex in Testing.

Do not “fix” it by dropping occupancy. The copy is already selling it.

---

### P1 — product-false, dogfood, money-adjacent

#### P1-1. Occupancy counts `open`, not paid. Copy says the opposite.

**Evidence.**

```5:6:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs
    public static bool CountsTowardCapacity(string status) =>
        status is "open" or "paid";
```

Staff copy:

```397:400:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
                  {capacity === 'one'
                    ? 'The link closes after one successful payment.'
                    : 'Anyone with the URL can pay. It does not close on its own.'}
```

A Stripe (or CHIP, …) start creates an `open` child **before** Plane B. That child fills a 1-person link. A second phone with a different `slot_key` gets 409 `"full"` / SPA “Link is full” even though nobody paid. Merchant table shows `1 / 1` and status `full` (statusLabel only relabels `full` → `paid` when `paid_count >= 1`).

Failed PSP start after mint has the same shape: `MintOrResume` `SaveChanges`s the child, **then** email/rail/CreateHostedUrl:

```96:148:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            var minted = await MintOrResume(link, body, db, config, env, ct);
            // ...
            row = minted.Row!;
        }
        // ... name/email copied ...
        if (PayProviders.RequiresEmail(name) && !BuyerEmail.IsUsable(row.PayerEmail))
        {
            return PayErrors.Status(400, "Bad Request", "email is required");
        }
```

Same slot can resume (existing open row). A **different** slot cannot. CHIP 503 after mint griefs the last seat.

Test rail hides this: start fulfills immediately, so `open` is brief.

**How to solve (product decision, then code).** Either:

- **A.** Count only `paid` toward capacity, and treat `open` as a reservation with a TTL (expire unpaid starts, free the seat). Matches the copy. Needs an expire job (none exists; SPA has an expired **view** only).
- **B.** Keep counting `open`, and **change the copy** to “closes after someone starts Pay.” Honest, worse cashier.
- **C.** Reservation holds only for this `slot_key` for N minutes; others see remaining including reservations.

Prefer A for SME cashier. Write tests: start Stripe without webhook on max=1 → second slot still 200 **or** documented reservation; after TTL, second slot 200.

#### P1-2. Test Plane B has no signature. Amount and currency are optional. Receipts look like Stripe.

**Evidence.** Webhook handler skips credentials for Test:

```49:77:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        if (PayProviders.IsTest(name))
        {
            if (!PayProviders.AllowsTest(env))
            {
                return PayErrors.Status(400, "Bad Request", "rail not configured");
            }
        }
        // ...
                PayProviders.Test => TestWebhook.Parse(raw),
```

`AllowsTest` is `!env.IsProduction()` (`PayProviders.cs` 21–22). Staging that is not named Production is open.

Parser:

```24:57:apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs
            if (string.IsNullOrWhiteSpace(eventId))
            {
                eventId = "test:" + Guid.NewGuid().ToString("N");
            }
            // amount_total / currency optional
```

Handler skips amount when `AmountMinor is null` and currency when `Currency is null` (`WebhookEndpoints.cs` 132–141). Each unsigned POST can mint a unique event id, so replay protection does not bind a hostile caller.

`TestRailTests.Webhook_pays_open_test_checkout` posts JSON with **no** auth header and expects a document.

Fulfillment title is always `"Official Receipt"` (`Fulfillment.cs` 118). Payments JSON includes `provider`; receipts JSON does not (`PaymentQueryEndpoints.cs` 99–116 vs 43–60). `ReceiptsPage.tsx` has no processor column. `PaymentQueryTests.List_receipts_includes_number_amount_and_payer` does not assert provider.

Production refuses Test mint (`CheckoutEndpoints.cs` 58–62, `PaymentLinkEndpoints.cs` 56–60). Merchant SPA **always** offers Test:

```32:37:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
function withTest(list: Processor[]): Processor[] {
  const ready = list.filter((p) => p.configured && isRail(p.provider))
  if (!ready.some((p) => p.provider === 'test')) {
    ready.unshift(testProcessor)
  }
```

`processors.ts` `rails` includes `'test'`. `GatewayPage` renders a Test card “Ready.” Host list omits Test in Production; the SPA puts it back.

**How to solve.** (1) Test webhook: require a process secret in non-Testing, or refuse Test webhooks entirely (start-pays is enough for dogfood). (2) Require amount+currency on Test parse, same as PSPs. (3) Receipts API + table: show `provider`, badge Test. (4) SPA: offer Test only when `GET /gateways` includes it. (5) Test: Production-like env, `POST /v1/checkouts` provider=test is 400; unsigned Test webhook 400.

This is P1 not P0 because **no PSP capture**. It becomes a P0-class honesty bug the moment a merchant forwards a Test `RCPT-` as a paid invoice.

#### P1-3. PSP HTTP then persist. Retry can mint a second session on four rails.

**Evidence.**

```168:190:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            // PSP HTTP then persist. A SaveChanges failure after the processor
            // already created a session may mint a second session on retry.
            var hosted = await rail.CreateHostedUrlAsync(row, ct);
            row.Provider = name;
            row.PspRedirectUrl = hosted.RedirectUrl;
            row.ProviderSessionId = hosted.ProviderSessionId;
            // Test fulfills; else SaveChanges
```

If `SaveChanges` throws, `PspRedirectUrl` is empty, next start calls Create again. Stripe mitigates with `IdempotencyKey = "lazuar-checkout:" + checkout.Id` (`StripeHosted.cs` 50). CHIP/Billplz/Xendit/Razorpay have no equivalent.

**How to solve.** Persist a `psp_create_pending` token **before** HTTP, or send rail-native idempotency keys (CHIP/Xendit/Billplz as they support). On persist failure, do not leave the row looking unstarted. Test: FakePspHandler succeeds, intercept SaveChanges throw, retry, send count stays 1. Stripe already has the key; write that test first as the pattern.

#### P1-4. CHIP purchase has no currency field.

**Evidence.** `ChipHosted.cs` 43–54: `products[].price` in minor, metadata, no `currency`. Webhook fail-closes if CHIP omits currency (`ChipWebhook.cs` 86–88). If CHIP **defaults** MYR and the checkout is USD, amount minors can still match then 400 currency — paid at CHIP, no `RCPT-`. Dogfood is MYR (`CatalogEndpoints` refuses non-MYR; checkout/pay-link **default** MYR).

**How to solve.** Send CHIP’s documented currency field if it exists (steal Hub judgment, do not steal the adapter class). Lived payload in the runbook. Test: FakePspHandler last body contains the checkout currency.

#### P1-5. Official Receipt numbers are not unique under concurrency.

**Evidence.** `Fulfillment.cs` 102–111: `FindAsync` sequence, `LastN += 1`, format `RCPT-{year}-{LastN:00000}`. `documents` PK is `Id` only (`PayDbContext.cs` 108–111). No unique on `Number`. Two concurrent fulfills (unlimited link, two Test starts, or P0-1’s double seat) can mint the same number.

**How to solve.** Unique `(OrgId, Number)`. Increment in the same TX as the document insert (`UPDATE document_sequences SET last_n = last_n + 1 RETURNING last_n`). Test two concurrent fulfills; numbers differ.

#### P1-6. InMemory is still not a transaction proof.

**Evidence.** `PayApiFactory.cs` 27–54. Probe test is a **seam**, not Npgsql. If `Fulfillment.SaveChangesAsync` succeeded and `CommitAsync` did not, InMemory would have already persisted (TX ignored). Current fulfill throws are **before** SaveChanges, so the seam is honest for those throws.

**How to solve.** One test project (or one fixture) on SQLite with transactions or Testcontainers Postgres. Do not replace InMemory for the whole suite in the same slice if that slows CI — add **one** fulfill-throw-retry class that requires a real TX.

#### P1-7. `Never_started_checkout_webhook_is_400` is a synthetic nulling of `Provider`.

**Evidence.** `CheckoutEndpoints.Create` writes `Provider` at mint (`CheckoutEndpoints.cs` 86). `FillTests.Never_started_checkout_webhook_is_400` sets `Provider = null` then expects `"provider mismatch"`. A minted-but-never-started Stripe checkout already has `Provider = stripe`. A forged `checkout.session.completed` with the org `whsec_` would pass the mismatch check. Real Stripe will not emit that event without a session; Test webhook will (`Webhook_pays_open_test_checkout` does not start).

**How to solve.** If the lock is “no cash without start,” require `PspRedirectUrl` or `ProviderSessionId` before fulfill (Test start-pays sets session id `test:{checkoutId}`). If the lock is only “path rail matches checkout rail,” rename the test. Do not keep a test that nulls a column the product always writes.

#### P1-8. pay-spec is behind the host.

**Evidence.** `packages/pay-spec/main.tsp`: no payment-links, no occupancy fields, no `slot_key`, no Test, no `GET /v1/orgs/{id}/gateways`, no payments/receipts. `CreateCheckoutRequest` has no `provider` (host 400 `"unknown provider"`). `StartPayRequest` has name/email only. `PublicPay` has `started`/`redirect_url` but not `provider`/`remaining`. Webhook response typed `{ ok: boolean }` only.

**How to solve.** Spec follows the host **after** occupancy/Test stabilize. Do not generate clients from this spec and call them the door. Isolation already keeps Hub `api-spec` out of the Vite apps.

#### P1-9. Catalog amount/interval still do not drive the charge.

**Evidence.** SPA posts a product then a payment-link with the **same typed amount**, `currency: 'MYR'`, `product_id` (`CheckoutsPage.tsx` 161–185). Host stores `Interval = "one_off"` regardless of catalog price interval (`PublicPayEndpoints.cs` 257). Fulfillment subscription branch (`Fulfillment.cs` 63–74) is dead. Catalog create still says `"Bar B currency is MYR"` (`CatalogEndpoints.cs` 33–36).

**How to solve.** Either load amount/interval from `prices` when `product_id` is set, or drop the product POST and keep a label string. Do not demo “subscriptions.” Honesty copy on the create dialog is already “MYR” / one-off.

#### P1-10. CORS and compose are still laptop-shaped.

**Evidence.** `Program.cs` 61–69: eight localhost origins. No Pay Dockerfile (grep). Root `docker-compose.yml` builds Hub `apps/lazuar-api`. `docker-compose.pay.yml` is DB.

**How to solve.** Later, after money. Preview 4178/4179 already landed. Do not retarget ops/portal.

#### P1-11. `/v1/orgs/{id}/ready` is still dummy.

**Evidence.** `OrgReadyEndpoints.cs` 25: `Ready = true` after member. `OrgReadyTests.Ready_when_one_allows_member`.

**How to solve.** Do not silently change the meaning. Either delete the route from demos or define ready as “at least one non-Test rail with webhook_configured.” Not this money slice.

#### P1-12. Verifying poll still dies at 30s.

**Evidence.** `App.tsx` 99–112: `n >= 15` → `setVerifyTimedOut`. Footer Refresh GETs once (`255–273`). Stripe retries can be minutes.

**How to solve.** After timeout, poll with backoff, or Refresh restarts the interval. Do **not** re-enable Pay on the verifying screen (that fights P0-A, already fixed).

---

### P2 — after money is boring

- `Billplz_paid_form_and_localhost_blocked` still does not block localhost (`BillplzRailTests.cs` 12–54). Split the name; the real 400 test is `Billplz_localhost_callback_start_is_400_without_psp_http`.
- `SstRegistered` and `ActiveProvider` leftover columns (`Rows.cs` 8–11).
- `mail_outbox` leftover; `AddDataProtection()` unused by `SecretBox`.
- GET `?provider=` exists; SPA lists `/gateways` (good). Bare GET `/gateway` now returns the list (`GatewayEndpoints.Get` with empty provider calls `List`). Spec still documents a single `GatewayView`.
- Receipt GET by id returns number/title/checkout_id only — no amount (`PaymentQueryEndpoints.cs` 136–143).
- No expire-open-sessions job; expired **UI** is dead until something sets `status = expired`.
- No receipt PDF.
- Checkout SPA has no vitest of `App.tsx` behavior (locks are source greps).
- `PutGatewayRequest.KeyId` unused by SPA (SPA concatenates Razorpay).
- CHIP `ImportFromPem` failure is 400 invalid signature (`ChipWebhook.cs` 45–47).
- Isolation does not grep `lzr_sk_` / outbound webhook types (they are absent; a later kernel slice must not import Hub’s outbox worker).
- Program.cs default connection string embeds `Password=postgres` when the env looks empty (`Program.cs` 50–54). Local-only; do not ship that as a Production default.
- `PayProviders.All` is five names; `Listed` is six. Test names still say “all five.”
- Merchant create-workspace / OIDC is runbook, not Playwright.

---

## Ranked gaps (kernel, cashier, occupancy, Test rail, shell)

Gaps are **missing product**, not necessarily bugs. 018’s thesis: the kernel is the strongest idea; escrow and WhatsApp SME are other buyers.

### G1. Kernel door — machine key + outbound event (018’s actual bet)

018:

> Until a second app (not the merchant Vite) can `POST` a checkout with a machine token and get a signed `payment.completed`, you do not have a kernel. You have a dashboard that mints links.

Live:

- Grep `lzr_sk_` in `apps/lazuar-pay/**` is empty. `Bearer.TryGet` accepts any `Bearer ` string and forwards it to One `GET me` / `authz/check`. One **human** access_token works. A Pay-issued machine key does not exist. 011 put `lzr_sk_` on **One** ([02-one-integration.md](../011-new-lazuar-pay/02-one-integration.md) line 15). Hub still introspects machine keys in `packages/api-spec`. New Pay never calls that.
- Grep `payment.completed` in `apps/lazuar-pay/**` and `packages/pay-spec/**` is empty. Fulfillment writes `AuditEventRow` `checkout.paid` **in-process** (`Fulfillment.cs` 121–127). Nothing POSTs a signed envelope to a merchant URL. Hub still has `IntegrationCheckoutGatewayEventsHandler` / One outbound dispatcher. Isolation forbids importing those types.
- `POST /v1/checkouts` **does** exist (human writer + `Idempotency-Key`). That is half a kernel object. Without machine auth and outbound verify, a second app still polls or scrapes.

**Occupancy of this gap:** first-party reuse (Aura / next Lazuar app) cannot dogfood 8081 without pretending to be a merchant SPA. That is exactly 018’s “if Aura still talks to CHIP directly, the idea is a slide.”

**How to solve (later than P0-1).** One-shaped door, not Hub’s factory:

1. Accept One-issued `lzr_sk_` **or** a Pay-issued key wrapped in `SecretBox`, scoped to `org_id` + `checkout:write`. Do not hold a Zitadel PAT (`NP-XX-007`).
2. Outbox row in the **same TX** as fulfill: `payment.completed` with amount, currency, `RCPT-`, checkout id. Sign like One (`t=,v1=` over `{unix}.{body}`). Retry worker in-process (011: Notify stays in Pay until a second caller exists).
3. Sample repo: Next.js that is **not** `lazuar-pay-merchant`.
4. IsolationTests: still ban Hub types; **add** a ban on `Modules.One` outbound worker class names if someone tries to project-reference them.

Do **not** staff this in the same slice as occupancy. A kernel on a racy 1-person link is a worse kernel.

### G2. SME cashier — WhatsApp / Malay / QR / share

018: HitPay already sells “send a WhatsApp payment link this afternoon.” This SHA has occupancy and a copy link button (`CheckoutsPage.tsx` 196–204, 286–297). No `navigator.share`, no `wa.me`, no Malay strings, no QR, no FPX tile on `:5179` (locked **out** by checkout `locks.test.ts`). Buyers land on an English card.

**Honest occupancy of the SME painkiller:** a cousin can open `:5179` on a phone **if** the merchant pasted the URL. Reconciliation is the Official Receipt list. Freeze-story marketing without named payers + no PAN is partially true (payer name/email are collected; Test skips email). It is not a product differentiator.

**How to solve.** After occupancy is true: share sheet, `wa.me/?text=` with the checkout origin URL, Malay copy **as a skin**, keep wallets on the processor page. Do not build DuitNow tiles on `:5179`. Setup-as-a-service (open CHIP, paste keys) is a **services** SKU, not a Processor card.

### G3. Occupancy as a product

Shipped: max=1 / N / unlimited, slot_key, full page, sequential tests. Missing: concurrency (P0-1), reservation vs paid (P1-1), cancel/expire of unpaid starts, staff visibility of **open un-paid seats** vs paid, identity of a slot (phone vs incognito vs shared iPad). `localStorage` means one browser is one payer; a shared family phone is one payer; two incognitos are two payers. That is defensible if written down. It is not written on the create dialog.

### G4. Test rail as a product

Shipped: no secrets, start=paid, Production host refuses mint, Isolation-friendly dogfood. Missing: signed webhook or no webhook, receipts badge, SPA hiding Test when the host hides it, a **distinct document title** or series (`TEST-` vs `RCPT-`) so Test cannot pass as cash. 018 put Test on the Processor card next to live rails. That is correct for dogfood and dangerous for honesty (P1-2).

Do **not** put escrow on that card (018 refuse). Test is already crowding it.

### G5. Merchant shell vs 016 WorkspacePage

Shipped: Aura chrome, last-workspace home, staff email, processor cards, Edit dialog, pay-link table, payments/receipts tables matching chrome (HEAD commit). Missing: PDF, refund button (refuse until refunds exist), SST toggle (refuse), kernel API-key screen, webhook delivery log, Billplz hint still shows `{payApi}/v1/webhooks/billplz/{orgId}` which is localhost 8081 while start uses `Pay:PublicBaseUrl` (`GatewayPage.tsx` 304–315 — copy now **warns** localhost will fail). That warning is honest; the `<code>` is still the wrong URL to paste into Billplz.

### G6. 011 v1 that this program explicitly dropped or parked

011 must-have included SST fail-closed, refunds, mail, buyer magic link, subscriptions renew. 015/016 parked tax, refunds, off-session. This SHA still has none of those. **Do not treat 011/01 as a bug list against 9f04ad58.** Treat 018 kernel + this paper’s P0-1 as the live scoreboard.

---

## Sequence: fix money, write the test that would have caught it, then SPA

Do **not** staff a factory, a registrar, DNS, SST, e-mandate, escrow on Processor, Hub cutover, or ops/portal retarget in this sequence. Do **not** flip 011/11 cells from this paper.

### 1. Money (product code)

1. **Serialize pay-link mint (P0-1).** Parent-row lock or equivalent. PSP HTTP only after the seat is committed. If the lock fails, 409 full.
2. **Decide occupancy grain (P1-1) and implement it.** Recommended: count paid + in-window reservation; expire unpaid `open` children; match the “successful payment” copy. Same slice as (1) or the copy will keep lying.
3. **Test rail cannot forge Official Receipts from the public internet (P1-2).** Refuse Test webhooks outside Testing, or sign them. Distinct series or provider on receipts.
4. **Unique `RCPT-` (P1-5).** Same TX as fulfill.
5. **Do not implement tax, refunds, factory, escrow here.**

CHIP currency (P1-4) and PSP-then-persist (P1-3) are next money, not the occupancy hole.

### 2. The test that would have caught it (hermetic unless noted)

Order matches leftover cash.

| Priority | Test | Why | Blocks saying |
|----------|------|-----|----------------|
| T0 | Two concurrent `POST /start` on `max_payers=1`, different slot keys, FakePspHandler; documents ≤ 1; PSP HTTP ≤ 1 | P0-1 | “1 person only” |
| T1 | Stripe start without webhook on max=1; second slot’s outcome matches the **written** occupancy rule | P1-1 | “closes after one successful payment” |
| T2 | Production env: provider=test mint 400; unsigned Test webhook 400 | P1-2 | “Test is local-only” |
| T3 | Two concurrent fulfills → distinct `RCPT-` numbers; unique index trip | P1-5 | “Official Receipt numbers” |
| T4 | Postgres/SQLite fulfill-throw: event absent, retry pays | 016 P0-2 residual | “one transaction” |
| T5 | CHIP start body includes currency; amount mismatch 400, event absent (already for Stripe) | P1-4 / 016 P0-D | “we fail closed on currency” |
| T6 | SaveChanges-fail after FakePsp success, retry send count 1 (CHIP) | P1-3 | “start is idempotent” under persist failure |
| T7 | Receipts list includes `provider`; Test ≠ Stripe in the merchant table (SPA grep or API) | P1-2 | “this RCPT is a card capture” |
| T8 | SPA: Test offered only when host lists it; locks.test | P1-2 | Production Test card |
| T9 | Rename/split `Billplz_paid_form_and_localhost_blocked` | 016 P1-15 | test name as evidence |

Lived PSP fixtures (CHIP `purchase.paid` JSON, Razorpay `payment.captured` with/without `payment_link`) stay runbook JSON checked in and replayed with FakePspHandler — not live CHIP in `task pay:test`.

### 3. SPA (after T0–T3 are red then green)

- Occupancy copy matches the rule (paid vs reserved).
- Receipts processor column; Test badge.
- Hide Test when host hides it.
- Billplz webhook `<code>` uses `Pay:PublicBaseUrl` **or** stops pretending the localhost path is pasteable (warning exists; the code still prints `payApi`).
- Verifying backoff (P1-12).
- Share sheet / `wa.me` **only** after occupancy is true — that is cashier GTM, not a money fix.

### 4. Kernel (after the cashier does not double-seat)

Machine auth + signed `payment.completed` + a second-app sample. 018’s strategy. Not step 1.

---

## Refuse list

Keep. Un-refusing these is how the museum comes back. Isolation locks **some** by string; the rest are process law.

| Refuse | Why live files + 018 still bind |
|--------|----------------------------------|
| **SST / e-invoice / LHDN / VALID / UBL** | Tax is out. `SstRegistered` unused. 014 P0-3 is not permission to bring SST back. 011 fail-closed was amended. Parked `parked-lhdn-sst.md`. |
| **Factory / `IPaymentGatewayAdapter` / `IEnumerable<IHostedRail>`** | Switch of known names. Isolation bans the tokens. A sixth **PSP** is a new folder + two switch arms, not a DI lookup. Test is a sixth **name** that is not a PSP — do not “clean that up” into a factory. |
| **CHIP registrar** | Isolation now bans `ChipWebhookRegistrar`. Dashboard PEM. |
| **PublicDnsFallback / `lazuar-local-dev.com` rewriter** | Isolation bans the class. Billplz fails closed on that host. |
| **Hub cutover / retarget ops `:3003` or portal `:3004`** | CORS denies them. Root compose still Hub. Five-plus-Test rails on 8081 ≠ Hub dark. |
| **Escrow on the Processor card** | 018: different buyer, different surface (`funded → inspect → released`). Test already occupies “not a real PSP” on that grid. |
| **E-mandate / off-session / `force_recurring` / silent debit** | Capability `hosted_link`. CHIP test asserts absence. |
| **Refunds as a quiet extra** | No Billplz bill-refund. Journal reverse must be once, later, parked. |
| **Zitadel PAT inside Pay / VIEWER role / merchants on `:5173`** | 011 NP-XX-007 / 018. |
| **Stripe Billing `subscription.updated` as SoT** | Interval is `one_off`. Do not “fix” subscriptions by listening to Stripe. |
| **Generating pay-spec clients and calling them the host** | Spec is behind. |
| **Sixth commercial rail “to complete SEA”** | 016 parked-sixth-rail. Test is not GrabPay. |

---

## Next ten work items (named, not coded)

1. **Occupancy lock** — serialize last-seat mint so two slots cannot both pay a 1-person link.  
2. **Occupancy definition** — paid vs open reservation + expire unpaid starts; rewrite the create-dialog sentence to match.  
3. **Concurrent occupancy test** — the test that would have caught P0-1 (Postgres or an explicit lock seam).  
4. **Test rail honesty** — no unsigned webhook outside Testing; receipts show provider; SPA hides Test when the host does; consider `TEST-` series.  
5. **RCPT uniqueness** — unique number + serial increment in the fulfill TX + concurrent fulfill test.  
6. **Real-TX fulfill-throw** — one fixture that is not InMemory so 016 P0-2 can be said as proof.  
7. **CHIP currency on create + lived unit fixtures** — pin P0-D so lived CHIP cannot 400-loop.  
8. **PSP-then-persist** — CHIP/Billplz/Xendit/Razorpay idempotency or persist-before-HTTP.  
9. **pay-spec catch-up** — payment-links, slot_key, Test, processors list, provider on create — after 1–4 stabilize.  
10. **Kernel dogfood door** — One `lzr_sk_` (or Pay equivalent) + signed `payment.completed` + a second app that is not `:5178`. Not on the Processor card. Not escrow.

Items 1–3 are the money sequence. 4–5 are honesty of documents. 6–8 are leftover 016 cash. 9 is contracts. 10 is 018’s company, after the cashier stops double-seating.

---

## Appendix: quoted evidence

### A. Start idempotency (016 P0-A **FIXED**)

```151:155:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        if (!string.IsNullOrWhiteSpace(row.PspRedirectUrl))
        {
            await db.SaveChangesAsync(ct);
            return Results.Json(new { redirect_url = row.PspRedirectUrl }, OneClient.Json);
        }
```

```39:78:apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs
    public async Task Start_twice_returns_same_url_without_second_psp_http()
    {
        // ...
        Assert.That(factory.Psp.SendCount, Is.EqualTo(1));
        // second start
        Assert.That(secondDoc.RootElement.GetProperty("redirect_url").GetString(), Is.EqualTo(url));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(1));
```

### B. One HMAC + pause (016 P0-4 **FIXED**)

```6:42:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs
/// Standard Webhooks–style verify: header t={unix},v1={lowercase hex} over {unix}.{body}.
        var signedPayload = $"{timestamp}.{body}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();
```

```32:35:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        if (settings?.ChargesPaused == true)
        {
            throw new ChargesPausedException();
        }
```

```126:130:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        if (orgSettings?.ChargesPaused == true)
        {
            return PayErrors.Status(409, "Conflict", "Org charges are paused");
        }
```

```56:68:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs
    public async Task Body_only_uppercase_hex_is_401()
```

```209:256:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/WebhookTests.cs
    public async Task Paused_org_does_not_mint_receipt()
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), ...);
            Assert.That(db.PspWebhookEvents.Count(e => e.EventId == eventId), Is.EqualTo(0));
        // unsuspend, retry, Documents.Count == 1
```

### C. Stripe `whsec_` Testing-only (016 P0-1 / P0-E **FIXED**)

```78:90:apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs
        if (!string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            return box.Unprotect(cred.WebhookCiphertext);
        }

        if (env.IsEnvironment("Testing"))
        {
            return config["Pay:StripeWebhookSecret"];
        }

        return null;
```

### D. Wrap key Testing-only (016 P1-8 **FIXED**)

```36:46:apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs
        if (string.IsNullOrWhiteSpace(b64))
        {
            if (!env.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException("Pay:WrapKey is required");
            }

            return SHA256.HashData("lazuar-pay-dev-wrap-key"u8.ToArray());
        }
```

### E. Fulfill TX shape (016 P0-2 **coded closed**)

```143:170:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PspWebhookEvents.Add(new PspWebhookEventRow { ... });
            await fulfillment.FulfillPaidAsync(checkout.Id, name, parsed.ProviderRef, ct);
            await tx.CommitAsync(ct);
        }
        catch (InvalidOperationException)
        {
            await tx.RollbackAsync(ct);
            return PayErrors.Status(500, "Internal Server Error", "fulfill failed");
        }
```

```27:31:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs
    /// InMemory BeginTransaction is a no-op. H25/G12 proof uses FulfillmentProbe,
    /// which throws before Fulfillment.SaveChanges so the event row is not committed.
```

### F. Razorpay join fallback (016 P0-C **FIXED**)

```101:107:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        if (string.IsNullOrWhiteSpace(checkoutId) && !string.IsNullOrWhiteSpace(parsed.HostedSessionId))
        {
            var bySession = await db.Checkouts.FirstOrDefaultAsync(
                x => x.OrgId == orgId && x.Provider == name && x.ProviderSessionId == parsed.HostedSessionId, ct);
            checkoutId = bySession?.Id;
        }
```

```109:135:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Razorpay/RazorpayRailTests.cs
    public async Task Razorpay_captured_without_notes_joins_plink()
```

### G. Test rail start=paid + unsigned webhook

```176:186:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            if (PayProviders.IsTest(name))
            {
                db.PspWebhookEvents.Add(...);
                await fulfillment.FulfillPaidAsync(row.Id, name, hosted.ProviderSessionId, ct);
            }
```

```42:58:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs
    public async Task Webhook_pays_open_test_checkout()
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(
                $$"""{"id":"evt_test_1","checkout_id":"{{checkoutId}}","amount_total":1000,"currency":"myr"}""",
```

### H. Independent vault (016 “one active rail” **retired**)

```172:173:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs
        Assert.That(db.OrgSettings.Single().ActiveProvider, Is.Null);
        Assert.That(db.GatewayCredentials.Count(), Is.EqualTo(2));
```

```122:123:apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx
        title="Processor"
        subtitle="Vault keys per rail. Saving a secret does not pick the rail for pay links."
```

### I. CheckoutBaseUrl (016 P1-5 **FIXED**)

```18:32:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs
    public static string Base(IConfiguration config, IHostEnvironment env)
    {
        var raw = config["Pay:CheckoutBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }
        if (env.IsEnvironment("Testing"))
        {
            return "http://localhost:5179";
        }
        throw new InvalidOperationException("Pay:CheckoutBaseUrl is required");
    }
```

### J. Kernel absence

No `lzr_sk_` / `payment.completed` under `apps/lazuar-pay` or `packages/pay-spec`. Fulfillment audit is in-process only:

```121:127:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            Action = "checkout.paid",
            At = DateTimeOffset.UtcNow
        });
```

Hub still owns outbound `payment.completed` (`apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs`) — **not** this host.

### K. Isolation still holds

```5:16:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
        "MediatR", "Modules.One", "BuildingBlocks", "IPaymentGatewayAdapter", "PaymentGatewayFactory",
        "IPaymentGatewayFactory", "AddPaymentsModule", "GatewayPaymentCompletedIntegrationEvent", "Modules.Payments",
        "ApplicationFeeAmount", "Razorpay.Api",
        "application_fee", "TransferData", "transfer_data",
        "ChipWebhookRegistrar", "PublicDnsFallback",
        "Lhdn", "MyInvois", "UBL", "XAdES", "Irbm",
        "IEnumerable<IHostedRail>",
        "namespace Lazuar.Pay.Gateways",
        "namespace Lazuar.Pay.One;"
```

Host csproj (`Lazuar.Pay.csproj` 13–18): EF Design, Npgsql, Stripe.net.

### L. 018 thesis (background, not authority)

```23:25:plans/018-evals/001-evals.md
Pay today is a **hosted cashier** (merchant pastes keys, buyer pays on a link, Official Receipt). It is **not** yet a kernel other apps can swallow in an afternoon: there is no machine key (`lzr_sk_`) and no outbound `payment.completed` on the new host. The idea is ahead of the door.
```

That sentence is still true on `9f04ad58`. What 018 could not see: Test rail, independent vault, occupancy, Aura shell, and the 016 P0 cash list **mostly closed**. The new cash lie is occupancy. The kernel door is still missing.

---

**Fix the last seat. Tell the truth about Test receipts. Then write the concurrent test. Then (and only then) build the kernel door. Do not staff SST, a factory, Hub cutover, or escrow on the Processor card.**
