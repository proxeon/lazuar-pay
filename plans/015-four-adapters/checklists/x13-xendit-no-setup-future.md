# X13 — Discard vault / setupFutureUsage

**Track:** Xendit · **Depends:** X12  
**Analysis:** Hub `_ = setupFutureUsage; // hosted invoice only — no token vault in v1`  
**IDs:** NP-GW-007  
**Goal:** Reminder-only. No payment-token soak in this program.

---

## X13.1

- [ ] Do not send Xendit recurrences / payment methods vault flags
- [ ] Capability `hosted_link`

## X13.2 Exit

- [ ] Payload has no vault flags
