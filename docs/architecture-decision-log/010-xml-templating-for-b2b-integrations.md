
# ADR 010: XML Templating for B2B and Tax Authority Integrations (LHDN/UBL)

**Status:** Accepted  
**Date:** January 2025  

## Context

As the platform integrates with enterprise B2B networks, banking systems (ISO 20022), and government tax authorities (e.g., Malaysia LHDN UBL 2.1), we must generate highly complex, strictly regulated XML payloads.

Traditionally, developers approach this by:
1. **Programmatic Node Builders:** Manually constructing the XML tree node-by-node in code.
2. **Auto-Serialization:** Decorating C# DTOs with `[XmlElement]` and relying on `XmlSerializer`.
3. **Proprietary JSON-to-XML APIs:** Relying on authority-specific JSON endpoints (e.g., LHDN's UBL-JSON format).

These approaches introduce severe friction in enterprise environments:
* **The "Black Box" Problem:** Tax accountants and business analysts cannot read nested C# or TypeScript builders to verify compliance. 
* **The Maintenance Trap:** Tax schemas change frequently. Hardcoding XML structures into the application layer means a single tag update requires recompiling, testing, and redeploying the entire microservice.
* **Serialization Failures:** Different authorities require highly specific native attributes (e.g., `schemeID="BRN"` vs `schemeID="0196"`). Auto-serializers frequently drop, mangle, or incorrectly format these attributes and namespaces.
* **Vendor Lock-in:** Using proprietary API formats (like LHDN's JSON flavor) prevents us from reusing our integration logic for global networks (like PEPPOL) that strictly require raw UBL XML.

## Decision

**We will treat B2B XML generation strictly as a text-rendering problem using XML Templating.**

Instead of programmatic builders or serializers, we will maintain raw, pre-verified "Golden Master" XML templates. The application layer will pass flat variables to a templating engine (e.g., Scriban, Liquid, or explicit string injection) to populate the document. 

## Rationale

1. **Absolute Readability:** The XML template is a plain text file that visually mirrors the exact output. Auditors and non-developers can inspect and verify it without reading C# code.
2. **Agility:** Updating a validation rule, adding a tax exemption tag, or modifying a namespace only requires updating a static text file, completely decoupling schema updates from core application deployments.
3. **Flawless Formatting:** We retain 100% control over namespaces, prefixes (`cbc:`, `cac:`), and required native attributes, bypassing the quirks of XML serialization engines.

## Implementation Guidelines

When implementing an XML template integration, developers must adhere to the following safety rules:

### 1. Mandatory XML Escaping
If an injected variable contains special characters (e.g., `&`, `<`, `>`), it will instantly invalidate the XML structure. You must ensure all injected data is properly escaped.
* *Example:* A company name like `A & B Trading` must be injected as `A &amp; B Trading`.

### 2. Local Two-Phase Validation
Never rely on the external API (like LHDN or a Bank) to validate your payload. Tax authority APIs are heavily rate-limited and slow.
Before transmitting the rendered XML, you must validate it locally:
* **Phase 1 (Syntax):** Validate the rendered string against the official XSD schemas.
* **Phase 2 (Business Logic):** Validate the rendered string against the official Schematron (`.sch`/XSLT) rules to verify mathematical totals and conditional rules.

### 3. The Digital Signature Placeholder Pattern (LHDN v1.1)
For documents requiring XML Digital Signatures (XMLDSig/XAdES) such as LHDN v1.1, the signature cryptographic hash is heavily dependent on the exact byte-structure of the document.

The LHDN schema uses an XPath rule `not(//ancestor-or-self::ext:UBLExtensions)` which dictates that the `<UBLExtensions>` block must be completely excluded when calculating the document's hash.

To support this via templates, you must use the **Placeholder Injection Pattern**.

**Critical Cryptographic Rule: XML Canonicalization (C14N)**  
Raw string hashing is highly vulnerable to environment differences (e.g., Windows `\r\n` vs Linux `\n` line endings, or UTF-8 BOM markers). Before calculating the `DigestValue`, the stripped XML string MUST be normalized using the W3C Exclusive XML Canonicalization (C14N) standard. 

**Critical Schema Rule: Element Ordering**  
Per the UBL 2.1 XSD strict sequence rules, the `<ext:UBLExtensions>` block MUST be the very first child element inside the root `<Invoice>` node.

**The Correct Workflow:**
1. **Design the Template:** Place a variable marker where the signature belongs, immediately after the root node.
   ```xml
   <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
            xmlns:ext="urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2">
     <!-- {ubl_extension} -->
     <cbc:ID>INV-001</cbc:ID>
   </Invoice>
   ```
2. **Render Variables:** Populate all business data (amounts, dates, items) but leave `<!-- {ubl_extension} -->` intact. Keep this rendered string in memory.
3. **Strip for Hashing:** Create a temporary copy of the string and replace the placeholder with `string.Empty`.
4. **Canonicalize & Sign:** Canonicalize the stripped string to a byte array, calculate the SHA-256 hash, and cryptographically sign it with the X.509 private key.
5. **Construct Extension:** Build the full `<UBLExtensions>` XML block containing your calculated `DigestValue` and `SignatureValue`.
6. **Final Injection:** Call `.Replace("<!-- {ubl_extension} -->", ublExtensionBlock)` on the original rendered string from Step 2.

```csharp
// Example C# Implementation
string rawXml = _templateEngine.Render(invoiceData); 

// 1. Strip the placeholder completely to prepare for hashing
string xmlToHash = rawXml.Replace("<!-- {ubl_extension} -->", "");

// 2. Canonicalize the XML (Normalize whitespace, quotes, and line endings to W3C standards)
byte[] canonicalizedBytes = _xmlCryptoService.ApplyC14N(xmlToHash);

// 3. Hash and Sign the canonicalized bytes
string digestValue = _cryptoService.CalculateSha256Base64(canonicalizedBytes);
string signatureValue = _cryptoService.SignHashBase64(canonicalizedBytes, privateKey);

// 4. Construct the signature block
string ublExtensionBlock = $"""
  <ext:UBLExtensions>
      <ext:UBLExtension>
          <ext:ExtensionURI>urn:oasis:names:specification:ubl:dsig:enveloped:xades</ext:ExtensionURI>
          <ext:ExtensionContent>
              <!-- Inject digestValue and signatureValue here -->
          </ext:ExtensionContent>
      </ext:UBLExtension>
  </ext:UBLExtensions>
""";

// 5. Inject the completed block into the final document
string finalSignedXml = rawXml.Replace("<!-- {ubl_extension} -->", ublExtensionBlock);
```

### 4. Performance & Memory Management (Validation)
Because the platform will validate thousands of payloads, XSD and Schematron parsing must not occur on a per-request basis.
* **Singleton Compilation:** The `XmlSchemaSet` (for Phase 1 XSD) and `XslCompiledTransform` (for Phase 2 Schematron/XSLT) must be loaded from disk, compiled, and registered as Singletons in the .NET Dependency Injection container at startup. 
* Instantiating new XML schema sets or XSLT transformers on every request will result in severe CPU overhead and Large Object Heap (LOH) memory fragmentation, leading to gateway degradation.
