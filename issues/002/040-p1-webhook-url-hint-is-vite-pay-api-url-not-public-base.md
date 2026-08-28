---
number: "040"
id: PAY-MERCH-006
severity: P1
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 040 — Webhook URL hint is `VITE_PAY_API_URL`, not `Pay:PublicBaseUrl`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B6
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Processor Edit dialog prints `{payApi}/v1/webhooks/{provider}/{orgId}` with `payApi` default `http://localhost:8081`. Billplz **start** registers `{Pay:PublicBaseUrl}/v1/webhooks/billplz/{orgId}?checkout_id=…` and 400s if the base is not public https. 018 added a Billplz sentence but still prints the loopback origin and still omits `checkout_id`. CHIP copy does **not** warn.

Staff who paste the hint into CHIP/Billplz dashboards configure a URL the PSP cannot reach.

## Related files

- `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx` **304–315**.
- `apps/lazuar-pay-merchant/src/lib/payApi.ts` **1**.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs` — public https callback.

## Reproduction

Open Edit CHIP. Copy the `<code>` URL. It is localhost:8081. CHIP cannot POST it from the internet.

## Blast radius

Every rail that needs a dashboard callback. Billplz dogfood without a tunnel already 400s at start; the printed URL makes it worse.

## Suggested fix

Do not pretend this SPA knows `Pay:PublicBaseUrl`. Either (a) host GET `/gateways` returns `webhook_url_hint` built from PublicBaseUrl, or (b) print only the **path** `/v1/webhooks/{provider}/{orgId}` and the Billplz sentence for **every** rail that needs a public callback. Never print `http://localhost:8081` as if it were the dashboard value.

## Tests

- Existing locks grep webhook URL shape loosely.
- Missing: lock that the hint is not `localhost:8081` in production builds, or that it comes from host JSON.

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B6
