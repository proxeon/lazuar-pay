---
number: "168"
id: B10-X12
severity: P1
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 168 — B10-X12 — `GetService` CRM / One / tokens / config fail-open on money comms

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X12 — P1 — `GetService` CRM / One / tokens / config fail-open on money comms

`BillingEngineJob` reminder-only path:

- `crm == null` or no email → PAST_DUE **without** minting a renewal checkout (warning log).
- `mediator` / `one` / `tokens` null → **throws** (fail-closed for mint). Asymmetric.

`InvoiceReminderJob`:

```61:62:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs
        var one = scope.ServiceProvider.GetService<IOneQueryService>();
        var config = scope.ServiceProvider.GetService<IConfiguration>();
```

```85:106:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs
        var portalBase = (config?["App:ClientUrl"] ?? "https://portal.lazuar.com").TrimEnd('/');
        // ...
            var workspace = one == null ? null : await one.GetWorkspaceByIdAsync(session.OrganizationId);
            var slug = workspace?.Slug ?? "";
            var payUrl = string.IsNullOrEmpty(slug)
                ? $"{portalBase}/pay/{session.Id}"
                : $"{portalBase}/{slug}/pay/{session.Id}";
```

`one == null` or workspace miss ⇒ email contains `https://portal.lazuar.com/pay/{guid}` with **no tenant slug**. That URL is not the portal’s `/{tenantSlug}/pay/{sessionId}` route. Buyer gets a 404. Job still records the dispatch log. The −3 / 0 / +3 unique index then **prevents a correct retry** after One is fixed.

