---
number: "017"
id: PAY-SEC-001
severity: P1
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 017 — Development WrapKey: docs lie, first vault PUT is 500

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B5 (also `04-processors-vault-test.md` G8)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`SecretBox.LoadKey` throws `Pay:WrapKey is required` unless environment is `"Testing"`. Git SHA256 fallback `"lazuar-pay-dev-wrap-key"` is **Testing-only** (016 P1-8 FIXED). `.env.example` still says “Dev has a fallback; production must set this.” README says required outside Testing (matches C#).

`appsettings.json` / `appsettings.Development.json` have no WrapKey. `Program.cs` never `LoadKey`s at boot. `task pay:dev` is Development. Health works. First `PUT /v1/orgs/{id}/gateway` calls `Protect` → unhandled `InvalidOperationException` → **500**. No `ValidateOnStart`. Laptop `.env` in this repo **does** set `Pay__WrapKey` — operators who follow `.env.example` only do not.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs` **36–56** — throw unless Testing; SHA256 only in Testing.
- `apps/lazuar-pay/.env.example` **8–9** — “Dev has a fallback”.
- `apps/lazuar-pay/README.md` **67** — required outside Testing (honest).
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json` — CheckoutBaseUrl only.
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` **98–99** — `Protect` on PUT.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Secrets/SecretBoxTests.cs` — Production-missing throws; Testing-empty hashes. **No Development test.**

## Reproduction

Development host without `Pay__WrapKey`. Health 200. PUT gateway 500.

## Blast radius

Local BYOK dogfood if `.env` is unused. Production missing WrapKey fails the same way on first PUT (fail-closed is correct; fail-at-boot would be better). Wrapping with the Testing default and then running Development against the same DB cannot Unprotect.

## Suggested fix

Pick one story and make files agree. Recommended: keep fail-closed outside Testing; **require** WrapKey in Development (`dotnet user-secrets`, or gitignored local appsettings). Delete “Dev has a fallback” from `.env.example`. Map PUT throw to 503 problem JSON, not 500. Optionally `ValidateOnStart` so `task pay:dev` dies before the first merchant click. Do **not** ship the SHA256 string as a Development default into a shared `lazuar_pay`.

## Tests

- Missing: Development-missing WrapKey → PUT 503 (or host fails boot). `.env.example` must not claim a fallback the C# does not have (doc test or just fix the file).

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B5
- `plans/019-evals/04-processors-vault-test.md` §G8
