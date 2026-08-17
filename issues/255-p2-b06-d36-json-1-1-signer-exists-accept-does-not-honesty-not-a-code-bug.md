---
number: "255"
id: B06-D36
severity: P2
status: open
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

---

