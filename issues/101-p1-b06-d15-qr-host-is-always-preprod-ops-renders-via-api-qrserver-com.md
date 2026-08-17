---
number: "101"
id: B06-D15
severity: P1
status: resolved
resolved_branch: fix/101-qr-host-preprod
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 101 — B06-D15 — QR host is always preprod; ops renders via `api.qrserver.com`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/101-qr-host-preprod`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D15 — QR host is always preprod; ops renders via `api.qrserver.com` (P1 / P2)

**Status:** open.

```15:18:apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/LhdnLinkService.cs
    public string GetPortalUrl()
    {
        return _configuration["Lhdn:PortalUrl"]?.TrimEnd('/') ?? "https://preprod.myinvois.hasil.gov.my";
    }
```

`appsettings.json:64` default is preprod. Tenant `Environment=PROD` does not change it. Payload shape `{portal}/{uuid}/share/{longId}` is the official MyInvois share URL. The **host** is wrong for production. There is no scanned QR from a real VALID UUID in this repo (honesty file).

Ops panel:

```261:261:apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx
                    src={`https://api.qrserver.com/v1/create-qr-code/?size=160x160&data=${encodeURIComponent(qrLink)}`}
```

UUID + LongId are sent to a third-party QR SaaS. QuestPDF uses in-process QRCoder (`BaseInvoiceDocument.cs:202–210`) — that half is fine.

