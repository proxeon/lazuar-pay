
# LHDN e-Invoicing Module (The "Tax Complier")

## 1. Overview
The `Lhdn` module acts as a strict, high-performance **Infrastructure Port and Compliance Gateway** for Malaysian e-Invoicing (MyInvois). It translates internal application events (like B2B invoice generation, automated B2C consolidated monthly runs, and partner commission payouts) into strictly regulated OASIS UBL 2.1 XML payloads, validates them locally against official government schemas, and manages asynchronous transmission and status polling.

---

## 2. Supported Document Types

Product publishers today file **type 01** (B2B invoice / B2C consolidated) and **type 02** (credit note on refund). Other codes have UBL strategies in the factory but **no ops composer and no production publisher**.

### Standard Workflows
*   ✅ **Standard & Consolidated Invoice (01):** Live. B2B pay publishes `B2bTaxInvoiceRequested`; the 28th job publishes `ConsolidatedInvoiceIssued`.
*   ✅ **Credit Note (02):** Live. Refund handler hardcodes `_02`.
*   ⚠️ **Debit Note (03) / Refund Note (04):** Strategy-only. Factory routes them to `CreditNoteStrategy`. Nothing in this repo submits 03 or 04.

### Self-Billed Workflows (Affiliates & Contractors)
*   ⚠️ **Types 11–14:** Strategy-only (`SelfBilledInvoice` / `SelfBilledCredit`). `ViewModelMapper` can entity-swap, but no production publisher files them. Do not sell self-billed affiliate invoices.

A type `01` with a blank buyer TIN is treated as B2C consolidated (General Public). That is not a self-bill path.

---

## 3. Pending/Future Roadmap

### Cryptographic Signatures (XAdES v1.1)
*   ❌ **Signatures (XMLDSig/XAdES):** Unimplemented.
*   *Staging Status:* During our architectural stabilization, we bypassed the V1.1 signature pipeline in favor of absolute V1.0 stability. The XML templates already contain the `<ext:UBLExtensions>` and `<!-- SIGNATURE_PLACEHOLDER -->` blocks wrapped in `{{ if document_version == "1.1" }}` conditionals, keeping the infrastructure fully prepared. 
*   *Action Required:* Once the business procures official **Sandbox Test Certificates (.p12)** from Pos Digicert, MSC Trustgate, or TM Node, the cryptographic signing (C14N canonicalization, hashing, and RSA-SHA256 signing) can be safely activated and verified against the gateway.

---

## 4. Key Architectural Decisions

### A. Strict XML Templating (Scriban Engine)
We treat B2B XML generation as a text-rendering problem rather than using programmatic builders or rigid C# `XmlSerializer` structures. We maintain raw "Golden Master" XML templates as embedded resources.
*   **Absolute Readability:** The templates are plain text files, allowing accountants, compliance auditors, and non-developers to inspect and verify them without navigating compiled C# code.
*   **Security & Escaping:** The engine utilizes a singleton `ScribanTemplateRendererService` that pre-compiles templates to eliminate CPU spikes and registers an automatic `xml_escape` filter to completely neutralize XML Injection attacks.

### B. In-Memory XSD Pre-flight Validation
To prevent rate-limit exhaustion and database pollution with corrupt data, we implement the `UblValidatorService` as an in-memory shield.
*   **Singleton Schema Compilation:** The validator loads and compiles the complete, multi-tiered OASIS UBL 2.1 schema set (`.xsd` files) into an `XmlSchemaSet` exactly *once* during application startup.
*   **Deterministic Resolver:** Our custom `EmbeddedResourceXmlResolver` parses relative file locations internally and handles standard W3C schema `<!DOCTYPE>` declarations by dynamically stripping them in-memory, bypassing standard .NET DTD security restrictions safely.

### C. Byte-to-Base64 Cryptographic Integrity
To prevent database ORMs or operating systems from silently altering the line endings (converting `\r\n` to `\n`) of our saved XML—which would instantly invalidate LHDN's `documentHash` checks—all strategy engines strictly normalize text to `\n` line endings before hashing. 

When the background `LhdnSubmissionJob` transmits the payload, it encodes the string strictly into UTF-8 bytes to guarantee absolute cryptographic alignment between the Base64 payload and the calculated hash.

---

## 5. Outbound customer webhooks — One durable path (end-state A)

**Locked decision:** platform durable delivery lives in **One** (`WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob`). LHDN e-invoice lifecycle customer webhooks use that path only (R42 enqueue + **R43** fire-and-forget retired).

**Path (current):**

* **Mechanism:** `DispatchExternalWebhookCommandHandler` publishes `OutboundWebhookRequestedIntegrationEvent` on `LhdnEventBus` → One `OutboundWebhookEventHandlers` → durable `WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob`.
* **Trigger:** `LhdnStatusPollingJob` → `DispatchExternalWebhookCommand` on MyInvois **VALID** / **INVALID**.
* **Registry (delivery):** `one.TenantWebhookEndpoints` (legacy rows backfilled via R41). `lhdn.WebhookSubscriptions` is **not** POSTed; table retained until optional later drop / façade period.
* **Events:** `invoice.valid`, `invoice.invalid` only (`TargetUrl: null` fan-out).
* **Payload:** data-only snake_case (`internal_id`, `lhdn_uuid`, `status`, `qr_link`, `error_message`); One wraps platform envelope.
* **Signing / retries:** One Standard Webhooks–style (`t=,v1=` HMAC over `{timestamp}.{body}`); retries/DLQ via One dispatcher.
* **Retired (R43):** Lhdn `WebhookSenderService` / `IWebhookSenderService` (fire-and-forget body-only HMAC). No `RecordWebhookFailed("lhdn")` call sites remain.

See: `plans/005-remaining/webhook-convergence-decisions.md`, `plans/005-remaining/r42-notes.md`, `plans/005-remaining/r43-notes.md`.

---

## 6. Developer API keys (platform-owned; dual-read closed — R05)

* **Mint/list/revoke SSoT is One** (`one.ApiCredentials` via `IApiCredentialService`). Lhdn `GET/POST/DELETE /lhdn/api-keys` and obsolete `GenerateApiKeyCommand` / `RevokeApiKeyCommand` / `ListApiKeysQuery` are **façades** — they do not insert into `lhdn.DeveloperApiKeys`.
* **Host auth:** dual-read is **closed (R05)**. Middleware authenticates against **One only**; pure Lhdn-local keys get **401**. Integrators must use One-minted keys (or Lhdn façade mint).
* **Legacy table:** `lhdn.DeveloperApiKeys` may still exist in DB for audit/history until **R06** archive/drop (≥ 30 days after One-only in prod). Host no longer reads it for auth.
* **Revoke events:** host no longer subscribes to Lhdn `ApiKeyRevokedIntegrationEvent`; cache eviction is One event only.
* **DEPLOY gate:** One-only code must not go live on an env until Q8 `active_legacy_only = 0` (or signed residual quarantine). See `plans/005-remaining/r05-notes.md`.
* **Design:** `plans/004-maintenance/api-key-cutover-design.md`, `plans/005-remaining/01-api-key-one-only-cutover.md`.

---

## 7. Directory Structure
```text
Modules/Lhdn/
├── Application/             # Command and Query Handlers, Ports
├── Domain/                  # Core Business rules, Aggregates (TaxDocument)
├── Infrastructure/          # Data Access, Gateways, Workers
│   ├── Schemas/             # Embedded OASIS UBL 2.1 .xsd files
│   ├── Services/            
│   │   └── Strategies/      # Document strategies and mapping logic
│   └── Templates/           # Raw Scriban .xml templates (Dumb views)
```

---

## 8. Community & Technical References
*   [OASIS UBL 2.1 Specification](https://www.datypic.com/sc/ubl21/s-UBL-CommonAggregateComponents-2.1.xsd.html)
*   [LHDN Official e-Invoicing SDK Portal](https://sdk.myinvois.hasil.gov.my/)
*   [allaboutevemirolive/lhdn-info](https://github.com/allaboutevemirolive/lhdn-info)
*   [ERPGulf/myinvois](https://github.com/ERPGulf/myinvois)
*   [zahidaramai/MyInvoice-SDK-Middleware](https://github.com/zahidaramai/MyInvoice-SDK-Middleware)
*   [ryzncodes/lhdn-e-invoice-guide](https://github.com/ryzncodes/lhdn-e-invoice-guide)
