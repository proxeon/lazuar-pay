---
number: "169"
id: B10-X13
severity: P1
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 169 — B10-X13 — `AppOptions.ClientUrl` default 3020 is unbound; three other fallbacks disagree

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X13 — P1 — `AppOptions.ClientUrl` default 3020 is unbound; three other fallbacks disagree

```7:10:apps/lazuar-api/src/Lazuar.Api/Configuration/AppOptions.cs
    /// The primary client-facing frontend URL (portal / public checkout surfaces, typically port 3020).
    /// </summary>
    public string ClientUrl { get; init; } = "http://localhost:3020";
```

Grep of `Configure<AppOptions>` / `IOptions<AppOptions>` is **empty**. The type is documentation that lies. Port **3020** is `examples/hub-cashier-next`, not the portal (3004) and not ops (3003).

Live readers:

| Reader | Fallback if `App:ClientUrl` missing |
|--------|--------------------------------------|
| `appsettings.json` | `http://localhost:3004` (present, so OK when config is loaded) |
| `OneLinkService` | `http://localhost:3004` |
| `PublicArrearsEndpoints` | `http://localhost:3004` |
| `InvoiceReminderJob` | `https://portal.lazuar.com` |
| Communications fulfillment / lifecycle / portal-access / digital-delivery / payment-failed handlers | `https://portal.lazuar.com` |

`297ba98` correctly mints invite URLs from `App:OpsUrl` (3003). Buyer recovery links still have two hosts and a fictional `portal.lazuar.com`.

