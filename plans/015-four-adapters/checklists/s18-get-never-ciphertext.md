# S18 — GET never returns ciphertext

**Track:** Schema · **Depends:** S10, S16  
**Analysis:** [00](../00-what-must-be-done.md) §3.2  
**IDs:** NP-GW-009  
**Goal:** Metadata only.

---

## S18.1 JSON

- [x] `GET /v1/orgs/{orgId}/gateway` has **no** `secret`, `ciphertext`, `webhook_secret`, PEM, `sk_`, Bearer token
- [x] May include: `org_id`, `provider`, `last4`, `configured`, `capability`, `public_merchant_id`, `environment`, `webhook_configured` (boolean)
- [x] Member can GET (`RequireMemberAsync`)
- [x] Writer PUT (`RequireWriterAsync`)

## S18.2 Test

- [x] Hermetic GET after PUT asserts the body does not contain the plaintext secret or `whsec_`
- [x] Assert `last4` is API key last4

## S18.3 Exit

- [x] GET assertion green
- [x] Unblocked for P14
