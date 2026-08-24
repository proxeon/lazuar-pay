# W17 — Read `tenant_id` or `org_id`

**Track:** One HMAC · **Depends:** W10  
**Analysis:** 014: One envelope may use `tenant_id`; Pay reads `org_id`  
**IDs:** —  
**Goal:** Suspend the org One named, even if the JSON key is Hub-era.

---

## W17.1 Live today

- [ ] `doc.RootElement.TryGetProperty("org_id")` only

## W17.2 Change

- [ ] `org_id` if present and non-whitespace
- [ ] else `tenant_id`
- [ ] else do not set pause (still 200 after verify if you insert the delivery — do not 500)
- [ ] One tenant id **is** Pay `org_id` (012 lock)

## W17.3 Must not

- [ ] Do not create a Pay `organizations` table
- [ ] Do not pause **all** orgs on a missing id

## W17.4 Exit

- [ ] Two hermetic vectors: `{org_id}` and `{tenant_id}` both set `ChargesPaused` on `t1`
