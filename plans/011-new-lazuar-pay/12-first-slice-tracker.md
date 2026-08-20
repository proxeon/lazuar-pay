# 12 — First-slice tracker

**Date:** 20 August 2026  
**Source:** [03-first-slice.md](./03-first-slice.md)  
**IDs:** [11-checklist.md](./11-checklist.md)  
**Schema:** [10-tracker-schema.md](./10-tracker-schema.md)

This is the **ordered** dogfood loop. [11](./11-checklist.md) is the catalog. Flip **Status** here when a step actually ran, and flip the matching IDs in 11.

Pass: merchant via One → keys → buyer (no One account) pays → `RCPT-` + balanced journal → webhook retry no-ops → MEMBER sees ops, VIEWER cannot charge.

---

## One side (S0) — stop after this

| Step | Job | IDs | Status | Notes |
|------|-----|-----|--------|-------|
| 1 | Register Pay SPA through One (`POST …/apps` or seed like `lazuar-app`) | NP-ONE-001, NP-ONE-002, NP-ONE-004 | todo | Not Console |
| 2 | Sign-in via `:5175`. `GET /me` | NP-ONE-003, NP-ONE-005, NP-ONE-006 | todo | Access token as Bearer |
| 3 | “Create workspace” in Pay = `POST /tenants` (or pick existing membership). One tenant id is Pay `org_id` | NP-ONE-007, NP-ONE-009 | todo | No second org table |
| 4 | Invite a second engineer with One **copy-link** | NP-ONE-011, NP-ONE-012, NP-ONE-022 | todo | Keep non-email accept |
| 5 | Mint a scoped `lzr_sk_`; `authz/check` `member` before merchant admin routes | NP-ONE-014, NP-ONE-015 | todo | Explicit scopes |
| 6 | Subscribe to `member.*` and `tenant.suspended` (stop charges if suspended) | NP-ONE-017, NP-ONE-018 | todo | HMAC; do not tail Zitadel |
| 7 | **Stop** on the One side | NP-XX-015, NP-XX-021, NP-XX-022 | refuse (keep) | No SCIM, no custom FGA types, no npm publish, no hosted SKU |

One’s next honesty (staging SMTP, staging proof) is **One’s**. Do not paper over a failed step 4 with a homemade invite.

---

## Pay side (S1) — money

| Step | Job | IDs | Status | Notes |
|------|-----|-----|--------|-------|
| 8 | Store BYOK Stripe **or** CHIP/Billplz keys for that tenant | NP-GW-001, NP-GW-002 or NP-GW-003, NP-GW-009 | todo | Encrypted; VIEWER cannot change |
| 9 | Create a product + pay link | NP-CAT-001 … NP-CAT-005, NP-CHK-006 | todo | MYR |
| 10 | Buyer (no One account) pays on the hosted page | NP-CHK-005, NP-CHK-007, NP-BUY-001 | todo | Fail if Zitadel login appears |
| 11 | Webhook verifies; idempotent replay no-ops; subscription + balanced journal + `RCPT-…` in **one** transaction | NP-GW-004, NP-GW-006, NP-FUL-001, NP-MON-001, NP-DOC-001, NP-DOC-002, NP-API-002 | todo | Empty body = 400 |
| 12 | Merchant sees the payment and receipt in ops. VIEWER cannot change keys or refund | NP-FUL-003, NP-DOC-005, NP-ONE-021, NP-ONE-022, NP-API-004 | todo | |

---

## Pass / fail locks (must stay true)

| Lock | Related IDs | Status |
|------|-------------|--------|
| No Pay password form | NP-XX-007 | refuse |
| No second org table | NP-XX-014 | refuse |
| Buyer is not a Zitadel human | NP-XX-013, NP-CHK-007 | todo / refuse |
| Setup session is not counted as paid | NP-GW-008 | todo |
| Receipt is not titled Tax Invoice; number is not a UUID | NP-DOC-002, NP-DOC-003, NP-XX-003 | todo / refuse |
| Webhook retry does not double-journal | NP-GW-006 | todo |
| Merchant is not sent to `lazuar-admin` | NP-ONE-005, NP-XX-018 | todo / refuse |

If a lock fails, the slice **fails**. Do not mark steps 1–12 `done`.
