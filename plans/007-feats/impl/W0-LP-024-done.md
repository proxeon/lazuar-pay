# W0-LP-024 — done

Success page unlocks only after commerce session `COMPLETED`. Zero-amount initiate now returns the same `…/success?sub_id={session.Id}` poller handle as the paid path. Status poll maps `EXPIRED` honestly; `OPEN` stays `PENDING`. Token is still never minted. Portal treats **only** `COMPLETED` as paid.

## Files changed

- `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` — zero-amount `CheckoutResultDto.Url` is the success poller URL
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Checkout.cs` — `EXPIRED` mapping; token still null
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` — navigate to initiate `url`; no bare `/success`
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` — removed client-guessed zero-amount success push
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutSuccessView.tsx` — `COMPLETED` only; `EXPIRED` UI; 20×3s poll + Check again; factual copy
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GetCheckoutStatusTests.cs` — poller mapping contract
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs` — zero-amount URL + webhook writer cases
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ProcessGatewayWebhookCommandHandlerTests.cs` — unverified parse publishes no money event
- `apps/lazuar-api/tests/Lazuar.IntegrationTests/CommerceQueryServiceTests.cs` — SQL `GetCheckoutStatusAsync` missing/wrong-org/`OPEN`/`COMPLETED`/`EXPIRED`

## Tests run

- `Lazuar.ModuleTests` filter `GetCheckoutStatusTests|CommerceProductCompletenessTests|Handle_UnverifiedParse_DoesNotPublishGatewayPaymentCompleted` — 18 passed
- `Lazuar.IntegrationTests` filter `CommerceQueryServiceTests` — 2 passed

Not committed. Not pushed.
