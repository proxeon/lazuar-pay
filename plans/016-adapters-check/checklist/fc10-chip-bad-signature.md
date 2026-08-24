# fc10 — CHIP bad RSA is 400

**Track:** Fill CHIP · **Depends:** S13  
**Analysis:** 09 method 10; C27  
**Goal:** `RailTests.Chip_bad_signature_is_400`

---

## fc10.1

- [ ] PUT chip with real PEM
- [ ] POST valid `purchase.paid` JSON, header `X-Signature: aGVsbG8=` (garbage base64)
- [ ] 400, zero documents

## fc10.2 Must not

- [ ] Do not copy Hub `ParseWebhook_BadSignature` as paid

## fc10.3 Exit

- [ ] Green
