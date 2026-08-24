# I14 — Resume must not bypass ChargesPaused

**Track:** Idempotent start · **Depends:** I10  
**Analysis:** P0-B; live Start 403 when `ChargesPaused`  
**IDs:** —  
**Goal:** A stored hosted URL is not a backdoor after suspend.

---

## I14.1 Live today (keep, tighten order)

- [ ] `settings?.ChargesPaused == true` → 403 `"Org charges are paused"`
- [ ] This check must run **before** the I10 stored-URL return

## I14.2

- [ ] Open checkout + stored URL + paused → **403**, not 200 with the old Stripe/CHIP URL
- [ ] Ada cannot finish paying a new click after suspend (in-flight Plane B is W21)

## I14.3 Must not

- [ ] Do not delete `PspRedirectUrl` on pause (W22 owns event ids, not start rows)

## I14.4 Exit

- [ ] Hermetic: PUT/seed paused, start twice — second still 403, FakePsp count still 1 from the first start
- [ ] Unblocked for W21
