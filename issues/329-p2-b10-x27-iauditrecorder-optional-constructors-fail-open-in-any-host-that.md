---
number: "329"
id: B10-X27
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 329 — B10-X27 — `IAuditRecorder?` optional constructors fail open in any host that forgets the registration

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X27 — P2 — `IAuditRecorder?` optional constructors fail open in any host that forgets the registration

`AddOneModule` does `services.AddScoped<IAuditRecorder, AuditRecorder>()`. Production invite/remove/refund/cancel should audit. The constructors default `= null`. A test host or a future composition that registers the command handlers without One’s DI **silently stops writing** `one.AuditEvents`. That is fail-open for compliance, fail-closed for nothing.

Accept-invite never took the dependency (B10-X20).

