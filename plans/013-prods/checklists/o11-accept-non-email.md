# O11 — Accept without email

**Track:** One extras · **Depends:** O10  
**Analysis:** [08](../08-one-identity-production.md) §5.2, §5.5  
**IDs:** NP-ONE-012  
**Goal:** Token-in-URL accept stays. One SMTP staging is not a Pay blocker.

---

## O11.1 Non-email path (keep)

- [ ] Accept remains One `POST /tenants/{tenantId}/members/accept-invite` `{ token }` after a human JWT
- [ ] Deep-link to `lazuar-app` `/invites/accept?tenant_id=&token=` is enough for Bar B
- [ ] Optional Pay accept page that posts the **same** One API — not required if deep-link works
- [ ] `GET /me/invites` is discovery only (no token); inbox cannot join

## O11.2 SMTP honesty

- [ ] If One staging email never arrives, copy-link still joins
- [ ] Do **not** block Bar B on One `prove-smtp.sh` / MEM-10 / STAGING-PROOF
- [ ] Do **not** paper over SMTP with Pay-sent mail

## O11.3 Must not

- [ ] No Pay mail outbox as invite SoT
- [ ] No rename of copy-link query keys (`tenant_id`, `token`) without a migration

## O11.4 Exit

- [ ] Non-email accept is the documented Bar B path
- [ ] Unblocked for O12
