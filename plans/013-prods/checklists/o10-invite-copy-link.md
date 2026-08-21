# O10 — Invite copy-link

**Track:** One extras · **Depends:** M26  
**Analysis:** [08](../08-one-identity-production.md) §5  
**IDs:** NP-ONE-011, NP-ONE-012, NP-ONE-022  
**Goal:** Second engineer joins via One copy-link. Pay is not an IdP.

---

## O10.1 One HTTP (already exists — call it)

- [x] Invite via One `POST /api/v1/tenants/{tenantId}/members/invite` (human JWT, role `admin` \| `member`)
- [x] Pending / revoke / resend stay on One invite routes
- [x] Copy-link form stays `{origin}/invites/accept?tenant_id=&token=`

## O10.2 Pay chrome (`:5178`)

- [x] Merchant may **deep-link** `lazuar-app` accept **or** show/copy the same link
- [x] Second engineer is invited as `member` (not `owner`; One rejects invite-owner)

## O10.3 Must not

- [x] No Pay `POST /v1/invites`
- [x] No homemade invite email / SMTP stack in Pay
- [x] No Pay `members` / `invites` / `users` table
- [x] No Zitadel InviteUser; do not scrape `GET …/invites` for a raw token

## O10.4 Exit

- [x] Copy-link path documented or wired; One remains membership SoT
- [x] Unblocked for O11
