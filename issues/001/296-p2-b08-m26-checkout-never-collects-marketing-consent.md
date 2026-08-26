---
number: "296"
id: B08-M26
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 296 — B08-M26 — Checkout never collects marketing consent

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M26 — P2 — Checkout never collects marketing consent

**Where:** `InitiateCheckoutCommand` has no consent; Resolve default `ConsentedToMarketing = false`; entity default false (`ConsentDefaultFalse` migration).

**What:** Correct PDPA default. Combined with B08-M18, broadcasts cannot reach hop-1 buyers without a back-door write. Not a “consent forced true” regression (that 007 gap is still closed).

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Hop-1 checkout never asks for marketing consent and never sends a consent bit to CRM. `InitiateCheckoutCommand` and `PublicCheckoutRequestDto` have no `consented_to_marketing` field. Portal `CheckoutForm` “consent” copy is legal ToS/Privacy acknowledgement, not a marketing opt-in. `ResolveClientProfileCommand.ConsentedToMarketing` defaults `false` and the product-checkout call site omits it. The entity and the `ConsentDefaultFalse` migration also default false. That is the correct PDPA fail-closed. Combined with issue 288 (broadcasts only fan out to `Consented_to_marketing` rows, while preview counts every ACTIVE/PAST_DUE), a merchant who “sends a broadcast” after real checkouts reaches zero hop-1 buyers unless someone writes the flag in the database. The 007 “consent forced true” hole stays closed.

### Still present?
**STILL BROKEN**

Command has no consent parameter:

```7:30:apps/lazuar-api/Modules/Commerce/Contracts/Commands/InitiateCheckoutCommand.cs
public record InitiateCheckoutCommand(
    string TenantSlug,
    string ProductSlug,
    string Name,
    string Email,
    string? Phone,
    string? TaxId,
    string? CompanyName,
    // … address / quantity / coupon / session — no ConsentedToMarketing
```

Public DTO (`packages/api-spec/modules/commerce/models/checkout.tsp:3–34`, `packages/api-types-ts/src/index.ts:2704–2738`) has no `consented_to_marketing`. `PublicCheckoutEndpoints.cs:44–67` maps every field except consent. Product resolve omits the flag (`InitiateCheckoutCommandHandler.cs:239–249`). Resolve default:

```7:17:apps/lazuar-api/Modules/CRM/Contracts/ResolveClientProfileCommand.cs
public record ResolveClientProfileCommand(
    …
    bool ConsentedToMarketing = false,
    string? CompanyName = null
```

New rows write that default (`ResolveClientProfileCommandHandler.cs:119`). Checkout legal copy is not marketing consent (`CheckoutForm.tsx:343–357`, `messages.ts` `form.consent`). Broadcast fan-out still requires the flag (`SubscriberQueryService.cs:76–79`). Manual enroll (`CreateManualSubscriberCommandHandler.cs:54–58`) also omits consent.

### Related files
- `apps/lazuar-api/Modules/Commerce/Contracts/Commands/InitiateCheckoutCommand.cs` — hop-1 command shape.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs` — maps the public DTO.
- `packages/api-spec/modules/commerce/models/checkout.tsp` — no consent field (do not regen unless product asks).
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` — ToS/Privacy only.
- `apps/lazuar-api/Modules/CRM/Infrastructure/ResolveClientProfileCommandHandler.cs` — writes `ConsentedToMarketing` only on insert.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/SubscriberQueryService.cs` — broadcast recipient filter.
- `issues/288-p2-b08-m18-broadcast-counts-lie-recordsent-is-pre-provider-consent-is-unrea.md` — the other half of “broadcasts never land.”

### Tests
- Existing: `ClientProfileAnonymizedEventTests.CreateAndResolveCommands_DefaultConsentFalse` locks the default false. `ConsentDefaultFalse` migration exists. No checkout test asserts a consent checkbox or a true write from hop-1.
- Would any test fail if the bug is still there? No. The default-false test **passes because** checkout never opts anyone in.
- First regression (only if product adds collection): POST checkout with the box checked writes `ConsentedToMarketing = true` for that (org, email, phone); unchecked stays false; existing true is not silently flipped false.

### Reproduction today
Arrange a fresh tenant, complete hop-1 checkout, inspect `crm.ClientProfiles.ConsentedToMarketing`. Assert: false. Act: create a broadcast; inspect preview count vs `GetActiveSubscriberRecipientsAsync`. Assert: preview ≥ 1, fan-out recipients = 0. Portal checkout shows ToS/Privacy text and no marketing checkbox.

### Blast radius
Marketing broadcasts are a vitamin path (ADR 021: do not productize). No money, no PII leak, no forced-true PDPA regression. Merchants who believe the TypeSpec sentence “fans out to all ACTIVE/PAST_DUE subscribers with marketing consent” will see a successful send and an empty audience. Frequency: every hop-1 buyer forever, until a write path exists.

### Suggested fix
Smallest honest fix: do **not** default consent true. Either (a) leave collection out and make broadcast preview/count use the same consent filter as fan-out (that is mostly 288), or (b) add an unchecked marketing checkbox on checkout and a named `ConsentedToMarketing` on Resolve — **without** TypeSpec regen if you can keep it internal; if the public DTO must grow, that is a spec change the wrap-rails say to avoid, so prefer (a) plus an ops-only consent toggle later. Do not implement WhatsApp. Do not treat ToS copy as consent.

### Evaluation notes
Paired with **288** (B08-M18). Still P2. The default is the correct PDPA posture; the defect is the missing opt-in path plus a lying broadcast count. Not a 007 consent-true regression. Do not “fix” by flipping `ConsentDefaultFalse` back to true.

