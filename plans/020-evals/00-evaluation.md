# 00 — Parent evaluation: production-ready Pay and the second-app kernel door

**Date:** 28 August 2026  
**Branch:** `fix/002-pay-host-bugs`  
**HEAD:** `6d730d15` — `fix(pay): store per-org One webhook secrets`  
**This file is the parent judgment.** The ten reports `01`–`10` are the uncondensed evidence (~12,700 lines). **Do not treat this file as a substitute for those reports.** Do not skip a report because a table below has a one-liner.

This paper does **not** implement. It does **not** flip [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. It does **not** add a project reference into `apps/lazuar-api`.

Live files on **this SHA** are authority. [019-evals](../019-evals/README.md) froze the 018 hosted-cashier bugs on `9f04ad58` and extracted [issues/002](../../issues/002/README.md) (YAML 001–080 resolved here). [012-one-to-pay](../012-one-to-pay/README.md), [013-prods](../013-prods/README.md), [006-sample](../006-sample/README.md), and [011-new-lazuar-pay/08-bezos-door.md](../011-new-lazuar-pay/08-bezos-door.md) are product papers. If they disagree with live files, live files win; the ten reports name the disagreement.

[10](./10-honesty-production-bar.md) was written **before** `01`–`09` existed. Its §8 disagreements are **predicted**. This parent observes the actual reports. Where 10 and a sibling disagree, the sibling that opened the live handler wins; §4 names the splits.

---

## 1. Verdict

Pay on `6d730d15` is a **hosted cashier for One workspaces**. It is **not** a payments API platform. It is **not** production-ready under the [013 Bar B](../013-prods/01-production-ready-bar.md) sentence. It is **not** a kernel another app can swallow in an afternoon.

Two bars must not be collapsed:

| Bar | Pass sentence | This SHA |
|-----|---------------|----------|
| **First-party dogfood** (013 Bar B) | Merchant signs in through One, pastes CHIP or Stripe keys, a buyer pays on `:5179` with no One account, Pay shows one `RCPT-` and a balanced journal, a PSP webhook retry no-ops. | **Partial.** Occupancy lock, Test unsigned Plane B, Stripe `payment_status`, unique charge/`RCPT-`, One HMAC **dialect**, per-org One `whsec_` are in source and named tests. Human loop, production CORS/WrapKey, `/ready` that actually fails, persist-before-PSP on non-Stripe rails, and a live One `tenant.suspended` capture are not. |
| **Second-app kernel** (020) | A stranger mints One `lzr_sk_`, `POST /v1/checkouts`, starts pay or hands the buyer a URL, and learns `payment.completed` without cloning this repo. | **Fail.** MemberGate omits `user_id` so live One **400s** API keys. No outbound Pay→app event. No Pay sample. Hub `examples/` still teaches museum 8080. |

002 did its job: the 019 **cash P0s of a hosted cashier** are closed in source. Those P0s were **not** the kernel door. 019’s parent already said that. Live files still say that.

**What other apps are missing** (the user question, answered by the reports, not instead of them):

| Ask | Live | Report |
|-----|------|--------|
| Secret key / M2M | One mints `lzr_sk_`. Pay forwards any Bearer. Org-gated doors 400 on that key. No Pay-minted `sk_*`. | [02](./02-machine-keys-m2m.md) |
| Webhooks **out** to the app | Absent. Fulfill writes charge/journal/`RCPT-` in-process. Stranger polls. | [03](./03-outbound-webhooks.md) |
| Webhooks **in** (PSP / One) | Live. Two planes, two secrets, two tables. Ops must register One URL; One SSRF blocks loopback. | [04](./04-inbound-webhooks.md) |
| Clean `/v1` | 24 Map* / 22 spec ops, honesty green. Cashier-shaped: human JWT writers, no pagination, no `pay_url` on mint, problem JSON is not RFC7807. | [01](./01-public-http-api.md) [09](./09-spec-docs-sample.md) |
| M2M | Same as secret key. Merchant SPA rejects non-JWT. Hermetic suite never sends `lzr_sk_`. | [02](./02-machine-keys-m2m.md) [05](./05-identity-authz-tenancy.md) [08](./08-headless-vs-spa.md) |

**Fix the kernel door for other apps (M2M that One will accept, then one signed `payment.completed`, then a `examples/pay-node` that is not Hub).** Separately, finish first-party go-live (WrapKey/CORS fail-boot, `/ready` bool, 014 persist-after-PSP, captured One pause). Do not staff refunds, subscriptions, SST, LHDN, escrow, MediatR, or Hub cutover in the same slice.

---

## 2. Where we actually are

| App | Port | What it is on `6d730d15` |
|-----|------|--------------------------|
| `apps/lazuar-pay` | **8081** | Six `hosted_link` names; independent vault; pay-link occupancy (`SemaphoreSlim` + parent `FOR UPDATE`, 30 min lazy TTL); Test auto-fulfill only in Development/Testing; per-org One webhook secret; 24 HTTP maps |
| `apps/lazuar-pay-merchant` | **5178** | Aura shell, One OIDC PKCE. Mints **payment-links**, not `POST /v1/checkouts`. Copy URL from `VITE_CHECKOUT_ORIGIN`. Never sends `lzr_sk_`. |
| `apps/lazuar-pay-checkout` | **5179** | No OIDC. Polls `?status=verifying`. Success URL is not paid. `slot_key` is one browser ≈ one seat. |
| Sibling `lazuar-one` | **8080** | Identity. Mints `lzr_sk_`. Outbound tenant webhooks. Pay is Consumer-0 over HTTP. |

Old Hub is still **museum, still in root compose** (`lazuar-api` 8080, ops 3003, portal 3004). Pay images exist (`docker-compose.pay.yml --profile apps`, bake group `pay`). Profile apps is still laptop-shaped (Development, empty WrapKey, no Postgres volume). **Refuse** retargeting Hub compose onto 8081.

Bezos door holds for the SPAs ([08](./08-headless-vs-spa.md)): they only `fetch` `/v1` (plus One `/tenants` to create a workspace). They do not import `internal/`. The headless one-off mint door exists and the dashboard **does not use it**.

---

## 3. Evidence map

Do not skip a report because this table has a one-liner. Line counts are of the file on disk at write time.

| Report | Slice | Lines | One-line take |
|--------|-------|------:|----------------|
| [01](./01-public-http-api.md) | Public HTTP API | 1155 | 24 Map* / 22 tsp, honesty exit 0. Stranger cannot call `/v1` as a product: human JWT writers, no pagination, no API-key header. Hatch: mint+poll with a working Bearer. |
| [02](./02-machine-keys-m2m.md) | Secret keys / M2M | 1227 | Pay does not mint keys. One `lzr_sk_` forwarded as Bearer; `authz/check` **without `user_id` 400s**. Writer overlay would still treat a typical key as member. Refuse Pay `sk_*`. |
| [03](./03-outbound-webhooks.md) | Plane C Pay→app | 1525 | Honest empty set. Hatch: one signed POST after fulfill, type `payment.completed`, per-org endpoint + SecretBox, One dialect. Refuse Hub dispatcher. |
| [04](./04-inbound-webhooks.md) | Planes A + B | 1231 | Inbound is the production webhook story that **exists**. Dialect + per-org secret live. Live One envelope still uncaptured. Ops registers URL. Other apps do not call Plane B. |
| [05](./05-identity-authz-tenancy.md) | Identity / tenancy | 1204 | Consumer-0 of One for **staff**. Buyers are not One. `org_id` is the One tenant UUID (product law). CORS CSV; Production empty throws. Never a Pay user table. |
| [06](./06-host-production.md) | Host production | 1315 | 080 letter closed; not a production process. `/ready` discards `CanConnectAsync`. Empty CS / One BaseUrl silent-laptop. No OTel. Vitest not in CI. |
| [07](./07-money-remaining.md) | Money leftover | 1455 | Occupancy P0 closed as cashier. YAML 014 still live (PSP HTTP then persist). No refund/cancel/expire/subscription doors. Catalog amount 400 on **links**, not checkout mint. |
| [08](./08-headless-vs-spa.md) | Headless vs SPA | 1209 | SPAs are `/v1` clients. Merchant never hits checkout mint. Headless path is live HTTP and unused. Missing `pay_url` on 201. 002 UI 035–061 live-fixed. |
| [09](./09-spec-docs-sample.md) | Spec / docs / sample | 1232 | Path honesty green. Dist gitignored. Root README hides Pay. `examples/` is Hub cashier. No Pay client. Refuse waiting on npm `@lazuar/one-client`. |
| [10](./10-honesty-production-bar.md) | Honesty / bar | 1184 | Two bars. Ranked leftover. Refuse list. Sequence **splits**. §8 predicted siblings; this parent observes them. |

---

## 4. Report disagreement you must not paper over

### 4.1 Does a live `lzr_sk_` work on Pay today?

| Paper | Claim |
|-------|--------|
| [10](./10-honesty-production-bar.md) §4.1 | Accident of forwarding: a valid One key **might** work **if** `/me` and `authz/check` accept it. Unproven; no test. |
| [02](./02-machine-keys-m2m.md) [05](./05-identity-authz-tenancy.md) | **No as a product.** `CheckMemberAsync` body is `{ relation, object }` — omits `user_id`. Live One **400s** API keys (`user_id is required when authenticating with an API key`). Writer overlay never reached. |

**Parent stance:** 02/05 win. The kernel door is not “document that Bearer is forwarded.” It is “MemberGate must send a subject One will accept for keys” **or** “Pay must not claim M2M.” Do not mint a second key table in Pay.

### 4.2 Is occupancy still P0?

| Paper | Claim |
|-------|--------|
| 019 parent | P0 count-then-insert. |
| [07](./07-money-remaining.md) [10](./10-honesty-production-bar.md) | **Closed as written** (`SemaphoreSlim` + parent `FOR UPDATE`, named Postgres test). Leftover: lazy TTL, fulfill re-check without parent lock, client `slot_key`, 014 second session. |

**Parent stance:** do not re-open 019 occupancy as the 020 P0. The money P0 *now* is **late PSP pay after TTL expire with no refund**, plus **014 persist-after-PSP** on CHIP/Billplz/Xendit/Razorpay. Read [07](./07-money-remaining.md).

### 4.3 002 YAML vs live

[07](./07-money-remaining.md) and [10](./10-honesty-production-bar.md) agree: YAML 001–080 `resolved` is **too clean**. Issue **bodies** still say Status: open. 014 source still comments “PSP HTTP then persist.” 015 mismatch 400 still does not consume the event (tests assert that — fail-closed, not a forgotten patch). 029 per-org secret **exists** and the process god-key **remains**.

### 4.4 `/ready`

[06](./06-host-production.md): `CanConnectAsync` result is **discarded** — Postgres down can still 200. 076 added a test that the door exists. That is not a production probe.

### 4.5 Who mints checkouts

[01](./01-public-http-api.md) hatch is `POST /v1/checkouts` + poll. [08](./08-headless-vs-spa.md): the merchant SPA **never** calls that door; it mints payment-links. Both are true. Dogfood of the kernel mint door is missing even first-party.

### 4.6 One HMAC live wire

019 07 vs 10 split is **narrowed**: Pay verifier accepts product One split headers; a named test mints that dialect. [04](./04-inbound-webhooks.md) / [10](./10-honesty-production-bar.md): **no captured** dispatcher POST from sibling One is in this repo. Envelope field names still guessed. Pause is not production-proven.

Everything else in §5 is consistent across the ten papers.

---

## 5. Ranked leftover (parent list — evidence lives in the reports)

### Kernel — other apps cannot integrate (020 P0)

1. **M2M Bearer is not a product.** One mints `lzr_sk_`. Pay 400s it. No hermetic `lzr_sk_` test. [02](./02-machine-keys-m2m.md), [05](./05-identity-authz-tenancy.md). **Solve (analysis):** send `user_id` on `authz/check` in the way One requires for keys (not the key id as subject — 012); map `/me` for keys to writer; scopes explicit (`authz:check`, never `*` / empty). Refuse Pay-local `sk_live_`.

2. **No Plane C.** No `payment.completed`, no merchant `whsec_`, no outbox. Second app polls member-gated GETs with a human JWT. [03](./03-outbound-webhooks.md). **Solve:** one signed POST after fulfill, One dialect Pay already verifies inbound, per-org endpoint + SecretBox, same TX as fulfill or an outbox row. Refuse Hub `OutboundWebhookDispatcherJob`.

3. **No second-app sample on Pay.** `examples/` is Hub `sk_` cashier at museum 8080. Root README hides Pay. [09](./09-spec-docs-sample.md). **Solve:** `examples/pay-node` with plain `fetch`, after 1–2 exist. Mark Hub sample museum. Refuse npm `@lazuar/one-client` as a gate.

### First-party go-live (013 Bar B leftover)

4. **`/ready` ignores `CanConnectAsync`.** [06](./06-host-production.md).

5. **014 PSP HTTP then persist** still in `PublicPayEndpoints` for CHIP/Billplz/Xendit/Razorpay. Stripe has an idempotency key. YAML resolved. [07](./07-money-remaining.md), [10](./10-honesty-production-bar.md).

6. **Production config fail-open:** empty connection string / `One:BaseUrl` silent-laptop; only CORS throws. Compose `--profile apps` is Development, empty WrapKey, no volume. [06](./06-host-production.md).

7. **One pause uncaptured on the live wire; Pay does not register the URL.** [04](./04-inbound-webhooks.md).

8. **No refund / expire-at-processor.** TTL can drop an `open` child whose buyer later pays at CHIP. Money sits at the processor. [07](./07-money-remaining.md).

### P1 — honesty / DX / leftover 002

- Checkout mint still ignores catalog amount; payment-link mint 400s drift ([07](./07-money-remaining.md)).
- Amount mismatch 400 does not consume event (keep fail-closed; do not claim ingested) ([07](./07-money-remaining.md), [04](./04-inbound-webhooks.md)).
- Writer is `/me` role overlay, not `authz/check admin` ([05](./05-identity-authz-tenancy.md)).
- Whoami 400/429 mapped to 503; MemberGate already passes them through ([05](./05-identity-authz-tenancy.md)).
- No `pay_url` on 201 checkout ([08](./08-headless-vs-spa.md), [01](./01-public-http-api.md)).
- Payment-links have no idempotency; lists dump the org ([01](./01-public-http-api.md)).
- Vitest not in CI; GHCR bake Hub-only ([06](./06-host-production.md), [09](./09-spec-docs-sample.md)).
- Merchant laptop `VITE_*` bake-ins ([08](./08-headless-vs-spa.md)).
- Process `Pay:OneWebhookSecret` remains a one-shop god-key ([04](./04-inbound-webhooks.md), [02](./02-machine-keys-m2m.md)).
- `slot_key` still client-supplied; 20/min is a grief brake ([07](./07-money-remaining.md)).
- Spec comments still lie in places (checkout list “mixes children”); Problem schema not in tsp ([09](./09-spec-docs-sample.md)).

### P2 — after the door is boring

- Invite chrome / VIEWER ([05](./05-identity-authz-tenancy.md), [10](./10-honesty-production-bar.md)).
- Pagination, filters, refund API, subscriptions (missing feats, not cashier bugs).
- OTel / metrics ([06](./06-host-production.md)).
- Issue markdown bodies still `Status: open`.

---

## 6. Refuse (do not staff these as “how we become production-ready”)

From [10](./10-honesty-production-bar.md) §7 and the standing law, restated so this parent cannot be quoted as permission:

- MediatR, `IEnumerable<IHostedRail>`, Hub `@repo/api-types-ts`, `Modules/One` copy, project reference into `apps/lazuar-api`.
- Zitadel PAT / OpenFGA admin / masterkey / Pay-local user table / god-key in Pay `.env` that speaks for every merchant.
- Homemade Pay `sk_test_` / `sk_live_` (prefix collision with Stripe; Hub museum).
- SST / LHDN / e-invoice on the pay path. Receipt ≠ tax invoice.
- Escrow, factory, registrar, four processes before a second caller.
- Retarget root Hub compose onto 8081; allow ops :3003 / portal :3004 on Pay CORS.
- Dual-write Hub outbound dispatcher; CloudEvents cathedral; Standard Webhooks **library** when One dialect is already inbound.
- Waiting on npm publish of `@lazuar/one-client`.
- Inventing `/v2` to look finished.

---

## 7. Sequence — two first tickets, do not mix them

**Other apps (020 kernel):**

1. MemberGate + One `lzr_sk_` (02/05) — one hermetic test that a key can `POST /v1/checkouts`.
2. Return `pay_url` on mint (08/01) so the stranger does not reverse-engineer checkout origin.
3. One signed `payment.completed` after fulfill (03).
4. `examples/pay-node` + README that does not point at Hub (09). Grow tsp when Map* lands (honesty already green).

**First-party go-live (013 Bar B):**

1. `/ready` uses the bool (06).
2. Production empty CS / WrapKey / One BaseUrl fail-boot; compose volume + non-Development profile (06).
3. Persist-before-PSP or processor idempotency on non-Stripe rails (07/014).
4. Capture a real One `tenant.suspended` against 8081; ops runbook for URL + per-org `whsec_` (04).
5. Then refund/expire-at-processor if you will take real CHIP money with a 30-minute reservation (07).

Do not wait for refunds to ship M2M. Do not wait for M2M to fix `/ready`.

---

## 8. Honest sentences we may say

**May:**

- Focused Pay is a hosted cashier for One workspaces: staff via One OIDC, buyers without One accounts, six hosted rails, Official Receipt + two-line journal.
- Occupancy on a capped pay link is serialized; Test Plane B is signed in Production; Stripe unpaid `completed` is ignored; unique charge-per-checkout exists.
- `/v1` is the only product door. Merchant and checkout are clients of it. Hub on 8080 is a different product.
- One already mints `lzr_sk_`. Pay does not yet accept it as a merchant credential.

**Must not:**

- Pay is production-ready.
- Pay is a Stripe-shaped / kernel API other apps can copy-paste this afternoon.
- We have merchant webhooks (`payment.completed`).
- We have API keys (Pay-minted or working M2M).
- 002 001–080 resolved means the money files have no holes.
- Official Receipt is an e-invoice.
- Test is always available (only when the host lists it).
- `examples/` is how you integrate **this** Pay.

The README sentence that belongs on this SHA is in [10](./10-honesty-production-bar.md) §10. Do not paraphrase it here and stop.

---

## 9. How to read the rest

1. Read [10](./10-honesty-production-bar.md) for the bar, refuse, and two sequences.
2. Read [02](./02-machine-keys-m2m.md) and [03](./03-outbound-webhooks.md) if the question is “why can’t another app integrate?”
3. Read [07](./07-money-remaining.md) and [06](./06-host-production.md) if the question is “can we take first-party money in production?”
4. Read [01](./01-public-http-api.md), [08](./08-headless-vs-spa.md), [09](./09-spec-docs-sample.md) if the question is “is `/v1` clean?”
5. Read [04](./04-inbound-webhooks.md) and [05](./05-identity-authz-tenancy.md) before mixing the three webhook planes or inventing a Pay user table.

Uncondensed means the evidence stays in those files. This parent is a map and a verdict, not a condensation of 12,700 lines into a slide.
