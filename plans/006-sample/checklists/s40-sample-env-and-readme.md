# S40 — Sample env contract + README

**Track:** Sample app · **Analysis:** `../06-provision-and-env.md`  
**Depends on:** S31  
**Goal:** Operator can configure sample without reading C#.

---

## S40.1 `.env.example`

- [x] `LAZUAR_HUB_BASE_URL=http://localhost:8080/api/v1` (include `/api/v1`)
- [x] `LAZUAR_SK_TEST_KEY=` or `LAZUAR_API_KEY=` (sk_test_…)
- [x] `LAZUAR_WEBHOOK_SECRET=` (whsec_…)
- [x] `NEXT_PUBLIC_APP_URL=http://127.0.0.1:3020` (or `PUBLIC_APP_URL` if server-only prefer)
- [x] Optional: provision secret vars commented for helper script only
- [x] Optional: `EXTERNAL_PRODUCT=sample-shop`, `EXTERNAL_ORG_ID=…`
- [x] Comments: never NEXT_PUBLIC_ for sk_/whsec_
- [x] Comments: BYOK required in Ops before checkout works

## S40.2 App README

- [x] What this proves / does not prove
- [x] Prerequisites (Hub, provision secret or keys, BYOK, tunnel for real pay)
- [x] Quick start: install, env, `pnpm --filter … dev`
- [x] Provision one-time curl (non-aura external_product + webhook_url)
- [x] Architecture short diagram (text)
- [x] Webhook path chosen in S31 (exact URL for provision)
- [x] Troubleshooting table (PAYMENTS_NOT_CONFIGURED, signature fail, hops)
- [x] Demo-only disclaimer
- [x] Link to lazuar-docs when run-sample page exists

## S40.3 Security

- [x] `.env.local` gitignored (root already covers)
- [x] README: never log full secrets

## S40.4 Exit

- [x] New engineer can configure env from example alone
