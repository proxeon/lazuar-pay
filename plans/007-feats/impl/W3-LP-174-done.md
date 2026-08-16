# W3-LP-174 — done

A magic-link buyer can list eligible recurring products and schedule a plan change. Same guards as admin plus token ownership. `PAST_DUE` and `CancelAtPeriodEnd` are rejected. Preview `amount_due_now` is always 0. Portal UI: picker, “No charge today. Starts {date}.”, revert pending.

## Files

- `GET /public/commerce/{slug}/portal/plans`
- `POST /public/commerce/{slug}/portal/change-plan`
- `ChangePortalPlanCommandHandler` (shared policy with admin)
- `PortalPlanChange` + portal page picker

## Tests run

- `ChangePlanCommandHandlerTests` + `PlanChangePolicyTests` (preview 0), Commerce filter **355 passed**

Not committed. Not pushed.

Tracker `LP-174` can move **N → Y**.
