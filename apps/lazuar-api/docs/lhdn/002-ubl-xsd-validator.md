
# UBL 2.1 XSD Validator Service

**File:** `Modules/Lhdn/Infrastructure/Services/UblValidatorService.cs`  
**Lifecycle:** `Singleton`

## 1. Overview
The `UblValidatorService` is a high-performance XML validation engine. Its primary responsibility is to intercept dynamically generated XML payloads (from Scriban templates) and validate them against the official OASIS UBL 2.1 XML Schema Definitions (XSD) **in-memory**, *before* they are saved to the database or transmitted to the LHDN MyInvois API.

By catching missing mandatory tags or structural violations locally, we:
1. Prevent database pollution with malformed XML payloads.
2. Provide instantaneous `400 Bad Request` feedback to API callers.
3. Conserve strict LHDN API rate limits by never sending doomed payloads.

## 2. Critical Architectural Decisions

### A. Singleton Lifecycle (Memory & CPU Protection)
The OASIS UBL 2.1 schema is massive, consisting of dozens of interconnected `.xsd` files. Parsing and compiling an `XmlSchemaSet` requires significant CPU cycles and creates Large Object Heap (LOH) allocations.
* **Decision:** The service is registered as a `Singleton`. The `XmlSchemaSet` is compiled exactly **once** during application startup.
* **Result:** Validation during API requests (`.Validate()`) takes microseconds, safely supporting high-throughput B2C batch consolidations.

### B. Embedded Resource Resolution
Cloud environments (like Docker containers) often suffer from absolute file path issues. 
* **Decision:** All `.xsd` files are bundled directly into the compiled `.dll` using `<EmbeddedResource Include="Schemas\**\*.xsd" />`.
* **The Custom Resolver:** UBL schemas use relative paths to import dependencies (e.g., `<xsd:import schemaLocation="../common/UBL-CommonBasicComponents-2.1.xsd"/>`). The `EmbeddedResourceXmlResolver` intercepts these internal HTTP/file requests, extracts the target filename, and serves the corresponding byte stream directly from the assembly's manifest resources.

### C. W3C Digital Signature DTD Security Bypass
The official W3C XML Digital Signature schema (`xmldsig-core-schema-2.1.xsd`) shipped by OASIS contains a legacy `<!DOCTYPE>` (DTD) block.
* **The Problem:** Modern .NET `XmlReader` implementations strictly prohibit DTD processing by default to prevent XML External Entity (XXE) injection attacks. If enabled globally, it introduces security risks; if disabled, the schema compiler crashes.
* **The Solution:** Inside the `EmbeddedResourceXmlResolver`, when the compiler requests the `xmldsig` file, the service reads the file into a string, dynamically strips out the `<!DOCTYPE ... ]>` block in-memory, and returns the sanitized byte stream to the compiler. This completely bypasses the .NET security crash without compromising application safety.

### D. Root-Only Schema Loading (Duplicate Declaration Prevention)
Because UBL schemas are heavily nested and recursively import one another, manually adding every `.xsd` file in the folder to the `XmlSchemaSet` causes "Duplicate Declaration" crashes in the .NET compiler.
* **The Solution:** The constructor explicitly loads **only** the root documents (`UBL-Invoice-2.1.xsd` and `UBL-CreditNote-2.1.xsd`). The `.Compile()` method then relies entirely on our `EmbeddedResourceXmlResolver` to naturally walk the dependency tree, ensuring each shared component is loaded exactly once.

## 3. Usage in the CQRS Pipeline

The validator acts as an interceptor in the Application Layer's Command Handlers (e.g., `SubmitTaxDocumentCommandHandler.cs`):

```csharp
// 1. Generate the raw XML string from the template
var rawXmlString = strategy.Generate(request.Payload, config, documentVersion);
var normalizedXmlString = rawXmlString.Replace("\r\n", "\n");

// 2. Pre-flight Validation
try
{
    _validatorService.Validate(normalizedXmlString, request.Payload.Document_type.ToString());
}
catch (Exception ex) when (ex is not BusinessRuleValidationException)
{
    // Throws a 400 Bad Request cleanly back to the client
    throw new BusinessRuleValidationException(new GenericBusinessRule($"XML Schema Validation Error: {ex.Message}"));
}

// 3. Hash and Persist
var xmlBytes = Encoding.UTF8.GetBytes(normalizedXmlString);
// ...
```

## 4. Maintenance Guide: Updating Schemas
If LHDN or OASIS releases a patch to the UBL 2.1 schemas:
1. Download the new `.xsd` files.
2. Drop them into `Modules/Lhdn/Infrastructure/Schemas/` (overwriting the old ones).
3. Rebuild the application. The `.csproj` wildcard will automatically bundle them, and the `UblValidatorService` will pick them up on the next startup. *No C# code changes are required.*
