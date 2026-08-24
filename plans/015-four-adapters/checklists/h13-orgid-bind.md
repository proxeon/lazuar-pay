# H13 — checkout.OrgId must equal path {orgId}

**Track:** Harden · **Depends:** H12  
**Analysis:** [00](../00-what-must-be-done.md) §3.3; [014/00](../../014-evals/00-evaluation.md) P0-2  
**IDs:** NP-API-005  
**Goal:** A webhook for org A must not pay org B’s checkout.

---

## H13.1 Live

- [ ] Path is `/v1/webhooks/{provider}/{orgId}`
- [ ] Resolve checkout id from Stripe `client_reference_id` / metadata `checkout_id` (and later CHIP metadata, Billplz query, etc.)
- [ ] Load checkout; if `checkout.OrgId != orgId` → **400** (or 404), **do not fulfill**
- [ ] Do not insert a **paid** unique row for a mismatched org (or insert as ignored and never fulfill — pick one and test)
- [ ] Stripe metadata `org_id` if present must match path `{orgId}` or refuse

## H13.2 Test

- [ ] Seed org `t1` checkout; POST webhook path `/v1/webhooks/stripe/t2` with that checkout id in metadata
- [ ] Assert not paid, no `RCPT-` for either org (or only t2 has an ignored event — **not** t1 paid)

## H13.3 Must not

- [ ] Do not fulfill by checkout id globally without org check
- [ ] `FulfillPaidAsync` should receive orgId or the handler must check before calling

## H13.4 Exit

- [ ] Cross-org test green
- [ ] Unblocked for H14
