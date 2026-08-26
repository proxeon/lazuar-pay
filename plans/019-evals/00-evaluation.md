# 00 — Parent evaluation: newest Pay after 018 merchant shell — bugs, gaps, how to solve

**Date:** 26 August 2026  
**Branch:** `feat/018-merchant-shell`  
**HEAD:** `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome`  
**This file is the parent judgment.** The ten reports `01`–`10` are the uncondensed evidence (~11,900 lines). **Do not treat this file as a substitute for those reports.** Do not skip a report because a table below has a one-liner.

This paper does **not** implement. It does **not** flip [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. It does **not** add a project reference into `apps/lazuar-api`.

Live files on **this SHA** are authority. [014](../014-evals/README.md) froze Stripe-only Bar B. [016](../016-adapters-check/README.md) froze five hosted_link rails on `c621ceba`. [018-evals](../018-evals/001-evals.md) is a **product** paper (kernel vs escrow vs WhatsApp SME), not a source audit. If any of those disagree with live files, live files win; the ten reports name the disagreement.

---

## 1. Verdict

018 shipped a **hosted cashier** that is honestly better than 016’s one-page Workspace:

- Independent processor vault (PUT no longer flips `active_provider`; bind rail at mint).
- Local **Test** rail with no secrets (start = paid).
- Pay links with a **capacity** field, table + mint dialog, Aura merchant shell, restyled buyer page.
- Six hosted names on a `switch` (`stripe|chip|billplz|xendit|razorpay|test`), same-handler Official Receipt, buyers still have no One account.

It is **not** a kernel other apps can swallow in an afternoon: there is still no machine key (`lzr_sk_`) and no outbound `payment.completed`. [018-evals](../018-evals/001-evals.md) already said that. Live files still say that.

**016’s cash P0 list is mostly retired on this SHA** (HMAC vs *Hub* One, pause-on-fulfill, writer mint, start idempotency when `PspRedirectUrl` is set, Testing-only process `whsec_` / WrapKey, Razorpay `plink_` join). Folders moved (`Gateways/` → `Rails/` + `Webhooks/` + `Credentials/` + `Identity/`). Do not quote 016 paths as live.

**The new P0 is occupancy.** “1 person only” is a `COUNT` then `INSERT` with no lock. Two `slot_key`s can both mint, both hit a real PSP, both get `RCPT-`. Sequential `PaymentLinkTests` are green. That is 016 P0-A’s cousin after payment-links shipped.

**Second money hole 018 introduced:** Test Plane B is unsigned in every **non-Production** environment, amount/currency optional, receipts look like Stripe. Staging that is not named `Production` is a forge path.

**Third hole the 016 papers cannot see:** product One (`lazuar-one`) HMAC vs Pay’s verifier. [07](./07-identity-authz-cors.md) and [10](./10-honesty-bugs-gaps.md) **disagree**. Do not pick a winner from this page — read both. Summary of the split is §4.

**Frontends:** `:5178` is no longer the 016 Workspace dump. Processor cards, Edit dialog, CHIP PEM textarea, GET `/gateways` hydrate, Test always offered, pay-link table, capacity copy that **lies** about paid vs start. `:5179` polls `?status=verifying`, sends `slot_key`, has a full pixel, does not treat success URL as paid, still has no OIDC. Remaining SPA holes are laptop CORS/API defaults, silent list GETs, Loading graveyard, Test injected even when Production host omits it.

**Fix money first (occupancy lock + occupancy definition + Test webhook), write the concurrent test that would have caught it, then SPA copy, then pay-spec, then the kernel door.** Do not staff a factory, a registrar, SST, e-mandate, escrow on Processor, or Hub cutover in the same slice.

---

## 2. Where we actually are (three new apps)

| App | Port | What it is on `9f04ad58` | What 016 said at `c621ceba` |
|-----|------|--------------------------|------------------------------|
| `apps/lazuar-pay` | **8081** | Six `hosted_link` names; independent vault; pay-link occupancy; Test auto-fulfill; folder-by-job | Five names; PUT flipped `active_provider`; no payment-links; `Gateways/` dump |
| `apps/lazuar-pay-merchant` | **5178** | Aura shell; last workspace; staff email; processor cards + Edit; Test always Ready; pay-link table + capacity dialog | One WorkspacePage; one active rail picker; CHIP PEM as `<input>` |
| `apps/lazuar-pay-checkout` | **5179** | Aura Card; `slot_key`; full/expired/paid/verifying pixels; `readDetail` on 400/503; Test copy | GET + poll; mashed 400 sentence; no occupancy |

Old stack is still **museum, still in root compose**: `lazuar-api` on **8080** (collides with product One), ops **3003**, portal **3004**. CORS on 8081 still denies 3003/3004. Do not retarget them.

Hermetic suite on this SHA ([09](./09-tests-inventory.md)): **123** NUnit `[Test]` + **32** vitest `it()` = **155**. 016 counted 58 / 8. InMemory still ignores transactions. IsolationTests ban cathedral strings including `IEnumerable<IHostedRail>` and `namespace Lazuar.Pay.Gateways`.

`packages/pay-spec` ([08](./08-contracts-spec-honesty.md)): live host has **22** doors; TypeSpec describes **13**; on-disk OpenAPI describes **11** of an older 13. Both Vite apps follow the **host**, not the spec. Kernel doors (machine key, outbound event) are absent from all three.

---

## 3. Evidence map

Do not skip a report because this table has a one-liner. Line counts are of the file on disk at write time.

| Report | Slice | Lines | One-line take |
|--------|-------|------:|----------------|
| [01](./01-pay-host-seams.md) | Host seams | 1368 | Independent vault is live. Occupancy is check-then-act. Two mint doors. Catalog is a label. WrapKey required outside Testing; `.env.example` lies. |
| [02](./02-merchant-frontend.md) | Merchant Vite | 1491 | Aura shell is real. 016 PEM/keys-400/no-provider findings are **false** here. Highest SPA holes: silent list GET, busy without `finally`, Test always injected, webhook URL is `VITE_PAY_API_URL`. |
| [03](./03-checkout-frontend.md) | Checkout Vite | 1488 | No OIDC, success≠paid, `slot_key`, full pixel. Remaining: localhost API/CORS, Loading graveyard, private-mode slot rotation, max-1 “Thank you” to strangers. |
| [04](./04-processors-vault-test.md) | Vault + Test | 1023 | PUT does **not** write `ActiveProvider`. Test is `!IsProduction()`. Unsigned Test webhook. Mint dialog defaults to Test (`firstReal` dead). |
| [05](./05-payment-links-occupancy.md) | Occupancy | 1273 | Seat = child `open` or `paid`. Copy says paid. Abandoned `open` never expires. Fulfillment does not re-check the cap. |
| [06](./06-rails-webhooks-fulfillment.md) | Rails + Plane B | 1039 | Six folders, one switch, Official Receipt. 016 HMAC/pause/`whsec_`/start-idempotency closed **in this host**. Stripe `payment_status` unread. No unique charge. InMemory TX unproven. |
| [07](./07-identity-authz-cors.md) | Identity | 1056 | Writer/member gates match law. No PAT. **Product One HMAC ≠ Pay verifier** (sibling `lazuar-one`). Tests lock Pay’s dialect. Pause never fires on the real wire **if 07 is right**. |
| [08](./08-contracts-spec-honesty.md) | pay-spec | 1262 | 22 live doors / 13 tsp / 11 stale OpenAPI. Payment-links, `/gateways`, Test, `slot_key`, receipts, payments missing from spec. |
| [09](./09-tests-inventory.md) | Tests | 966 | Every method named. Xendit/Razorpay still lack paid **replay**. Occupancy tests sequential only. Vitest is grep; not in CI. |
| [10](./10-honesty-bugs-gaps.md) | Honesty | 921 | What we may say. 016 P0 re-verify table. New P0 occupancy. Sequence: lock, then Test honesty, then spec, then kernel. Treats One HMAC as **FIXED vs 016**. |

---

## 4. Report disagreement you must not paper over

**One HMAC / pause charges.**

| Paper | Claim |
|-------|--------|
| [10](./10-honesty-bugs-gaps.md) §016 P0-4 | **FIXED.** Pay verifies Standard Webhooks `t=,v1=` over `{unix}.{body}`. Fulfillment reads `ChargesPaused`. Tests: suspend, reactivate, body-only uppercase hex is 401. |
| [07](./07-identity-authz-cors.md) B1 | **OPEN against product One.** Sibling `lazuar-one` `WebhookSigning` / `WebhookDispatcher` send `X-Lazuar-Signature: v1=<hex>` and a **separate** `X-Lazuar-Timestamp`. Pay `TryParseHeader` requires `t=` **inside** the signature header. Live `tenant.suspended` → 401. Hermetic `OneWebhookTests.Sign` mints Pay’s dialect, so CI stays green. |
| Hub museum `OutboundWebhookSignature` in `apps/lazuar-api` | Combined `t={unix},v1={hex}` in one header — **matches Pay**, **is not** the IdP Pay’s README points at (`One__BaseUrl=http://localhost:8080/api/v1` = product One). |

**Parent stance:** 10 is right that **016’s Hub dialect P0 is closed in Pay source**. 07 is right that **Pay vs live lazuar-one is a separate question**, and the tests do not prove the live wire. Treat pause-charges as **unproven on product One** until someone replays a real `tenant.suspended` from `lazuar-one` against 8081. Do not “fix” it by importing Hub `Modules/One`. Copy the **algorithm** product One actually sends.

Everything else in §5 is consistent across the ten papers.

---

## 5. Ranked bugs (parent list — evidence lives in the reports)

### P0 — money can be wrong

1. **Pay-link occupancy is count-then-insert.** Two different `slot_key`s can take the last seat, both call a PSP, both get Official Receipts. Unique index is `(PaymentLinkId, SlotKey)` on Npgsql only; it does not cap `N`. [01](./01-pay-host-seams.md) B1, [05](./05-payment-links-occupancy.md) B1/B7, [10](./10-honesty-bugs-gaps.md) P0-1. **Solve:** `SELECT … FOR UPDATE` on the parent link in the same TX as the child insert; PSP HTTP only after the seat commits; 409 full and **no** PSP on violation. Concurrent test (T0 in [10](./10-honesty-bugs-gaps.md)). InMemory cannot prove this.

2. **Test Plane B is unsigned in every non-Production env; amount/currency optional.** `AllowsTest = !IsProduction()`. `TestWebhook.Parse` has no HMAC; missing `id` becomes a new Guid (replay never duplicates). Same `RCPT-` title as Stripe. [04](./04-processors-vault-test.md) B1/B2, [06](./06-rails-webhooks-fulfillment.md) B1–B3, [10](./10-honesty-bugs-gaps.md) P1-2 (parent ranks this **P0** if Staging exists). **Solve:** allow Test only in Development/Testing **or** drop the webhook route and keep start-to-pay; require `id` + `checkout_id` + amount + currency if the route stays; Production factory test 400s Test.

3. **Occupancy grain vs copy (product-false with cash effect).** Host counts `open` **or** `paid`. Merchant: “closes after one **successful payment**.” A Stripe start fills a 1-person link before Plane B. Abandoned starts never expire (`expired` is read, never written). Failed email/PSP after mint still holds the seat. [05](./05-payment-links-occupancy.md) B3/B4/B5, [01](./01-pay-host-seams.md) B3, [03](./03-checkout-frontend.md) B6. **Solve:** pick a rule (paid + TTL reservation recommended) and make host, merchant copy, and GET `full` quote it. Do not leave A in code and B in the dialog.

4. **Stripe `checkout.session.completed` without `payment_status`; no unique charge-per-checkout.** [06](./06-rails-webhooks-fulfillment.md) B4/B5. Delayed methods can book unpaid; two grains can double `RCPT-`. **Solve:** ignore unpaid completed; honor `async_payment_succeeded`; unique `charges.CheckoutId`; CAS `open→paid`.

5. **Product One HMAC (conditional P0).** If [07](./07-identity-authz-cors.md) is right, `tenant.suspended` never sets `ChargesPaused`, so paused orgs still take buyer money. Public start only reads that flag. **Solve:** match product One’s two-header dialect; rewrite `OneWebhookTests.Sign`; keep rejecting body-only uppercase hex.

### P1 — product-false / dogfood / leftover 016 cash

- Merchant always injects Test (`withTest`); mint `<Select>` defaults to Test even when a real rail is on file (`firstReal` dead). Production host 400s `"test processor is not enabled"`. [02](./02-merchant-frontend.md) B8, [04](./04-processors-vault-test.md) B3/B4.
- SaveChanges **after** PSP HTTP on CHIP/Billplz/Xendit/Razorpay can orphan a processor session (016 P0-A residual). Stripe has an idempotency key. [06](./06-rails-webhooks-fulfillment.md) B6, [10](./10-honesty-bugs-gaps.md) P1-3/P1-10.
- CHIP metadata-only join; amount mismatch 400 does **not** consume event id (fail-closed if hostile; lost cash if **our** units are wrong). [06](./06-rails-webhooks-fulfillment.md) B7/B8.
- CHIP create sends no purchase currency. [10](./10-honesty-bugs-gaps.md) 016 P1-4 **OPEN**.
- Fulfill TX coded, unproven on Postgres. `PayApiFactory` ignores transactions. [06](./06-rails-webhooks-fulfillment.md) G3, [09](./09-tests-inventory.md).
- Same-slot unique violation → 500 on Npgsql (MintOrResume does not catch it). [01](./01-pay-host-seams.md) B2, [05](./05-payment-links-occupancy.md) B2.
- WrapKey required outside Testing; `.env.example` still implies a fallback. First vault PUT 500s if `.env` is unused. [01](./01-pay-host-seams.md) B5, [04](./04-processors-vault-test.md) G8.
- Checkout SPA: `VITE_PAY_API_URL` defaults to `localhost:8081`; CORS is laptop literals; GET failure is infinite Loading. [03](./03-checkout-frontend.md) B1–B3.
- Merchant list GETs fail closed-silent (empty table looks like “no rows”). Writer `busy` has no `finally` (orphan products). Webhook hint is loopback `payApi`. [02](./02-merchant-frontend.md) B3/B4/B6.
- `slot_key` is client-supplied (8–128 chars). localStorage throw mints a new UUID every call. Private mode double-seats. [05](./05-payment-links-occupancy.md) B6/B7, [03](./03-checkout-frontend.md) B4.
- Catalog amount is stored then **ignored** at mint; interval stays `one_off`. Label only. [01](./01-pay-host-seams.md) B11/G1.
- pay-spec / dist OpenAPI do not describe payment-links, `/gateways`, Test, `slot_key`, receipts, payments. [08](./08-contracts-spec-honesty.md).
- Xendit SETTLED ≠ paid replay; Razorpay no captured replay. F00 still uneven. [09](./09-tests-inventory.md).

### P2 — polish after money is boring

Writer = `/me` role overlay, not `authz/check admin`. Dummy `/ready`. No invite from merchant. CORS production still localhost. No Pay Docker image. Root compose still Hub. IsolationTests do not lock “do not write ActiveProvider.” GET receipt-by-id untested. Child checkout tokens are extra pay URLs. Malay copy absent. `dist/` stale. Vitest not in CI. Dead `ActiveProvider` / `SstRegistered` columns. `AddDataProtection()` unused.

---

## 6. What we may honestly say vs must not say

**May say (on this SHA, local dogfood):**

- Staff paste BYOK keys per rail. Saving a vault does **not** pick the pay-link rail.
- Mint a hosted link with an explicit provider that already has keys (Test needs none in non-Production).
- Buyer pays on `:5179` with no One account, no PAN, no PSP picker.
- Success URL is not paid; the page polls until Plane B (or Test start) writes Official Receipt `RCPT-…`, not a tax invoice.
- Capability is `hosted_link`. We do not auto-debit.
- Isolation from Hub Payments / MediatR / factory still holds.

**Must not say:**

- “1 person only” is safe under two simultaneous Pays.
- Test is local-only / Production-safe. (`!IsProduction()`, unsigned webhook, SPA always offers it.)
- We are a Stripe-shaped kernel. No `lzr_sk_`, no `payment.completed`.
- Five (or six) rails are production BYOK. Xendit/Razorpay replay is incomplete; InMemory is not a transaction; CHIP currency on create is missing.
- `task pay:spec` / compiled OpenAPI is the host.
- One `tenant.suspended` pauses charges **on the live product One wire** (unproven; see §4).
- Catalog prices the link. Amount is typed in the dialog.
- Official Receipt is an e-invoice / SST invoice.
- Hub adapters run on 8081. HTTP extracts only; no factory, no registrar, no DNS folklore.

---

## 7. How to solve (sequence)

Matches [10](./10-honesty-bugs-gaps.md) §sequence. Do not invert it.

**Money (product code)**

1. Serialize last-seat mint (P0-1).
2. Write the occupancy rule (paid vs reservation + TTL) and make three surfaces quote it (P0-3).
3. Close Test Plane B (P0-2): narrow `AllowsTest`, or drop unsigned webhook.
4. Confirm product One HMAC with a live envelope (P0-5 / §4). Pause is the buyer belt.
5. Unique charge + unique `RCPT-`; Stripe `payment_status`.
6. Then: CHIP currency on create; persist-before-PSP or per-rail idempotency.

**The test that would have caught it** (hermetic unless named)

| # | Test | Blocks saying |
|---|------|----------------|
| T0 | Two concurrent `POST /start`, `max_payers=1`, different slots; documents ≤ 1; PSP HTTP ≤ 1 | “1 person only” |
| T1 | Stripe start without webhook on max=1; second slot matches the **written** rule | “closes after one successful payment” |
| T2 | Production env: Test mint 400; unsigned Test webhook 400 | “Test is local-only” |
| T3 | Two concurrent fulfills → distinct `RCPT-` | “Official Receipt numbers” |
| T4 | Non-InMemory fulfill-throw: event absent, retry pays | “one transaction” |
| T5 | Product One `v1=` + `X-Lazuar-Timestamp` suspends charges (and `t=,v1=` either 200 or documented 401) | “pause works” |
| T6 | CHIP start body includes currency; lived-unit mismatch 400 | “we fail closed on currency” |

Do not call live PSP from `task pay:test`.

**SPA (after T0–T3 are red then green)**

- Occupancy copy matches the rule.
- Render Test from `GET /gateways`; delete `withTest`; empty default, prefer first real rail.
- Receipts show `provider`; Test ≠ Stripe.
- Fail list GETs loudly; `try/finally` on mint.
- Checkout: error Card + Retry; never default production API to localhost; fail-build without `VITE_PAY_API_URL`.
- Billplz webhook `<code>` is `Pay:PublicBaseUrl` or it stops pretending loopback is pasteable.

**Contracts**

Grow `packages/pay-spec` to the **22 live doors** after 1–4 stabilize. Do not generate SPA types from today’s tsp (that would make the UIs worse — [08](./08-contracts-spec-honesty.md)).

**Kernel (after the cashier does not double-seat)**

Machine auth + signed `payment.completed` + a second app that is not `:5178`. That is 018’s company. It is not step 1.

---

## 8. Next ten work items (named, not coded)

1. Occupancy lock on `payment_links` (last seat).
2. Occupancy definition + expire unpaid `open` children; rewrite the dialog sentence.
3. Concurrent occupancy test (Postgres or an explicit lock seam).
4. Test rail honesty (env gate, webhook, receipts, SPA hide).
5. Unique `RCPT-` + unique charge in the fulfill TX.
6. Product One HMAC replay (or a dual-header verifier with tests that name both dialects).
7. Real-TX fulfill-throw fixture (not InMemory).
8. CHIP currency on create + lived unit fixtures; persist-before-HTTP residual.
9. pay-spec catch-up (payment-links, `slot_key`, Test, `/gateways`, `provider` on create).
10. Kernel dogfood door (`lzr_sk_` + `payment.completed` + sample). Not on the Processor card. Not escrow.

---

## 9. Refuse

Do **not** in the same program as the occupancy lock:

- `IEnumerable<IHostedRail>` / `PaymentGatewayFactory` / Hub `IPaymentGatewayAdapter`
- Project reference into `apps/lazuar-api`; MediatR; outbox; `ChipWebhookRegistrar`; `PublicDnsFallback`
- SST / LHDN / Tax Invoice / `SstRegistered` on the pay path
- E-mandate / off-session / `force_recurring`
- Escrow on the Processor card
- Hub cutover; retarget ops `:3003` or portal `:3004`
- Revive `ActiveProvider` as the mint default
- Enable Test in Production
- Generate clients from stale `pay-spec` and call them the host
- Flip 011/11 cells from this evaluation
- A sixth commercial rail “to complete SEA” (Test is not GrabPay)

---

## 10. Closed since 016 (do not re-open as if live)

Evidence in [10](./10-honesty-bugs-gaps.md) §016 table and [06](./06-rails-webhooks-fulfillment.md) “Plane A not still wrong”:

- Writer-gated mint (checkouts **and** payment-links)
- Start returns stored hosted URL (no second PSP HTTP when `PspRedirectUrl` is set)
- Per-org webhook ciphertext; Stripe process `whsec_` **Testing-only**
- WrapKey git-string fallback **Testing-only**
- Fulfillment reads `ChargesPaused`; webhook 409 without consuming the paid event id
- Razorpay join via `plink_` / `ProviderSessionId`, not notes-only
- Billplz localhost callback fail-closed **and** tested; currency not hardcoded MYR
- CHIP PEM is a textarea; GET hydrates Billplz `environment`; mashed start-400 sentence gone
- `CheckoutUrls.Base` requires `Pay:CheckoutBaseUrl` outside Testing
- Independent vault: PUT does not write `ActiveProvider`
- Verifying poll has a timeout + Refresh (not a stuck pixel)
- Isolation fence still holds (no Hub types in Vite; no `IEnumerable<IHostedRail>`)

016 papers that still say “PUT always flips active provider” or “One HMAC is body-only uppercase hex **in Pay source**” are **stale**. Quote this SHA.

---

Read [01](./01-pay-host-seams.md)–[10](./10-honesty-bugs-gaps.md). This parent is a map.
