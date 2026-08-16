# W3-LP-161 — done

MRR is the monthly equivalent of **committed ACTIVE** rows using the subscription snapshot (`UnitAmount × Quantity`, yearly ÷ 12). Collection-paused and `PAST_DUE` are excluded. Catalog edits do not move MRR until a successful period payment refreshes the snapshot. Dashboard shows MRR + ARR with the glossary line.

## Files

- `Subscription.UnitAmount` snapshot + `RefreshSnapshot` on paid renewal
- `CommerceMrr.MonthlyEquivalent`, `GetStatsAsync` + `arr`
- `DashboardPage` MRR / ARR cards + tooltip

## Tests run

- `CommerceMrrTests`, Commerce filter **355 passed**

Not committed. Not pushed.

Tracker `LP-161` can move **P → Y**.
