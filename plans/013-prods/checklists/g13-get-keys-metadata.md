# G13 — GET gateway metadata (never the secret)

**Track:** Rails · **Depends:** G12  
**Analysis:** [06](../06-money-rails.md) §4.1 / §2.8  
**Goal:** List what is stored. **Never** return raw secret.

---

## G13.1 Route

- [ ] `GET /v1/orgs/{orgId}/gateways` (or the G12 twin). Bearer + `authz/check` **member** is enough to **read**
- [ ] 200 JSON snake_case: `provider`, last4 **or** masked hint, `updated_at`
- [ ] May also return `is_active`, `environment`, `has_api_key`, `has_webhook_secret`, `merchant_id` (Brand ID is not secret)
- [ ] Missing config → empty list or 404, not a fake Billplz row

## G13.2 Never the secret

- [ ] Response must **not** contain `sk_`, `whsec_`, CHIP Bearer, or the fixture plaintext
- [ ] Do not fill password fields from GET (steal Hub GET-never-secrets judgment)
- [ ] Test: GET body does not include the PUT secret

## G13.3 Must not

- [ ] No ciphertext in JSON. No Hub `/admin/commerce/payment-config`
- [ ] Do not decrypt into logs

## G13.4 Exit

- [ ] Masked GET is the merchant chrome source (`supports_off_session` may wait for G15)
- [ ] Unblocked for G15 copy on `:5178` (paper 04)
