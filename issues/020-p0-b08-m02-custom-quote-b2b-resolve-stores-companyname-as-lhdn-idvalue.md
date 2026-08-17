---
number: "020"
id: B08-M02
severity: P0
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 020 — B08-M02 — Custom-quote B2B resolve stores CompanyName as LHDN IdValue

- **Severity:** P0
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M02 — P0 — Custom-quote B2B resolve stores CompanyName as LHDN IdValue

**Where:** `InitiateCheckoutCommandHandler.cs` 134–142 vs `ResolveClientProfileCommand.cs` 7–17 vs `LhdnBuyerMapper.cs` 51–61.

**What:** Positional argument 7 is `IdValue`, not `CompanyName`. Quote pay with `IsB2bRequired` writes `"Acme Sdn Bhd"` into `ClientProfileEntity.IdValue`. Mapper treats that as BRN (IdType is null → default BRN) and will submit it if TIN is also present.

**Why it matters:** Lazuar is Compliance CaaS. A MyInvois buyer identification number that is a company **name** is a rejected or worse, accepted-wrong, tax document. The session’s `ClientProfileId` is not updated from Resolve’s return, so this always mutates the quote-time profile when emails match.

**Tests that should have caught it and did not:** `CheckoutB2bIdentityTests.InitiateCheckout_CustomSession_CopiesIsB2bRequiredIntoMetadata`; `CreateCustomCheckoutAndInitiateSessionTests.InitiateCheckout_SessionId_StampsB2bMetadataAndRequiresTin`. Both fire Resolve. Neither reads `IdValue` / `CompanyName` on the command.

Product hop-1 is fine (named args, `CheckoutB2bIdentityTests` 49–55).

---

