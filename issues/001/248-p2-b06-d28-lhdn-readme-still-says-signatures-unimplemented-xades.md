---
number: "248"
id: B06-D28
severity: P2
status: resolved
resolved_branch: fix/248-lhdn-signer-readme
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 248 — B06-D28 — Lhdn README still says signatures unimplemented / XAdES

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D28 — Lhdn README still says signatures unimplemented / XAdES (P2)

`Modules/Lhdn/README.md:32–36` still says XMLDSig/XAdES unimplemented and wait for `.p12`. Wave 2 added `JsonUblDocumentSigner`. Default path is unsigned 1.0, which the README’s “V1.0 stability” half gets right. The “signatures unimplemented” half is stale. Claiming XAdES in a demo is a lie. Claiming “we have no signer” is also a lie.

## Evaluation (current tree, 2026-08-18)

### What the bug is
The Lhdn module README §3 still says cryptographic signatures (XMLDSig/XAdES) are unimplemented and that the next step is to wait for a `.p12` then turn on C14N + RSA-SHA256. Wave 2 already added `JsonUblDocumentSigner` (JSON UBL 1.1, not XML XAdES). Submit’s default is unsigned XML 1.0; `Lhdn:Signing=Auto` plus a decryptable PFX signs JSON 1.1 and refuses to emit `SIGNATURE_PLACEHOLDER`. Saying “we have no signer” is false. Saying “we do XAdES v1.1” is also false. The README is one sentence away from both lies.

### Still present?
**DOCS / HONESTY ONLY**

README §3 is unchanged:

```33:36:apps/lazuar-api/Modules/Lhdn/README.md
### Cryptographic Signatures (XAdES v1.1)
*   ❌ **Signatures (XMLDSig/XAdES):** Unimplemented.
*   *Staging Status:* During our architectural stabilization, we bypassed the V1.1 signature pipeline in favor of absolute V1.0 stability. The XML templates already contain the `<ext:UBLExtensions>` and `<!-- SIGNATURE_PLACEHOLDER -->` blocks wrapped in `{{ if document_version == "1.1" }}` conditionals, keeping the infrastructure fully prepared. 
*   *Action Required:* Once the business procures official **Sandbox Test Certificates (.p12)** from Pos Digicert, MSC Trustgate, or TM Node, the cryptographic signing (C14N canonicalization, hashing, and RSA-SHA256 signing) can be safely activated and verified against the gateway.
```

The signer and DI registration are live:

```13:21:apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/JsonUblDocumentSigner.cs
/// MyInvois JSON UBL 1.1 signer. Hashes the unsigned JSON (no UBLExtensions), RSA-SHA256 signs,
/// then appends a signature object. XML XAdES is not used — LHDN's XML-DSig path is known-broken.
public sealed class JsonUblDocumentSigner : IDocumentSigner
{
    public bool CanSign(LhdnTenantConfig config) =>
        !string.IsNullOrWhiteSpace(config.EncryptedPfxBase64)
        && !string.IsNullOrWhiteSpace(config.PfxPasswordCiphertext);
```

`DependencyInjection.cs:59` registers `IDocumentSigner` → `JsonUblDocumentSigner`. Submit uses it when Auto + cert (`SubmitTaxDocumentCommand.cs:213–247`): explicit 1.1 without cert is 400; Auto failure falls back to unsigned 1.0 unless 1.1 was requested. XML XAdES is still unimplemented — that half of the README is true. The “signatures unimplemented / wait for C14N” half is stale.

### Related files
- `apps/lazuar-api/Modules/Lhdn/README.md` — the lie to edit.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/JsonUblDocumentSigner.cs` — actual signer.
- `apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` — Auto / 1.1 / fallback policy.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/DependencyInjection.cs` — `IDocumentSigner` registration.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/MyInvoisLoopTests.cs` — `JsonSigner_WithSelfSignedCert_EmitsNonPlaceholderSignature`.
- `docs/honesty/lhdn-sandbox-valid.md` — still “not captured” (255).
- `scripts/lhdn_sandbox/04_upload_dummy_cert.sh` / `05_test_b2b_v1_1.sh` — exist; `run_all.sh` skips them.
- `plans/007-feats/impl/W2-LP-117-done.md` — already states JSON 1.1, not XML XAdES.

### Tests
- `MyInvoisLoopTests.JsonSigner_WithSelfSignedCert_EmitsNonPlaceholderSignature` locks local RSA-SHA256 JSON shape, not README text, not MyInvois ACCEPT.
- No test reads `Modules/Lhdn/README.md`.
- No test would fail if the README kept “Signatures unimplemented.”
- First regression: a cheap string test or review checklist that README §3 names `JsonUblDocumentSigner`, says default is unsigned 1.0, and says XML XAdES is **not** used.

### Reproduction today
Open `apps/lazuar-api/Modules/Lhdn/README.md` §3. Then open `JsonUblDocumentSigner` and `SubmitTaxDocumentCommand.RenderDocument`. Assert the README does not mention the JSON signer. Run `JsonSigner_WithSelfSignedCert_EmitsNonPlaceholderSignature` (green, self-signed, not sandbox).

### Blast radius
Demo / sales honesty. A merchant who reads the README will think signing is entirely future work **or** (if they only read the heading) that XAdES is the plan. No money movement. No PII. Frequency: every Lhdn onboarding read.

### Suggested fix
Replace §3 with: default unsigned XML 1.0; optional JSON UBL 1.1 via `JsonUblDocumentSigner` when `Lhdn:Signing=Auto` and a stored `.p12` decrypts; XML XAdES / XML-DSig is known-broken and not used. Do not claim sandbox ACCEPT (that is 255). No TypeSpec regen. Do not implement XAdES in this ticket.

### Evaluation notes
Paired with **255** (signer exists; ACCEPT does not). W2-LP-117-done.md is already more honest than the module README. Still P2 as docs. Not a 161–200 fail-closed residual.

## Resolution

README §3 names `JsonUblDocumentSigner`, default unsigned 1.0, and says XML XAdES is not used. Does not claim sandbox ACCEPT. Locked by `LhdnReadme_NamesJsonSigner_AndDoesNotClaimXades`.

