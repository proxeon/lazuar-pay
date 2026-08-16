# W2-LP-117 — done

Default submit stays **unsigned XML 1.0**. `Lhdn:Signing` is `Off`. Auto + decryptable `.p12` signs **JSON UBL 1.1** (not XML XAdES — LHDN XML-DSig is known-broken). Missing/broken cert: honest skip to 1.0. Explicit `document_version=1.1` without cert/Auto is 400. Placeholder `<!-- SIGNATURE_PLACEHOLDER -->` is never submitted. Worker `format` is JSON vs XML from payload. Ops Legal card states signed vs unsigned honestly.

## Tests run

- Auto + no cert → unsigned 1.0; explicit 1.1 without cert → 400; self-signed JSON has SignatureValue — **ok**

Not committed. Not pushed.

Tracker `LP-117` **N → P** (honesty). **Y** only after sandbox MyInvois accepts JSON 1.1.
