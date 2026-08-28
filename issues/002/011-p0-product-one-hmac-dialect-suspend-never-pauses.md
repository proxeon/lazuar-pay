---
number: "011"
id: PAY-ONE-001
severity: P0
status: resolved
source: plans/019-evals/07-identity-authz-cors.md
head: "9f04ad58"
---

# 011 — Product One HMAC dialect ≠ Pay verifier; suspend never pauses charges

- **Severity:** P0
- **Status:** open
- **Source:** `plans/019-evals/07-identity-authz-cors.md` B1 (parent `00-evaluation.md` §4 disagrees with `10-honesty-bugs-gaps.md` on 016 P0-4)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Pay verifies Standard Webhooks packaging: one header `X-Lazuar-Signature: t=<unix>,v1=<hex>` over `{unix}.{body}`. Product One (`lazuar-one`) sends `X-Lazuar-Signature: v1=<hex>` and a **separate** `X-Lazuar-Timestamp`. The HMAC algorithm (UTF-8 `whsec_…`, lowercase hex, 300s skew, `{unix}.{body}`) is the same. The **packaging is not**.

Pay `TryParseHeader` requires both `t=` and `v1=` **inside** the signature header. One’s `v1=<hex>` has no `t=`. Verify fails → **401 Invalid HMAC** → `OrgSettings.ChargesPaused` unchanged → public `POST /v1/pay/{token}/start` does not 403.

Staff belt: One membership 403s `"Tenant is suspended."` Pay maps that to `"Not a member of this org"`. **Buyers never send Bearer.** The buyer belt is only `ChargesPaused`. It is unwired on the live product One wire.

`OneWebhookTests.Sign` mints Pay’s dialect, so CI stays green. Body-only uppercase hex (old Hub) is correctly 401.

`10-honesty-bugs-gaps.md` called 016’s Hub dialect **FIXED** in Pay source. That is true vs Hub `OutboundWebhookSignature` (`t=,v1=` in one header). It is **not** a proof against `lazuar-one`. Parent 00 §4: treat pause as unproven until a live One envelope is replayed.

File comment on `OneWebhookSignature` (“Judgment stolen from One's signer”) is false against product One (`FormatHeader` = `"v1=" + hex`).

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs` **7–43**, **45–79** — combined `t=,v1=` parser.
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs` **24–34**, **53–70** — verify then `tenant.suspended` / `tenant.reactivated`.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs` **13–16** — `Sign` mints `t={unix},v1={hex}`.
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` **32–35** — pause throw (never reached if flag stays false).
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **214–216** — start 403 when paused.
- Sibling (do not copy as a project reference): `lazuar-one/.../WebhookSigning.cs`, `WebhookDispatcher.cs`.
- Museum contrast: `apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs` — **matches Pay**, is **not** the IdP Pay’s README points at.

## Reproduction

Replay a real `lazuar-one` `tenant.suspended` POST (headers `X-Lazuar-Signature: v1=…`, `X-Lazuar-Timestamp`, body `{ id, type, tenant_id, … }`) against `http://localhost:8081/v1/one/webhooks` with `Pay:OneWebhookSecret` = that endpoint’s `whsec_`. Expect today: 401. `ChargesPaused` false. Buyer start still 200.

## Blast radius

Paused shops still take buyer money. Staff mint 403s via membership, public pay does not. Dogfood of one tenant with a hand-signed Pay-dialect header will **appear** to work and hide the live miss.

## Suggested fix

Match product One:

1. Read `X-Lazuar-Signature`. Accept `v1=<hex>`. Reject raw uppercase hex of the body (keep `Body_only_uppercase_hex_is_401`).
2. Read `X-Lazuar-Timestamp` as unix seconds. Reject missing / non-integer / skew > 300.
3. HMAC `{timestamp}.{body}` with UTF-8 `Pay:OneWebhookSecret`.
4. Dual dialect (`t=,v1=` in one header) is how Hub vs One got confused. Prefer One-only unless a test names both.

Rewrite `OneWebhookTests.Sign` to set **both** headers the way the dispatcher does. Wrap `JsonDocument.Parse` in try/catch → 400 (063). Do **not** import Hub `OutboundWebhookSignature`. Do **not** project-reference `lazuar-one`. Copy the algorithm, Pay-owned.

029 (one process secret vs per-tenant `whsec_`) is a separate issue.

## Tests

- Existing: suspend/reactivate, stale timestamp, missing secret, body-only uppercase hex — all Pay dialect.
- Missing: `v1=` + `X-Lazuar-Timestamp` suspends charges. Combined `t=,v1=` either 200 (compat) or documented 401. Empty signed body 400 not 200.

## Source reports

- `plans/019-evals/07-identity-authz-cors.md` §B1 §S1
- `plans/019-evals/00-evaluation.md` §4
- `plans/019-evals/10-honesty-bugs-gaps.md` §016 P0-4 (FIXED vs Hub only)
