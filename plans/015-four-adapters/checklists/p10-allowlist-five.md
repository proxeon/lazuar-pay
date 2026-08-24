# P10 — Lowercase allow-list of five names

**Track:** Provider door · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** NP-GW-002, NP-GW-003, NP-LAT-002  
**Goal:** Path and PK use `stripe|chip|billplz|xendit|razorpay`. Not Hub `STRIPE`.

---

## P10.1 Constant

- [x] One allow-list in Pay host (static set or const array) — **lowercase**
- [x] PUT `provider` trimmed + `ToLowerInvariant()` then allow-list check
- [x] Webhook `{provider}` same normalize + allow-list
- [x] Unknown → **400** `"unknown provider"` (P22)
- [x] Remove the Stripe-only message `"Bar B first rail is stripe"` once P11 lands (or keep it only if provider is a known-but-not-yet-wired name during partial land — prefer allow-list grows **when the class exists**)

## P10.2 Land order honesty

- [x] Until C10 exists, `chip` may 400 `"rail not implemented"` **or** stay off the allow-list. Do not accept PUT chip with no class
- [x] Do not register unused names “for later”

## P10.3 Exit

- [x] Five names documented in `decisions.md` (already)
- [x] Unblocked for P11
