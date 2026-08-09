# 04 — Re-home email / messaging ports from BuildingBlocks (FW-3)

**Status:** Analysis only — **no app code changes in this document**  
**Date:** 2026-08-09  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Track:** Future work **FW-3** (BuildingBlocks product-port moves) / checklist **F12**  
**Primary code root:** `apps/lazuar-api`  
**Authority docs (do not reopen casually):**
- [`apps/lazuar-api/docs/009-building-blocks-ownership.md`](../../apps/lazuar-api/docs/009-building-blocks-ownership.md) — ownership map (Phase 15)
- [`apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md`](../../apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md) — BB vs SharedKernel intent
- [`plans/004-maintenance/decisions.md`](../004-maintenance/decisions.md) §**00.4** (Messaging / WhatsApp freeze) and §**00.6** (no new modules)
- [`plans/004-maintenance/FUTURE-WORK.md`](../004-maintenance/FUTURE-WORK.md) **FW-3** (recommended move order items 4–6)
- [`plans/004-maintenance/checklists-future/phase-f12-bb-email-messaging.md`](../004-maintenance/checklists-future/phase-f12-bb-email-messaging.md) — F12 checklist skeleton
- [`plans/004-maintenance/06-building-blocks-shared-kernel.md`](../004-maintenance/06-building-blocks-shared-kernel.md) — fatness analysis
- Module READMEs: `Modules/Messaging/README.md`

**Related but out of scope of this doc (sibling FW-3 items):**
- LLM stack → Ops (F11)
- Metrics pluginization (F13)
- MarkdownParser Markdig package fan-out (mentioned where it touches email content; full move is optional companion PR)

---

## 0. Why this exists

BuildingBlocks currently hosts **product-shaped delivery ports and adapters**:

| Concern | Why it is *not* pure technical spine |
|---------|--------------------------------------|
| `IEmailService` + `ResendEmailService` | Parameters encode **Resend BYOK**, **org tags for bounce attribution**, **List-Unsubscribe**, and **no platform fallback for tenant email** — product policy, not generic SMTP |
| `EmailTemplateBuilder` | Hardcodes **“Powered by Lazuar”** brand HTML |
| `IMessagingService` + `ConsoleMessagingService` | Module-named transport; only Messaging consumes it; WhatsApp is frozen (00.4) as console stub |
| `IMagicLinkTokenService` / `MagicLinkTokenService` | API is **`subscriptionId`-shaped**; secret reuses `Jwt:Secret`; sole product use is Commerce subscriber portal (+ Communications minting portal URLs for dunning) |

Architecture tests enforce **BB ↛ `Modules.*` assembly edges**. They do **not** catch conceptual leakage (subscription magic links, brand HTML, Resend BYOK rules) inside BB. Re-homing restores ownership so Messaging/Communications/Commerce can evolve delivery without growing the kitchen-sink trio.

**Constraint (locked):** Do **not** invent an Email module or Messaging↔Communications merge for purity (00.6 / 00.4). Do **not** implement Meta WhatsApp as part of this move.

---

## 1. Current inventory (file-level, absolute paths under repo)

### 1.1 BuildingBlocks.Application (ports + brand helper)

| Type | Path | Public surface | Product smell |
|------|------|----------------|---------------|
| `IEmailService` | `apps/lazuar-api/BuildingBlocks/Application/IEmailService.cs` | `SendEmailAsync(to, subject, body, organizationId?, tenantApiKey?, tenantSenderEmail?, unsubscribeUrl?) → string?` | BYOK + org + unsubscribe are product rules |
| `IMessagingService` | `apps/lazuar-api/BuildingBlocks/Application/IMessagingService.cs` | `SendMessageAsync(recipient, text)` | Minimal “SMS/WA-ish” port; no templates, media, status callbacks |
| `IMagicLinkTokenService` | `apps/lazuar-api/BuildingBlocks/Application/IMagicLinkTokenService.cs` | `GenerateToken(Guid subscriptionId)`, `ValidateToken(string) → Guid?` | Parameter name + semantics = Commerce subscription |
| `EmailTemplateBuilder` | `apps/lazuar-api/BuildingBlocks/Application/EmailTemplateBuilder.cs` | static `WrapWithBrandHtml(rawBody, unsubscribeUrl?)` | Brand footer + `\n`→`<br/>` on already-HTML bodies |
| `MarkdownParser` *(adjacent)* | `apps/lazuar-api/BuildingBlocks/Application/MarkdownParser.cs` | `ToHtml` / `ToPlainText` via **Markdig** | Content pipeline for templates; package rides every Contracts fan-out via `BuildingBlocks.Application` |

Package on Application today: **MediatR + Markdig + OpenAI** (`BuildingBlocks.Application.csproj`). Email move alone does not remove Markdig; MarkdownParser is a companion cleanup.

### 1.2 BuildingBlocks.Infrastructure (adapters + options)

| Type | Path | Role |
|------|------|------|
| `ResendEmailService` | `apps/lazuar-api/BuildingBlocks/Infrastructure/ResendEmailService.cs` | Real Resend HTTP adapter; system-tenant platform key; tenant BYOK required; org tags; List-Unsubscribe headers; returns provider id |
| `ConsoleEmailService` | `apps/lazuar-api/BuildingBlocks/Infrastructure/ConsoleEmailService.cs` | Dev log adapter; **not** registered in current `Program.cs` (Resend always selected) |
| `ConsoleMessagingService` | `apps/lazuar-api/BuildingBlocks/Infrastructure/ConsoleMessagingService.cs` | Logs `[MESSAGING/SMS]`; production WhatsApp stand-in under 00.4 freeze |
| `MagicLinkTokenService` | `apps/lazuar-api/BuildingBlocks/Infrastructure/MagicLinkTokenService.cs` | HMAC-SHA256 over `{subscriptionId}:{expiryUnix}` Base64; **24h**; secret = `Jwt:Secret` (fallback string if missing) |
| `ResendOptions` | `apps/lazuar-api/BuildingBlocks/Infrastructure/Configuration/ResendOptions.cs` | `SectionName = "Resend"`; `ApiKey`, `SenderEmail` only — **`WebhookSecret` is used in config but not on this options class** |

### 1.3 Host composition (registration owner today = host)

**File:** `apps/lazuar-api/src/Lazuar.Api/Program.cs`

| Registration | Lifetime | Notes |
|--------------|----------|--------|
| `AddOptions<ResendOptions>().BindConfiguration(ResendOptions.SectionName)` | options | Platform Resend |
| Named `HttpClient` `"Resend"` → `https://api.resend.com/`, 30s timeout, optional platform Bearer at factory time | — | Per-send overwrites `Authorization` on the client instance (subtle shared-header mutation) |
| `IMessagingService` → `ConsoleMessagingService` | Singleton | Always console |
| `IEmailService` → `ResendEmailService` | Singleton | Always Resend (console email unused) |
| `IMagicLinkTokenService` → `MagicLinkTokenService` | Singleton | Host-owned DI |

**Config:** `apps/lazuar-api/src/Lazuar.Api/appsettings.json`

```json
"Resend": { "ApiKey": "", "SenderEmail": "", "WebhookSecret": "" },
"Messaging": { "WhatsAppEnabled": false }
```

`Resend:WebhookSecret` is read ad hoc as `config["Resend:WebhookSecret"]` in Communications public endpoints — **not** bound via `ResendOptions`.

### 1.4 Module-owned product surfaces (already correct home; not in BB)

These **stay** where they are; the move is about **ports/adapters**, not re-homing policy aggregates:

| Surface | Owner module | Path / notes |
|---------|--------------|--------------|
| `TenantEmailConfiguration` (encrypted BYOK key) | **Communications** | `Modules/Communications/Domain/Aggregates/TenantEmailConfiguration.cs` |
| `SaveEmailConfigCommand` (validates key via Resend `GET domains`) | **Communications** | Uses named HttpClient `"Resend"` + `ISecretVault` |
| `ICommunicationsQueryService.GetEmailConfigCredentialsAsync` / `HasValidEmailConfigAsync` | **Communications.Contracts** | Dispatch + Commerce product gates |
| Suppressions + Resend bounce/complaint webhook | **Communications** | `PublicComplianceEndpoints` — attributes org via Resend `org` tag set by `ResendEmailService` |
| Templates, broadcasts, fan-out | **Communications** | Renders → publishes `DispatchMessageIntegrationEvent` |
| `DispatchMessageIntegrationEvent` | **Messaging.Contracts** | Universal dispatch contract |
| `DispatchMessageIntegrationEventHandler` | **Messaging.Infrastructure** | **Only production caller of `IEmailService` + primary caller of `IMessagingService` + `EmailTemplateBuilder`** |
| `MessageDeliveryLog` | **Messaging.Domain** | SENT / FAILED / SKIPPED |
| Subscriber portal auth (validate magic token) | **Commerce** | `PublicPortalEndpoints`, `CancelPortalSubscriptionCommandHandler` |
| Dunning channel demotion when WA disabled | **Commerce** | `DunningEngineJob` reads `Messaging:WhatsAppEnabled` |

---

## 2. Usage graph (who injects / calls what)

### 2.1 `IEmailService` / Resend / Console email

| Consumer | Assembly | How used |
|----------|----------|----------|
| `DispatchMessageIntegrationEventHandler` | `Modules.Messaging.Infrastructure` | **Sole runtime caller.** Fetches BYOK via `ICommunicationsQueryService`, wraps HTML with `EmailTemplateBuilder`, calls `SendEmailAsync`, logs `MessageDeliveryLog` |
| `Program.cs` | Host | DI registration only |
| `ConsoleEmailService` | BB Infrastructure | Implements port; **not registered** in current host |
| Tests | — | **No direct unit/integration tests** of ResendEmailService found under `tests/` |

**Non-callers that are easy to misread as email senders:**
- One, Communications, Commerce, Billing **never** inject `IEmailService`.
- They publish `DispatchMessageIntegrationEvent` (or domain events that become dispatch). That is the correct “render at source, dispatch at edge” rule (`Modules/Messaging/README.md` §8).

**Publishers of `DispatchMessageIntegrationEvent` (indirect email path):**

| Publisher | Module | Trigger examples |
|-----------|--------|------------------|
| `FulfillmentRequestedIntegrationEventHandler` | Communications | Dunning / reminder.due |
| `OrderCompletedDigitalDeliveryHandler` | Communications | Digital product delivery |
| `DocumentPublishedIntegrationEventHandler` | Communications | Document emails |
| `LifecycleEventHandlers` | Communications | Subscription suspended/canceled notices |
| `MessageTemplateCommandHandlers` | Communications | Template preview/test send |
| `BroadcastFanoutJob` | Communications | Broadcast fan-out (with credit hold) |
| `NotificationDispatchDomainEventHandlers` | One | Password reset, email verify, workspace invite (system tenant / org for invite) |

### 2.2 `EmailTemplateBuilder`

| Consumer | Notes |
|----------|-------|
| `DispatchMessageIntegrationEventHandler` only | Wraps final HTML before Resend. `Replace("\n","<br/>")` on **already** Markdown-rendered HTML is a known quality bug (gap docs / 08-communications). |

### 2.3 `IMessagingService` / Console messaging

| Consumer | How used |
|----------|----------|
| `DispatchMessageIntegrationEventHandler` | WhatsApp/phone path when channel is WHATSAPP/ALL and `Messaging:WhatsAppEnabled` is true |
| `SendTenantNotificationCommandHandler` | Admin `/api/v1/messaging/notify` — sends to **tenant.Slug** as “recipient” with a system alert string (console-only semantics; not a real SMS product) |
| `Program.cs` | DI registration |

With `Messaging:WhatsAppEnabled: false` (default), dispatch **skips** WhatsApp and logs SKIPPED. Commerce dunning demotes WHATSAPP/ALL to email-capable channels. The console adapter remains registered but is rarely exercised on the happy email path.

### 2.4 `IMagicLinkTokenService` / `MagicLinkTokenService`

| Consumer | Module | Generate / Validate | Purpose |
|----------|--------|---------------------|---------|
| `FulfillmentRequestedIntegrationEventHandler` | Communications | **Generate** | Build `{{portal_magic_link}}` for dunning/reminder templates |
| `PublicPortalEndpoints` `GET /{tenantSlug}/portal` | Commerce | **Validate** | Resolve subscription id from token |
| `CancelPortalSubscriptionCommandHandler` | Commerce | **Validate** | Auth portal cancel against token subscription |
| `CommerceQueryService` ctor | Commerce | **Injected but field appears unused** in partials (dead DI dependency risk — cleanup opportunity during move) |
| Tests | `DunningTemplateVariableSubstitutionTests`, `CommerceQueryServiceTests` | Substitute | |

**Not used by One** — platform identity uses JWT / `ITokenGeneratorService`, not magic links (see `docs/001-gaps/10-one-identity-module.md`).

### 2.5 Resend-adjacent (not BB ports, but couple to the move)

| Touchpoint | Module | Coupling to BB Resend adapter |
|------------|--------|-------------------------------|
| Named HttpClient `"Resend"` | Host | Shared by `ResendEmailService` **and** `SaveEmailConfigCommandHandler` (domains validation) |
| `Resend:WebhookSecret` + Svix verify | Communications `PublicComplianceEndpoints` | Depends on **org tag** contract written by `ResendEmailService` payload `tags: [{ name: "org", value }]` |
| `TenantEmailCredentials` decrypt | Communications query service | Dispatch handler pulls plaintext key only at send time |

Moving the send adapter **must preserve** the org-tag contract or bounce/complaint suppression breaks silently.

### 2.6 Call-flow diagram (product path)

```
[Commerce dunning / checkout gates / One domain events / Communications templates]
        │
        │  render markdown + variables (MarkdownParser, template DB, magic link mint)
        ▼
 DispatchMessageIntegrationEvent  (Messaging.Contracts)
        │
        ▼
 Messaging inbox → DispatchMessageIntegrationEventHandler
        │
        ├─ suppression check (Communications ISuppressionService)
        ├─ BYOK credentials (Communications ICommunicationsQueryService)
        ├─ EmailTemplateBuilder.WrapWithBrandHtml  ← brand (today BB)
        ├─ IEmailService.SendEmailAsync           ← Resend (today BB)
        ├─ IMessagingService.SendMessageAsync     ← console WA (today BB)
        └─ MessageDeliveryLog + optional credit deduct (Billing commands)
```

---

## 3. Ownership model (recommended)

### 3.1 Split of concerns (already intended; reinforce with code homes)

| Responsibility | Owner | Rationale |
|----------------|-------|-----------|
| **Physical send** (HTTP to Resend, future WA provider) | **Messaging** | Terminal sink; delivery logs; channel routing; only module that should know provider APIs for send |
| **Brand HTML shell** (“Powered by Lazuar”, unsubscribe footer layout) | **Messaging** (or Communications template layer) | Applied at dispatch edge today; keep next to send OR move wrap into Communications pre-render — see options below |
| **BYOK config, domain validation, encrypted keys** | **Communications** | Already owns `TenantEmailConfiguration`, admin email settings UI contract, `HasValidEmailConfigAsync` |
| **Content / policy** (templates, suppressions, broadcast eligibility, unsubscribe links) | **Communications** | Already true |
| **Inbound Resend webhooks** (bounce/complaint → suppress) | **Communications** | Public compliance endpoints; must stay aligned with send-side tags |
| **Platform Resend key for system tenant** (password reset, verify email) | Host config → Messaging adapter | System org empty/`000…001` uses platform key; One publishes with system tenant id |
| **Subscription portal magic tokens** | **Commerce** | API is subscription-scoped; portal endpoints own validate; Communications only **mints** for URLs |
| **WhatsApp productization** | Frozen (00.4) | Keep console adapter under Messaging; no multi-channel product PR under this workstream |

### 3.2 Recommended final homes (target state)

| Item | Target project / folder | Namespace suggestion |
|------|-------------------------|----------------------|
| `IEmailService` | `Modules/Messaging/Application` (or `Application/Ports`) | `Modules.Messaging.Application` |
| `ResendEmailService`, `ConsoleEmailService` | `Modules/Messaging/Infrastructure/Email` | `Modules.Messaging.Infrastructure` |
| `ResendOptions` (+ ideally `WebhookSecret`) | Messaging.Infrastructure **or** split: platform send options in Messaging; webhook secret stay Communications-bound | Prefer one `ResendOptions` with optional `WebhookSecret` if both modules read it via options pattern — **or** keep webhook secret as Communications-only `IOptions` |
| Named HttpClient `"Resend"` registration | Messaging module DI (`AddMessagingModule`) **or** host still if Communications validation needs same client name | Prefer Messaging registers client; Communications continues to use factory by name (host/module order must register before first use) |
| `EmailTemplateBuilder` | Messaging.Infrastructure (next to send) **or** Communications content helper | See §4 options |
| `IMessagingService` + `ConsoleMessagingService` | Messaging Application + Infrastructure | Same module as name implies |
| `IMagicLinkTokenService` + impl | **Commerce** Application (port) + Infrastructure (HMAC impl) | `Modules.Commerce.Application` / `.Infrastructure` |
| Cross-module minting for dunning URLs | Communications continues to inject port via **DI** (already references Commerce.Contracts; may need Commerce.Application port type accessible without infra cycle — see §5) | Prefer port interface on **Commerce.Contracts** if Application type is not referenceable |

### 3.3 Explicit non-owners

| Anti-pattern | Why not |
|--------------|---------|
| New **Email** module | 00.6 scope freeze; lifecycle does not justify a tenth module |
| Merge Messaging into Communications | 00.4 until real multi-channel product funds it |
| Leave brand HTML in BB “because shared” | Violates 009 forbidden list (brand/product HTML) |
| Put Resend send adapter in Communications | Breaks “dumb pipe” Messaging ownership; dual send paths risk |
| Put magic-link in One / BuildingBlocks “security” forever | Shape is Commerce subscription, not CIAM |

---

## 4. Strategy options

### Option A — Full re-home (recommended primary path)

**Move ports + adapters out of BB into Messaging / Commerce.** Host only calls module DI extensions. BB Application loses product ports.

| Pros | Cons |
|------|------|
| Aligns code with 009 map and Messaging README | Touches namespaces, usings, Program.cs, two modules, tests |
| Brand HTML and Resend policy leave BB | Risk of missing a using / DI order bug |
| Magic-link ownership becomes obvious | Communications must resolve Commerce-owned port without cycles |
| Clear review boundary for future WA adapter | Slightly larger PR series |

**Fits FW-3 order:** item 4 (email/templates) then 5 (messaging console) then 6 (magic-link).

### Option B — Keep thin ports in BuildingBlocks.Application; move only Infrastructure adapters

Keep **minimal** interfaces in BB Application:

```csharp
// Thin technical port — no BYOK params
Task SendAsync(EmailMessage message, CancellationToken ct);
```

Move **ResendEmailService**, **Console\***, **EmailTemplateBuilder**, **ResendOptions**, **MagicLink\*** implementations into modules. Modules implement BB ports **or** Messaging defines richer internal ports and host maps them.

| Pros | Cons |
|------|------|
| Less churn if something else needed a shared email port later | **Today only Messaging calls IEmailService** — multi-module justification is theoretical |
| Architecture tests already allow modules → BB | Leaves product-shaped method parameters if interface not redesigned |
| Easier temporary dual-registration | 009 already says prefer Messaging-owned long-term |

**When Option B is justified:** if a second module must call email **without** `DispatchMessageIntegrationEvent` (there is **no** such caller today). Do not invent that path during the move.

### Option C — “Keep-thin-port” hybrid (recommended compromise for email only)

1. **Delete** product-shaped `IEmailService` from BB after Messaging owns the real port.  
2. **Do not** keep a shared BB email interface “just in case.”  
3. Optionally keep a **future-proof** ultra-thin technical note in 009 grey area text only (no code).  
4. All product traffic continues through `DispatchMessageIntegrationEvent` (current design intent — already true).

For **magic-link**, no hybrid: it is product-shaped; **must** leave BB.

For **IMessagingService**, same as email: only Messaging (+ notify command) uses it → full move to Messaging.

### Option D — Brand wrap ownership fork

| Sub-option | Behavior |
|------------|----------|
| **D1 (default):** Move `EmailTemplateBuilder` to Messaging next to send | Minimal behavior change; dispatch still wraps |
| **D2:** Move wrap into Communications when building `HtmlEmailBody` | Messaging becomes pure deliver-as-is; better “render at source”; requires updating all publishers or a single Communications helper used by all render sites |
| **D3:** Split brand chrome (Messaging platform) vs content (Communications) | Overkill for now |

**Recommend D1 first** (zero semantic change), optional follow-up D2 for purity.

### Option E — Magic-link cross-module access

Communications mints tokens; Commerce validates. After move:

| Approach | Mechanism | Prefer? |
|----------|-----------|---------|
| **E1** Port interface on `Modules.Commerce.Contracts` | Communications.Infrastructure already references Commerce.Contracts | **Yes** — matches other cross-module ports (`ICrmQueryService`, etc.) |
| **E2** Port on Commerce.Application; Communications.Infrastructure references Commerce.Application | Unusual layering (infra → other application) | No |
| **E3** Pre-build portal URL in Commerce dunning pipeline; Communications only substitutes strings | Removes magic-link dep from Communications entirely | Nice long-term; larger dunning redesign — **out of scope** for FW-3 hygiene |

**Recommend E1:** move interface to Commerce.Contracts (or a small `IPortalMagicLinkService` name), implementation stays Commerce.Infrastructure, register in `AddCommerceModule`.

---

## 5. Move steps (detailed, ordered)

### Phase 0 — Preconditions (analysis done; execution checklist)

1. Confirm architecture tests: BB must not reference Modules; after move, Messaging/Commerce still only depend downward on BB + Contracts.  
2. Confirm 00.4: no WhatsApp product work in same PRs.  
3. Confirm no second `IEmailService` caller (grep at start of PR).  
4. Note dead `CommerceQueryService._tokenService` field for cleanup.  
5. Inventory usings of `BuildingBlocks.Application` symbols to update.

### Phase 1 — Magic-link → Commerce (smallest blast radius, pure product shape)

**Why first:** Independent of email; 2–3 modules touch it; no HttpClient; high conceptual clarity.

1. Add `IPortalMagicLinkTokenService` (or keep name `IMagicLinkTokenService`) to **Commerce.Contracts** or Commerce.Application with same method signatures (preserve wire format of tokens — **token format is a breaking change if altered**).  
2. Move `MagicLinkTokenService` implementation to `Modules/Commerce/Infrastructure/Security/` (or `Services/`).  
3. Register in `AddCommerceModule`:  
   `services.AddSingleton<IMagicLinkTokenService, MagicLinkTokenService>();`  
   (or Scoped if preferred — currently Singleton; keep Singleton unless tested otherwise).  
4. Update consumers:  
   - Commerce endpoints / cancel handler  
   - Communications `FulfillmentRequestedIntegrationEventHandler`  
   - Tests  
5. Remove host registration from `Program.cs`.  
6. Delete BB Application + Infrastructure magic-link files.  
7. **Do not** change HMAC algorithm, payload shape, 24h expiry, or secret source in the same PR unless deliberately versioning tokens.  
8. Optional same-PR: remove unused `_tokenService` from `CommerceQueryService` if confirmed unused.  
9. Update 009 map row for MagicLink to **Moved**.

**Token secret debt (document, do not fix in move PR unless agreed):** reusing `Jwt:Secret` means JWT compromise invalidates portal links and vice versa. Future: dedicated `Commerce:PortalMagicLinkSecret` — **separate security PR**.

### Phase 2 — Messaging channel port (`IMessagingService` + console)

**Why next:** Trivial types; only Messaging (+ notify) uses them; no Resend coupling.

1. Move interface → Messaging.Application.  
2. Move `ConsoleMessagingService` → Messaging.Infrastructure.  
3. Register in `AddMessagingModule` (not host):  
   `services.AddSingleton<IMessagingService, ConsoleMessagingService>();`  
4. Update `DispatchMessageIntegrationEventHandler` and `SendTenantNotificationCommandHandler` namespaces.  
5. Remove host registration.  
6. Delete BB files.  
7. Update Messaging README language: ports are **module-owned**, not BuildingBlocks.  
8. Update 009.

### Phase 3 — Email port + Resend stack + brand builder

**Largest PR; keep atomic or split as 3a/3b.**

#### 3a — Move types without behavior change

1. Move `IEmailService` → Messaging.Application.  
2. Move `ResendEmailService`, `ConsoleEmailService`, `ResendOptions` → Messaging.Infrastructure (+ Configuration folder).  
3. Move `EmailTemplateBuilder` → Messaging.Infrastructure (or Application static helper).  
4. Register in `AddMessagingModule`:  
   - `AddOptions<ResendOptions>().BindConfiguration(...)`  
   - Named HttpClient `"Resend"` (copy factory logic from Program.cs)  
   - `IEmailService` → `ResendEmailService` (and optional env switch to Console for local)  
5. **Keep** host registration only if Communications `SaveEmailConfigCommand` needs HttpClient registered before Messaging module is added — preferably **single registration site** inside Messaging DI called from host module composition order **before** first request.  
6. Update `DispatchMessageIntegrationEventHandler` usings.  
7. Delete BB email files.  
8. Ensure system-tenant GUID checks and BYOK throw message remain identical (behavioral parity).

#### 3b — Options / webhook secret hygiene (same or follow-up PR)

1. Extend `ResendOptions` with `WebhookSecret` **or** introduce `CommunicationsResendWebhookOptions`.  
2. Point `PublicComplianceEndpoints` at typed options.  
3. Document org-tag contract in Messaging README or XML docs on `ResendEmailService`.

#### 3c — Optional ConsoleEmail selection

Host currently always uses Resend (logs to console only when platform key missing for system tenant). Consider:

```csharp
// conceptual
if (env.IsDevelopment() && string.IsNullOrEmpty(resendApiKey))
  → ConsoleEmailService
else
  → ResendEmailService
```

Not required for re-home; improves local DX. Separate PR if desired.

### Phase 4 — Documentation & ownership map

1. Update `apps/lazuar-api/docs/009-building-blocks-ownership.md` §3 / §7 rows: email, messaging, magic-link → **Moved** with date.  
2. Update `docs/002` “present but product-shaped” bullets.  
3. Update `Modules/Messaging/README.md` §1 (remove “BuildingBlocks ports stay technical” if ports are now module-owned).  
4. Update `apps/lazuar-api/README.md` BuildingBlocks bullet list if it still claims email/messaging ports live in BB.  
5. Mark F12 checklist items complete in future-work tracker when code lands.  
6. Gap docs (`docs/001-gaps/08`, `12`) can stay historical or get a one-line “superseded by 009 + this plan” — optional.

### Phase 5 — Companion (optional, not required for FW-3 email exit)

| Item | Note |
|------|------|
| `MarkdownParser` → Communications (and thin duplicate for One, or shared content helper) | Removes Markdig from BB Application package fan-out; **not** blocking for email move |
| Fix `EmailTemplateBuilder` newline→br on HTML | Product quality; can ride Phase 3 if tiny |
| Dedicated portal magic-link secret | Security hardening |
| Pre-mint portal URLs in Commerce only (E3) | Removes Communications dependency on magic-link port |
| Real WhatsApp adapter | **Requires 00.4 reopen** — not FW-3 |

---

## 6. Keep-thin-port options (decision matrix)

| Port | Keep thin in BB? | Recommendation | Justification |
|------|------------------|----------------|---------------|
| `IEmailService` | Only if redesigned to provider-agnostic DTO **and** ≥2 modules inject it | **No — move to Messaging** | Single consumer; product params |
| `IMessagingService` | Same | **No — move to Messaging** | Single real owner; console stub |
| `EmailTemplateBuilder` | Never in BB | **Move** (Messaging D1) | Brand HTML forbidden in BB |
| `ResendOptions` / Resend adapter | Never in BB long-term | **Move to Messaging** | Provider + policy |
| `IMagicLinkTokenService` | Never | **Move to Commerce** | Subscription-shaped |
| Named HttpClient `"Resend"` | Host or Messaging DI | **Messaging DI** | Composition ownership follows adapter |
| Generic HMAC helper (if extracted from magic-link) | Yes, BB Security | Optional later | Only if multiple product token shapes share crypto primitives |
| `DispatchMessageIntegrationEvent` | N/A (already Messaging.Contracts) | **Stay** | Correct |
| `ISecretVault` | Stay BB | **Stay** | Multi-module crypto |
| Outbox/inbox | Stay BB | **Stay** | Technical spine — do not confuse with product Messaging module |

**Grey-area quote from 009 (email):**  
> Prefer Messaging-owned long-term. Thin `IEmailService` may remain in Application while product traffic goes through Messaging integration events.

**Interpretation for this plan:** that grey text is a **temporary permission**, not a target. Given zero non-Messaging injectors, **delete from BB** rather than leave a thin husk.

---

## 7. Risks and mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| **Silent break of bounce/complaint attribution** if org tag format changes | High | Freeze tag name `org` + value = organizationId string; add comment/tests; Communications webhook tests if none exist |
| **BYOK / system-tenant policy drift** during move | High | Copy-paste parity first; no “cleanup” of throw messages in same PR |
| **HttpClient `"Resend"` double-registration or missing registration** | Medium | Single registration site; integration smoke: save email config + dispatch |
| **DI order:** Communications validates Resend before Messaging module registered | Medium | Host composition already calls all `Add*Module` at startup; ensure Messaging DI always runs |
| **Token invalidation** if MagicLink secret source or format changes | High | Do not change crypto in move PR |
| **Cross-module reference cycle** Communications ↔ Commerce | Medium | Port on Commerce.**Contracts** only; impl in Commerce.Infrastructure; Communications already references Commerce.Contracts |
| **Architecture test failure** if BB gains Modules reference | High | Never put module types in BB; move **out**, not reverse |
| **Namespace churn** across tests | Low | Global replace + build |
| **ConsoleEmail never registered** continues | Low | Document; optional DX PR |
| **Thread-safety:** `DefaultRequestHeaders.Authorization` mutation on shared HttpClient | Medium (pre-existing) | Prefer `HttpRequestMessage.Headers` per request in a follow-up hardening PR — not blocking re-home |
| **Scope creep into WhatsApp / MarkdownParser / LLM** | Process | One concern per PR (FW-3 rules); refuse bundling |
| **Host still documents BB as email owner** | Low | Doc updates in Phase 4 |
| **CommerceQueryService dead dependency** confuses reviewers | Low | Delete unused field during magic-link PR |

### Behavioral parity checklist (must stay identical)

- System tenant ids: `Guid.Empty` and `00000000-0000-0000-0000-000000000001` use platform Resend key.  
- Non-system without BYOK: throw — **no platform fallback**.  
- Platform key missing for system: log + return null (current ResendEmailService) vs throw — preserve.  
- List-Unsubscribe headers when `unsubscribeUrl` set.  
- Provider id parse from Resend JSON `id`.  
- Magic-link: Base64(`{guid}:{unix}:{hmacHex}`), 24h, Jwt secret.  
- WhatsApp skip when `Messaging:WhatsAppEnabled=false`.

---

## 8. PR breakdown (recommended series)

Follow FW-3 rule: **one concern per PR**. Suggested titles and scopes:

### PR-1 — Magic-link → Commerce

**Title:** `refactor(commerce): move portal magic-link tokens out of BuildingBlocks (FW-3)`

| Include | Exclude |
|---------|---------|
| Interface + impl move | Secret rotation |
| DI register in Commerce module | Portal UX changes |
| Consumer using updates | Token TTL changes |
| Delete BB magic-link files | Email/Resend |
| Test updates | MarkdownParser |
| 009 row update | |

**Exit:** portal GET/cancel + dunning magic link substitution tests green; host no longer registers magic-link.

### PR-2 — IMessagingService + Console → Messaging

**Title:** `refactor(messaging): own IMessagingService console adapter (FW-3)`

| Include | Exclude |
|---------|---------|
| Port + console move | Real WhatsApp provider |
| Register in AddMessagingModule | Credit model changes |
| Delete BB messaging console | Email |
| README honesty tweak | |

**Exit:** notify endpoint + dispatch handler compile; WA still gated off by default.

### PR-3 — Email stack → Messaging

**Title:** `refactor(messaging): move Resend email port and brand wrapper from BuildingBlocks (FW-3)`

| Include | Exclude |
|---------|---------|
| IEmailService, Resend*, ConsoleEmail, EmailTemplateBuilder, ResendOptions | Webhook secret redesign (optional small include OK) |
| HttpClient registration move into Messaging DI | SaveEmailConfig domain changes |
| Program.cs strip | MarkdownParser package removal |
| Behavioral parity | New retries/circuit breakers (follow-up) |
| 009 + Messaging README | Meta WA |

**Exit:** password-reset / system email path still works with platform key; tenant BYOK path still requires config; architecture tests green; BB Application no longer contains email types.

### PR-4 — Docs / map / F12 exit

**Title:** `docs: mark FW-3 email/messaging/magic-link ownership moved`

| Include | Exclude |
|---------|---------|
| 009, 002, api README, F12 checklist, FUTURE-WORK note | Code |

Can merge into PR-3 if small.

### Optional PR-5 — Hygiene riders (only if still cheap)

- `ResendOptions.WebhookSecret` typed binding  
- HttpClient per-request Authorization (thread-safety)  
- Remove dead `CommerceQueryService` token field if not done in PR-1  
- ConsoleEmail in Development when no API key  
- EmailTemplateBuilder do-not-br-escape HTML

### Explicitly later / separate tracks

| Work | Track |
|------|-------|
| MarkdownParser out of BB | FW-3 companion / package fan-out |
| LLM → Ops | F11 / FW-3 order 2–3 |
| Metrics plugins | F13 |
| Meta WhatsApp | Product + 00.4 reopen |
| Messaging ↔ Communications merge | Phase 16 style extract gate after product trigger |

---

## 9. Test plan (for implementers)

| Area | How to verify |
|------|----------------|
| Architecture | `Lazuar.ArchitectureTests` / `ModuleBoundaryTests` — BB ↛ Modules |
| Module tests | `DunningTemplateVariableSubstitutionTests` (magic link mint) |
| Integration | `CommerceQueryServiceTests` constructor substitutes |
| Manual / smoke | Register user → verification email path (system Resend or console log) |
| Manual / smoke | Tenant with BYOK → template test send / dunning email |
| Manual / smoke | Portal `?token=` open + cancel |
| Manual / smoke | Resend webhook with org tag → suppression (if secret configured) |
| Compile | Full `dotnet build` on Lazuar.slnx |
| Grep gate | Zero references to old BB type names after delete |

**Gap today:** no dedicated ResendEmailService unit tests and thin coverage of dispatch handler. Consider adding a focused test with mocked `HttpMessageHandler` when moving (recommended in PR-3, not mandatory if parity-only).

---

## 10. File move map (concrete)

### From BuildingBlocks → Messaging

| Source | Destination (suggested) |
|--------|-------------------------|
| `BuildingBlocks/Application/IEmailService.cs` | `Modules/Messaging/Application/IEmailService.cs` |
| `BuildingBlocks/Application/IMessagingService.cs` | `Modules/Messaging/Application/IMessagingService.cs` |
| `BuildingBlocks/Application/EmailTemplateBuilder.cs` | `Modules/Messaging/Infrastructure/Email/EmailTemplateBuilder.cs` |
| `BuildingBlocks/Infrastructure/ResendEmailService.cs` | `Modules/Messaging/Infrastructure/Email/ResendEmailService.cs` |
| `BuildingBlocks/Infrastructure/ConsoleEmailService.cs` | `Modules/Messaging/Infrastructure/Email/ConsoleEmailService.cs` |
| `BuildingBlocks/Infrastructure/ConsoleMessagingService.cs` | `Modules/Messaging/Infrastructure/Messaging/ConsoleMessagingService.cs` |
| `BuildingBlocks/Infrastructure/Configuration/ResendOptions.cs` | `Modules/Messaging/Infrastructure/Configuration/ResendOptions.cs` |

### From BuildingBlocks → Commerce

| Source | Destination (suggested) |
|--------|-------------------------|
| `BuildingBlocks/Application/IMagicLinkTokenService.cs` | `Modules/Commerce/Contracts/IMagicLinkTokenService.cs` **or** `Modules/Commerce/Application/...` |
| `BuildingBlocks/Infrastructure/MagicLinkTokenService.cs` | `Modules/Commerce/Infrastructure/Security/MagicLinkTokenService.cs` |

### Host edits

| File | Change |
|------|--------|
| `src/Lazuar.Api/Program.cs` | Remove Resend options/client/email/messaging/magic-link registrations **if** moved into module DI |
| Module `DependencyInjection.cs` | Add the registrations |

### Leave untouched (ownership already correct)

- Communications email config aggregate, suppressions, public Resend webhook endpoint  
- Messaging `DispatchMessageIntegrationEvent` + handler orchestration logic (only change **namespaces** of ports)  
- One notification handlers (continue publishing dispatch events)  
- `ISecretVault` / AES vault in BB  

---

## 11. Interaction with decisions 00.4 / 00.6

| Decision | Impact on this plan |
|----------|---------------------|
| **00.4** No WA / multi-channel for 6 months; thin Messaging; no merge | Moving console adapter into Messaging is **allowed** (ownership hygiene). Implementing Meta Cloud API is **not**. Docs must not claim WA is live. |
| **00.4** “BB ports stay technical” | After move, the ports are **module ports**; the decision’s intent was “don’t productize channels as BB kitchen-sink PRs” — re-home **fulfills** that intent. |
| **00.6** No new modules | No Email module. No Storage-like parallel. |
| **FW-3** One concern per PR; update 009 | Follow PR series above. |

---

## 12. Definition of done (this workstream)

All of the following:

1. **No** `IEmailService`, `IMessagingService`, `EmailTemplateBuilder`, `IMagicLinkTokenService`, `ResendEmailService`, `ConsoleEmailService`, `ConsoleMessagingService`, `MagicLinkTokenService`, or `ResendOptions` under `BuildingBlocks/`.  
2. Messaging module owns email + channel transport adapters and registers them.  
3. Commerce owns portal magic-link port + HMAC impl and registers them.  
4. Communications still configures BYOK and suppressions; still mints portal magic links via Commerce port; still owns Resend inbound webhook.  
5. Host composition is thinner; module DI is the source of truth for these services.  
6. Architecture tests green; existing dunning/portal tests green; smoke paths in §9 OK.  
7. `docs/009-building-blocks-ownership.md` updated (Moved, not Deferred).  
8. F12 checklist / FUTURE-WORK FW-3 items 4–6 marked done when code lands.  
9. **No** WhatsApp product implementation shipped under this banner.  
10. **No** brand HTML remains in BuildingBlocks.

---

## 13. Executive recommendation (short)

1. **Do not keep product-shaped email/messaging ports in BuildingBlocks** — single consumer (Messaging) and brand/BYOK policy make them module code.  
2. **Owner split:** Messaging = send + brand wrap + channel console; Communications = BYOK/config/content/suppressions/inbound webhooks; Commerce = portal magic-link tokens.  
3. **PR order:** Magic-link → IMessagingService → Resend/email/brand → docs.  
4. **Thin BB port:** reject for email/messaging given current graph; only revisit if a second module must send outside `DispatchMessageIntegrationEvent`.  
5. **Do not** merge modules, add Email module, or productize WhatsApp in this track.

---

## 14. Evidence index (grep / read anchors)

| Claim | Evidence |
|-------|----------|
| Sole `IEmailService` runtime consumer is Messaging dispatch | `Modules/Messaging/Infrastructure/EventHandlers/DispatchMessageIntegrationEventHandler.cs` |
| Host registers Resend + console messaging + magic-link | `src/Lazuar.Api/Program.cs` ~63–103 |
| Brand HTML “Powered by Lazuar” | `BuildingBlocks/Application/EmailTemplateBuilder.cs` |
| Magic-link API is subscription-shaped | `BuildingBlocks/Application/IMagicLinkTokenService.cs` |
| Magic-link secret = Jwt:Secret, 24h | `BuildingBlocks/Infrastructure/MagicLinkTokenService.cs` |
| Communications mints portal tokens for dunning | `FulfillmentRequestedIntegrationEventHandler.cs` |
| Commerce validates portal tokens | `PublicPortalEndpoints.cs`, `CancelPortalSubscriptionCommandHandler.cs` |
| BYOK stored in Communications | `TenantEmailConfiguration`, `SaveEmailConfigCommand` |
| Org tag for webhooks | `ResendEmailService` tags + `PublicComplianceEndpoints` comment |
| Ownership map deferred move | `docs/009-building-blocks-ownership.md` §3, §7 |
| FW-3 order | `plans/004-maintenance/FUTURE-WORK.md` FW-3 table rows 4–6 |
| 00.4 freeze | `plans/004-maintenance/decisions.md` §00.4 |
| ConsoleEmail not selected | Program registers only `ResendEmailService` |
| WhatsApp default off | `appsettings.json` `Messaging:WhatsAppEnabled: false` |

---

*End of analysis. Implementation should open PR-1 against this plan without expanding into LLM, metrics, or WhatsApp product work.*
