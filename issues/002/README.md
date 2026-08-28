# 002 — Newest Pay (`feat/018-merchant-shell`) bugs

**Date:** 26 August 2026  
**HEAD:** `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome`  
**Source:** [plans/019-evals](../../plans/019-evals/README.md)  
**Status:** 001–018, 020–028, 030–061 resolved on `fix/002-pay-host-bugs`. Remaining: 019, 029, 062–080.

Extracted from the uncondensed 019 evaluation of:

- `apps/lazuar-pay` (8081)
- `apps/lazuar-pay-merchant` (5178)
- `apps/lazuar-pay-checkout` (5179)

Parent map: [plans/019-evals/00-evaluation.md](../../plans/019-evals/00-evaluation.md). Do not treat this index as the analysis.

Duplicates across 01–10 reports were merged into **one issue each**. Gaps that are not live lies (kernel door, Malay copy, invite chrome) are **not** listed here.

| # | Sev | File |
|---|-----|------|
| 001 | P0 | Occupancy count-then-insert overfills capped pay links |
| 002 | P0 | Seat reserved before start can succeed (email/PSP 400 occupies) |
| 003 | P0 | Abandoned `open` children never expire |
| 004 | P0 | Occupancy copy lies: “successful payment” vs start |
| 005 | P0 | Fulfillment pays over-capacity children |
| 006 | P0 | Test Plane B unsigned in every non-Production env |
| 007 | P0 | Test webhook omits amount/currency and still pays |
| 008 | P1 | Test webhook mints a new EventId when `id` is missing |
| 009 | P0 | Stripe `checkout.session.completed` paid without `payment_status` |
| 010 | P0 | Concurrent fulfill can double-book one checkout / collide `RCPT-` |
| 011 | P0 | Product One HMAC dialect ≠ Pay verifier; suspend never pauses |
| 012 | P0 | CHIP paid join is metadata-only |
| 013 | P1 | Same-slot start race is a 500, not a resume |
| 014 | P1 | PSP HTTP then persist can mint a second hosted session |
| 015 | P1 | Amount/currency mismatch 400 does not consume the event |
| 016 | P1 | CHIP purchase create has no currency field |
| 017 | P1 | Development WrapKey: docs lie, first vault PUT is 500 |
| 018 | P1 | Connection-string “password keep” replaces the whole CS |
| 019 | P1 | Public `slot_key` is a client-supplied seat; capped links can be griefed |
| 020 | P1 | Checkout idempotency is racy, body-blind, always 201 |
| 021 | P1 | Development `MigrateAsync` can crash the host; Cors/Health tests boot it |
| 022 | P1 | `CheckoutUrls.Base` throw in `MintOrResume` is uncaught 500 |
| 023 | P1 | Catalog `product_id` is not money; amount is typed at mint |
| 024 | P1 | `.env.example` still advertises a Dev process `whsec_` fallback |
| 025 | P2 | `ChargesPausedException` catch order is brittle |
| 026 | P1 | 016-era open checkouts with null `Provider` can no longer start |
| 027 | P1 | PUT accepts any CHIP `webhook_secret`; PEM checked only at verify |
| 028 | P1 | Re-saving a non-Billplz vault always writes `environment=test` |
| 029 | P1 | Single process `Pay:OneWebhookSecret` vs One per-tenant `whsec_` |
| 030 | P1 | Writer is `/me` role overlay, not `authz/check admin` |
| 031 | P2 | GET org checkouts mixes one-off mints and occupancy children |
| 032 | P2 | Child checkout public tokens are a second pay URL |
| 033 | P2 | Charges-paused after mint stuck-occupies the seat |
| 034 | P1 | Test occupancy tests hide the reservation model |
| 035 | P1 | Org layout spins forever if `access_token` is not a JWT |
| 036 | P1 | Whoami 401 on org routes is a stuck banner |
| 037 | P1 | List GETs fail closed-silent; empty tables lie |
| 038 | P1 | Writer busy flags have no `try/finally`; catalog orphans |
| 039 | P1 | Overview counts Test as “On file” |
| 040 | P1 | Webhook URL hint is `VITE_PAY_API_URL`, not `Pay:PublicBaseUrl` |
| 041 | P1 | Buyer copy URL defaults to hardcoded `:5179` |
| 042 | P1 | Merchant always offers Test; host refuses it in Production |
| 043 | P1 | Mint dialog defaults to Test even when a real rail is on file |
| 044 | P2 | `automaticSilentRenew` uses `/callback` as the iframe target |
| 045 | P2 | Duplicate `<h1>` on money pages; Settings is Processor |
| 046 | P2 | “Not a member” / first-workspace create have no way out |
| 047 | P1 | Stale `returnTo` / `setOrgHint` before membership |
| 048 | P1 | Non-404 GET failure paints Loading… forever |
| 049 | P1 | CORS allow-list is laptop-only |
| 050 | P1 | Hardcoded checkout API fallback `http://localhost:8081` |
| 051 | P1 | `localStorage` failure mints a new `slot_key` on every call |
| 052 | P1 | One-person paid link shows “Payment received” to strangers |
| 053 | P2 | Email-required Pay is disabled with no explanation |
| 054 | P2 | No prefill after cancel / resume |
| 055 | P1 | Verifying timeout does not restart; Pay form stays unreachable |
| 056 | P2 | Poll ignores missing and can disagree with the missing pixel |
| 057 | P1 | `startPay` network throw is unhandled |
| 058 | P2 | Path regex has no `$` — extra path segments still pay |
| 059 | P2 | Checked-in checkout `dist/` is not this SPA |
| 060 | P2 | Card titles are not headings; confirming has no live region |
| 061 | P2 | Start 200 with no `redirect_url` is silent |
| 062 | P2 | `GET /v1/checkouts/{id}` 404s before Bearer (existence oracle) |
| 063 | P2 | Invalid JSON on Plane A after HMAC success is an unhandled 500 |
| 064 | P2 | One 400 / 429 on `authz/check` become Pay 503 |
| 065 | P2 | Suspended tenant copy says “not a member” |
| 066 | P2 | CORS tests do not prove `/v1/pay/*` or OPTIONS |
| 067 | P1 | `dist/openapi.yaml` is stale vs `main.tsp` |
| 068 | P1 | `GET /gateway` without `?provider=` returns the list envelope |
| 069 | P1 | TypeSpec catalog create has no body; host requires name+amount |
| 070 | P1 | TypeSpec `CreateCheckoutRequest` omits required `provider` |
| 071 | P1 | Start `slot_key` required on links; spec body optional / dist none |
| 072 | P2 | Spec says 200; host returns 201 on mint doors |
| 073 | P2 | Webhook spec requires `{ ok }`; live duplicate/ignored omit it |
| 074 | P2 | Whoami `name` is on the wire, not in TypeSpec |
| 075 | P2 | `GET .../receipts/{id}` mapped, untested, unused |
| 076 | P2 | Unversioned `GET /ready` mapped and untested |
| 077 | P1 | InMemory is still not a transaction proof |
| 078 | P2 | `/v1/orgs/{id}/ready` is still dummy `ready: true` |
| 079 | P1 | Occupancy remaining display clamps over-admit (honesty leftover) |
| 080 | P1 | Single-process CORS / compose still laptop-shaped (Pay image missing) |
