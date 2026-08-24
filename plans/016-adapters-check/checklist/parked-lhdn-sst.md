# Parked — LHDN / SST / Tax Invoice

**Do not start in 016.**  
**Analysis:** [`../00-evaluation.md`](../00-evaluation.md) §4 P0-3; 015 tax-out

---

- [ ] Do not add `SstTaxMath`, merchant SST yes/no, tax journal lines
- [ ] Do not title Tax Invoice or print VALID
- [ ] Do not add UBL / XAdES / `Modules/Lhdn` / MyInvois
- [ ] Book `checkout.Amount`. Leave `sst_registered` unused
- [ ] Razorpay webhook `tax` / `fee` stay unbooked
- [ ] 014 P0-3 is **not** permission to bring SST back
