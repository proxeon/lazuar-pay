# T14 — Official Receipt only

**Track:** Tax · **Depends:** T10  
**Analysis:** [00](../00-what-must-be-done.md) §4  
**IDs:** NP-DOC-003, NP-XX-003  
**Goal:** Documents stay commercial receipts. Tax theatre stays refuse.

---

## T14.1 Live

- [ ] `Fulfillment` still sets `Title = "Official Receipt"`
- [ ] `PaymentQueryEndpoints` missing number still serializes `"PENDING"`
- [ ] Series remains `RCPT` / `RCPT-{MYT year}-#####` (`MalaysiaTime.Year`)
- [ ] Number is never the checkout Guid

## T14.2 Grep

- [ ] Grep `apps/lazuar-pay` src + merchant + checkout for a **document title** `Tax Invoice` / `MyInvois` / `VALID` — none
- [ ] Honesty copy “not an e-invoice” on UI is allowed (T18)

## T14.3 Must not

- [ ] Do not rename series to `INV-`
- [ ] Do not print a MyInvois-looking QR

## T14.4 Exit

- [ ] No Tax Invoice string on money documents
- [ ] Unblocked for T18
