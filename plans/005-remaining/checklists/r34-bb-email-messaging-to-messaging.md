# R34 — Email + IMessagingService → Messaging

**Track:** BB · **Analysis:** `../04-bb-email-messaging-move.md`  
**Respect:** 00.4 no WhatsApp product work

---

## R34.1 Move

- [x] `IEmailService`, Resend, ConsoleEmail, ResendOptions → Messaging
- [x] `EmailTemplateBuilder` brand HTML → Messaging
- [x] `IMessagingService` + Console → Messaging
- [x] Host/Messaging DI still resolves for DispatchMessage path
- [x] Communications BYOK stays in Communications (not moved into BB)

## R34.2 Parity

- [x] Org tag `org` behavior unchanged
- [x] BYOK rules unchanged

## R34.3 Tests

- [x] Messaging dispatch / notify tests
- [x] Host build

## R34.4 Docs

- [x] Update 009

## R34.5 Exit

- [x] BB has no Resend/brand email stack
