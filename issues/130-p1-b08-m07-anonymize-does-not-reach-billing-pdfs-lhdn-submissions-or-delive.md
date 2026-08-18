---
number: "130"
id: B08-M07
severity: P1
status: resolved
resolved_branch: fix/130-anonymize-honest
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 130 — B08-M07 — Anonymize does not reach Billing PDFs, LHDN submissions, or delivery logs

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/130-anonymize-honest`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M07 — P1 — Anonymize does not reach Billing PDFs, LHDN submissions, or delivery logs

**Where:** `AnonymizeSubscriberCommandHandler` (commerce logs + CRM command only); `GenerateAndStoreDocumentCommandHandler` 107–120; `LhdnBuyerMapper` / submitted UBL; `MessageDeliveryLog`; no Billing anonymize consumer.

**What:** After Subscribers → Anonymize, CRM is dummy, commerce log name/email are dummy, mail is suppressed, subscriptions cancel. The official receipt PDF in R2 still has the live name, email, TIN, and address. MyInvois already has the buyer. `GET /messaging/delivery-logs` still lists the live inbox. Outbox rows for `ClientProfileAnonymized` keep the pre-wipe email until processed (necessary) and remain readable after.

**Why it matters:** Ops UI copy says “This cannot be undone. Subscriptions cancel. Emails stop.” It does not say “your filed tax invoices and receipt PDFs still have the NRIC.” CRM README §5 claims a GDPR fan-out. The fan-out is Communications + Commerce cancel only.

**Commerce log scrub is real** (`AnonymizeSubscriberCommandHandlerTests` 65–69). Do not re-file that as missing.

---

