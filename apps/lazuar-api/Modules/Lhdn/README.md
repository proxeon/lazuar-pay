
# LHDN e-Invoicing Module (The "Tax Complier")

## 1. Overview
The `Lhdn` module acts as a strict, high-performance **Infrastructure Port and Compliance Gateway** for Malaysian e-Invoicing (MyInvois). It translates internal application events (like B2B invoice generation, automated B2C consolidated monthly runs, and partner commission payouts) into strictly regulated OASIS UBL 2.1 XML payloads, validates them locally against official government schemas, and manages asynchronous transmission and status polling.

---

## 2. Supported Document Types

The module natively supports both standard and self-billed workflows under the **LHDN V1.0 (Unsigned)** specification, fully covering your platform's core operational, customer billing, and affiliate payout needs.

### Standard Workflows
*   ✅ **Standard & Consolidated Invoice (01):** Supported via `StandardInvoiceStrategy.cs` and `ConsolidatedInvoiceStrategy.cs`.
*   ✅ **Credit Note (02):** Supported via `CreditNoteStrategy.cs`.
*   ✅ **Debit Note (03):** Supported via `CreditNoteStrategy.cs` (utilizing dynamic structural routing).
*   ✅ **Refund Note (04):** Supported via `CreditNoteStrategy.cs` (utilizing dynamic structural routing).

*Architecture Note:* In our `DocumentStrategyFactory.cs`, we group `02`, `03`, and `04` to use the same `CreditNoteStrategy` and `CreditNote.xml` template. Because the structural layout of Credit, Debit, and Refund notes is identical under UBL 2.1, the template dynamically injects `{{ doc_type_code }}`. This significantly minimizes template duplication.

### Self-Billed Workflows (Affiliates & Contractors)
*   ✅ **Self-Billed Invoice (11):** Supported via `SelfBilledInvoiceStrategy.cs` and `SelfBilledInvoice.xml`.
*   ✅ **Self-Billed Credit Note (12):** Supported via `SelfBilledCreditNoteStrategy.cs`.
*   ✅ **Self-Billed Debit Note (13):** Supported via `SelfBilledCreditNoteStrategy.cs`.
*   ✅ **Self-Billed Refund Note (14):** Supported via `SelfBilledCreditNoteStrategy.cs`.

*Architecture Note:* Self-billed transactions occur when the Lazuar Tenant acts as the Buyer but issues the invoice *on behalf of* an external unregistered supplier (e.g. paying out an affiliate). Our `ViewModelMapper.cs` automatically executes an **"Entity Swap"**—routing the tenant configuration to the Buyer XML nodes and the incoming partner data to the Supplier XML nodes—allowing you to use standard API payloads transparently.

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

## 5. Outbound customer webhooks — **C freeze** (maintenance 00.2)

**Locked decision:** platform durable delivery lives in **One** (`WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob`). End-state for LHDN is **A** — route e-invoice lifecycle customer webhooks through that One dispatcher.

**Until A ships, LHDN outbound is a frozen special-case:**

* **Mechanism:** fire-and-forget HTTP via `WebhookSenderService` (no delivery outbox, no retries, no DLQ).
* **Trigger:** `LhdnStatusPollingJob` → `DispatchExternalWebhookCommand` on MyInvois **VALID** / **INVALID**.
* **Registry:** `lhdn.WebhookSubscriptions` (module-local; not `one.TenantWebhookEndpoints`).
* **Events:** `invoice.valid`, `invoice.invalid` only.
* **Signing:** HMAC-SHA256 of the raw body → header `X-Lazuar-Signature` as hex (no timestamp). This is **not** One’s Standard Webhooks–style `t=…,v1=…` scheme.
* **Observability:** failures log + `LazuarMetrics.RecordWebhookFailed("lhdn")` — silent dual stacks without metrics are not acceptable under freeze.

### Freeze rules (do not violate without reopening 00.2)

1. Do **not** build a second full Lhdn outbox/signing/retry stack (rejected option **B**).
2. Do **not** “improve” fire-and-forget into durable delivery half-way.
3. Allowed: docs, structured logs/metrics, bugfixes that keep the same shape.
4. End-state **A**: publish LHDN lifecycle through One’s `OutboundWebhookRequestedIntegrationEvent` (or equivalent), migrate integrators to One registry/signing, then retire this path.

See: `plans/004-maintenance/decisions.md` §00.2, `plans/004-maintenance/phase-04-analysis.md`.

---

## 6. Directory Structure
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

## 7. Community & Technical References
*   [OASIS UBL 2.1 Specification](https://www.datypic.com/sc/ubl21/s-UBL-CommonAggregateComponents-2.1.xsd.html)
*   [LHDN Official e-Invoicing SDK Portal](https://sdk.myinvois.hasil.gov.my/)
*   [allaboutevemirolive/lhdn-info](https://github.com/allaboutevemirolive/lhdn-info)
*   [ERPGulf/myinvois](https://github.com/ERPGulf/myinvois)
*   [zahidaramai/MyInvoice-SDK-Middleware](https://github.com/zahidaramai/MyInvoice-SDK-Middleware)
*   [ryzncodes/lhdn-e-invoice-guide](https://github.com/ryzncodes/lhdn-e-invoice-guide)
