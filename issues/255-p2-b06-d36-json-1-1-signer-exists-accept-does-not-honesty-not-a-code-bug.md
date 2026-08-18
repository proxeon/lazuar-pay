---
number: "255"
id: B06-D36
severity: P2
status: resolved
resolved_branch: fix/255-lhdn-valid-honesty
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 255 — B06-D36 — JSON 1.1 signer exists; ACCEPT does not (honesty, not a code bug)

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D36 — JSON 1.1 signer exists; ACCEPT does not (honesty, not a code bug)

`JsonUblDocumentSigner` hashes unsigned JSON, RSA-SHA256, appends `UBLExtensions` (`13–50`). Unit test with a **self-signed** cert asserts `SignatureValue` and no placeholder (`MyInvoisLoopTests.cs:205–219`). That is not MyInvois ACCEPT.

`run_all.sh` runs 00, 01, 02, 03, 06, 07 — **skips 04 cert + 05 v1.1** (`run_all.sh:7–12`). `LhdnSandboxE2ETests` is `[Ignore]` and only gets a token + polls a known submission uid. It does **not** submit. It does not assert `acceptedDocuments` or `overallStatus=Valid` (`LhdnSandboxE2ETests.cs:20–63`).

Until `docs/honesty/lhdn-sandbox-valid.md` is replaced, LP-110/111/113/117 stay unproven. That is **not** B06-D36 as a code defect. It is the honesty fence around every VALID-shaped claim.

## Evaluation (current tree, 2026-08-18)

### What the bug is
This is not a code defect. Wave 2 shipped a JSON UBL 1.1 signer and a unit test that signs with a **self-signed** cert and asserts `SignatureValue` / no placeholder. That is not MyInvois ACCEPT and not `overallStatus=Valid`. The sandbox runner still skips the cert + v1.1 scripts. `LhdnSandboxE2ETests` is `[Ignore]` and only fetches a token plus polls a known submission uid — it does not submit. `docs/honesty/lhdn-sandbox-valid.md` is still “not captured.” Until that file is replaced with a redacted VALID artifact, do not mark LP-110/111/113/117 as proven and do not sell “sandbox VALID / signed 1.1 accepted.”

### Still present?
**DOCS / HONESTY ONLY**

Signer + unit test unchanged:

```13:50:apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/JsonUblDocumentSigner.cs
/// MyInvois JSON UBL 1.1 signer. Hashes the unsigned JSON (no UBLExtensions), RSA-SHA256 signs,
/// then appends a signature object. XML XAdES is not used — LHDN's XML-DSig path is known-broken.
...
        invoice["UBLExtensions"] = UblJsonDocumentBuilder.BuildSignatureExtensions(
            Convert.ToBase64String(signature),
            Convert.ToBase64String(certDer),
            hashHex);
```

```205:219:apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/MyInvoisLoopTests.cs
    public void JsonSigner_WithSelfSignedCert_EmitsNonPlaceholderSignature()
    {
        ...
        signed.Content.Should().Contain("SignatureValue");
        signed.Content.Should().NotContain("SIGNATURE_PLACEHOLDER");
```

`run_all.sh` still skips 04 and 05:

```7:12:scripts/lhdn_sandbox/run_all.sh
./00_provision.sh && \
./01_test_b2b.sh && \
./02_test_credit_note.sh && \
./03_test_b2c.sh && \
./06_test_cancel.sh && \
./07_test_self_billed.sh
```

`04_upload_dummy_cert.sh` and `05_test_b2b_v1_1.sh` exist on disk. E2E is still ignored and does not submit (`LhdnSandboxE2ETests.cs:20–63`). Honesty file:

```1:7:docs/honesty/lhdn-sandbox-valid.md
# LHDN sandbox VALID
**Status: not captured.**
No operator run in this repo has produced a MyInvois sandbox document that polls to `VALID` with a scannable QR.
```

### Related files
- `docs/honesty/lhdn-sandbox-valid.md` — the fence; replace this, do not delete the warning.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/JsonUblDocumentSigner.cs` — local signer.
- `apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` — Auto / 1.1 / unsigned 1.0 (`RenderDocument` 207–247).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/MyInvoisLoopTests.cs` — self-signed unit test.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/LhdnSandboxE2ETests.cs` — ignored token+poll.
- `scripts/lhdn_sandbox/run_all.sh` / `04_upload_dummy_cert.sh` / `05_test_b2b_v1_1.sh`.
- `issues/248-p2-b06-d28-lhdn-readme-still-says-signatures-unimplemented-xades.md` — README vs signer.
- `issues/328-p2-b10-x26-tests-that-pin-bugs-tautologies-or-never-run.md` — ignored E2E called out again.

### Tests
- `MyInvoisLoopTests.JsonSigner_WithSelfSignedCert_EmitsNonPlaceholderSignature` — **passes** and does **not** prove ACCEPT.
- `LhdnSandboxE2ETests.GetTokenAsync_ShouldReturnValidJwt_FromLhdnSandbox` / `GetDocumentStatusAsync_ShouldReturnStatus_ForKnownSubmission` — never run in CI (`[Ignore]`). They do not assert `acceptedDocuments` or `overallStatus=Valid`.
- No test would fail because the honesty file still says “not captured.”
- First “regression” is operational, not NUnit: a committed redacted log + screenshot in `docs/honesty/lhdn-sandbox-valid.md` with IRBM uuid, document number, and QR payload; optionally un-Ignore an E2E that **submits** and asserts `acceptedDocuments` + Valid.

### Reproduction today
Read `docs/honesty/lhdn-sandbox-valid.md`. Run `task api:test` (or the MyInvoisLoop fixture): signer unit test is green. Run `scripts/lhdn_sandbox/run_all.sh`: 04/05 are skipped. Do not treat a green unit test or an ignored poll as VALID.

### Blast radius
Sales / tracker honesty. Claiming VALID or “signed 1.1 accepted” from this repo is a lie until the honesty file is replaced. No runtime money bug. Frequency: every demo that points at a VALID badge or LP-110–117.

### Suggested fix
Do not change product code for D36. Land one sandbox ACCEPT+VALID (unsigned 1.0 via `01_test_b2b.sh`, and 04+05 if 1.1 is in scope) and replace `docs/honesty/lhdn-sandbox-valid.md`. Keep the fence until then. Un-ignore E2E only after it submits. Pair README honesty with **248**. No TypeSpec regen. No XAdES project.

### Evaluation notes
Explicitly **not** a code bug. Fence around 012/094/141 VALID-shaped UI and around 248’s signer claim. 091/103 fail-closed deduct does not prove MyInvois ACCEPT. Still P2 as an honesty ticket. Do not mark resolved when the signer test is green.

## Resolution

Honesty file still says **not captured**. It now names the self-signed signer test as not ACCEPT. Architecture test locks the fence. No sandbox VALID artifact was produced.

---

