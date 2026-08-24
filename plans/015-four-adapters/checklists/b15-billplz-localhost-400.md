# B15 — Localhost callback is 400

**Track:** Billplz · **Depends:** B14  
**Analysis:** [00](../00-what-must-be-done.md) §5.2; Hub `BillplzPublicBase.TryResolveCallbackBase`  
**IDs:** —  
**Goal:** Billplz cannot POST loopback. Fail create. Steal fail-closed, not fiction DNS.

---

## B15.1

- [x] If `Pay:PublicBaseUrl` host is `localhost`, `127.0.0.1`, `::1`, loopback, or contains `lazuar-local-dev.com` → **do not** call Billplz
- [x] Start → 400 or 503 with a clear `"callback base not public"` (Hub code `CALLBACK_BASE_NOT_PUBLIC`)
- [x] Scheme must be `https` (http only if you later add an explicit allow-insecure flag — **default off**, and not in this program unless A00 amended)
- [x] Tunnel dogfood is B29

## B15.2 Must not

- [x] Do not rewrite localhost to `lazuar-local-dev.com`
- [x] Do not port `PublicDnsFallback`

## B15.3 Test

- [x] Default local `PublicBaseUrl=http://localhost:8081` + billplz start → 400/503 without HTTP to Billplz

## B15.4 Exit

- [x] Test green
- [x] Unblocked for B29
