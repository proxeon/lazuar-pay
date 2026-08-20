# 03 — First slice

**Date:** 20 August 2026  

Not an implement order for One. One’s next honesty (staging SMTP, staging proof) is One’s. This is **Pay’s** first living loop on that plane.

## One side (stop after this)

1. Register Pay SPA through One (`POST …/apps` or seed like `lazuar-app`).
2. Sign-in via `:5175`. `GET /me`.
3. “Create workspace” in Pay = `POST /tenants` (or pick an existing membership). One tenant id is Pay `org_id`.
4. Invite a second engineer with One **copy-link**.
5. Mint a scoped `lzr_sk_`; `authz/check` `member` before merchant admin routes.
6. Subscribe to `member.*` and `tenant.suspended` (stop charges if suspended).
7. **Stop** on the One side: no SCIM, no custom FGA types, no npm publish, no hosted SKU.

## Pay side (money)

8. Store BYOK Stripe **or** CHIP/Billplz keys for that tenant.
9. Create a product + pay link.
10. Buyer (no One account) pays on the hosted page.
11. Webhook verifies, idempotent replay no-ops, Pay writes subscription + balanced journal + `RCPT-…` in **one** transaction.
12. Merchant sees the payment and receipt in ops. VIEWER cannot change keys or refund.

## Pass / fail

**Pass:** the dogfood test in [01-product.md](./01-product.md).

**Fail (do not paper over):**

- Pay password form or second org table.
- Buyer created as a Zitadel human.
- Setup session counted as paid.
- Receipt titled Tax Invoice or numbered with a UUID.
- Webhook retry double-journals.
- Merchant sent to `lazuar-admin`.
