---
number: "100"
id: B06-D14
severity: P1
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 100 — B06-D14 — Poller does not write poll UUID back onto `TaxDocument`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D14 — Poller does not write poll UUID back onto `TaxDocument` (P1)

**Status:** open. Closest thing in the hunt to “poll never advances.”

`MarkAsSubmitted` stores UUID from `acceptedDocuments[0]` (`TaxDocument.cs:50–58`). Submit parser:

```88:94:apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.Submit.cs
        string? uuid = null;
        if (root.TryGetProperty("acceptedDocuments", out var acceptedDocs) && acceptedDocs.GetArrayLength() > 0)
        {
            uuid = acceptedDocs[0].TryGetProperty("uuid", out var uuidProp) ? uuidProp.GetString() : null;
        }

        return new LhdnSubmissionResult(true, submissionUid, uuid, null);
```

If `acceptedDocuments` is empty but `submissionUid` exists, Success=true, Uuid=null. Document is SUBMITTED. Poll later gets uuid + longId from `documentSummary`. Poller:

```89:99:apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs
                    if (result.Status == "VALID")
                    {
                        doc.MarkAsValid(result.LongId!);
                        ...
                        await eventBus.PublishAsync(new LhdnDocumentValidatedIntegrationEvent(
                            doc.OrganizationId, doc.InternalReferenceId, result.Uuid!, "VALID", qrLink));
```

`MarkAsValid` sets `LongId` only (`TaxDocument.cs:91–98`). **`LhdnUuid` stays null.** GET `/lhdn/documents/{internalId}` builds QR only from `doc.LhdnUuid` + `doc.LongId` (`LhdnQueries.cs:35–39`). Ops panel QR is therefore **blank** after VALID if submit missed the uuid, even though Billing received `result.Uuid` on the event.

Poll **does** advance SUBMITTED → VALID/INVALID on the happy path. 404 is Success=false + retry 5s (`LhdnGatewayAdapter.Status.cs:34–37`). Missing config `continue`s after the lease was claimed (`LhdnStatusPollingJob.cs:78–81`); the row is not stuck forever, only leased. “Poll never advances” as a total hang is **not** what this code does. “Poll advances and the QR/GET still look empty” is.

