---
number: "102"
id: B06-D16
severity: P1
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 102 — B06-D16 — Tenant `Environment` is cosmetic; checkout country default is `MY`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D16 — Tenant `Environment` is cosmetic; checkout country default is `MY` (P1)

**Status:** open.

```44:47:apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.cs
    private string GetBaseUrl()
    {
        return _configuration["Lhdn:BaseUrl"]?.TrimEnd('/') ?? "https://preprod-api.myinvois.hasil.gov.my";
    }
```

Ops Legal card can flip SANDBOX/PROD. GET config echoes it (`LhdnQueries.cs:84–86`). Traffic does not move.

CheckoutForm `useState("MY")` (`CheckoutForm.tsx:53`). Initiate stores `request.CountryCode ?? "MYS"` (`InitiateCheckoutCommandHandler.cs:194`). If `requires_address` is on and the buyer leaves the default, CRM gets `MY`. UBL `Country.IdentificationCode` becomes `MY`. LHDN wants ISO 3166-1 alpha-3 `MYS`. Realistic INVALID.

