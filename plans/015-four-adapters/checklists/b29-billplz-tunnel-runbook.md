# B29 — Billplz tunnel runbook (docs, not product code)

**Track:** Billplz · **Depends:** B15, B14  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** —  
**Goal:** Local dogfood needs public HTTPS. Write it down. Do not invent DNS.

---

## B29.1 Host README

- [ ] Document `Pay__PublicBaseUrl` = Cloudflare tunnel (or similar) https origin that forwards to `:8081`
- [ ] Billplz dashboard / collection callback is that origin + `/v1/webhooks/billplz/{orgId}`
- [ ] Hub off; One on 8080; Pay 8081
- [ ] Sandbox `environment=test` + sandbox API key
- [ ] Explicit: localhost will 400 (B15)

## B29.2 Must not

- [ ] Do not add `lazuar-local-dev.com` to `/etc/hosts` as a “fix”
- [ ] Do not claim CI talks to Billplz

## B29.3 Exit

- [ ] README section exists
- [ ] A99 lived sentence for Billplz is possible
