# C13 — CHIP price in cents, AwayFromZero

**Track:** CHIP · **Depends:** C12  
**Analysis:** [00](../00-what-must-be-done.md) §5.1; Hub `GatewayCommon.ToMinorUnitsRounded`  
**IDs:** —  
**Goal:** Same minor-unit policy as StripeHosted. Do not truncate.

---

## C13.1

- [ ] `purchase.products[].price` = `(int)` / `(long)` `Math.Round(checkout.Amount * 100m, MidpointRounding.AwayFromZero)`
- [ ] Quantity this program is 1 (checkout row has no seats yet)
- [ ] Do not use Hub `ToMinorUnitsTruncating`

## C13.2 Must not

- [ ] Do not send MYR as a float ringgit to `price` (CHIP expects cents)
- [ ] Do not apply SST

## C13.3 Exit

- [ ] Unit test of the rounding helper if extracted; else covered by C17 mock asserting body
