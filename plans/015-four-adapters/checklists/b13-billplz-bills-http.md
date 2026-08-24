# B13 — POST Billplz bills

**Track:** Billplz · **Depends:** B12  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** —  
**Goal:** Basic `{apiKey}:` JSON create. Return `url` + bill `id`.

---

## B13.1

- [x] `POST {host}bills`
- [x] `Authorization: Basic base64(apiKey + ":")`
- [x] JSON: `collection_id`, `email`, `name`, `amount` (cents AwayFromZero), `description`, `callback_url` (B14), `redirect_url`, `reference_1` (B17), `reference_1_label`
- [x] Read `url` and `id`
- [x] Missing url → throw → Start 503

## B13.2 Must not

- [x] Do not send `setupFutureUsage`
- [x] Do not use Hub `PublicDnsFallback` HttpClient name

## B13.3 Exit

- [x] Method exists
- [x] Unblocked for B14–B17
