# W4-LP-074 — WhatsApp recovery sequence

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-074`. Tracker: *WhatsApp recovery sequence* — Lazuar **N**. Pair with [W4-LP-155](./W4-LP-155-analysis.md).  
**Not this ID:** Email recovery (`LP-073` done). Interactive in-chat FPX (ADR 020 — later). Marketing blasts. Enabling the flag on `ConsoleMessagingService`.

**Invariant:** A PAST_DUE campaign step `WHATSAPP` either **delivers a Meta utility template** with `{{renewal_link}}` / update-payment URL, or we **stop offering the step as a live channel**. Demoting to email while the UI says WhatsApp is how the lie started.

---

## 0. Scope lock

In scope if LP-155 Meta is live:

- Stop demoting WHATSAPP→email when enabled + template configured  
- Default new-org campaign: optional +3 WHATSAPP **after** email, not instead  
- Hydrate same links as LP-053 (`renewal_link`)  
- Require CRM phone E.164  

In scope if LP-155 is delete-claims:

- Remove or keep-disabled the builder option (already “not connected”)  
- Do not add new WA steps to the default seed  

Out of scope: ALL channel meaning email+WA in one step until Meta works; inbox; 00.4 reopen without 155.

**Blocked on LP-155.**

---

## 1. Verdict

Campaign builder and `ActionType=WHATSAPP` exist. Dispatcher demotes to email or skips. That is orchestration without a channel. Tracker **N** must stay until a phone lights up.

---

## 2. Current files

| Path | Role |
|------|------|
| `DunningStepDispatcher.ResolveEffectiveCommunicationAction` | Demote |
| `PastDueDunningProcessor` | Skip WA if no email body |
| `DunningStepEditor.tsx` | Honest “not connected” |
| `GenerateDefaultDunningCampaigns` | EMAIL + AUTO_CHARGE (no WA in seed — good) |
| `DispatchMessageIntegrationEventHandler` | SKIPPED when flag false |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No Meta send |
| G2 | Demotion hides failed WA as “email worked” |
| G3 | No template name on the step |
| G4 | Phone optional on CRM |

---

## 4. Recommended model

```
DunningStep: WhatsAppTemplateName?  // Meta approved utility
When WhatsAppEnabled && Meta billable:
  WHATSAPP → SendTemplate(to=profile.phone, template, body vars + button URL)
  missing phone → skip + ops log, do not silently email
When not enabled:
  do not show WA as a working action (keep current copy)
```

Do not change EMAIL/AUTO_CHARGE. Do not put WA on day 0 by default (utility quality + cost).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| Dispatcher | If 155 live, do **not** demote when template+phone present |
| Step editor | Template name field; hide if flag false |
| Dispatch | Template API |
| Tests | Enabled+Meta → messaging called; no phone → skip not email |

Delete-claims: no engine change.

Must not: `WhatsAppEnabled=true` in appsettings for production default.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Flag false | Today’s demote/skip |
| Flag true, no phone | SKIPPED, no email impersonation |
| Flag true, template | `SendTemplate` once; delivery log SENT |
| Email step still email | |

---

## 7. Acceptance

**Build:** A PAST_DUE vaulted fail with phone receives a WhatsApp utility message whose CTA is the live pay/update URL; email sequence still independent.

**Delete:** No campaign can be saved that a merchant would demo as WhatsApp; public claims gone (155).

Tracker **N → Y** only on build acceptance. Demote-as-email is **not** Y.

---

## 8. Order

155 first (build or delete). Then this file’s engine/UI delta only on build.

Do **not** implement from this file.
