# R51 — LhdnGatewayAdapter partials notes

**Date:** 2026-08-09  
**Track:** Polish  
**Checklist:** `checklists/r51-polish-lhdn-gateway-partials.md`  
**Analysis:** `09-polish-godfiles-testsupport.md` §2.2  
**Scope this pass:** Mechanical split of `LhdnGatewayAdapter` (~384 LOC) into operation partials; zero behavior change.

---

## Summary

| Concern | State |
|---------|--------|
| Public type | `LhdnGatewayAdapter` unchanged (`public partial class`) |
| Port | `ILhdnGatewayAdapter` unchanged |
| DI | `AddScoped<ILhdnGatewayAdapter, LhdnGatewayAdapter>()` unchanged |
| Rate limiters | Static registries + `EnforceRateLimitAsync` stay on core partial |
| Behavior | Pure file move; method bodies identical |

---

## Layout

```
Modules/Lhdn/Infrastructure/Gateways/
  LhdnGatewayAdapter.cs           # fields, ctor, shared helpers (~91 LOC)
  LhdnGatewayAdapter.Token.cs     # GetTokenAsync
  LhdnGatewayAdapter.Submit.cs    # SubmitDocumentAsync
  LhdnGatewayAdapter.Status.cs    # GetDocumentStatusAsync
  LhdnGatewayAdapter.Tin.cs       # ValidateTaxpayerTinAsync
  LhdnGatewayAdapter.Cancel.cs    # CancelDocumentAsync
```

| File | Members |
|------|---------|
| Core | `_httpClientFactory`, `_cache`, `_configuration`, `_logger`; 5 limiter registries; ctor; `GetBaseUrl`, `EnforceRateLimitAsync`, `TryAddIntermediaryHeader`, `ExtractRetryAfterSeconds` |
| Token | `GetTokenAsync` (OAuth client_credentials + 55m cache; login 12/min) |
| Submit | `SubmitDocumentAsync` (documentsubmissions; 100/min) |
| Status | `GetDocumentStatusAsync` (poll + invalid details; 300/min) |
| TIN | `ValidateTaxpayerTinAsync` (60/min) |
| Cancel | `CancelDocumentAsync` (12/min) |

No separate rate-limit partial — shared helpers already live on the core file; registries must stay co-located with `EnforceRateLimitAsync`.

---

## Move rules (verified)

- [x] Type name `LhdnGatewayAdapter` unchanged (DI registration unchanged)
- [x] Interface method signatures unchanged
- [x] Static limiter dictionaries on primary partial (shared state)
- [x] No HTTP client / base URL / cache-key format changes
- [x] Rate limits unchanged (12 login, 100 submit, 300 poll, 60 TIN, 12 cancel)
- [x] Partials over inheritance (matches ProcessGatewayWebhook / GatewayPaymentCompleted style)

---

## Verification

| Check | Result |
|-------|--------|
| `dotnet build Modules.Lhdn.Infrastructure` | Success, 0 warnings |
| `dotnet test … --filter FullyQualifiedName~Lhdn` | **Passed: 31**, Skipped: 2 (sandbox E2E env), Failed: 0 |
| Notable suites | `LhdnRateLimitingTests`, `LhdnSingleCreditPathTests`, endpoints/auth/outbox/secrets/claim |

Sandbox E2E still skipped without credentials (`GetTokenAsync_ShouldReturnValidJwt_FromLhdnSandbox`, `GetDocumentStatusAsync_ShouldReturnStatus_ForKnownSubmission`).

---

## Explicit non-goals

- Change rate limits, cache TTL, base URL, or intermediary header behavior
- Introduce collaborator types / inheritance split
- Touch DI, port interface, or consumers (jobs/commands)
- Live MyInvois sandbox run

---

## Exit

- [x] Navigable partials by MyInvois operation
- [x] Public type + port stable
- [x] Module tests green
- [x] Zero intentional behavior change
- [ ] Commit (deferred — operator request)
