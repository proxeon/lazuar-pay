---
number: "039"
id: PAY-MERCH-005
severity: P1
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 039 — Overview counts Test as “On file”

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B5 (also `04-processors-vault-test.md` B8)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Host `GET /gateways` includes Test with `configured: true` (`TestGatewayJson`). Overview `processors.filter((p) => p.configured)` then “On file {n}”. A workspace with **zero** pasted keys shows **On file 1** (Test) and a “Paste keys” link. Processor cards are honest (Test = Ready, others Empty).

## Related files

- `apps/lazuar-pay-merchant/src/pages/org/OverviewPage.tsx` **23–40** (approx.).
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` **240–250** — Test always `configured: true`.
- `apps/lazuar-pay-merchant/src/locks.test.ts` — overview greps `/gateways` and `On file`, not “exclude test”.

## Reproduction

New workspace, no keys. Overview “On file 1”.

## Blast radius

Ada thinks a rail is vaulted. Local dogfood only (Production host omits Test — 042).

## Suggested fix

Count `configured && provider !== 'test'`, or list names the way the cards do. Say “Test is always available” in copy that already exists on Processor.

## Tests

- Missing: lock Overview does not count Test. Host Test JSON can stay `configured: true` for the picker.

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B5
- `plans/019-evals/04-processors-vault-test.md` §B8
