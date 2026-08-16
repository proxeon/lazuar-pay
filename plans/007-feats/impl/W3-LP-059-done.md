# W3-LP-059 — done

Catalog changes (plan, seats) are **next-renewal-only**. `PlanChangePolicy.Preview` hard-codes `amount_due_now=0` and `policy=next_renewal`. `prorate=true` and `apply=immediate` return 400. Change-plan / set-quantity handlers never call `ExecuteOffSessionCharge`. UI copy: no charge today.

This is not unused-time credit.

## Files

- `PlanChangePolicy.cs`
- Preview DTO on change-plan and quantity responses
- Ops / portal “No charge today” copy

## Tests run

- `PlanChangePolicyTests`, `ChangePlanCommandHandlerTests` (prorate 400), Commerce filter **355 passed**

Not committed. Not pushed.

Tracker `LP-059` can move **N → Y** with note **next-renewal-only**.
