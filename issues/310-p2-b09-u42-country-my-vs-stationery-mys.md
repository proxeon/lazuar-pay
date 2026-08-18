---
number: "310"
id: B09-U42
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 310 — B09-U42 — Country `MY` vs stationery `MYS`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U42 — Country `MY` vs stationery `MYS` (P2)

`CheckoutForm.tsx` 53 vs billing profile.

## Evaluation (current tree, 2026-08-18)

### What the bug is
At audit HEAD, address-required product checkout defaulted `countryCode` to ISO alpha-2 `"MY"` while Legal & Billing stationery (and hop-1’s omitted-field default) used ISO alpha-3 `"MYS"`. MyInvois `Country.IdentificationCode` wants alpha-3. A buyer who left the default posted `MY`; CRM/UBL stored `MY`; a live submit could INVALID. That is the same data-path as resolved issues 102 and 190.

### Still present?
**ALREADY FIXED** (102 `fix/102-environment-cosmetic-country-my`, 190 `fix/190-country-alpha3`)

Checkout default is now `MYS`, matching stationery:

```53:53:apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx
  const [countryCode, setCountryCode] = useState("MYS");
```

```60:60:apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx
  const [countryCode, setCountryCode] = useState("MYS");
```

Hop-1 no longer trusts a raw `MY` even if the buyer types it:

```226:236:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        BillingAddressDto? billingAddress = null;
        if (!string.IsNullOrEmpty(request.AddressLine1))
        {
            billingAddress = new BillingAddressDto
            {
                Line1 = request.AddressLine1,
                City = request.City ?? "",
                Postal_code = request.PostalCode ?? "",
                State_code = request.StateCode ?? "",
                Country_code = Iso3166Country.NormalizeToAlpha3(request.CountryCode)
            };
        }
```

```10:20:apps/lazuar-api/BuildingBlocks/Domain/Iso3166Country.cs
    public static string NormalizeToAlpha3(string? code, string fallback = "MYS")
    {
        if (string.IsNullOrWhiteSpace(code))
            return fallback;
        var trimmed = code.Trim().ToUpperInvariant();
        return trimmed switch
        {
            "MY" => "MYS",
            _ => trimmed
        };
    }
```

Residual honesty: `messages.ts` `form.country` is still `"Country Code (e.g. MY)"` / `"Kod negara (cth. MY)"` — the placeholder text, not the posted default.

### Related files
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` — default `MYS`.
- `apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx` — stationery / LHDN country `MYS`.
- `apps/lazuar-api/BuildingBlocks/Domain/Iso3166Country.cs` — MY → MYS.
- `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` — both custom and product hops normalize.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnLinkServiceTests.cs` `NormalizeToAlpha3_MapsMyToMys`.
- `apps/lazuar-portal/src/modules/checkout/i18n/messages.ts` — leftover “e.g. MY” copy.
- Issues `102-p1-b06-d16-…`, `190-p2-b01-c20-…` — the fixes.

### Tests
- Existing tests that touch this path: `LhdnLinkServiceTests.NormalizeToAlpha3_MapsMyToMys` (null/`MY`/`my`/`MYS` → `MYS`). No portal component test that the input default is `MYS`.
- Whether any test would fail if the *original* bug returned: **Yes, partially** — `NormalizeToAlpha3_MapsMyToMys` fails if the mapper regresses; it would **not** fail if CheckoutForm’s `useState` flipped back to `"MY"` because hop-1 would still normalize.
- What a first regression test should assert (only if this ticket is reopened for the leftover copy): `form.country` examples say `MYS`, and/or CheckoutForm initial state is `"MYS"`.

### Reproduction today
Arrange a product with “Require Full Billing Address.” Open checkout. Assert: country field is `MYS`. Submit without editing. Assert: CRM/address snapshot is `MYS` (handler `NormalizeToAlpha3`). Type `MY` and submit: still stored as `MYS`. Stationery Legal & Billing placeholder is “e.g. MYS”.

### Blast radius
Originally: every address-required (often B2B) checkout could INVALID at LHDN. That path is closed. Remaining blast is a buyer who reads “e.g. MY” and thinks they should type alpha-2; the handler still saves them. No current money loss from this ticket’s original fault.

### Suggested fix
Do not re-fix the default. If closing residual copy: change `form.country` to “Country Code (e.g. MYS)” in `en` and `ms`. Do not expand `Iso3166Country` into a full ISO table in this ticket. No TypeSpec regen.

### Evaluation notes
U42 is a frontend restatement of 102 + 190. YAML `status` stays `open` per instructions; an implementer should close this as duplicate/already-fixed rather than change hop-1 again. Severity as a live INVALID risk is no longer P2. Not blocked.

