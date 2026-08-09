# R53 — GatewayCommon + outbox DI pilot (notes)

**Date:** 2026-08-09  
**Track:** Polish  
**Checklist:** `checklists/r53-polish-gateway-common-outbox-di.md`  
**Analysis:** `09-polish-godfiles-testsupport.md` §2.4 + §4  
**No commit** (per task).

---

## Summary

| Concern | State |
|---------|--------|
| GatewayCommon | **Landed** — static helpers only (no abstract base) |
| Adapters wired | Billplz, CHIP, Razorpay, Stripe (defaults only) |
| Minor-unit semantics | **Preserved** — truncating (Billplz/Razorpay) vs rounded (CHIP) |
| `AddModuleOutboxInbox` | **Landed** — Option A (thin job subclasses) |
| `ApplyOutboxInbox` | **Landed** — byte-identical filter SQL |
| Pilot module | **CRM** DI + `CrmDbContext` |
| EF migrations | **Zero** — `dotnet ef migrations has-pending-model-changes` → no changes |
| Arch tests | **Green** (14/14) |
| R53.3 ProblemDetails | **Skipped** (optional / opportunistic) |

---

## R53.1 GatewayCommon

**Path:** `Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` (`internal static`)

| Helper | Purpose |
|--------|---------|
| `ExtractName` | Local-part of email → display name |
| `ResolveEmail` / `PlaceholderEmail` | Blank email → `customer@example.com` |
| `DefaultProductName` / `ProductDescription` | `"Lazuar Payment"` + `(xN)` suffix |
| `ToMinorUnitsTruncating` | Billplz/Razorpay `(int)(amount * qty * 100)` |
| `ToMinorUnitsRounded` | CHIP `Math.Round(amount * qty * 100, 0)` |

**Explicit non-goals kept:** no `PaymentGatewayAdapterBase`; no Stripe SDK unification; webhook parse remains local.

**Razorpay qty=1 empty product name:** now uses `DefaultProductName` via shared `ProductDescription` (was blank string) — safer gateway payload, not a money path.

---

## R53.2 Outbox DI pilot (Option A)

```
BuildingBlocks/Infrastructure/
  ModuleOutboxInboxServiceCollectionExtensions.cs  # AddModuleOutboxInbox<TDb,TOut,TIn>
  OutboxInboxModelBuilderExtensions.cs             # ApplyOutboxInbox
```

**CRM call site:**

```csharp
services.AddModuleOutboxInbox<CrmDbContext, CrmOutboxPublisherJob, CrmInboxConsumerJob>("CrmEventBus");
// thin job subclasses retained for arch test + typed loggers
modelBuilder.ApplyOutboxInbox();
```

**Not rolled out:** Billing, Commerce, Communications, Lhdn, Messaging, One, Ops, Payments (follow-up PR-6 shape).

---

## Files

| Action | Path |
|--------|------|
| New | `Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs` |
| Edit | `BillplzGatewayAdapter.cs`, `ChipCollectGatewayAdapter.cs`, `RazorpayGatewayAdapter.cs`, `StripeGatewayAdapter.cs` |
| New | `BuildingBlocks/Infrastructure/ModuleOutboxInboxServiceCollectionExtensions.cs` |
| New | `BuildingBlocks/Infrastructure/OutboxInboxModelBuilderExtensions.cs` |
| Edit | `Modules/CRM/Infrastructure/DependencyInjection.cs` |
| Edit | `Modules/CRM/Infrastructure/CrmDbContext.cs` |
| New | `tests/.../Payments/GatewayCommonTests.cs` |
| New | `tests/.../CRM/CrmOutboxInboxRegistrationTests.cs` |
| New | `tests/.../BuildingBlocks/ModuleOutboxInboxExtensionsTests.cs` |
| Edit | `tests/Lazuar.ModuleTests/Lazuar.ModuleTests.csproj` (CRM.Infrastructure ref) |
| Edit | checklist + FULL-CHECKLIST R53 |
| New | `plans/005-remaining/r53-notes.md` |

---

## Verification

```bash
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests --filter \
  "FullyQualifiedName~GatewayCommon|FullyQualifiedName~CrmOutboxInbox|FullyQualifiedName~ModuleOutboxInbox|FullyQualifiedName~LhdnOutboxPublisher|FullyQualifiedName~BillplzGateway"
# Passed!  - Failed: 0, Passed: 19

dotnet test apps/lazuar-api/tests/Lazuar.ArchitectureTests --nologo
# Passed!  - Failed: 0, Passed: 14

dotnet ef migrations has-pending-model-changes \
  --project Modules/CRM/Infrastructure --startup-project src/Lazuar.Api --context CrmDbContext
# No changes have been made to the model since the last migration.
```

---

## Follow-ups (not this PR)

1. Roll `AddModuleOutboxInbox` + `ApplyOutboxInbox` to remaining 8 modules (zero migrations expected).
2. Optional R53.3 ProblemDetails codes on LHDN/One when those endpoints are already open.
3. Do **not** delete thin job subclasses (Option B rejected).
