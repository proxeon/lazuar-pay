---
number: "019"
id: B08-M01
severity: P0
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 019 — B08-M01 — Resend bounce/complaint webhook never verifies a real `whsec_` secret

- **Severity:** P0
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M01 — P0 — Resend bounce/complaint webhook never verifies a real `whsec_` secret

**Where:** `PublicComplianceEndpoints.cs` 126–135; `ResendOptions.cs` 1–8; `appsettings.json` 35–38.

**What:** Manual Svix verification requires HMAC-SHA256 with the **base64-decoded** bytes after `whsec_`. Lazuar HMAC-SHA256s `Encoding.UTF8.GetBytes(secret)` of the whole string. Resend’s dashboard value will never match.

**Why it matters:** The product claims bounce/complaint suppression is live (008 §4, lanes tests, README-adjacent honesty). In production the inbound pipe is either 503 (empty secret) or 400 (correctly pasted secret). Mail keeps going to hard-bounced and complained addresses. That burns the tenant’s Resend domain and ignores a legal complaint.

**Not fixed by:** parser tests. Those never touch HMAC.

**Fix direction (do not implement here):** strip `whsec_`, `Convert.FromBase64String`, HMAC that key; put `WebhookSecret` on `ResendOptions`; add a signed-body integration test using the Svix sample (`secret = 'whsec_plJ3nmyCDGBKInavdOK15jsl'` …).

---

