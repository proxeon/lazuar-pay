# S40 — Sample env contract + README

**Track:** Sample app · **Analysis:** `../06-provision-and-env.md`  
**Depends on:** S31  
**Goal:** Operator can configure sample without reading C#.

---

## S40.1 `.env.example`

- [ ] `LAZUAR_HUB_BASE_URL=http://localhost:8080/api/v1` (include `/api/v1`)
- [ ] `LAZUAR_SK_TEST_KEY=` or `LAZUAR_API_KEY=` (sk_test_…)
- [ ] `LAZUAR_WEBHOOK_SECRET=` (whsec_…)
- [ ] `NEXT_PUBLIC_APP_URL=http://127.0.0.1:3020` (or `PUBLIC_APP_URL` if server-only prefer)
- [ ] Optional: provision secret vars commented for helper script only
- [ ] Optional: `EXTERNAL_PRODUCT=sample-shop`, `EXTERNAL_ORG_ID=…`
- [ ] Comments: never NEXT_PUBLIC_ for sk_/whsec_
- [ ] Comments: BYOK required in Ops before checkout works

## S40.2 App README

- [ ] What this proves / does not prove
- [ ] Prerequisites (Hub, provision secret or keys, BYOK, tunnel for real pay)
- [ ] Quick start: install, env, `pnpm --filter … dev`
- [ ] Provision one-time curl (non-aura external_product + webhook_url)
- [ ] Architecture short diagram (text)
- [ ] Webhook path chosen in S31 (exact URL for provision)
- [ ] Troubleshooting table (PAYMENTS_NOT_CONFIGURED, signature fail, hops)
- [ ] Demo-only disclaimer
- [ ] Link to lazuar-docs when run-sample page exists

## S40.3 Security

- [ ] `.env.local` gitignored (root already covers)
- [ ] README: never log full secrets

## S40.4 Exit

- [ ] New engineer can configure env from example alone
