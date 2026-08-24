# fb24 — Live environment hits www.billplz.com

**Track:** Fill Billplz · **Depends:** S14  
**Analysis:** [`../06-billplz-crosscheck.md`](../06-billplz-crosscheck.md) B12 live host unchecked  
**Goal:** `RailTests.Billplz_live_environment_hits_www_host`

---

- [ ] PUT `environment: live`, FakePsp 200 bill URL
- [ ] Start with email
- [ ] `Psp.LastUri` contains `www.billplz.com` (not sandbox)
- [ ] Must not: infer live from `lazuar.com`
- [ ] Exit: green
