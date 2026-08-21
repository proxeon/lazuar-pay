# O10 — Invite copy-link

**Track:** One extras · **Depends:** M26  
**Analysis:** [08](../08-one-identity-production.md) §5  
**IDs:** NP-ONE-011, NP-ONE-012, NP-ONE-022  
**Goal:** Second engineer joins via One copy-link. Pay is not an IdP.

---

## O10.1 One HTTP (already exists — call it)

- [ ] Invite via One `POST /api/v1/tenants/{tenantId}/members/invite` (human JWT, role `admin` \| `member`)
- [ ] Pending / revoke / resend stay on One invite routes
- [ ] Copy-link form stays `{origin}/invites/accept?tenant_id=&token=`

## O10.2 Pay chrome (`:5178`)

- [ ] Merchant may **deep-link** `lazuar-app` accept **or** show/copy the same link
- [ ] Second engineer is invited as `member` (not `owner`; One rejects invite-owner)

## O10.3 Must not

- [ ] No Pay `POST /v1/invites`
- [ ] No homemade invite email / SMTP stack in Pay
- [ ] No Pay `members` / `invites` / `users` table
- [ ] No Zitadel InviteUser; do not scrape `GET …/invites` for a raw token

## O10.4 Exit

- [ ] Copy-link path documented or wired; One remains membership SoT
- [ ] Unblocked for O11
