---
number: "205"
id: B03-C17
severity: P2
status: resolved
resolved_branch: fix/205-base64url-portal-tokens
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 205 — B03-C17 — Tokens are standard Base64 concatenated into query strings

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/205-base64url-portal-tokens`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C17 — P2 — Tokens are standard Base64 concatenated into query strings

`MessageLinkBuilder` 36, `RenewalCheckoutIssuer` 45, portal `href` 174, arrears cancel URL 140. `PortalPlanChange` encodes; everyone else does not. Current alphabet appears to avoid `+`/`/`; padding `=` is always present. A token-format change, or a proxy that strips `=`, breaks the gate you just added.

**Fix.** Base64url + `Uri.EscapeDataString` at every mint site.

---

