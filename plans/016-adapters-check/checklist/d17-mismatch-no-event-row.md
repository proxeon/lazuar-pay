# D17 — Amount/currency mismatch 400 does not insert the unique grain

**Track:** Units · **Depends:** D16  
**Analysis:** live handler compares **before** TX insert — keep; test it  
**IDs:** H14; P0-D  
**Goal:** Fail-closed must be visible: no `psp_webhook_events` row, PSP can retry after we fix a parser.

---

## D17.1 Live today (keep)

- [ ] Amount/currency mismatch returns 400 before `BeginTransaction`
- [ ] Unique insert does not run

## D17.2 Tests must assert absence

- [ ] G15 / fs11 / fs12 / fc18 / fb22 / fx20 / fr22 all assert `PspWebhookEvents.Count == 0` (or no row for that EventId)
- [ ] Documents 0, checkout `open`

## D17.3 Must not

- [ ] Do not insert an `ignored: mismatch` grain that would block a later correct payload with the **same** EventId
- [ ] Do not 200

## D17.4 Exit

- [ ] At least Stripe G15 asserts event row absent
- [ ] Unblocked for F mismatch methods
