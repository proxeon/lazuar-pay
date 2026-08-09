# R34 — Email + IMessagingService → Messaging

**Track:** BB · **Analysis:** `../04-bb-email-messaging-move.md`  
**Respect:** 00.4 no WhatsApp product work

---

## R34.1 Move

- [ ] `IEmailService`, Resend, ConsoleEmail, ResendOptions → Messaging
- [ ] `EmailTemplateBuilder` brand HTML → Messaging
- [ ] `IMessagingService` + Console → Messaging
- [ ] Host/Messaging DI still resolves for DispatchMessage path
- [ ] Communications BYOK stays in Communications (not moved into BB)

## R34.2 Parity

- [ ] Org tag `org` behavior unchanged
- [ ] BYOK rules unchanged

## R34.3 Tests

- [ ] Messaging dispatch / notify tests
- [ ] Host build

## R34.4 Docs

- [ ] Update 009

## R34.5 Exit

- [ ] BB has no Resend/brand email stack
