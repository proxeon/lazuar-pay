---
number: "316"
id: B09-U48
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 316 — B09-U48 — QR via qrserver.com

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U48 — QR via qrserver.com (P2)

`TaxInvoiceDetailPanel.tsx` 258–261. Third-party sees the MyInvois URL.

## Evaluation (current tree, 2026-08-18)

### What the bug is
When a merchant opened a VALID sales document in Hub Ops, the MyInvois share QR was not rendered in-process. The panel pointed an `<img src>` at `https://api.qrserver.com/v1/create-qr-code/?size=160x160&data=…`, so the full MyInvois share URL (`{portal}/{uuid}/share/{longId}`) left the browser as a query string to a third-party QR SaaS. That host saw document UUIDs and LongIds that belong on a tax invoice. PDF generation already used in-process QRCoder; only the ops preview leaked. The audit treated that as P2 privacy / supply-chain exposure, sibling to P1 issue 101 (QR *host* always preprod).

### Still present?
**ALREADY FIXED**

Issue 101 (`fix/101-qr-host-preprod`) replaced the third-party `<img>` with a same-origin blob fetch. There is no `qrserver.com` / `api.qrserver.com` / `create-qr-code` string anywhere in `*.tsx` / `*.ts` / `*.cs` today. Ops now loads PNG from the API:

```121:137:apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx
  const qrLink = liveStatus === "VALID" ? lhdnDoc?.qr_link : undefined;

  const { data: qrImageUrl } = useQuery({
    queryKey: ["lhdn-document-qr", lhdnInternalId, qrLink],
    enabled: !!invoice && !!lhdnInternalId && !!qrLink,
    queryFn: async () => {
      const tenantId = localStorage.getItem("ops_active_workspace_id");
      const res = await fetch(
        `${API_URL}/lhdn/documents/${encodeURIComponent(lhdnInternalId!)}/qr`,
        {
          credentials: "include",
          headers: tenantId ? { "X-Tenant-Id": tenantId } : undefined,
        }
      );
      if (!res.ok) throw new Error("QR unavailable");
      return URL.createObjectURL(await res.blob());
    },
  });
```

The API encodes that PNG with QRCoder, same as the invoice PDF path:

```82:96:apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs
        documentsRead.MapGet("/documents/{internalId}/qr", async Task<IResult> (
            string internalId,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetLhdnDocumentStatusQuery(ctx.TenantId, internalId));
            if (result?.Qr_link is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.File(
                MyInvoisQrPng.Encode(result.Qr_link),
                "image/png",
                fileDownloadName: null);
        });
```

`MyInvoisQrPng.Encode` (`apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/MyInvoisQrPng.cs:7–13`) is in-process QRCoder. The `<img>` at `TaxInvoiceDetailPanel.tsx:285–290` now uses the blob URL, not a third-party host. The share *link* text still points at MyInvois (`qrLink` from `GetLhdnDocumentStatusQuery` in `LhdnQueries.cs:37–41`); that is the official scan target, not a leak to qrserver.

### Related files
- `apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx` — ops QR preview; first-party fetch + blob object URL.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs` — `GET /lhdn/documents/{internalId}/qr` (policy `IntegrationLhdnDocumentsRead`).
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/MyInvoisQrPng.cs` — in-process PNG encoder.
- `apps/lazuar-api/Modules/Lhdn/Application/Queries/LhdnQueries.cs` — builds `qr_link` only when status is VALID with UUID + LongId.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Documents/BaseInvoiceDocument.cs` — PDF QR already used QRCoder (audit said this half was fine).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnLinkServiceTests.cs` — `Encode_ShareUrl_ReturnsPngBytes`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnEndpointsAuthorizationTests.cs` — asserts the `/qr` route exists and is on the documents-read policy.
- `issues/101-p1-b06-d15-qr-host-is-always-preprod-ops-renders-via-api-qrserver-com.md` — the P1 that actually shipped the same-origin QR.

### Tests
- Existing: `LhdnLinkServiceTests.Encode_ShareUrl_ReturnsPngBytes` (PNG magic bytes from `MyInvoisQrPng.Encode`). `LhdnEndpointsAuthorizationTests` (GET `/lhdn/documents/{internalId}/qr` is mapped + `IntegrationLhdnDocumentsRead`). `LhdnLinkServiceTests.GetPortalUrl_Prod_UsesProductionHost` covers host selection, not ops rendering.
- None of those would fail if someone pasted `api.qrserver.com` back into the panel. There is still no ops/admin frontend test (see 325).
- First regression test: assert `TaxInvoiceDetailPanel` source does not contain `qrserver.com` (or a component test that the `<img>` `src` is a blob: URL from `GET …/lhdn/documents/{id}/qr`, never an absolute third-party host). Optionally keep the existing PNG-magic assertion.

### Reproduction today
Arrange: OrgAdmin session in ops, a VALID tax document with UUID + LongId so `GET /lhdn/documents/{internalId}` returns `qr_link`. Act: open Sales documents → row → detail panel. Assert: Network tab shows `GET /api/v1/lhdn/documents/{internalId}/qr` with cookie + `X-Tenant-Id`; no request to `api.qrserver.com`; the 160×160-class image is a blob: URL; clicking the mono link still opens the MyInvois share URL. A repo-wide search for `qrserver` in app source should be empty.

### Blast radius
Was: every VALID e-invoice preview sent UUID/LongId to a third-party QR CDN (PII-adjacent tax identifiers, not card data). Frequency: every time a merchant opened a validated sales document. Money: none. Residual risk after the fix is only if the first-party `/qr` 403s (Viewer) and the panel shows an empty box (`TaxInvoiceDetailPanel.tsx:291–292`) while still printing the share URL as text.

### Suggested fix
Do not re-introduce qrserver. This ticket can close as a duplicate of 101. If an implementer is asked to “fix 316” anyway: keep the blob fetch; do not add a client-side QR library that phones home; do not regenerate TypeSpec. Leave Wave 5 / WhatsApp / Xero / homemade e-mandate alone.

### Evaluation notes
Duplicate of the ops half of **101** (resolved on `fix/101-qr-host-preprod`; README: “ops QR is same-origin”). 101 also fixed preprod-vs-prod *host* selection (`LhdnLinkService`). Severity for 316 is no longer a live P2. Close 316 when triaging duplicates; do not change YAML status here. No FE test was added with 101, so 325 still applies.

