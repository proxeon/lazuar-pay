---
number: "098"
id: B06-D12
severity: P1
status: resolved
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
resolved_branch: fix/098-tin-200-not-valid
---

# 098 — B06-D12 — TIN HTTP 200 with empty / unparseable body is treated as valid

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/098-tin-200-not-valid`

HTTP 200 with an empty or non-JSON body is `IsValid=false`. Only a JSON object is accepted.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D12 — TIN HTTP 200 with empty / unparseable body is treated as valid (P1)

**Status:** open.

```27:38:apps/lazuar-api/Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter.Tin.cs
        if (response.IsSuccessStatusCode)
        {
            try
            {
                var json = JsonDocument.Parse(responseBody);
                var taxpayerName = json.RootElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                return new LhdnTinValidationResult(true, true, taxpayerName, null);
            }
            catch
            {
                return new LhdnTinValidationResult(true, true, null, null);
            }
        }
```

Any 2xx, including empty body, is `IsValid=true`. `TaxpayerValidationService` then caches **valid for 30 days** (`TaxpayerValidationService.cs:71`). Product checkout and `SubmitTaxDocumentCommand` both trust this. A gateway/proxy that 200s an HTML error page false-accepts a TIN and then **files type `01`**.

404 is correctly `IsValid=false` (`41–44`). Other statuses throw / fail. The false-accept is specifically the 200+garbage path.

Default cache salt is `"default_local_salt_replace_in_prod"` (`TaxpayerValidationService.cs:31`). Shared salt across tenants is fine (cache key includes org). A production deploy that never sets `Lhdn:TinHashSalt` is a comment-shaped hole, not a false-accept.

