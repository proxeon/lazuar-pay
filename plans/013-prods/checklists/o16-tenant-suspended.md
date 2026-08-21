# O16 — `tenant.suspended` pauses new charges

**Track:** One extras · **Depends:** O15, D19  
**Analysis:** [08](../08-one-identity-production.md) §8  
**IDs:** NP-ONE-018  
**Goal:** Suspend stops **new** money. Cash already captured stays.

---

## O16.1 Flag

- [x] On verified `tenant.suspended`, set `org_settings.charges_paused` (D19 row keyed by One tenant id)
- [x] `tenant.reactivated` clears the pause
- [x] Persist with the processed event — not in-memory-only for live

## O16.2 Fail closed (new attempts)

- [x] `POST /v1/checkouts` fails closed while paused
- [x] `POST /v1/pay/{token}/start` fails closed while paused
- [x] PSP fulfill of **new** attempts fails closed while paused
- [x] Do not fail open on the **start** path because One is down

## O16.3 Money already true

- [x] Already-paid journals stay
- [x] Late webhook does **not** unwind cash
- [x] In-flight PSP capture of an already-open attempt may still commit (G/F plane)

## O16.4 Must not

- [x] Reverse journal / void `RCPT-` on suspend
- [x] Put buyer entitlement in One

## O16.5 Exit

- [x] Pause + reactivate proven hermetically
- [x] Unblocked for B99 live-charge honesty (O17 table if not already)
