# R34 — Email + IMessagingService → Messaging (notes)

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Scope:** PR-2 + PR-3 from `04-bb-email-messaging-move.md` Phases 2–3 — email stack + channel console.  
**No commit** (per task).  
**Respect:** 00.4 — no WhatsApp product work.

---

## 1. What moved

| Item | From | To |
|------|------|-----|
| `IEmailService` | `BuildingBlocks.Application` | `Modules.Messaging.Application` |
| `IMessagingService` | `BuildingBlocks.Application` | `Modules.Messaging.Application` |
| `EmailTemplateBuilder` | `BuildingBlocks.Application` | `Modules.Messaging.Infrastructure.Email` |
| `ResendEmailService` | `BuildingBlocks.Infrastructure` | `Modules.Messaging.Infrastructure.Email` |
| `ConsoleEmailService` | `BuildingBlocks.Infrastructure` | `Modules.Messaging.Infrastructure.Email` |
| `ConsoleMessagingService` | `BuildingBlocks.Infrastructure` | `Modules.Messaging.Infrastructure.Messaging` |
| `ResendOptions` | `BuildingBlocks.Infrastructure.Configuration` | `Modules.Messaging.Infrastructure.Configuration` |
| Named HttpClient `"Resend"` | Host `Program.cs` | `AddMessagingModule` |

### Explicit non-moves / non-changes

- Communications BYOK (`TenantEmailConfiguration`, `SaveEmailConfigCommand`, suppressions, inbound Resend webhook) stays in Communications
- Org tag contract frozen: Resend payload `tags: [{ name: "org", value: organizationId }]` — constant `ResendEmailService.OrgTagName`
- BYOK rules frozen: system tenant may use platform key; non-system without tenant key throws (no platform fallback)
- List-Unsubscribe headers when `unsubscribeUrl` set
- WhatsApp remains console stub; gated by `Messaging:WhatsAppEnabled` (default false)
- Magic-link already moved in R33 (Commerce)
- MarkdownParser still BB (companion / not R34)

---

## 2. DI

| Before | After |
|--------|--------|
| Host `AddOptions<ResendOptions>`, `AddHttpClient("Resend")`, `IEmailService`→Resend, `IMessagingService`→Console | **Removed from Program.cs** |
| — | `AddMessagingModule` registers all of the above (same lifetimes: Singleton email + messaging) |

Communications `SaveEmailConfigCommand` continues to use named client `"Resend"` via `IHttpClientFactory` — registered when Messaging module loads (host composition order already includes Messaging).

---

## 3. Consumers retargeted

| Consumer | Assembly | Change |
|----------|----------|--------|
| `DispatchMessageIntegrationEventHandler` | Messaging.Infrastructure | usings → Messaging.Application + Email |
| `SendTenantNotificationCommandHandler` | Messaging.Application | `IMessagingService` now same module namespace |

---

## 4. Deleted from BB

- `BuildingBlocks/Application/IEmailService.cs`
- `BuildingBlocks/Application/IMessagingService.cs`
- `BuildingBlocks/Application/EmailTemplateBuilder.cs`
- `BuildingBlocks/Infrastructure/ResendEmailService.cs`
- `BuildingBlocks/Infrastructure/ConsoleEmailService.cs`
- `BuildingBlocks/Infrastructure/ConsoleMessagingService.cs`
- `BuildingBlocks/Infrastructure/Configuration/ResendOptions.cs`

---

## 5. Docs

| File | Change |
|------|--------|
| `apps/lazuar-api/docs/009-building-blocks-ownership.md` | Email + messaging rows → **R34 done** |
| `apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md` | Email/messaging listed under moved-out |
| `Modules/Messaging/README.md` | Module-owned ports; BYOK stays Communications |
| Checklist `r34-bb-email-messaging-to-messaging.md` | All items checked |

---

## 6. Verification

```bash
dotnet build apps/lazuar-api/Lazuar.slnx

dotnet test apps/lazuar-api/tests/Lazuar.ModuleTests \
  --filter "FullyQualifiedName~Messaging"

# Grep gate
rg 'IEmailService|IMessagingService|EmailTemplateBuilder|ResendEmailService|ConsoleEmailService|ConsoleMessagingService|ResendOptions' \
  apps/lazuar-api/BuildingBlocks  # expect zero
```

### Tests added

| Test | Asserts |
|------|---------|
| `EmailTemplateBuilderTests` | Brand footer, unsubscribe link, empty body |
| `ResendEmailServiceTests` | Org tag `org`, BYOK auth header, List-Unsubscribe, no platform fallback, system missing key → null |
| `ConsoleMessagingServiceTests` | Completes without error |
| `DispatchMessageIntegrationEventHandlerTests` | Brand wrap + email send + delivery log; WA disabled SKIPPED |

---

## 7. Residual debt (not this PR)

| Item | Note |
|------|------|
| `ResendOptions.WebhookSecret` typed binding | Communications still reads `config["Resend:WebhookSecret"]` |
| HttpClient `DefaultRequestHeaders.Authorization` mutation | Pre-existing thread-safety smell; prefer per-request headers |
| ConsoleEmail not selected in DI | Still available type; host always registers Resend (same as before) |
| EmailTemplateBuilder `\n`→`<br/>` on already-HTML bodies | Known quality bug; optional follow-up |
| Real WhatsApp provider | Requires 00.4 reopen |
