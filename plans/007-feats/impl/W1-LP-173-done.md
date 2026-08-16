# W1-LP-173 — done

Update payment is first-class on the magic-link portal. `POST …/update-payment` accepts `ACTIVE` (and existing PAST_DUE/SUSPENDED). ACTIVE reminder-only → **400 REMINDER_ONLY** (no full Billplz bill). ACTIVE vaulted → RM 1 verification + `update_payment=1`; completed webhook updates vault ids only (`NextBillingDate` unchanged). Portal CTA on ACTIVE/PAST_DUE (hidden for reminder-only ACTIVE). Success URL remains portal.

## Files

- `PublicArrearsEndpoints` + arrears DTO `is_reminder_only`
- `GatewayPaymentCompletedIntegrationEventHandler.Subscription`
- Portal dashboard CTA + update-payment page copy
- `CommerceHonestyDtoTests` portal map

## Tests run

- `CommerceHonestyDtoTests` — **passed**
- Portal `tsc` — clean

Not committed. Not pushed.

Tracker `LP-173` **P → Y**.
