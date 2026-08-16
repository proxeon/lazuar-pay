# W3-LP-076 — done

Hard vs soft decline is a static table plus an AUTO_CHARGE gate. Stripe PI fail and off-session exceptions now carry `decline_code`. `ChargeAttemptLog.DeclineClass` is `hard` or `soft`. A cycle that already has a hard FAILED attempt skips later AUTO_CHARGE (offset consumed, EMAIL still sends).

## Files

- `DeclineClassifier.cs`, `ChargeAttemptLog.DeclineClass` / `MarkSkipped`
- Stripe `MapPaymentIntentPaymentFailed` + `OffSessionDeclinedException`
- `ExecuteOffSessionChargeIntegrationEventHandler` passes Stripe decline code
- `GatewayPaymentFailedIntegrationEventHandler` classifies
- `PastDueDunningProcessor` AUTO_CHARGE hard skip
- Migration `20260820130000_AddChargeAttemptDeclineClass`

## Tests run

- `DeclineClassifierTests`, Stripe map includes `decline_code`, AUTO_CHARGE after hard does not charge

Not committed. Not pushed.

Tracker `LP-076` **N → Y**.
