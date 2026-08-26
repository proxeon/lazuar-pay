---
number: "224"
id: B04-P23
severity: P2
status: resolved
resolved_branch: fix/224-m2m-amount-decimal
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 224 — B04-P23 — M2M amount is `double` on the wire

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/224-m2m-amount-decimal`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P23 — P2 — M2M amount is `double` on the wire

**Where.** `IntegrationEndpoints.cs:45` `(decimal)body.Amount`; response `(double)result.Amount` (`154`). NSwag DTO `CreateIntegrationCheckoutRequestDto.Amount` is `double`.

**What.** Binary floating point on money at the HTTP edge. Internal command is `decimal`. Typical MYR 2-dp values survive; 3-dp / repeating fractions do not.

## Evaluation (current tree, 2026-08-18)

### What the bug is
M2M `POST /integrations/payments/checkouts` binds `CreateIntegrationCheckoutRequestDto.Amount` as a CLR `double` (NSwag / TypeSpec `float64`). The endpoint immediately casts `(decimal)body.Amount` into `CreateIntegrationCheckoutCommand.Amount`. The response DTO writes `(double)result.Amount`. IEEE-754 cannot represent many decimal money values (0.1, 10.015, repeating sen). Typical MYR two-decimal amounts that are also exact binary fractions (2.00, 10.25, 50.00) survive the round-trip. Values with a 3rd decimal, or JSON numbers that were already rounded by the client’s `number` type, can land a sen off before `CheckoutAmountRules` and `GatewayCommon.ToMinorUnits` ever run. Internal command, session row, and cashier are `decimal`. The wire is the leak. TypeSpec regen is forbidden for this fix, so the generated DTO will stay `double` until a later contract wave.

### Still present?
**STILL BROKEN**

TypeSpec still declares IEEE floats:

```7:10:packages/api-spec/modules/payments/models.tsp
model CreateIntegrationCheckoutRequestDto {
  /** Positive amount in major currency units (e.g. 10.00 MYR). */
  amount: float64;
```

```42:42:packages/api-spec/modules/payments/models.tsp
  amount: float64;
```

Generated C# still uses `double`:

```7879:7879:packages/api-types-dotnet/Lazuar.ApiContracts.cs
        public double Amount { get; set; } = default!;
```

Runtime still casts at the HTTP edge:

```43:45:apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs
                var result = await mediator.Send(new CreateIntegrationCheckoutCommand(
                    OrganizationId: ctx.TenantId,
                    Amount: (decimal)body.Amount,
```

```146:153:apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs
    private static IntegrationCheckoutResponseDto ToResponse(IntegrationCheckoutResult result) =>
        new()
        {
            // ...
            Amount = (double)result.Amount,
```

Command and session remain honest (`CreateIntegrationCheckoutCommand.Amount` is `decimal`; `IntegrationCheckoutSession.Amount` is `decimal`). `CheckoutAmountRules.ValidateAmountAndCurrency` checks `> 0` and MYR min 2.00 against the already-cast decimal — it does not reject values that are not 2-dp. I found no custom `JsonConverter` that parses the amount token as `decimal`. Ledger list lines have the same `double` DTO problem (`BillingQueryService.cs:116`) but that is out of this issue’s scope.

### Related files
- `apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs` — the only production cast in/out.
- `packages/api-spec/modules/payments/models.tsp` — `float64` (do not regen from here in this fix).
- `packages/api-types-dotnet/Lazuar.ApiContracts.cs` — generated `double Amount`.
- `packages/api-types-ts/src/index.ts` — TS client `number` for the same field.
- `apps/lazuar-api/Modules/Payments/Contracts/Commands/CreateIntegrationCheckoutCommand.cs` — internal `decimal`.
- `apps/lazuar-api/Modules/Payments/Application/Services/CheckoutAmountRules.cs` — min/ISO checks after the cast.
- `apps/lazuar-api/Modules/Payments/Domain/Aggregates/IntegrationCheckoutSession.cs` — stores `decimal`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/CreateIntegrationCheckoutTests.cs` — uses the command (`decimal`), not the HTTP DTO.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/IntegrationCheckoutEndpointsAuthorizationTests.cs` — auth only.

### Tests
- Existing tests: `CreateIntegrationCheckoutTests` (`Create_AmountBelowMin_AmountBelowMinimum`, `Create` happy path with `50m`) go through `CreateIntegrationCheckoutCommandHandler` with a `decimal` amount. Authorization tests do not POST a JSON body with a repeating fraction. I found no Payments test that JSON-deserializes `CreateIntegrationCheckoutRequestDto`.
- Whether any test would fail if the bug is still there: **no**.
- What a first regression test should assert: HTTP (or `JsonSerializer`) of `{"amount":10.015,"currency":"MYR",...}` either rejects as `amount_invalid` or stores `10.02` / `10.01` under an explicit rounding policy — not `10.014999...`. Also assert `10.00` and `2.00` still round-trip. Prefer parsing the raw JSON number as `decimal` without changing TypeSpec.

### Reproduction today
Arrange: machine API key with `payments.checkouts:write`. Act: `POST /integrations/payments/checkouts` with JSON `"amount": 10.015` (or `0.1` repeated). Read `payments.IntegrationCheckoutSessions.Amount` and the JSON `amount` on the 200. Assert the stored decimal is not necessarily 10.015; a value such as `10.014999999999999` (or the nearest double) can be what `(decimal)double` produced. Repeat with `50.00` — that one matches.

### Blast radius
M2M integrators only (Commerce hop-2 does not use this DTO). MYR 2-dp catalog prices almost always survive; ad-hoc / FX / 3-dp callers can mint a bill one sen off vs what they intended, then CHIP/Stripe minor-units lock that error in. Not PII. Not a webhook hole. Frequency: low for MY launch if everyone sends `xx.xx`; high if a JS client does `price * qty` in `number` before POST. Ops “Net Cash” (230) is a different `double` DTO.

### Suggested fix
Do **not** regen TypeSpec. Smallest fix at the Minimal endpoint: read `amount` from `JsonElement` / a tiny local request record with `decimal` (System.Text.Json supports decimal) and ignore the generated `double` property; or add a `JsonConverter` on a hand-written bind model used only by this route. Reject amounts whose exact decimal representation has more than 2 dp for MYR (or that do not survive `decimal ↔ minor units` round-trip). Keep `CreateIntegrationCheckoutCommand.Amount` as `decimal`. LP-059 / Stripe Billing / Wave 5 are unrelated.

### Evaluation notes
Duplicates: Billing `LedgerLineDto.Amount` is also `(double)` (`BillingQueryService.cs:116`) — same class of wire leak, different issue. Severity still **P2** (typical MYR 2-dp is fine; this is an edge contract smell). Not blocked. Residual after 172 (TypeSpec honesty comment on `PaymentWebhookPayloadDto`): that fix explicitly said “do not regenerate packages from this model”; same rail applies here.


