---
number: "250"
id: B06-D31
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 250 — B06-D31 — Quote-only buyer cannot open portal documents; same-email union

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D31 — Quote-only buyer cannot open portal documents; same-email union (P2)

Portal documents require a subscription id from the magic-link token (`PortalDocumentQueryService.cs:44–52`). Quote-only buyers have no subscription. QuoteView “Open buyer portal” has no token (`QuoteView.tsx:96–98`).

Within a tenant, profiles that share an email are unioned (`57–63`) and all of those emails’ transaction logs become one table. Two clients of the same merchant who share a billing mailbox see each other’s documents. Not cross-tenant. HMAC download binds `tenantSlug + ledgerEntryId` (`PublicBillingEndpoints.cs:44–46`). Cross-tenant PDF theft via slug swap does not work without the JWT secret.

Public GET does **not** verify the ledger belongs to the tenant beyond the key path `vault/{tenantId}/documents/{id}.pdf`. Wrong-tenant GUID + valid HMAC for **this** slug would presign a missing object, not the other tenant’s file.

