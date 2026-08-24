# T14 — Official Receipt only

**Track:** Tax · **Depends:** T10  
**Analysis:** [00](../00-what-must-be-done.md) §4  
**IDs:** NP-DOC-003, NP-XX-003  
**Goal:** Documents stay commercial receipts. Tax theatre stays refuse.

---

## T14.1 Live

- [x] `Fulfillment` still sets `Title = "Official Receipt"`
- [x] `PaymentQueryEndpoints` missing number still serializes `"PENDING"`
- [x] Series remains `RCPT` / `RCPT-{MYT year}-#####` (`MalaysiaTime.Year`)
- [x] Number is never the checkout Guid

## T14.2 Grep

- [x] Grep `apps/lazuar-pay` src + merchant + checkout for a **document title** `Tax Invoice` / `MyInvois` / `VALID` — none
- [x] Honesty copy “not an e-invoice” on UI is allowed (T18)

## T14.3 Must not

- [x] Do not rename series to `INV-`
- [x] Do not print a MyInvois-looking QR

## T14.4 Exit

- [x] No Tax Invoice string on money documents
- [x] Unblocked for T18
