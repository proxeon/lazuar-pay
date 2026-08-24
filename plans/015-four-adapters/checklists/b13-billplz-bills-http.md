# B13 — POST Billplz bills

**Track:** Billplz · **Depends:** B12  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** —  
**Goal:** Basic `{apiKey}:` JSON create. Return `url` + bill `id`.

---

## B13.1

- [ ] `POST {host}bills`
- [ ] `Authorization: Basic base64(apiKey + ":")`
- [ ] JSON: `collection_id`, `email`, `name`, `amount` (cents AwayFromZero), `description`, `callback_url` (B14), `redirect_url`, `reference_1` (B17), `reference_1_label`
- [ ] Read `url` and `id`
- [ ] Missing url → throw → Start 503

## B13.2 Must not

- [ ] Do not send `setupFutureUsage`
- [ ] Do not use Hub `PublicDnsFallback` HttpClient name

## B13.3 Exit

- [ ] Method exists
- [ ] Unblocked for B14–B17
