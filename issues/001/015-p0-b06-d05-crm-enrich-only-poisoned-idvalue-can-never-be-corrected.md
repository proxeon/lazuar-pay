---
number: "015"
id: B06-D05
severity: P0
status: resolved
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
resolved_branch: fix/015-crm-overwrite-poisoned-idvalue
---

# 015 — B06-D05 — CRM enrich-only: poisoned `IdValue` can never be corrected

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/015-crm-overwrite-poisoned-idvalue`

Resolve overwrites `IdType`/`IdValue` when checkout supplies a pair. A later product pay can correct a company-name `IdValue`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D05 — CRM enrich-only: poisoned `IdValue` can never be corrected (P0)

**Status:** open. 008 named the write. It did not name the permanence.

```50:58:apps/lazuar-api/Modules/CRM/Infrastructure/ResolveClientProfileCommandHandler.cs
            if (string.IsNullOrWhiteSpace(existingProfile.IdType) && !string.IsNullOrWhiteSpace(request.IdType))
            {
                existingProfile.IdType = request.IdType;
                isModified = true;
            }
            if (string.IsNullOrWhiteSpace(existingProfile.IdValue) && !string.IsNullOrWhiteSpace(request.IdValue))
            {
                existingProfile.IdValue = request.IdValue;
                isModified = true;
            }
```

First quote pay writes `IdValue = "Acme Sdn Bhd"`. A later product checkout with a real BRN finds the profile by email and **leaves IdValue alone**. Every subsequent type `01` for that email uses the company name as BRN until someone anonymizes the profile.

`ClientProfileCompanyNameTests` only covers the **named** product-shaped command. It does not cover the session branch.

