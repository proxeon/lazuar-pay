---
number: "104"
id: B06-D18
severity: P1
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 104 — B06-D18 — Integrator `accepted_for_processing` is Lazuar, not MyInvois; product B2B checkout is coupled to MyInvois

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D18 — Integrator `accepted_for_processing` is Lazuar, not MyInvois; product B2B checkout is coupled to MyInvois (P1)

**Status:** open.

```37:38:apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs
                await mediator.Send(new SubmitTaxDocumentCommand(ctx.TenantId, idempotencyKey, req));
                return TypedResults.Ok(new StatusResponse { Status = "accepted_for_processing" });
```

The document is PENDING in Lazuar. Worker has not POSTed yet.

Separately: product checkout **requires** `validate-tin` to succeed before pay (`CheckoutForm.tsx:96–110`). Public TIN 400s “Merchant has not connected MyInvois.” if creds are missing (`PublicTinValidationEndpoints.cs:45–47`). A merchant who only wants a commercial INV- PDF cannot take a B2B product payment until MyInvois is connected. Quote path, which is the broken identity path, has no such gate.

