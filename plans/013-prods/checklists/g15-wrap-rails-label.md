# G15 — Wrap-rails honesty (hosted vs vault)

**Track:** Rails · **Depends:** G10  
**Analysis:** [06](../06-money-rails.md) §7  
**IDs:** NP-GW-007  
**Goal:** Store capability. Copy must not lie. `NP-GW-007`.

---

## G15.1 Capability next to charge (not a Contracts project)

- [ ] Store capability for the G10 rail: `hosted_link` vs `vaulted_autocharge`
- [ ] Stripe / CHIP **may** vault later; Billplz-class is reminder-only forever
- [ ] Bar B **can** be `hosted_link` only (first charge is hosted hop-2)
- [ ] `SupportsEmandate` stays false. No homemade FPX e-mandate (`NP-XX-011`)

## G15.2 Copy (UI + API)

- [ ] UI + API must **not** say “we will charge your card automatically” on reminder-only / hosted-link
- [ ] Do not print “auto-debit enabled” before a real PM / CHIP `recurring_token` exists
- [ ] GET gateways may expose `supports_off_session` from **Pay**, not a Vite reimplementation
- [ ] Steal Hub amber **judgment**, not `lazuar-ops` i18n keys

## G15.3 Must not

- [ ] No dunning `AUTO_CHARGE` in Bar B
- [ ] No unread DuitNow / wallet tiles on `:5178` / `:5179`

## G15.4 Exit

- [ ] `NP-GW-007` may move when copy matches the stored capability
- [ ] Unblocked for G16 (hosted session is the Bar B charge)
