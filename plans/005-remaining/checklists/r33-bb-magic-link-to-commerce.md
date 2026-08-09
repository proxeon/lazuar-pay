# R33 — Magic-link token service → Commerce

**Track:** BB · **Analysis:** `../04-bb-email-messaging-move.md`  
**Consumers:** Commerce portal validate; Communications dunning mint

---

## R33.1 Move

- [ ] `IMagicLinkTokenService` + HMAC impl → Commerce (Contracts port if Communications needs it)
- [ ] Preserve wire format + secret source (parity freeze)
- [ ] Communications uses Contracts not BB

## R33.2 Tests

- [ ] Portal magic link + dunning mint tests green

## R33.3 Exit

- [ ] No magic-link product shapes in BB
