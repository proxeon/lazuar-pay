---
number: "054"
id: B03-C09
severity: P1
status: resolved
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
resolved_branch: fix/054-success-url-magic-token
---

# 054 — B03-C09 — Success and “dashboard” links drop the HMAC; buyer pays and cannot open the portal

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/054-success-url-magic-token`

Renewal and arrears success URLs include a portal HMAC. Dashboard header keeps `?token=` when present.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C09 — P1 — Success and “dashboard” links drop the HMAC; buyer pays and cannot open the portal

**Evidence.**

- `RenewalCheckoutIssuer` 43: `successUrl = $"{clientUrl}/{workspace.Slug}/portal"` — no token.
- Arrears POST 139: same.
- Update-payment page 74 and 110: `<Link href={`/${tenantSlug}/portal`}>`.
- Portal layout 21–26: header “Buyer Dashboard” → `/{tenantSlug}`.

After a Billplz/Stripe success redirect the buyer hits the magic-link form. The token they already had is gone.

**Blast.** Paid-through buyer files “I paid and I’m locked out.” They request another link (B03-C10). Support load, not double charge — unless they also click a leftover session (B03-C02).

**Tests.** Billing tests assert cancel URL has `?token=mint-token` (`BillingEngineJobTests` ~316). Nobody asserts success URL.

**Fix direction.** Mint a fresh token into `successUrl` (and keep it on dashboard/header links when the request had one).

---

