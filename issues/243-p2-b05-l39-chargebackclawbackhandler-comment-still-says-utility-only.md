---
number: "243"
id: B05-L39
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 243 — B05-L39 — `ChargebackClawbackHandler` comment still says “utility only”

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L39 — P2 — `ChargebackClawbackHandler` comment still says “utility only”

The SaaS `PAST_DUE` branch has been there since W1-LP-004. Comment at `:18-25` never mentions it. Next editor will miss the branch.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`ChargebackClawbackHandler` has two live branches: utility top-up claw + ledger reverse, and Hub SaaS `MarkPastDue`. The class XML still describes only the first (“Scope (A.6 / C.1 MVP): utility clawback only”) and says it does not reverse merchant GMV (true) without mentioning that `type == platform_saas_fee` is handled and returned before the utility check. Billing README §5 still says dispute consume is “utility chargeback”. The next editor who “knows” this file is utility-only will miss the SaaS status flip — or will add GMV logic in the wrong handler (`GatewayDisputeLostHandler` is the GMV lost path). This is a comment/README honesty bug; the code path itself is **240**.

### Still present?
**DOCS / HONESTY ONLY**

```18:25:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs
/// <summary>
/// Consumes gateway dispute events and claws back credits granted for a disputed utility-credit
/// top-up, and reverses the matching SYSTEM_CREDIT_TOPUP ledger entry.
///
/// Scope (A.6 / C.1 MVP): utility clawback only.
/// - Handles only metadata.type == "utility_credit_topup" (platform credit purchases).
/// - Does NOT suspend commerce subscriptions or reverse merchant GMV ledger entries.
/// </summary>
```

The SaaS branch sits immediately after the type read (`ChargebackClawbackHandler.cs:50-54`) and has been there since W1-LP-004. `GatewayDisputeLostHandler.cs:14-17` is more honest (“Utility / Hub disputes stay on ChargebackClawbackHandler”). README §5:37: “`GatewayDisputeCreatedIntegrationEvent` (utility chargeback)”. Tests name the SaaS behaviour (`PlatformSaasFeeDispute_MarksPastDue_DoesNotClawCredits`) but the file header still lies.

### Related files
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs) — stale XML vs SaaS branch.
- [`apps/lazuar-api/Modules/Billing/README.md`](apps/lazuar-api/Modules/Billing/README.md) — §5 dispute line.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayDisputeLostHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayDisputeLostHandler.cs) — the comment that already names Hub.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ChargebackClawbackHandlerTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ChargebackClawbackHandlerTests.cs) — documents the branch better than the class comment.
- [`issues/240-p2-b05-l36-hub-saas-dispute-does-not-reverse-systemsaasfee.md`](issues/240-p2-b05-l36-hub-saas-dispute-does-not-reverse-systemsaasfee.md) — the actual money gap on that branch.

### Tests
- Existing: `ChargebackClawbackHandlerTests.PlatformSaasFeeDispute_MarksPastDue_DoesNotClawCredits`; `NonUtility_IsNoOp`; utility claw tests. No architecture test that XML matches branches.
- No test fails because of a stale comment. The SaaS test would fail if the branch were deleted, not if the comment were fixed.
- First “regression” is a review check: XML lists utility **and** Hub `PAST_DUE`, and states GMV is `GatewayDisputeLostHandler`. Do not add a unit test that parses comments.

### Reproduction today
Open `ChargebackClawbackHandler.cs` at the class summary, then scroll to `if (type == PlatformCheckoutTypes.PlatformSaasFee)`. The summary does not mention that `if`. README §5 same lie. Send a `platform_saas_fee` dispute: `PAST_DUE` still happens (240).

### Blast radius
Editors only. Risk is a wrong GMV change in this file, or an editor “cleaning up” the SaaS branch as dead. No money movement from the comment itself. Still P2 as filed; practically P3 honesty.

### Suggested fix
Rewrite the summary to: utility claw + reverse; Hub SaaS → `PAST_DUE` only (journal not reversed — 240); GMV ignored here (`GatewayDisputeLostHandler` on lost). One sentence in Billing README §5. Do not change behaviour in this issue. Do not emit `subscription.updated`. No TypeSpec. No Wave 5.

### Evaluation notes
Do not “fix” 240 under this number. 009 claw-retry / pack-table issues (**009 / 088**) were on the utility branch and are out of scope. Still P2 on the ticket; implementer should treat as docs. Not blocked. 161–200 did not refresh this XML.

