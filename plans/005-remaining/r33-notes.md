# R33 — Magic-link token service → Commerce (notes)

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Scope:** PR-1 from `04-bb-email-messaging-move.md` Phase 1 — portal magic-link only.  
**No commit** (per task).

---

## 1. What moved

| Item | From | To |
|------|------|-----|
| `IMagicLinkTokenService` | `BuildingBlocks.Application` | `Modules.Commerce.Contracts` |
| `MagicLinkTokenService` (HMAC) | `BuildingBlocks.Infrastructure` | `Modules.Commerce.Infrastructure.Security` |

### Explicit non-moves / non-changes

- Wire format frozen: Base64(`{subscriptionId}:{expiryUnix}:{hmacHex}`), HMAC-SHA256, 24h TTL
- Secret source frozen: `Jwt:Secret` (fallback `fallback_dev_secret_key`) — dedicated `Commerce:PortalMagicLinkSecret` is **future security PR**
- Email / messaging / Resend / brand HTML still in BB (R34+)
- No portal UX or token TTL changes

---

## 2. Cross-module access (E1)

Communications mints portal URLs for dunning; Commerce validates.

| Approach | Chosen |
|----------|--------|
| Port on **Commerce.Contracts** | **Yes** — Communications.Infrastructure already references Commerce.Contracts |
| Port on Commerce.Application only | No (infra → other Application layering smell) |
| Pre-mint URLs in Commerce dunning only | Out of scope |

---

## 3. DI

| Before | After |
|--------|--------|
| Host `Program.cs` `AddSingleton<IMagicLinkTokenService, MagicLinkTokenService>()` | **Removed** |
| — | `AddCommerceModule` registers Singleton (same lifetime) |

---

## 4. Consumers retargeted

| Consumer | Assembly | Role |
|----------|----------|------|
| `PublicPortalEndpoints` | Commerce.Infrastructure | **Validate** on portal GET |
| `CancelPortalSubscriptionCommandHandler` | Commerce.Application | **Validate** on portal cancel |
| `FulfillmentRequestedIntegrationEventHandler` | Communications.Infrastructure | **Generate** for `{{portal_magic_link}}` |
| `DunningTemplateVariableSubstitutionTests` | ModuleTests | Mint substitute via Contracts |
| `CommerceQueryServiceTests` | IntegrationTests | Dead ctor arg removed |

### Dead dependency cleanup

`CommerceQueryService` injected `IMagicLinkTokenService` but never used it — **removed** field + ctor param (and integration test mock).

---

## 5. Deleted from BB

- `BuildingBlocks/Application/IMagicLinkTokenService.cs`
- `BuildingBlocks/Infrastructure/MagicLinkTokenService.cs`

---

## 6. Docs

| File | Change |
|------|--------|
| `apps/lazuar-api/docs/009-building-blocks-ownership.md` | Magic-link row → **R33 done** |
| `apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md` | Magic-link listed under moved-out |

---

## 7. Verification

```bash
# Compile
dotnet build apps/lazuar-api/Lazuar.slnx --no-restore  # or with restore

# Focused tests
dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests \
  --filter "FullyQualifiedName~MagicLinkTokenServiceTests|FullyQualifiedName~DunningTemplateVariableSubstitutionTests"

# Grep gate
rg 'IMagicLinkTokenService|MagicLinkTokenService' apps/lazuar-api/BuildingBlocks  # expect zero
```

### Test coverage added/strengthened

| Test | Asserts |
|------|---------|
| `MagicLinkTokenServiceTests` | Roundtrip validate, wire format shape, tamper/wrong-secret/garbage null, fallback secret |
| `DunningTemplateVariableSubstitutionTests` | `GenerateToken` called; `portal_magic_link` → `https://portal.test/{slug}/portal?token=...` |

---

## 8. Residual debt (not this PR)

| Item | Note |
|------|------|
| Shared `Jwt:Secret` for portal tokens | Compromising JWT invalidates portal links |
| Communications still depends on mint port | E3 pre-built URL in Commerce dunning would remove dep |
| OrderCompleted digital delivery | Uses plain portal URL, not token mint (pre-existing) |
