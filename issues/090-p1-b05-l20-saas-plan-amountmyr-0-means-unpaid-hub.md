---
number: "090"
id: B05-L20
severity: P1
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 090 — B05-L20 — `Saas:Plan:AmountMyr = 0` means unpaid Hub

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L20 — P1 — `Saas:Plan:AmountMyr = 0` means unpaid Hub

See §6. Checkout 400. GET returns `UNPAID`. Public page “free today” is true. Not a money-corruption bug. Do not sell plane S against this repo’s default config. Tests lock the throw (`CreateSaasCheckoutCommandHandlerTests.Handle_AmountNotConfigured_Throws`) and the unpaid view.

---

