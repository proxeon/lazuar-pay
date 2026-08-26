---
number: "055"
id: B03-C10
severity: P1
status: resolved
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
resolved_branch: fix/055-magic-link-throttle
---

# 055 — B03-C10 — Magic-link endpoint is always-200 and unthrottled in this tree

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/055-magic-link-throttle`

POST magic-link is 5 requests / 10 minutes per IP (and email+IP). Over budget returns 429.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C10 — P1 — Magic-link endpoint is always-200 and unthrottled in this tree

**Evidence.** `PublicPortalEndpoints.cs` 65–73. Handler early-returns (`RequestPortalMagicLinkCommandHandler.cs` 34–55). No `AddRateLimiter` / public-commerce throttle under `apps/lazuar-api/src`. Timing: unknown email stops after CRM; known email + sub publishes an outbox event and `SaveChanges`.

Always-200 is the **correct** anti-enumeration shape. Unthrottled always-200 plus a measurable CRM/outbox delta is an oracle and an email-bomb.

**Repro.** Script `POST /public/commerce/{slug}/portal/magic-link` with a victim email 1 000 times.

**Blast.** Inbox flood; Resend spend; confirmation that the email is a customer if the attacker can see the mailbox or the send latency.

**Tests.** Handler unit test only. Add a limiter test and a constant-time/constant-work path.

**Fix direction.** Per-IP and per-email throttle on this route (the comment already pretends it exists). Optionally always enqueue a no-op delay.

---

