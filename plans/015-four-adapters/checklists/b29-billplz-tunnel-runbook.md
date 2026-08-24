# B29 — Billplz tunnel runbook (docs, not product code)

**Track:** Billplz · **Depends:** B15, B14  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** —  
**Goal:** Local dogfood needs public HTTPS. Write it down. Do not invent DNS.

---

## B29.1 Host README

- [x] Document `Pay__PublicBaseUrl` = Cloudflare tunnel (or similar) https origin that forwards to `:8081`
- [x] Billplz dashboard / collection callback is that origin + `/v1/webhooks/billplz/{orgId}`
- [x] Hub off; One on 8080; Pay 8081
- [x] Sandbox `environment=test` + sandbox API key
- [x] Explicit: localhost will 400 (B15)

## B29.2 Must not

- [x] Do not add `lazuar-local-dev.com` to `/etc/hosts` as a “fix”
- [x] Do not claim CI talks to Billplz

## B29.3 Exit

- [x] README section exists
- [x] A99 lived sentence for Billplz is possible
