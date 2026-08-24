# fb15 — Billplz localhost callback start is 400, no PSP HTTP

**Track:** Fill Billplz · **Depends:** S14  
**Analysis:** 09 method 24; B15; **this** is the test the current method name pretended to be  
**Goal:** `RailTests.Billplz_localhost_callback_start_is_400_without_psp_http`

---

## fb15.1

- [ ] Do **not** use default factory PublicBaseUrl
- [ ] `UseSetting("Pay:PublicBaseUrl", "http://localhost:8081")` on a factory subclass or extra property
- [ ] PUT billplz, start with email
- [ ] 400, body contains `callback base not public`, `Psp.LastUri` is null

## fb15.2 Same method, extra POSTs (not three classes)

- [ ] `https://127.0.0.1/`
- [ ] `https://foo.lazuar-local-dev.com`

## fb15.3 Must not

- [ ] Do not assert localhost on `Billplz_paid_form_and_localhost_blocked`
- [ ] Do not port `PublicDnsFallback`

## fb15.4 Exit

- [ ] Green
