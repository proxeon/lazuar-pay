# P12 — Mode M worker `ONE_API_KEY` (parked)

**Track:** Parked · **Not required for second-app cashier**  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §10.1 Mode M; freeze no god-key  
**Unpark when:** A Pay **hosted job** must call One without a user (e.g. register webhooks — we refused auto-register).

**Why parked:** Interactive doors forward the **caller**. A process env key that speaks for every merchant is a PAT. One-tenant dogfood worker is a different hatch.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneOptions.cs` | Two fields only |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs` | No default Authorization |
| M16 | Locks no env fallback on interactive |
| G17 | Ops registers One URL by hand |

**Current (`6d730d15`):** No `One:ApiKey`. Keep M16 red if someone adds it for Job A.

---

## P12.1 When unparking

- [ ] Prefix-check `lzr_sk_` before outbound
- [ ] Bound to **one** tenant in docs
- [ ] Separate client instance — not `DefaultRequestHeaders` on the interactive `OneClient`
- [ ] Reject `sk_live_` in that slot
- [ ] Tests: missing request Bearer still 401 even if env is set
