# T10 — Remove Fulfillment SST throw

**Track:** Tax · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §3.1  
**IDs:** NP-MON-004 (out of this program)  
**Goal:** Fulfillment does not throw on unknown SST. Tax is not implemented.

---

## T10.1 Live

- [ ] Open `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- [ ] Delete the block `if (settings?.SstRegistered is null) throw new InvalidOperationException("SST registration unknown; fail closed")`
- [ ] Do not call `settings.SstRegistered` on the pay path after this
- [ ] Keep cash debit + revenue credit for `checkout.Amount`
- [ ] Keep amount≤0 early return and `status != "open"` no-op

## T10.2 Must not

- [ ] Do not replace the throw with `SstTaxMath` or a hard-coded 8%
- [ ] Do not add a tax journal line “for later”
- [ ] Do not title the document Tax Invoice

## T10.3 Exit

- [ ] Fulfillment compiles with no SST throw
- [ ] Unblocked for T11, T16
