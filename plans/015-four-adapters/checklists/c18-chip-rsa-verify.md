# C18 — CHIP X-Signature RSA-PEM verify

**Track:** CHIP · **Depends:** C11, P21, H10  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-GW-004  
**Goal:** Steal Hub `RSA.ImportFromPem` + SHA256 PKCS1. Not HMAC.

---

## C18.1

- [ ] Header `X-Signature` (case-insensitive) base64
- [ ] Missing header → 400
- [ ] `SecretBox.Unprotect(WebhookCiphertext)` is the PEM
- [ ] `rsa.VerifyData(bodyBytes, signatureBytes, SHA256, RSASignaturePadding.Pkcs1)`
- [ ] Invalid → 400 `"invalid signature"` (not 500)
- [ ] Verify **raw** body bytes UTF-8 (same string used for JSON parse)

## C18.2 Test key

- [ ] Generate an ephemeral RSA key in the test (or fixture PEM in tests folder) — **not** a production CHIP PEM
- [ ] Sign the test body; assert 200 path can run (C19)
- [ ] Wrong signature → 400 (C27)

## C18.3 Must not

- [ ] Do not reuse Stripe `EventUtility`
- [ ] Do not treat Billplz `x_signature` HMAC as CHIP (different algorithm, same English)

## C18.4 Exit

- [ ] Verify helper + C27
- [ ] Unblocked for C19
