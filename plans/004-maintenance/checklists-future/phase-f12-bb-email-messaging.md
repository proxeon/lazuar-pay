# F12 — Email / messaging ports ownership (FW-3)

**Goal:** Product email/messaging ownership matches Messaging/Communications.  
**Depends on:** decisions §00.4 (no multi-channel product expansion unless reopened)  
**Do not:** Implement WhatsApp as part of this phase

---

## F12.1 Inventory

- [ ] `IEmailService`, Resend, ConsoleEmail, `EmailTemplateBuilder` locations
- [ ] `IMessagingService` / ConsoleMessaging locations
- [ ] Who calls them (Messaging only vs others)

## F12.2 Move / re-home

- [ ] Move product template HTML builders out of BB Application if still there
- [ ] Place email/messaging implementations under Messaging (or agreed owner)
- [ ] Keep thin technical ports only if multi-module justified
- [ ] Magic-link product shapes → Commerce if still in BB

## F12.3 Tests / DI

- [ ] Host still resolves email for existing flows
- [ ] Module tests for Communications/Messaging still green

## F12.4 Exit

- [ ] 009 map updated
- [ ] No brand/product HTML left in BB
