---
number: "216"
id: B03-C28
severity: P2
status: resolved
resolved_branch: fix/216-arrears-slug-bind
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 216 — B03-C28 — Arrears API is not tenant-slug-bound

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/216-arrears-slug-bind`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C28 — P2 — Arrears API is not tenant-slug-bound

After HMAC, slug is irrelevant. 008 asked to bind slug. Residual: a stolen token works on `/public/commerce/checkout/{anySibling}/…` without knowing the workspace slug (the GUID is already in the email). Low extra risk given the token.

---

