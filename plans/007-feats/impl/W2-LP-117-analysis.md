# W2-LP-117 — XAdES V1.1 signing when `.p12` present (feature-flag if no cert)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-117`. Tracker: *XAdES V1.1 signing* — Lazuar **N**. Alias `LP-TAX-010`.  
**Not this ID:** Storing the cert (`UpdateLhdnCertificateCommand` already works). Unsigned V1.0 submit (already the live path). UBL template content besides the signature block.

**Invariant:** Default submit stays **unsigned document version `1.0`**. If and only if the tenant has a decryptable `.p12` **and** a feature flag is on, submit **signed `1.1`**. If XML-DSig remains brittle against MyInvois, sign **JSON UBL** (see `docs/lhdn/000-xml-vs-json.md`) and still call the product “V1.1 signed” — do not ship a placeholder `<!-- SIGNATURE_PLACEHOLDER -->` as a signature.

---

## 0. Scope lock

In scope:

- Feature flag (config + `has_certificate`)
- Real sign path **or** an honest banner “unsigned v1.0 only”
- Fail closed: missing/broken cert → stay 1.0, never send empty UBLExtensions as 1.1

Out of scope:

- Buying Pos Digicert / MSC / TM Node certs in-product
- Claiming MDEC Peppol AP
- Dummy self-signed as production

---

## 1. Verdict

Tracker **N** is correct. Vault encrypts PFX + passphrase. **Nothing calls `GetDecryptedCertificate` on the submit path.** Templates wrap `{{ if document_version == "1.1" }}` + `<!-- SIGNATURE_PLACEHOLDER -->`. `SubmitTaxDocument` defaults `document_version` to `"1.0"`. Worker always `format = "XML"`.

Sandbox script `04_upload_dummy_cert.sh` + `05_test_b2b_v1_1.sh` recorded **“Root element is missing”** from LHDN’s XML-DSig stack (`docs/lhdn/000-xml-vs-json.md`). ADR 010’s inject-after-hash XML plan is the path that already failed.

Recommended implementation: **JSON v1.1 sign when cert present**, XML 1.0 otherwise. Marketing may still say “digitally signed e-invoice (MyInvois 1.1)” if JSON sign clears sandbox. Do not say “XAdES XML” unless XML actually validates.

---

## 2. Current files

| Path | Role |
|------|------|
| `CertificateVaultService.cs` | AES wrap PFX + password |
| `UpdateLhdnCertificateCommand.cs` | `PUT /lhdn/workspaces/{id}/lhdn-certificate` |
| `LhdnTenantConfig.EncryptedPfxBase64` | Stored |
| `GetLhdnTenantConfigQuery` | `has_certificate` flag |
| `Modules/Lhdn/Infrastructure/Templates/*.xml` | Placeholder only |
| `SubmitTaxDocumentCommandHandler` | Version default 1.0; no sign |
| `LhdnSubmissionJob` | XML only |
| `docs/lhdn/000-xml-vs-json.md` | Known XML-DSig failure |
| `apps/lazuar-api/Modules/Lhdn/README.md` §3 | Honest: unsigned |

No `XmlSignatureService` in the live tree (docs mention it as a rejected approach).

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No sign step |
| G2 | 1.1 XML would submit an **empty** signature block if someone sets version |
| G3 | Worker cannot send `format=JSON` |
| G4 | No flag; no ops “signed / unsigned” badge |
| G5 | XML-DSig known-broken |

---

## 4. Recommended model

```
Lhdn:Signing = Off | Auto

Auto && has usable cert:
  document_version = 1.1
  build UBL JSON (or XML if you later fix C14N)
  hash + RSA-SHA256 with private key from vault
  attach UBLExtensions signature object
  submit format=JSON (or XML)
else:
  document_version = 1.0
  unsigned XML (today)

If signing throws: log, do **not** fall back to unsigned 1.1; fall back to 1.0 or fail the submit.
```

Ops Legal / LHDN config: “Certificate on file: yes/no. Submissions: unsigned v1.0” until Auto succeeds once in sandbox.

Do not enable Auto in prod until `05_test_b2b_v1_1` equivalent is green.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `appsettings` `Lhdn:Signing` | `Off` default |
| New `IDocumentSigner` | JSON sign first; XML optional |
| `SubmitTaxDocumentCommandHandler` | Choose version; invoke signer after render |
| `LhdnSubmissionJob` | `format` from document (XML vs JSON) |
| Persist format / version on `TaxDocument` if needed |
| Ops LHDN config (with LP-122) | Flag + has_certificate + last error |
| Guard | Reject `document_version=1.1` from API if no cert / signing off |

Must not: submit placeholder 1.1; use dummy.p12 in prod.

---

## 6. Tests

| Case | Expect |
|------|--------|
| No cert, Signing=Auto | 1.0 unsigned XML |
| Signing=Off, payload 1.1 | 400 or coerced to 1.0 |
| Cert present, Signing=Auto | Document contains a non-placeholder signature node/JSON; hash matches payload |
| Bad passphrase | Fail closed; no PENDING 1.1 empty sig |
| Vault round-trip | Existing `LhdnSecretsVaultTests` stay |

A sandbox module test with a self-signed cert proving **JSON** structure is enough; do not require live MyInvois in CI.

---

## 7. Acceptance

1. Tenants without a cert: unchanged unsigned 1.0. No 1.1 placeholder.  
2. Tenant with `.p12` + flag: sandbox 1.1 **accepted** by MyInvois (JSON or fixed XML — document which).  
3. Ops shows signed vs unsigned honestly.  
4. Tracker **N → P** after 1 (honesty). **Y** only after 2.

If JSON sign is chosen, update tracker label in a note: “V1.1 signed (JSON UBL), not XML XAdES.”

---

Do **not** implement from this file.
