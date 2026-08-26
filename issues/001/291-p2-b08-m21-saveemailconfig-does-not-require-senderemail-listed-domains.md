---
number: "291"
id: B08-M21
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 291 — B08-M21 — SaveEmailConfig does not require SenderEmail ∈ listed domains

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M21 — P2 — SaveEmailConfig does not require SenderEmail ∈ listed domains

**Where:** `SaveEmailConfigCommand.cs` 73–81.

**What:** 008 recorded this. Still true. Key that can `GET /domains` + `from: gmail.com` saves. Checkout gate then goes green.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`SaveEmailConfigCommandHandler` treats a successful `GET https://api.resend.com/domains` (bearer = tenant key) as “key + domain verified.” It never parses the domain list or checks that `SenderEmail`’s host is one of those domains. Ops copy (`EmailSettingsPage`) tells the merchant they cannot use Gmail/Yahoo; the server will still persist `receipts@gmail.com` if the key can list *some* domain. `HasValidEmailConfigAsync` then returns true when the row is active with non-empty key + sender (`CommunicationsQueryService.cs:117–123`), so hop-1 checkout goes green. Resend will later reject the `from` at send time (or deliver from an unrelated verified domain, depending on their API). 008 recorded this; 128 fixed decrypt/legacy-key false-valid, not sender∈domains.

### Still present?
**STILL BROKEN**

```73:81:apps/lazuar-api/Modules/Communications/Application/Commands/SaveEmailConfigCommand.cs
        var client = _httpClientFactory.CreateClient("Resend");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", plainKeyToValidate);

        var response = await client.GetAsync("domains", ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule("Invalid Resend API Key or Domain not verified on Resend. Please check your credentials and try again."));
        }
```

No further use of `response.Content`. Save then writes whatever `request.SenderEmail` is (`83–101`). Checkout gate:

```117:123:apps/lazuar-api/Modules/Communications/Infrastructure/Services/CommunicationsQueryService.cs
    public async Task<bool> HasValidEmailConfigAsync(Guid tenantId)
    {
        var creds = await GetEmailConfigCredentialsAsync(tenantId);
        return creds is not null
            && creds.IsActive
            && !string.IsNullOrWhiteSpace(creds.SenderEmail)
            && !string.IsNullOrWhiteSpace(creds.ApiKey);
    }
```

Ops UI still claims you cannot use Gmail (`EmailSettingsPage.tsx:92–94`). PUT binds `ctx.TenantId` + `req.Sender_email` (`Endpoints.cs:38–48`). `TenantEmailConfigurationTests` only exercise the aggregate’s Update methods.

### Related files
- `apps/lazuar-api/Modules/Communications/Application/Commands/SaveEmailConfigCommand.cs` — domains GET, no sender check.
- `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints.cs` — `PUT /admin/communications/email-config`.
- `apps/lazuar-api/Modules/Communications/Infrastructure/Services/CommunicationsQueryService.cs` — `HasValidEmailConfigAsync` does not re-check domain.
- `apps/lazuar-ops/src/modules/workspace/pages/EmailSettingsPage.tsx` — lying “no Gmail” banner.
- `apps/lazuar-api/Modules/Messaging/Infrastructure/Email/ResendEmailService.cs` — send-time `from`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/TenantEmailConfigurationTests.cs` / `TenantEmailKeyTests.cs` — no handler HTTP mock.

### Tests
- Existing: `TenantEmailConfigurationTests.UpdateWithoutKey_PreservesEncryptedApiKey`, `UpdateConfiguration_ReplacesKeyAndSender`; `TenantEmailKeyTests.*` (128 decrypt). Commerce tests stub `HasValidEmailConfigAsync` to `true`. **No** `SaveEmailConfigCommandHandler` test that inspects the domains JSON.
- None would fail if `from: gmail.com` still saves. 128’s key tests stay green.
- First regression: mock `GET domains` 200 with `{ "data": [ { "name": "shop.test", "status": "verified" } ] }` + `SenderEmail = receipts@gmail.com` → business rule, no persist. Same JSON + `hello@shop.test` → save. Unverified listed domain should fail. Do not require TypeSpec changes.

### Reproduction today
Arrange: real or mocked Resend key that can `GET /domains` for `shop.test`. Act: Ops Email Provider (or `PUT /api/v1/admin/communications/email-config`) with that key, `sender_email=receipts@gmail.com`, `is_active=true`. Assert: 200 `saved`; GET email-config shows the gmail sender; `HasValidEmailConfigAsync` is true; hop-1 product checkout is not blocked by the comms gate. Send a receipt: Resend error or unexpected `from`.

### Blast radius
Merchants who believe the Ops banner. Buyers: missing or failing Official Receipt / dunning mail after a green checkout (the 128 class of “took money you cannot receipt,” but for sender domain rather than decrypt). Frequency: every BYOK save that pairs a valid key with a public-webmail from. Still **P2** (Resend usually rejects; the lie is the gate). Not a PII leak.

### Suggested fix
Parse the domains payload; require `SenderEmail` host (case-insensitive) to equal a verified domain (or a subdomain of one, if product wants `receipts.shop.test`). Reject `gmail.com` / `yahoo.com` / unmatched hosts with the same business-rule tone as an invalid key. Optionally have `HasValidEmailConfigAsync` refuse senders whose host was never checked (store verified host on the row if you do not want to re-call Resend). Do not implement a homemade e-mandate or Wave 5 WhatsApp. No TypeSpec unless you add a `verified_domain` field to the existing DTO on purpose.

### Evaluation notes
**128 / B08-M05** (`fix/128-email-config-decrypt`) is the sibling “gate is a false valid” ticket — decrypt, not sender∈domains. Do not reopen 128 here. Quotes skipping the gate is also 128’s remaining product choice. Still P2. Not blocked by 292. Source: `plans/009-bugs/08-communications-messaging-crm.md` (user alias `08-comms-crm-messaging.md`).

