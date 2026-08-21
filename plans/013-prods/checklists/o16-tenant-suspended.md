# O16 — `tenant.suspended` pauses new charges

**Track:** One extras · **Depends:** O15, D19  
**Analysis:** [08](../08-one-identity-production.md) §8  
**IDs:** NP-ONE-018  
**Goal:** Suspend stops **new** money. Cash already captured stays.

---

## O16.1 Flag

- [ ] On verified `tenant.suspended`, set `org_settings.charges_paused` (D19 row keyed by One tenant id)
- [ ] `tenant.reactivated` clears the pause
- [ ] Persist with the processed event — not in-memory-only for live

## O16.2 Fail closed (new attempts)

- [ ] `POST /v1/checkouts` fails closed while paused
- [ ] `POST /v1/pay/{token}/start` fails closed while paused
- [ ] PSP fulfill of **new** attempts fails closed while paused
- [ ] Do not fail open on the **start** path because One is down

## O16.3 Money already true

- [ ] Already-paid journals stay
- [ ] Late webhook does **not** unwind cash
- [ ] In-flight PSP capture of an already-open attempt may still commit (G/F plane)

## O16.4 Must not

- [ ] Reverse journal / void `RCPT-` on suspend
- [ ] Put buyer entitlement in One

## O16.5 Exit

- [ ] Pause + reactivate proven hermetically
- [ ] Unblocked for B99 live-charge honesty (O17 table if not already)
