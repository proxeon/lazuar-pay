# C12 — POST CHIP purchases

**Track:** CHIP · **Depends:** C10, C11  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-GW-003  
**Goal:** Hosted `checkout_url` from `https://gate.chip-in.asia/api/v1/purchases/`.

---

## C12.1 HTTP

- [ ] `POST https://gate.chip-in.asia/api/v1/purchases/`
- [ ] Header `Authorization: Bearer {Unprotect(Ciphertext)}`
- [ ] JSON body (snake or CHIP’s expected keys — Hub used `brand_id`, `client`, `purchase`, `success_redirect`, `failure_redirect`, `cancel_redirect`)
- [ ] `brand_id` = `PublicMerchantId`
- [ ] `client.email` / `client.full_name` from checkout payer (C30)
- [ ] Read `checkout_url` and `id` from the response
- [ ] Missing `checkout_url` → throw `InvalidOperationException` (Start → 503)
- [ ] Non-success HTTP → 503 with a short error, do not leak the full secret

## C12.2 Must not

- [ ] Do not use a different CHIP host without amending A00
- [ ] Do not send `force_recurring` / `skip_capture` (C15)

## C12.3 Exit

- [ ] Method exists
- [ ] Unblocked for C13–C16, C17
