---
number: "059"
id: B03-C14
severity: P1
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 059 — B03-C14 — AUTO_CHARGE / Gross ignore `HasOpenDispute`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C14 — P1 — AUTO_CHARGE / Gross ignore `HasOpenDispute`

**Evidence.** `cannotCharge` (`PastDueDunningProcessor.cs` 114–119) does not read `HasOpenDispute`. The flag **is** written now (`CommerceGatewayDisputeCreatedHandler` 82, 124) — 008’s “dead boolean” is half-fixed. Dunning will still open attempt 2–4 on a card that is in chargeback.

**Blast.** Another PI on a disputed card. Scheme risk. Out of ledger scope but this is the dunning trigger.

**Fix direction.** Treat `HasOpenDispute` as `cannotCharge` (and skip billing attempt 1 in report 02).

---

