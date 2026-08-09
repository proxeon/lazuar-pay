# R60 — Module extract / merge gate (default SKIP)

**Track:** Extract · **Analysis:** `../07-module-extract-and-merge.md`  
**Default outcome:** N/A — do not implement

---

## R60.0 Gate (all required to proceed)

- [ ] Product trigger written (credits / webhooks product / multi-channel funded)
- [ ] `decisions.md` reopened and updated
- [ ] Design note (schema, events, dual-write)
- [ ] Product sign-off

If any unchecked → mark **SKIP** and stop.

---

## R60.1 If Credits extract triggered

- [ ] Follow analysis § Credits full steps (module, consumers, cutover)

## R60.2 If Webhooks extract triggered

- [ ] Follow analysis § Webhooks extract (after FW-1/FW-2 preferred)

## R60.3 If Messaging→Communications merge triggered

- [ ] Follow analysis § merge steps

## R60.4 Exit

- [ ] SKIP documented **or** extract complete with Contracts-only boundaries
