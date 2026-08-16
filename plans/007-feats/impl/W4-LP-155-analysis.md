# W4-LP-155 — WhatsApp via Meta Cloud (or do not ship a channel)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-155`. Tracker: *WhatsApp via Meta Cloud* — Lazuar **N**. Alias `LP-MSG-003`. Pair with [W4-LP-074](./W4-LP-074-analysis.md).  
**Not this ID:** SMS (`LP-156` refuse). Marketing blasts (`LP-157` refuse). WATI / respond.io inbox. Console stub as “connected.” Decision **00.4** freeze (must **reopen** before code).

**Invariant:** `IMessagingService` either calls **Meta Cloud API** (utility templates, E.164, template namespace) **or** we delete every live-product claim. A third state (console log + README hero) is forbidden. `ConsoleMessagingService.IsBillable` stays `false` until Meta is real.

---

## 0. Scope lock

In scope (if we **build**):

- `MetaCloudMessagingService : IMessagingService`  
- BYOK: tenant WABA token + phone-number-id (same shape as Resend) **or** one platform app + per-tenant WABA (pick **tenant BYOK** — matches CaaS)  
- Template send only (utility). No session-message spam.  
- `Messaging:WhatsAppEnabled` true only when adapter ≠ console **and** credentials present  
- Credits: existing `WhatsAppSend` deduct after **accepted** Graph send  
- Delivery log SENT/FAILED from Graph

In scope (if we **delete claims**):

- README hero, Phase 1 “Native WhatsApp,” ADR 020 interactive FPX-in-chat as current  
- Default campaign WHATSAPP steps / builder option  
- Keep console + flag false + “not connected” (already honest in the step editor)

Out of scope either way:

- Interactive “pay inside WhatsApp” buttons (LP-MSG-004 / ADR 020) in v1 — URL in template body is enough  
- Merge Messaging into Communications  
- SMS

---

## 1. Verdict

Transport is `ConsoleMessagingService` (`[MESSAGING/SMS]` log). Dispatch skips when `WhatsAppEnabled=false` (default). Decision 00.4: no production WhatsApp through ~2027-02. README still sells automated WhatsApp dunning.

**N** is correct. Wave 4 is the reopen of 00.4 **or** a docs purge. Implementers must choose in the PR title: `feat(wa): meta cloud` **xor** `docs: remove whatsapp claims`.

---

## 2. Current files

| Path | Role |
|------|------|
| `IMessagingService` | `SendMessageAsync(recipient, text)` — **too thin for templates** |
| `ConsoleMessagingService` | Stub |
| `DispatchMessageIntegrationEventHandler` | Flag + credits + log |
| `appsettings.json` | `WhatsAppEnabled: false` |
| `DunningStepEditor` | “Send WhatsApp (not connected)” |
| `DunningStepDispatcher` | Demotes WHATSAPP → email |
| README / ADR 020 / 021 | Claims vs keep-as-roadmap |

---

## 3. Exact gaps (build path)

| # | Gap |
|---|-----|
| G1 | Port cannot send `template` + components + language |
| G2 | No Graph client / credentials store |
| G3 | No approved utility template names  
| G4 | 00.4 still locked |
| G5 | Marketing contradicts the stub |

---

## 4. Recommended model

**Build (only after 00.4 reopen + a paying tenant):**

```
IMessagingService  +=  SendTemplateAsync(toE164, templateName, language, components)
MetaCloudMessagingService
  POST graph /{phone-number-id}/messages
  { type: template, template: { name, language, components } }
Communications still renders nothing — Commerce passes template + vars
Do not send free-text outside 24h window
```

Ops: WhatsApp settings next to Email (token last-4, phone-number-id, test ping). Flag on iff `IsBillable`.

**Delete-claims path (default if no tenant):**

- README hero + Phase 1 + “Native WhatsApp Dunning”  
- Docs product-lines  
- Optional: remove WHATSAPP from step `<option>` (keep enum for later)  
- Leave 00.4 closed  

---

## 5. Minimal code changes

### If Meta

| File | Change |
|------|--------|
| `decisions.md` 00.4 | Reopen dated note |
| Port + Meta adapter + DI | Replace console when configured |
| One/Communications settings | BYOK fields |
| Dispatch handler | Template path; fail if free-text and no session |
| Tests | HTTP fake 200 → SENT + credit; 401 → FAILED no silent skip |

### If delete

| File | Change |
|------|--------|
| `README.md` | Hero + Phase 1 |
| `apps/lazuar-docs` | No live WA |
| Builder option | Already honest — keep |

Must not: Twilio/WATI “just for now”; set `WhatsAppEnabled=true` on console.

---

## 6. Tests

Build: Graph payload shape; E.164 reject; credits only if `IsBillable`; flag false still SKIPPED.  
Delete: grep CI optional — not required; review README.

---

## 7. Acceptance

**Either:**

1. A sandbox WABA delivers a utility template to a real phone, log SENT, credit deducted; flag true only with keys.  

**Or:**

2. Public README / docs / pricing do not claim WhatsApp dunning; flag remains false; console never billed.

Tracker **N → Y** only on (1). On (2) stay **N** and add a note “claims removed.”

---

## 8. Order

Decide build vs delete **before** LP-074. Do not implement dunning WA steps on a stub.

Do **not** implement from this file.
