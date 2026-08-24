# 10 — Honesty, frontend wiring, ranked risks, refuse, tests vs fixes

**Date:** 24 August 2026  
**Slice:** Cross-cut of the **new** Pay host after 015 landed five `hosted_link` rails, plus how `:5178` / `:5179` actually call 8081. What we may say. What we must not say. What 014 called P0 and whether live files closed it. What to **fix first** versus what tests to write next.  
**Kind:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a Hub cutover. **Not** a project reference into `apps/lazuar-api`.

Live files on this SHA are authority. [015/checklists/decisions.md](../015-four-adapters/checklists/decisions.md) is the freeze this code claims to implement. [014/00-evaluation.md](../014-evals/00-evaluation.md) named the money-safety holes **before** the four extra rails existed. This paper re-reads the host and both Vite apps **after** that code landed.

---

## Coordinates (this write)

| Field | Value |
|-------|--------|
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `feat/015-four-adapters` (`.git/HEAD` → `ref: refs/heads/feat/015-four-adapters`) |
| HEAD | `c621ceba7fc7b79f16954d0819200cb21db6f22b` (`c621ceba`) — `docs(015): check off implemented T–Q phases` |
| 016 index SHA at analysis start | same `c621ceba` |
| 014 parent SHA (historical) | `ee2db8e5758305089a38298456c456d6bf0e97ca` — Stripe-only Bar B |
| 015 freeze | [checklists/decisions.md](../015-four-adapters/checklists/decisions.md), filled by A00 |

### `git log --oneline -15` (newest first, reconstructed from `.git/logs/HEAD`)

```text
c621ceba docs(015): check off implemented T–Q phases
02c68c55 docs(pay): Q10-Q14 spec and README match hosted rails
f500898b feat(pay): U/K merchant rail picker and checkout verifying poll
ed76c1dd test(pay): hermetic rails, setup-not-paid, writer gates
374b0f3f feat(pay): H/P/C/B/X/R hosted_link rails
277f7c0e feat(pay): T10-S17 tax out and hosted-rail schema
5ec65a90 docs(015): A00 freeze four hosted_link rails, tax out
ee2db8e5 feat(pay): Bar B receipts, webhook secret, merchant money UI
f9f4779b feat(pay): D16 Initial PayDbContext EF migration
0f62e996 feat(pay): D–Q Bar B host, public pay, Stripe, fulfill, CI
f95916a5 feat(pay-merchant): M13–M27 OIDC PKCE shell on :5178
d7cf5262 feat(pay-merchant): M12/M21 pickApiBearerToken never id_token
d847e507 feat(pay-merchant): M11 public OIDC env example
4bfac874 feat(pay-merchant): M10 register SPA via One apps API
06c87015 docs(013): B00 freeze Bar B — Stripe rail, one PayDbContext
```

Everything from `5ec65a90` through `c621ceba` is the 015 program. 014 evaluated `ee2db8e5`. If a sentence in 014 says “CHIP is not on 8081,” it was true then and is **false now**. If a sentence in 014 says One HMAC is body-only uppercase hex, it is **still true** — that file was not rewritten.

### Files actually opened for this slice

Host:

- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/{Stripe,Chip,Billplz,Xendit,Razorpay}{Hosted,Webhook}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Gateways/{PayProviders,IHostedRail,BuyerEmail,PspParseResult}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/PaymentQueryEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/{CheckoutEndpoints,CheckoutStore,CheckoutSession}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/One/{MemberGate,OneWebhookEndpoints,OrgReadyEndpoints}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/{PayDbContext,Rows}.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260824120000_FourAdaptersHostedRails.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj`
- `apps/lazuar-pay/README.md`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/{IsolationTests,WebhookTests,GatewayTests,RailTests,PublicPayTests,CheckoutTests,PayApiFactory,CorsTests,CatalogTests,FakePspHandler}.cs`

Frontends:

- `apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx`
- `apps/lazuar-pay-merchant/src/pages/{LoginPage,CreateWorkspacePage}.tsx`
- `apps/lazuar-pay-merchant/src/lib/{payApi,roles}.ts`
- `apps/lazuar-pay-merchant/src/auth/{oidcConfig,bearerToken}.ts`
- `apps/lazuar-pay-merchant/src/locks.test.ts`
- `apps/lazuar-pay-checkout/src/App.tsx`
- `apps/lazuar-pay-checkout/src/locks.test.ts`
- `apps/lazuar-pay-checkout/package.json`
- `packages/pay-spec/main.tsp`

Hub (HTTP judgment only; not a package):

- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` (cents vs major; no `force_recurring` unless setup)
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` (notes on payment_link; webhook reads `payment.entity.notes`)
- `apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs` (`t={unix},v1={hex}` over `{unix}.{body}`)

015 / 014 / 011:

- `plans/015-four-adapters/checklists/decisions.md`
- `plans/015-four-adapters/00-what-must-be-done.md`
- `plans/015-four-adapters/checklists/parked-{factory,chip-registrar,dns-fallback,emandate,lhdn-sst,refunds,offsession,hub-cutover}.md`
- `plans/014-evals/00-evaluation.md` §6 P0 table
- `plans/011-new-lazuar-pay/11-checklist.md` NP-XX-001–024

---

## 0. Verdict in one page (then the evidence)

015 did the job 014 said must happen **before** copying four Hub adapter files: allow-list of five lowercase names, per-org webhook ciphertext, one DB transaction around fulfill, tax throw gone, writer-gated checkout create, `IHostedRail` + a switch, merchant picker, checkout `email_required` + verifying poll. IsolationTests still fail the cathedral strings. There is still no `PaymentGatewayFactory`, no `ChipWebhookRegistrar`, no `PublicDnsFallback`, no SST math, no e-mandate.

That is not the same as “Bar B is closed” or “the five rails are production BYOK.”

**014’s six P0s after 015 code:** three are closed in product code (member mint, setup-not-paid now tested, SST throw/seed). One is coded for Postgres and **unproven** in CI because tests ignore transactions (webhook one-TX). One is **mostly** closed (per-org `whsec_`) with a leftover forge path on empty-ciphertext rows in non-Production. One is **still open, verbatim** (One HMAC dialect + fulfill ignores pause).

**New P0s 015 introduced or left next to the new rails:** public start is not idempotent (two PSP sessions for one checkout); Razorpay paid join is `notes.checkout_id` only; Plane B amount/currency mismatch returns 400 **without** consuming the event id (correct fail-closed if the payload is wrong; lost cash if the parser is wrong); Billplz hardcodes `Currency = "MYR"` against the freeze “do not default MYR.”

**Frontends:** `:5178` fields line up with PUT for the five names. `:5179` now reads `?status=verifying` and polls. The remaining frontend/host mismatches are not “the SPA still ignores verifying” (014 is stale on that sentence). They are: hardcoded `localhost:5179` pay links and success URLs, 503/400 bodies discarded, poll dies after 30s, catalog create is decorative, no `success_url` on mint.

**Fix money first. Then write the tests that would have caught the remaining money holes on Postgres. Do not staff a factory, a registrar, DNS folklore, SST, or e-mandate in the same slice.**

---

## 1. 014 P0s — closed vs still open after 015 code

014/00 §6 ranked six P0s on `ee2db8e5`. 015’s written amendment was: close the host holes **then** add four HTTP extracts. Live files on `c621ceba`:

### 1.1 P0-1 — process-wide Stripe `whsec_` (014: open)

**014 claim:** `Pay:StripeWebhookSecret` verifies every org. Merchant PUT stored `sk_`, not `whsec_`. Anyone with the process secret could forge `checkout.session.completed` for every Stripe row.

**Live PUT** (`GatewayEndpoints.Put`):

```50:53:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return PayErrors.Status(400, "Bad Request", "webhook_secret is required");
        }
```

Ciphertext is wrapped with `SecretBox` into `WebhookCiphertext`. GET JSON exposes `webhook_configured` as a boolean, never the secret (`GatewayTests.Put_and_get_does_not_echo_secret`).

**Live Stripe verify** (`StripeWebhook.ResolveSecret`):

```70:83:apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeWebhook.cs
    static string? ResolveSecret(GatewayCredentialRow cred, SecretBox box, IConfiguration config, IHostEnvironment env)
    {
        if (!string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            return box.Unprotect(cred.WebhookCiphertext);
        }

        if (!env.IsProduction())
        {
            return config["Pay:StripeWebhookSecret"];
        }

        return null;
    }
```

Missing secret after that is `InvalidOperationException("webhook secret missing")` → HTTP 503 (handler special-case). CHIP / Billplz / Xendit / Razorpay have **no** process fallback; empty `WebhookCiphertext` is 503.

**Verdict: mostly closed.** New PUT always stores a per-org webhook secret. Production with a populated row is BYOK verify. Residual:

- Migration `20260824120000_FourAdaptersHostedRails` adds `WebhookCiphertext` **nullable**. Pre-015 Stripe rows can still be empty. Non-Production then falls back to the **platform** env var — the original forge-all-orgs vector, limited to those rows.
- `WebhookTests.Missing_webhook_secret_is_503_when_rail_configured` proves 503 only after the test **nulls** ciphertext **and** sets `Pay:StripeWebhookSecret` to `""`. It does not prove Production refuses the fallback. It does not prove a leftover empty row in Development is 503 when the process env is set.

Treat leftover empty-ciphertext rows as a **cutover** bug, not as “014 P0-1 still describes the PUT path.”

### 1.2 P0-2 — `psp_webhook_events` committed before fulfill; path org unbound (014: open)

**014 claim:** insert + `SaveChanges`, then `FulfillPaidAsync` opens its own TX. Throw after insert → Stripe retry `{ duplicate: true }` → buyer paid, no `RCPT-`. Path `{orgId}` not bound to `checkout.OrgId`.

**Live handler** (`WebhookEndpoints.Handle`), after verify and parse:

```70:123:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
        if (await db.PspWebhookEvents.FindAsync([orgId, name, parsed.EventId], ct) is not null)
        {
            return Results.Ok(new { duplicate = true });
        }
        // ...
        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == parsed.CheckoutId, ct);
        if (checkout is null || checkout.OrgId != orgId)
        {
            return PayErrors.Status(400, "Bad Request", "checkout not found");
        }
        // currency + amount match ...
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PspWebhookEvents.Add(new PspWebhookEventRow { /* ... */ });
            await db.SaveChangesAsync(ct);
            await fulfillment.FulfillPaidAsync(checkout.Id, name, parsed.ProviderRef, ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return Results.Ok(new { duplicate = true });
        }
```

Org bind is real (`WebhookTests.Cross_org_checkout_is_400`). Unique violation → 200 duplicate is coded. Amount match is coded (`parsed.AmountMinor != MoneyMath.ToMinor(checkout.Amount)` → 400).

**Verdict: coded closed on Npgsql; unproven in CI.** `PayApiFactory` uses EF InMemory and **explicitly ignores** transactions:

```40:41:apps/lazuar-pay/tests/Lazuar.Pay.Tests/PayApiFactory.cs
            services.AddDbContext<PayDbContext>(o => o.UseInMemoryDatabase(_dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
```

On InMemory, `BeginTransactionAsync` is a no-op. `Fulfillment.FulfillPaidAsync` still calls `SaveChangesAsync` on the same context; a throw **after** the event `SaveChangesAsync` and **inside** fulfill still persists the event id in the test database. The hermetic suite cannot fail red on the original lost-cash bug. There is **no** test that injects a fulfill throw and asserts the event row is absent.

Also still true on every provider:

- The unique `FindAsync` is **before** the transaction (TOCTOU). The PK `(OrgId, Provider, EventId)` plus `DbUpdateException` → duplicate is the real lock, on Postgres. InMemory usually enforces PK too.
- **Ignored** events (`setup_or_zero`, `purchase.preauthorized`, Billplz unpaid, Xendit SETTLED, Razorpay `payment.failed`) insert via `InsertEventAsync` **outside** that TX, swallowing unique violations. That is intended consume-without-pay. It is not the lost-cash path.
- Amount / currency mismatch 400 happens **before** insert. PSP retries forever. Fail-closed if the payload is hostile. Lost-cash if **our parser** invented a mismatch (see new P0s).

`catch (DbUpdateException)` does not catch a fulfill `InvalidOperationException`. On Postgres the `await using` transaction should roll back on dispose. That is the 015 design. **CI does not prove it.**

### 1.3 P0-3 — SST fail-closed defeated by auto-seed `false` (014: open)

**014 claim:** Fulfillment throws if `SstRegistered is null`; checkout create seeds `false`; unknown coerced to unregistered is undercharge; journal has no tax line even if `true`.

015 amended the lock: **tax is out.** Live `Fulfillment.FulfillPaidAsync` books `checkout.Amount` cash debit + revenue credit. No SST read. No throw. Title remains `"Official Receipt"`. `CheckoutEndpoints.Create` no longer seeds `SstRegistered`. `OneWebhookEndpoints` no longer seeds it. Column remains on `OrgSettingsRow` with comment “Unused. Tax is out of this program.” Grep of `apps/lazuar-pay` for `SstRegistered` hits the row type and the Initial migration only.

**Verdict: closed as a money bug**, by deleting the tax path, not by implementing fail-closed SST. Do not re-open it as “we should compute SST.” That is refuse / parked (see §5). The leftover column is P2 hygiene, not P0 cash.

### 1.4 P0-4 — One HMAC dialect wrong; fulfill ignores pause (014: open)

**Live `OneWebhookEndpoints` on this SHA is the same shape 014 quoted.**

Verify:

```31:38:apps/lazuar-pay/src/Lazuar.Pay/One/OneWebhookEndpoints.cs
        var provided = request.Headers["X-Lazuar-Signature"].ToString().Trim();
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(json)));
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        if (providedBytes.Length != expectedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
        {
            return PayErrors.Status(401, "Unauthorized", "Invalid HMAC");
        }
```

One’s real signer (`OutboundWebhookSignature.ComputeHeaderValue`) is Standard Webhooks: header `t={unix},v1={lowercase hex}` over payload `{unix}.{body}`. Pay HMAC’s **body only**, emits **uppercase** hex (`Convert.ToHexString`), and compares that 64-char string to the **entire** header. Length never matches a real `t=…,v1=…` header. Real `tenant.suspended` is 401. Always.

Org id: handler reads JSON `org_id`. 014 said One’s envelope uses `tenant_id`. Live One Workers greps in this pass did not show a current payload builder with either name in `Modules/One/Infrastructure/Workers` (the dispatcher signs; the envelope may live elsewhere). Even if the property were `org_id`, the HMAC still fails. The field name is a second independent miss.

Pause: `CheckoutEndpoints.Create` and `PublicPayEndpoints.Start` both 403 when `OrgSettings.ChargesPaused`. **`Fulfillment.FulfillPaidAsync` does not read the flag.** An in-flight hosted session still books cash after suspend, if Plane B verifies.

Tests: **zero** One HMAC vectors. **Zero** suspend-then-fulfill tests.

**Verdict: still open, P0.** 015 did not touch Plane A. Four new rails inherit “webhook can fulfill a paused org.”

### 1.5 P0-5 — `POST /v1/checkouts` was `RequireMemberAsync` (014: open)

**Live:** `MemberGate.RequireWriterAsync`. UI already hid the button (`canWriteMoney` = owner/admin). Curl with a member token is now 403.

```168:186:apps/lazuar-pay/tests/Lazuar.Pay.Tests/CheckoutTests.cs
    public async Task Member_cannot_create_checkout()
    {
        // role member, authz allowed:true
        // ...
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
```

Also `GatewayTests.Member_cannot_put_gateway`, `CatalogTests.Member_cannot_create_product`.

**Verdict: closed**, with a test. Member GET payments/receipts/products/gateway metadata remains.

### 1.6 P0-6 — setup-not-paid untested (014: open)

**Live tests:**

- `WebhookTests.Setup_mode_is_ignored` — `mode=setup`, `amount_total=0` → 200 body contains `ignored`, documents 0, checkout still `open`.
- `WebhookTests.Zero_amount_session_is_ignored` — `mode=payment`, `amount_total=0` → documents 0.
- `RailTests.Chip_preauthorized_is_ignored` — `purchase.preauthorized` with a recurring token → 200 contains `preauthorized`, documents 0.

**Verdict: closed as proof** for Stripe setup/zero and CHIP preauth. Still missing as tests (not 014’s original P0-6, but the same family): Billplz unpaid, Xendit pending (SETTLED is tested as non-second-document), Razorpay `payment.failed`, amount mismatch, currency mismatch, fulfill-throw-retry.

---

## 2. Sales-script sentences the code will back **today**

Say these on a whiteboard. Each sentence is scoped to the three new processes on `c621ceba`. **CI** means `task pay:test` (hermetic). **Runbook** means a human, One on 8080 with Hub **off**, Postgres 5435, a tunneled `Pay:PublicBaseUrl`, and a dashboard paste.

### 2.1 Process and doors (CI + runbook as marked)

1. **“Focused Pay is a separate `net10.0` host on 8081.”** `launchSettings.json` / `Program.cs`. Does not bind 8080. IsolationTests fail the build if a csproj contains `apps/lazuar-api` or `Razorpay.Api`. Host PackageReference is EF Design + Npgsql + Stripe.net **only**. CHIP/Billplz/Xendit/Razorpay are `HttpClient`. **CI.**

2. **“Pay talks to One over HTTP. It does not contain `Modules/One`.”** `OneClient`. IsolationTests `BannedSrc` includes `Modules.One`, `MediatR`, `BuildingBlocks`, `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `IPaymentGatewayFactory`, `AddPaymentsModule`, `GatewayPaymentCompletedIntegrationEvent`, `Modules.Payments`, `ApplicationFeeAmount`, `Razorpay.Api`. **CI.**

3. **“Merchant staff sign in through One `:5175`, not a Pay password form.”** `LoginPage.tsx`: “This page is not a password form.” `locks.test.ts` greps `type="password"`, `/one/auth/login`, `lazuar_auth`. `pickApiBearerToken` sends JWT `access_token`, never `id_token`. **CI** (grep + unit). Live OIDC is **runbook**.

4. **“Merchant homepage is `:5178`. Buyer page is `:5179`. Not ops `:3003`, not portal `:3004`, not admin `:5173`.”** Vite `strictPort`. `CorsTests` allow 5178/5179, deny 3003/3004. **CI.**

5. **“One tenant id is Pay `org_id`. There is no Pay `organizations` table.”** `CreateWorkspacePage` calls One `POST /tenants`. IsolationTests ban `ToTable("organizations")` / `"users"` / `"members"`. **CI.**

6. **“Buyers have no One account.”** `lazuar-pay-checkout/package.json` is `react` + `react-dom`. `locks.test.ts` forbids `oidc-client-ts`, `react-oidc-context`, `@repo/api-types-ts`. `GET /v1/pay/{token}` does not send Bearer; second GET does not call One (`PublicPayTests.Public_get_does_not_need_bearer`). **CI.**

7. **“Checkouts survive process restart in Postgres `lazuar_pay` on 5435.”** `CheckoutStore` comment: “Postgres-backed checkouts. Not a ledger.” Tests use InMemory. Persistence across restart is **runbook**, schema existence is **CI** (EnsureCreated in factory).

8. **“`/health` never calls One. `/ready` is Postgres `CanConnect`.”** Health tests. `/v1/orgs/{id}/ready` is still dummy `ready: true` after member check — **do not fold that into this sentence.**

### 2.2 Five hosted_link names in **code**, one active rail per org (CI)

9. **“The wrap set is five lowercase names: `stripe`, `chip`, `billplz`, `xendit`, `razorpay`. Capability is `hosted_link` for all five.”** `PayProviders.All`, `PayProviders.Capability`. PUT unknown → 400 `"unknown provider"`. Webhook unknown (`paypal`) → 400 (`WebhookTests.Unknown_provider_is_400`). GET returns `capability: "hosted_link"`. **CI.**

10. **“One active rail per org. PUT sets `org_settings.active_provider`. The buyer page has no PSP picker.”** PUT always writes `ActiveProvider = provider`. `WorkspacePage` is a `<select>` of five names on the **staff** page, not on `:5179`. Checkout `locks.test.ts` greps out GrabPay/TnG/Boost/DuitNow/FPX/Shopee and `autocomplete="cc-number"`. **CI.**

11. **“Dispatch is a switch of known names, not a factory.”** `PublicPayEndpoints.Start` and `WebhookEndpoints.Handle` `switch` on `PayProviders.*`. `Program.cs` `AddScoped` each hosted class. Grep of `apps/lazuar-pay` for `PaymentGatewayFactory` / `IPaymentGatewayAdapter` / `ChipWebhookRegistrar` / `PublicDnsFallback` is empty except IsolationTests’ ban list. **CI.**

12. **“Merchant `owner` / `admin` paste keys; `member` cannot.”** Writer gate on PUT and checkout create. UI `canWriteMoney`. Tests named in §1.5. **CI.**

13. **“BYOK API secret and webhook secret are AES-GCM at rest.”** `SecretBox`. GET never echoes `sk_` / `whsec_` / PEM / callback token (`GatewayTests.Put_and_get_does_not_echo_secret`). `Pay:WrapKey` is required in Production; non-Production still hashes git-known `"lazuar-pay-dev-wrap-key"` — **do not sell Production wrap without setting the env.** **CI** for GET; wrap-key Production throw is untested.

14. **“Stripe Checkout is `mode=payment` hosted. We do not take PAN on `:5179`.”** `StripeHosted` `Mode = "payment"`. Checkout has name + email + Pay. **CI** (source + locks). Live redirect is **runbook**.

15. **“CHIP / Billplz / Xendit / Razorpay starts are HttpClient hosted links; email is required.”** `PayProviders.RequiresEmail` is true for every name except stripe. `BuyerEmail.IsUsable` rejects empty and `customer@example.com`. `RailTests.Chip_start_without_email_is_400`. **CI** (fake PSP HTTP). Live CHIP/Billplz/Xendit/Razorpay is **runbook**, and Billplz additionally needs public https `Pay:PublicBaseUrl`.

16. **“Billplz localhost callback is 400. We did not port `PublicDnsFallback`.”** `BillplzHosted.TryPublicBase` refuses non-https, loopback, `localhost`, `127.0.0.1`, `::1`, `lazuar-local-dev.com`. **CI for the predicate** (the factory sets `Pay:PublicBaseUrl=https://pay.test.example`, so start succeeds). There is **no** test that actually 400s a localhost base — the test is named `Billplz_paid_form_and_localhost_blocked` and only asserts paid-form fulfill + sandbox host. Do not sell the localhost 400 as tested; sell the function.

### 2.3 Money path (CI hermetic; live dogfood is runbook)

17. **“A verified Stripe `checkout.session.completed` with `mode=payment` and amount > 0 writes charge + balanced two-line journal + Official Receipt `RCPT-{MYT year}-#####`. Replay is `{ duplicate: true }` without a second document.”** `WebhookTests.Completed_session_writes_receipt_and_replay_is_noop`. **CI.** Lived Ada-on-Stripe is **runbook**. B99 remains the close for “a human paid.”

18. **“The same sentence is true for CHIP `purchase.paid`, Billplz form `paid=true`, Xendit `PAID`, Razorpay `payment.captured` — in hermetic tests with faked PSP HTTP and faked signatures.”** `RailTests`. **CI.** Lived loops are **runbook**. Razorpay’s join is notes (see P0 below); the test **injects** `notes.checkout_id`.

19. **“Empty PSP body is 400. Bad signature is 400. Unknown provider is 400. Missing org webhook secret is 503. Rail not configured is 400 on webhook / 503 on start.”** Shared handler. Stripe + CHIP empty-body tests exist. **CI.**

20. **“Setup / zero / preauthorized / SETTLED are not paid.”** Tests in §1.6. Xendit SETTLED after PAID does not write a second document (`RailTests.Xendit_paid_and_settled`). **CI.**

21. **“Receipt is Official Receipt, not a tax invoice, not VALID. We do not compute SST. We do not file MyInvois.”** `Fulfillment` title hard-coded. Merchant copy: “Pay does not file SST or MyInvois.” Checkout paid copy: “Official Receipt, not an e-invoice.” Grep no LHDN types in host. **CI.**

22. **“Fees and processor tax are not booked. `unknown ≠ 0`.”** Journal is two lines, cash/revenue, `checkout.Amount`. Razorpay test payload includes `"tax":12,"fee":30` and asserts `JournalLines.Count() == 2`. **CI.**

23. **“Cross-org checkout id on the webhook path is 400, no document.”** `WebhookTests.Cross_org_checkout_is_400`. **CI.**

24. **“CHIP start does not send `force_recurring`.”** `RailTests.Chip_start_and_paid_webhook` asserts `LastBody` does not contain `force_recurring`. Hub only set that flag for `setupFutureUsage`. **CI.**

### 2.4 Frontends (CI grep + runbook click)

25. **“`:5178` writer pastes per-rail fields: Stripe `sk_` + `whsec_`; CHIP Bearer + Brand ID + PEM; Billplz secret + Collection ID + X-Signature + test|live; Xendit secret + callback token; Razorpay `key_id` + `key_secret` + webhook secret.”** `WorkspacePage.tsx` payload construction matches PUT. Host concatenates Razorpay `key_id:key_secret` if sent split; the SPA concatenates itself. **CI** as source review; live paste is **runbook**.

26. **“`:5179` shows `email_required` from GET, disables Pay when CHIP/Billplz/Xendit/Razorpay need email, redirects to `redirect_url`, and if the success URL has `?status=verifying` it polls GET `/v1/pay/{token}` every 2s until `paid`/`expired` or 15 ticks.”** `App.tsx`. 014’s sentence “checkout SPA never reads the query” is **false on this SHA**. **CI** as source; the poll is untested (no Playwright, no vitest of App).

27. **“Success URL is not paid. The verifying copy says so.”** Hosted rails default `…?status=verifying`. SPA: “The processor success URL is not paid. Waiting for the webhook.” **CI** as copy. Lived race is **runbook**.

---

## 3. Do not say

If a sentence is in this table, a screen share will lie. Several are 014 lies that 015 **did** retire; they are marked **retired** so a later editor does not keep repeating a closed smear. The rest are still live.

| Do not say | Why live files refuse it | 014? |
|------------|--------------------------|------|
| “We replaced Hub.” | Root compose still boots `lazuar-api` on 8080. `parked-hub-cutover.md` unchecked. No Pay Dockerfile. | still |
| “Pay v1 / Bar B is done.” | B99 unlived. Plane A HMAC still wrong. Start not idempotent. | still |
| “We have a payment-gateway factory of five.” | Switch of five known names. IsolationTests ban `PaymentGatewayFactory`. | **retired as ‘five adapters absent’**; **keep** as factory lie |
| “CHIP/Billplz/Xendit/Razorpay are not on 8081.” | They are, as hosted_link HTTP. 014 parent is stale. | **retired** |
| “Five logos on the buyer page / wallets on `:5179`.” | Staff `<select>` only. Checkout greps out wallet names. Wallets, if any, are on the **processor** page. | still (new wording) |
| “We take cards on our page.” | Redirect. No PAN. | still |
| “Off-session / vault / e-mandate / auto-debit.” | Capability `hosted_link`. CHIP copy says “Auto-debit later, not this program.” Razorpay copy: “Not e-mandate.” `parked-offsession.md`, `parked-emandate.md`. | still |
| “Pay registers CHIP webhooks for you.” | No `ChipWebhookRegistrar`. Copy: “Paste PEM from the CHIP dashboard — Pay does not register webhooks.” | still (new) |
| “We rewritten Billplz DNS / `lazuar-local-dev.com`.” | Predicate **rejects** that host. No `PublicDnsFallback`. | still (new) |
| “We file MyInvois / this is a Tax Invoice / SST is computed.” | Tax out. Official Receipt. `NP-XX-001`/`003`. | still, stronger |
| “Webhook secret is a platform env var for every rail.” | PUT requires per-org `webhook_secret`. Process `Pay:StripeWebhookSecret` is Stripe **dev fallback** only. | **retired as the PUT story**; **keep** for empty-ciphertext non-prod rows |
| “`:5179` ignores `?status=verifying`.” | It polls. 014 is stale. | **retired** |
| “Member can mint a pay link via curl.” | `RequireWriterAsync` + test. 014 is stale. | **retired** |
| “`tenant.suspended` stops in-flight fulfill.” | HMAC never verifies; fulfill ignores pause. | still |
| “VIEWER is a One role.” | owner/admin/member. | still |
| “`/v1/orgs/{id}/ready` means we can charge.” | Dummy `ready: true` after member. | still |
| “We email receipts.” | `mail_outbox` has a table and no producer. | still |
| “Compose is Pay.” | `docker-compose.pay.yml` is DB only. No `apps/lazuar-pay/Dockerfile`. | still |
| “Subscriptions renew / interval follows the product.” | Checkout create hard-codes `Interval = "one_off"`. Catalog `product_id` is never written onto the checkout. Merchant “Product + pay link” POSTs product then POSTs checkout with only `org_id`/`amount`/`currency`. | still |
| “Refunds work.” | `parked-refunds.md`. No refund route. | still |
| “pay-spec is the whole host.” | Spec gained gateway PUT/GET and `email_required`. Still missing payments, receipts, unversioned `/ready`, start **body**, webhook `{duplicate}`/`{ignored}`. Start in TypeSpec is `start(@path token: string): StartPayResponse` with **no body**. | still, reduced |
| “Host README still says in-memory fixture.” | README on this SHA describes Postgres + five rails + Official Receipt. 014 stale-README smear is **retired**. | **retired** |
| “InMemory tests prove one DB transaction.” | Factory ignores `TransactionIgnoredWarning`. | still (new) |
| “Lived Billplz works with the webhook URL on the merchant page.” | That `<code>` is `{payApi}/v1/webhooks/billplz/{orgId}` with `payApi` default `http://localhost:8081`. Billplz start builds callback from `Pay:PublicBaseUrl`, not from that copy. Localhost callback 400s. | still (new) |

### Demo footguns (the click will lie)

| Do not demo | What actually happens |
|-------------|------------------------|
| Curl `POST /v1/checkouts` and call it paid. | `status: "open"`. Money moves on verified Plane B. |
| Open `:5178` without `VITE_ZITADEL_CLIENT_ID`. | Alert; Sign in disabled. (`.env` in the tree has a client id — that is a **local** register, not a secret key, but do not commit to treating `.env` as the product.) |
| Boot Hub `task dev` and One together. | Both want 8080. |
| Point `lazuar-ops` at 8081. | CORS denies 3003. P60. |
| Paste only `sk_test_` and expect webhooks. | PUT 400s without `webhook_secret`. SPA does not client-validate empty webhook field (`keys 400`). |
| Trust Stripe dashboard “success” without a tunnel to `/v1/webhooks/stripe/{orgId}`. | SPA sits on Verifying for 30s, then **stays** on Verifying with no retry and no Pay button. |
| Refresh `:5179/c/{token}` **without** `?status=verifying` after paying if the webhook is late. | Form comes back. Pay is clickable. Start will mint a **second** processor session (see P0-A). |
| Trigger One `tenant.suspended` and assume Plane B stops. | 401 on HMAC; even a hand-set `ChargesPaused` still fulfills. |
| Tell a member “you cannot see last4.” | Active rail + last4 render **outside** the writer gate. Intended by 015 u17. Do not oversell secrecy of last4. |
| Create “Dogfood” product and claim the pay link is that SKU. | Product row is listed. Checkout amount is typed in the same form, not loaded from `prices`. `CheckoutRow.ProductId` stays null. |
| Billplz dogfood without a public https `Pay:PublicBaseUrl`. | Start 400 `"callback base not public"`. SPA maps **every** 400 to “callback base not public or email required.” |
| CHIP start with `customer@example.com`. | Host 400. SPA already sent a non-empty email so it did not block client-side. Same conflated 400 string. |

---

## 4. Frontend vs host table

Columns: **staff SPA `:5178`**, **buyer SPA `:5179`**, **host 8081**, **match?**

| Concern | `:5178` WorkspacePage | `:5179` App.tsx | Host | Match? |
|---------|----------------------|-----------------|------|--------|
| Origin / CORS | Vite 5178 `strictPort`. `VITE_PAY_API_URL` default `http://localhost:8081`. | Vite 5179 `strictPort`. Same default. | CORS allow-list 5178/5179 and 127.0.0.1 twins. Denies 3003/3004. | **Match** for local. Four localhost literals; no prod origin. |
| Auth | OIDC PKCE, sessionStorage, `access_token` Bearer, `X-Lazuar-Tenant-Id` org hint. | None. Public fetch. | Writer/member gates on merchant routes. Public pay unauthenticated. | **Match.** |
| Provider list | `rails = ['stripe','chip','billplz','xendit','razorpay']` `<select>`. | No picker. | `PayProviders.All` same five lowercase names. | **Match.** |
| PUT body | `provider`, `secret` (or Razorpay concat), `webhook_secret`, CHIP/Billplz `public_merchant_id`, Billplz `environment`. | n/a | `PutGatewayRequest` also accepts `key_id`/`key_secret` split; SPA does not use split fields. Requires webhook_secret, public id for CHIP/Billplz, environment for Billplz. Rejects public id on Stripe/Xendit/Razorpay. | **Match** for what the SPA sends. Host is a superset. |
| GET gateway | `GET /v1/orgs/{id}/gateway` **no** `?provider=`. Sets dropdown from `body.provider` (active). Shows last4, capability, configured. | n/a | GET without query uses `active_provider`. Optional `?provider=` exists on the host and is **unused** by the SPA. | **Partial.** Staff cannot inspect a non-active rail’s last4 without making it active (PUT also switches active). |
| Webhook URL copy | `{payApi}/v1/webhooks/{provider}/{orgId}` | n/a | Stripe/CHIP/Xendit/Razorpay dashboards want that path **public**. Billplz callback is `{Pay:PublicBaseUrl}/v1/webhooks/billplz/{orgId}?checkout_id=…` built at **start**, not from this copy. | **Mismatch** for Billplz (copy is localhost 8081; real callback is PublicBaseUrl + query). **Mismatch** for the others unless a tunnel equals `payApi`. |
| Wrap / registrar copy | Per-rail strings: PEM paste, no register; Billplz localhost fails; no e-mandate; wallets on processor page. | Paid: Official Receipt not e-invoice. Verifying: success URL is not paid. | Matches 015 freeze. | **Match** as copy. |
| Product + pay link | POST product (MYR), then POST `/v1/checkouts` `{ org_id, amount, currency: 'MYR' }`. Link `http://localhost:5179/c/{public_token}`. No `success_url` / `cancel_url` / `product_id`. | n/a | Create accepts optional success/cancel. Stores `Interval = "one_off"`. Catalog amount is independent. Hosted rails default success to `http://localhost:5179/c/{token}?status=verifying` when checkout SuccessUrl is null. | **Mismatch:** hardcoded 5179 even if checkout Vite is elsewhere. **Mismatch:** product is decorative. **Match:** defaults make verifying query exist for dogfood on one laptop. |
| Writer vs member UI | Paste + create behind `canWriteMoney`. Member sees lists + active last4 + SST disclaimer. | n/a | Writer PUT/create; member GET. | **Match.** |
| Errors | `keys ${status}`, `product ${status}`, `checkout ${status}`. No problem `detail`. | 503 → always “rail not configured”. 400 → always “callback base not public or email required”. 403/409 → `start ${status}`. | 503 also means Stripe/CHIP/Billplz/Xendit/Razorpay rejected the org key, wrap missing, rail not configured. 400 also means email required, placeholder email, Billplz base, (not start) amount/currency mismatch on webhooks. | **Mismatch.** SPA discards the JSON `detail`. |
| Public GET | n/a | Types `PayView` with `email_required?`. Does not display provider. | GET returns token, amount, currency, status, payer_*, `email_required` derived from `row.Provider ?? active_provider`. No provider name. | **Match** (no picker). Stale `email_required` if merchant switches rail while the tab stays open (GET once on mount). |
| Email | n/a | Client block: `email_required && !email.trim()`. No format check. Sends `{ name, email }` including empty strings. | Host only copies email if non-whitespace. `RequiresEmail` + `BuyerEmail.IsUsable` (rejects placeholder). Stripe optional. | **Partial.** Empty blocked when flag true. Placeholder and garbage pass SPA, 400 on host, conflated message. |
| Start | n/a | POST `/v1/pay/{token}/start`, `location.assign(redirect_url)`. No idempotent replay of existing `PspRedirectUrl`. | Always `CreateHostedUrlAsync` then persist `Provider`, `PspRedirectUrl`, `ProviderSessionId`. 409 if already paid/expired. 403 if paused. | **Mismatch:** second click while `open` creates a second PSP session (P0-A). |
| Verifying poll | n/a | `status=verifying` → interval 2s × 15, then **clearInterval** and remain on “Verifying…”. Stops if paid/expired. | Webhook flips `checkouts.status`. GET public returns it. | **Partial.** Poll exists (014 closed). 30s cap + no escape hatch is P1 UX. Refresh without query is P0-A. |
| Wallet / PAN | n/a | locks.test forbids wallet words and `cc-number`. | Rails do not render tiles. | **Match.** |
| Spec | n/a | Sends JSON body on start. | Accepts `StartPayRequest`. | TypeSpec `PublicPayApi.start` has **no body**. **Mismatch** spec vs both. |

### Hosted success URL vs SPA poll (all five rails)

Every `*Hosted.CreateHostedUrlAsync` uses:

```text
checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying"
```

Cancel/failure omit the query (except Billplz `redirect_url` uses the success default). Merchant mint never sends `SuccessUrl`. Therefore **local dogfood** lands on verifying. A deployed checkout origin that is not `http://localhost:5179` will send the buyer to the developer’s laptop after pay. Plane B can still fulfill (Billplz callback is PublicBaseUrl). The buyer never sees Paid. That is a product bug, not a ledger bug.

---

## 5. Refuse (keep; do not un-refuse because five names compile)

015 parked files plus 011 NP-XX. Un-refusing any of these is how the museum comes back. IsolationTests lock **some** of them by string; the rest are process law.

### 5.1 Factory — `parked-factory.md` / IsolationTests

Do not add `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `IPaymentGatewayFactory`, `IEnumerable<IHostedRail>` lookup, `AddPaymentsModule`, `GatewayPaymentCompletedIntegrationEvent`, `Modules.Payments`, or a `ProjectReference` to Hub.

Live dispatch is a **switch of five known names**. That is allowed by the 015 freeze. Growing a sixth name by registering a class “for later” and resolving it from DI by string is how Hub grew five adapters before any of them were dogfood.

`IHostedRail` on this SHA is **one** method (`CreateHostedUrlAsync`) plus `Provider`. Parse lives in static `*Webhook` classes next to the route. That is slightly thinner than 015’s “two methods on the interface.” It is not a factory. Do not “fix” it by introducing `IEnumerable`.

### 5.2 CHIP registrar — `parked-chip-registrar.md`

Do not `POST https://gate.chip-in.asia/api/v1/webhooks/` on PUT or boot. Dashboard PEM paste is the path. A later **explicit** merchant button may steal list-before-create HTTP; it is not this slice. IsolationTests do **not** grep `ChipWebhookRegistrar`. Absence in `src/` is current, not a red test. If someone ports the Hub class, Isolation will not catch the name unless the file also contains a banned token.

### 5.3 DNS fallback — `parked-dns-fallback.md`

Do not port `PublicDnsFallback` / `lazuar-local-dev.com` rewriter. Billplz create **fails closed** on loopback and on that host. If `www.billplz.com` actually fails to resolve from the Pay process, amend A00 and add a tiny handler — do not copy 193 lines “just in case.” IsolationTests do not grep `PublicDnsFallback` either.

### 5.4 Tax / LHDN — `parked-lhdn-sst.md` / NP-XX-001 / NP-XX-003

Do not add `SstTaxMath`, merchant SST yes/no, tax journal lines, Tax Invoice title, VALID, UBL, `Modules/Lhdn`. Book `checkout.Amount`. Leave `sst_registered` unused. Razorpay webhook `tax` / `fee` stay unbooked (test already).

014 P0-3 is **not** permission to bring SST back. 015 amended 013’s “SST fail closed.”

### 5.5 E-mandate — `parked-emandate.md` / NP-XX-011

`SupportsEmandate` remains false (the capability string is `hosted_link`, not a Hub flags class). Do not build Billplz Agreements v5 as a quiet extra. Do not label Razorpay “e-mandate.” CHIP `force_recurring` is asserted absent on start.

### 5.6 Adjacent parked (not 016’s named five, still refuse-this-slice)

| Parked | Meaning |
|--------|---------|
| `parked-offsession.md` | No setup-intent-as-paid. No CHIP token charge. Billplz/Xendit/Razorpay never silent-debit. |
| `parked-refunds.md` | No `IssueRefundAsync`. Billplz has no bill-refund API. |
| `parked-hub-cutover.md` | Five rails on 8081 ≠ Hub dark. Do not retarget ops/portal. |
| NP-XX-007 / 017 | No Zitadel PAT inside Pay. Register-spa is One. |
| NP-XX-012 | Stripe Billing `subscription.updated` is not SoT. Webhook still only fulfills `checkout.session.completed` as paid. |
| NP-XX-013 | No Zitadel human per cardholder. |
| NP-XX-018 | Merchants stay on `:5178`, not `:5173`. |

---

## 6. Ranked bugs in the **new** implementation (cash first)

Priority is blast radius on **cash**, not Hub parity, not missing tests of a correct path.

### P0 — money can be wrong, forged, doubled, or charged after suspend

#### P0-A. Public start is not idempotent — two processor charges, one Official Receipt

`PublicPayEndpoints.Start` does not return an existing `PspRedirectUrl`. Every successful click calls `CreateHostedUrlAsync` and overwrites `ProviderSessionId`.

Race:

1. Buyer Pay → Stripe/CHIP/… session A, redirect.
2. Webhook slow **or** buyer lands without `?status=verifying` (cancel URL, stripped query, typed path, “back”).
3. SPA shows the form (`verifying` state is **only** from the query on first render).
4. Buyer Pay again → session B.
5. Buyer pays B as well, or A and B.
6. First paid webhook fulfills, `status=paid`, `RCPT-`.
7. Second paid webhook: amount matches, `FulfillPaidAsync` returns early because `status != "open"`, **but** the handler still inserts that event id and returns `{ ok: true }`.

Pay’s ledger is not double-booked. **The merchant’s PSP is.** Ada is charged twice. One receipt. This was a 014 P1 (“start not idempotent”) when there was one rail and a SPA that always showed Pay after Stripe. It is **worse** now: five rails, and the verifying screen **hides Pay** only while the query param is present. After 15 polls it stays on Verifying (cannot double-click). Refresh without query can.

Fix before writing more rail tests: if `row.PspRedirectUrl` is set and status is `open`, return it (or 409 “already started”). Optionally 409 start when a `ProviderSessionId` exists. Checkout SPA: after start failure/back, GET again; if you must show Pay, warn.

#### P0-B. Plane A HMAC still does not speak One; fulfill ignores `ChargesPaused`

Quoted in §1.4. Real `tenant.suspended` never sets the flag. Even if a human SQL-sets `charges_paused`, an in-flight CHIP/Billplz/Xendit/Razorpay/Stripe session still books. 013/012 called this a money gate **before live keys**. 015 added four live-shaped rails without it.

This is 014 P0-4, still P0.

#### P0-C. Razorpay paid join is `payment.entity.notes.checkout_id` only

`RazorpayHosted` puts `notes.checkout_id` on the **payment link**. `RazorpayWebhook` reads notes on **`payload.payment.entity`**. If Razorpay does not copy payment-link notes onto the payment (or the merchant’s webhook is `payment_link.paid` which this handler **ignores** unless `event == payment.captured`), `CheckoutId` is null → 400 `"checkout not found"` **before** unique insert → retries forever → buyer paid, no `RCPT-`.

Hub used the same notes read. That is HTTP judgment, not proof the field is present in production payloads. `RailTests.Razorpay_captured` **injects** notes. There is no fallback to `checkout.ProviderSessionId` (`plink_…`) or `external` ids.

Stripe (`client_reference_id`), Xendit (`external_id` = checkout id), Billplz (`?checkout_id=` plus `reference_1`), CHIP (purchase metadata) are stronger joins. Razorpay is the weak one.

#### P0-D. Parser mismatch 400 does not consume the event — lost cash if **we** are wrong

Handler:

- CHIP `(long)purchase.total` as minor units. Hub treated CHIP `total` as **cents** then divided by 100 for major `AmountPaid`. New Pay compares minor to `ToMinor(checkout.Amount)`. Hermetic test uses `total: 1000` for amount `10`. If a live CHIP `purchase.paid` sends major units, every paid CHIP webhook 400s `amount mismatch` and never fulfills.
- Xendit `MoneyMath.ToMinor(paid_amount)` — test uses `paid_amount: 10` major. If a live invoice sends cents, mismatch the other way.
- Billplz `paid_amount` parsed as minor; missing → 0 → mismatch 400.
- Stripe `AmountTotal` is cents (matches). Currency omit → `Currency = null` → **currency check skipped** (see P1).

Fail-closed on a hostile payload is correct. Fail-closed on a **unit error in our parser** is 014’s lost-cash bug with a different costume: no unique row, PSP retries, merchant support sees 400s, Ada’s card is captured.

Fix: one lived payload fixture per rail (not CI if hermetic is law) **or** pin units in tests against Hub’s documented cents/major and add amount-mismatch tests that show 400 **and** event id absent, plus a runbook capture.

#### P0-E. Residual Stripe platform `whsec_` on empty ciphertext, non-Production

§1.1. Pre-015 rows, or anyone who NULLs `webhook_ciphertext`. Development/Testing will verify with `Pay:StripeWebhookSecret` and book cash for **that org** if the attacker can also hit the path (public). Production is closed if ciphertext is empty (503). Do not call BYOK done until a migrator backfills or start/webhook 503s in **every** environment when the org row has no webhook secret.

014 P0-1 is not fully dead.

---

### P1 — honesty, dogfood, and money-adjacent holes that do not forge every org

#### P1-1. One-TX rollback unproven; CI uses InMemory

`PayApiFactory` ignores `TransactionIgnoredWarning`. A fulfill throw test on Postgres is the actual close of 014 P0-2. Until then, do not say “same transaction” in a customer demo as a **tested** property. The Npgsql code is the argument; CI is not.

#### P1-2. Billplz hardcodes `Currency = "MYR"`

015 freeze: “Fail closed if PSP omits currency. **Do not default MYR.**” `BillplzWebhook` sets `Currency = "MYR"` always. A USD checkout against a Billplz bill 400s currency mismatch (accidental safety). A MYR checkout always passes currency even if Billplz omitted it. Opposite of CHIP/Xendit/Razorpay (`PspVerifyException("missing currency")`).

#### P1-3. Stripe omits currency → skip currency check

`TryNormalizeCurrency` failure becomes `Currency = null`, and the handler only compares currency when `parsed.Currency is not null`. Amount still checked. Partial fail-open vs the freeze.

#### P1-4. CHIP create sends no purchase currency

Hub `ChipCollectGatewayAdapter.GenerateCheckoutAsync` also omitted an explicit currency field on the purchase (price in cents only). New `ChipHosted` matches Hub. Webhook fail-closes if CHIP omits currency. If CHIP **defaults** MYR and the checkout is USD, amount minors might still match and then 400 currency — paid at CHIP, no RCPT. Same family as P0-D, lower because dogfood is MYR.

#### P1-5. Success / pay URLs hardcoded to `http://localhost:5179`

Merchant copy link. All five hosted defaults. Billplz `redirect_url` is localhost even when `callback_url` is public https. Lived Billplz on a phone: webhook can fulfill, buyer cannot see Paid.

SPA has no `VITE_CHECKOUT_ORIGIN`. Host has no `Pay:CheckoutBaseUrl` for defaults (only `Pay:PublicBaseUrl` for Billplz callback).

#### P1-6. Verifying poll stops at 30s and traps the buyer

`n >= 15` clears the interval. UI stays on “Verifying…”. No “refresh”, no return to Pay, no error. Late webhook (Stripe retries are minutes) looks like a hung cashier. Opposite problem of P0-A (hiding Pay is what prevents double start). Need: keep polling with backoff, or after N ticks show “not paid yet” + disabled Pay + manual refresh that GETs again **without** starting.

#### P1-7. SPA maps all 503 and all 400 to one string

Host `InvalidOperationException` without `"callback base"` is 503 with the exception message (`"CHIP rejected the org key"`, `"rail not configured"`, `"Stripe rejected the org key"`). SPA: `"rail not configured"`. 400: `"callback base not public or email required"` even for placeholder email. Operators cannot debug from the buyer page. Merchant `keys 400` is the same class of bug on PUT.

#### P1-8. Git-known wrap key outside Production

`SecretBox.LoadKey`: Production throws if `Pay:WrapKey` missing; otherwise SHA-256 of `"lazuar-pay-dev-wrap-key"`. 014 P1. 015 said “no git-known default outside Testing.” Live code still allows Development. Tests run as `Testing` and use the default. A Staging environment that is not `Production` and not `Testing` will silently wrap with the git-known key.

#### P1-9. Catalog is a side show; interval is always `one_off`

`WorkspacePage.createProductAndLink` never sends `product_id`. `CheckoutRow.ProductId` is unused on the pay path. Price `mo`/`yr` never reaches `Fulfillment`’s subscription branch. Do not demo “subscriptions.” The branch that inserts `SubscriptionRow` is dead until create stops hard-coding interval.

#### P1-10. Start can persist after PSP session exists if `SaveChanges` fails

Order: HTTP to PSP → mutate row → `SaveChanges` → return URL. If SaveChanges throws, the processor already has an unpaid session, buyer gets 500, retry creates another (P0-A). Rare. Same family.

#### P1-11. Ignored-event unique grain vs later pay, Razorpay other-event ids

Stripe ignored and paid use Stripe’s `evt_…` (unique per event — OK). CHIP/Billplz/Xendit namespace `paid:` / `preauth:` / `unpaid:` / `settled:` — OK. Razorpay **non-captured** events use `headerEventId ?? eventType ?? "razorpay"` without payment id. Two ignored `payment.authorized` deliveries without the header collide; the second is swallowed. Not lost cash unless a later captured event reused that id (header ids should be unique). Still sloppy vs 015 “never bare object id for fail-then-pay.”

#### P1-12. `email_required` GET is one-shot; placeholder

Covered in the table. Stale flag if merchant switches stripe→chip with the buyer tab open: SPA allows Pay without email, host 400s, buyer sees the Billplz sentence.

#### P1-13. Spec / README residuals

Host README is largely honest now. `packages/pay-spec/main.tsp` still omits payments, receipts, `/ready`, start body, webhook dialects. GET gateway query `provider` unlisted. Do not generate clients from this spec and call them the host.

#### P1-14. No Pay image; compose is Hub; CORS is four literals

Same as 014 P1. Five rails did not add a Dockerfile.

#### P1-15. Test name lies

`RailTests.Billplz_paid_form_and_localhost_blocked` does not block localhost; factory PublicBaseUrl is `https://pay.test.example`. Selling “we tested localhost 400” from the method name is a Hub-class honesty bug **inside the new test project**.

#### P1-16. Member last4 + Brand ID / Collection ID on GET

Intended. `public_merchant_id` is not ciphertext. Still: Collection ID and Brand ID are enough to confuse a demo (“is this a secret?”). Copy should say they are public ids.

---

### P2 — after money is boring

- GET `?provider=` unused in UI; cannot view inactive rails.
- Dead `switch` default `_ => stripe` in Start (TryNormalize already restricted).
- `SstRegistered` column leftover.
- `mail_outbox` leftover.
- `AddDataProtection()` unused by `SecretBox`.
- IsolationTests do not ban `ChipWebhookRegistrar` / `PublicDnsFallback` strings.
- Razorpay last4 is `key_id` suffix (good) vs other rails last4 of API secret (PEM last4 is not shown — last4 is the Bearer).
- OrgReady dummy.
- Checkout GET does not return `provider` (locked no picker; support cannot see which rail from the buyer page).
- Playwright / vitest of App.tsx poll: none.
- `PutGatewayRequest.KeyId` unused by SPA.
- `environment` default `test` stored for Stripe/CHIP even when unused.
- CHIP `ImportFromPem` failure is 400 invalid signature, not 503 bad PEM.
- Billplz HMAC tries extra fields then without; additional unknown fields could still 400.
- No expire-open-sessions job.
- No receipt PDF; list is number + title.
- 011 tracker cells still not this paper’s job to flip.

---

## 7. IsolationTests — what they actually lock

`IsolationTests` is the architectural red line, not a money test.

| Test | What it fails on |
|------|------------------|
| Host/test csproj | `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api` |
| `src/**/*.cs` | MediatR, Modules.One, BuildingBlocks, IPaymentGatewayAdapter, PaymentGatewayFactory, IPaymentGatewayFactory, AddPaymentsModule, GatewayPaymentCompletedIntegrationEvent, Modules.Payments, ApplicationFeeAmount, Razorpay.Api |
| tables | `ToTable("organizations"|"users"|"members")` |
| Vite package.json | `@repo/api-types-ts` |
| all Pay csproj | `apps/lazuar-api`, `Razorpay.Api` |

They do **not** lock: One HMAC dialect, one TX, ChargesPaused in fulfill, start idempotency, localhost success URLs, SST absence (except nobody imported SstTaxMath), registrar class name, DNS class name, frontend poll.

`RailTests` asserting `force_recurring` absent is the CHIP off-session lock that Isolation does not have.

---

## 8. What tests to write next vs what to fix first

**Do not** add CHIP registrar tests, factory tests, SST tests, refund tests, or e-mandate tests. **Do not** start a sixth rail to “complete SEA.”

### 8.1 Fix first (product code, in this order)

1. **Start idempotency (P0-A).** Return existing hosted URL or 409. Cheapest cash bug. Touches `PublicPayEndpoints` + checkout SPA (stop offering Pay if `PspRedirectUrl` exists / status verifying without query).
2. **Fulfillment respects `ChargesPaused` (P0-B part 2).** New attempts already 403; in-flight Plane B must 403/ignore **without** consuming paid event id if you want PSP retry after unsuspend — product decision, write it down. Default: do not fulfill, do not insert paid event, 409/403 so Stripe retries after reactivate **or** insert ignored `paused:{id}` and accept lost auto-retry (worse). Prefer not consuming.
3. **One HMAC dialect (P0-B part 1).** Steal `OutboundWebhookSignature.TryVerify` **judgment** (t/v1, `{unix}.{body}`, lowercase hex, skew). Read `tenant_id` **and** `org_id`. This is not an adapter; it is the suspend door 012 required before live keys.
4. **Razorpay join (P0-C).** Confirm lived `payment.captured` notes. If absent, join via payment_link id stored as `ProviderSessionId` or require `payment_link.paid` (and still not treat it as cash unless captured). Do not “fix” by fulfilling `order.paid` blindly.
5. **Pin units (P0-D)** against one captured payload per rail. If CHIP `total` is cents (Hub + current tests), document it in the parser with a comment that is a **constraint**, not a novel. If live disagrees, change **one** parser, not the journal.
6. **Empty webhook ciphertext: no process fallback except explicit Testing** (P0-E). 015 freeze already said process env is Stripe dev fallback. Tighten: fallback only when `IHostEnvironment.IsEnvironment("Testing")`, not every non-Production.
7. **CheckoutBaseUrl / merchant copy link** (P1-5). One config, used by mint + hosted defaults. Billplz redirect should not stay localhost when PublicBaseUrl is a tunnel — that is buyer UX, callback stays PublicBaseUrl.

Do **not** implement tax, factory, registrar, DNS, refunds, or off-session in this list.

### 8.2 Then write tests (hermetic unless noted)

Order matches leftover cash, then honesty.

| Priority | Test | Why | Blocks saying |
|----------|------|-----|----------------|
| T0 | Postgres (or SQLite with real transactions) **fulfill throw rolls back event id**; PSP retry fulfills | Closes 014 P0-2 as **proof**. InMemory cannot. | “one transaction” |
| T1 | Start twice returns same `redirect_url` (or 409) and FakePspHandler send count stays 1 | P0-A | “Pay is safe to double-click” |
| T2 | One HMAC: vector `t=…,v1=…` over `{unix}.{body}` 200 sets `ChargesPaused`; body-only uppercase hex 401; `tenant_id` works | P0-B | “suspend works” |
| T3 | `ChargesPaused=true` + valid paid webhook does not write `RCPT-` (and event-id policy from 8.1.2) | P0-B | “in-flight still charges” |
| T4 | Amount mismatch 400, documents 0, **event row absent**; currency omit fail-closed per rail (Billplz currently defaults — decide then test) | P0-D / P1-2 / P1-3 | “we fail closed on currency” |
| T5 | Razorpay `payment.captured` **without** notes → not a silent pay (400 or join via plink). `payment.failed` ignored. Header `X-Razorpay-Event-Id` preferred | P0-C | “Razorpay is done” |
| T6 | Placeholder `customer@example.com` start 400 for chip/billplz/xendit/razorpay | 015 email lock | “we reject Hub’s placeholder” |
| T7 | Billplz `Pay:PublicBaseUrl=http://localhost:8081` start 400 — **rename or split** `Billplz_paid_form_and_localhost_blocked` | P1-15 | test name as evidence |
| T8 | Production-like env: missing `Pay:WrapKey` throws on Protect; missing org webhook ciphertext 503 even if process Stripe secret set | P1-8 / P0-E | “BYOK webhook” |
| T9 | Member GET gateway never contains ciphertext (already) + PUT audit (already). GET `?provider=` returns inactive row without switching active | P2 UI | |
| T10 | SPA: vitest that `verifyingQuery` is true for `?status=verifying`; 503/400 copy is not a money test — do it after T0–T7 | P1-6/7 | |

**Do not** prioritize: Playwright of OIDC, Hub cutover, refund once, SST × seats, mail producer, Pay Dockerfile, 011 cell flips, generating pay-spec clients, wallet tiles (already locked), five-logo wall (already absent).

Hermetic CI remains law (`decisions.md` Tests row). Lived PSP payloads for T5/P0-D belong in a **runbook fixture** checked in as JSON, verified with FakePspHandler, not a live CHIP call in `task pay:test`.

---

## 9. What 015 checklists over-claim if you only read ticks

`c621ceba` is `docs(015): check off implemented T–Q phases`. Ticks are a map. Live counters:

| Claim | Live |
|-------|------|
| One DB transaction with fulfill | Coded; InMemory ignores TX; no throw-retry test |
| Billplz localhost 400 | Predicate exists; named test does not exercise it |
| Currency fail closed, do not default MYR | CHIP/Xendit/Razorpay throw; Stripe skips; Billplz defaults MYR |
| `:5179` verifying poll | Exists; 15 ticks; untested |
| Per-org webhook secret | PUT requires it; Stripe non-prod fallback remains |
| Writer checkout | True + test |
| Setup ≠ paid | Stripe + CHIP tested; Billplz unpaid / Razorpay failed not |
| No factory | True + Isolation |
| Tax out | True on pay path; column remains |
| Frontends match PUT fields | True for writer paste; pay link / success URL / error bodies do not |

Use this file, not the checkbox, when staffing the next slice.

---

## 10. Demo script that does not require lying (updated for five names)

One on **8080** (Hub **off**) → Pay **8081** + Postgres **5435** (migrate `FourAdaptersHostedRails`) → merchant **5178** OIDC via **5175** → owner picks **one** rail and pastes **API secret + webhook secret** (+ Brand/Collection id as required). For Stripe, still tunnel `POST /v1/webhooks/stripe/{orgId}` to the process; the `whsec_` is the **endpoint** secret from **that** Stripe account, stored on the row. For Billplz, set `Pay:PublicBaseUrl` to the **https tunnel**, not localhost; ignore the localhost webhook `<code>` on the page. Create MYR amount → copy the `http://localhost:5179/c/{token}` link (honest only on that laptop) → buyer, no login → Pay → processor test instrument → wait on **Verifying** until webhook → Paid / `RCPT-` on 5178. Replay webhook; document count stays 1.

**Pick one rail per demo.** A five-name `<select>` is not five dogfood loops. CHIP PEM is dashboard paste, not “we registered it.” Razorpay: if `RCPT-` does not appear, assume notes join (P0-C), not “the host is down.”

Do **not** claim: Hub replaced, SST, e-invoice, e-mandate, off-session, refunds, member-cannot-see-last4, suspend-stops-fulfill, BYOK wrap without `Pay:WrapKey`, one-TX as CI-proven, Billplz from a phone against localhost redirect, or “start is idempotent.”

---

## 11. Closing

015 moved new Pay from “one thin Stripe class with platform `whsec_` and a two-TX fulfill” to “five hosted_link HTTP extracts, per-org webhook ciphertext, writer mint, tax out, a verifying poll.” Isolation still holds. The cathedral types still fail CI. The refuse list still binds.

014’s P0 list is **not** fully retired. Member mint and setup-not-paid-as-proof and SST-seed are. Process `whsec_` is retired for **new** PUT rows in Production. One HMAC + pause-on-fulfill is not. One-TX is a Postgres reading of the handler, not a test. The new P0 is the one five rails all share: **start mints a new processor session every time**, and Razorpay’s paid join is the weakest of the five.

Frontends are closer to the host than 014 found. They are not “wired.” The staff form matches PUT. The buyer poll matches the default success query. The copied pay link, the localhost success URL, the collapsed error strings, and the decorative catalog are the remaining SPA lies.

**Fix start idempotency, One HMAC, pause-on-fulfill, and Razorpay join. Prove rollback on a real transaction. Then write the mismatch tests. Do not staff a factory, a registrar, DNS, tax, or e-mandate.**

This paper does not flip 011/11 cells. CHIP hosted_link **exists** on 8081 in hermetic tests; `NP-GW-003` still wants a lived loop before anyone honest flips it. Five names in a `<select>` are not five lived loops.
